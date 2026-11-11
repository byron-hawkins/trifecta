*author: Copilot*

*Subject: how the high-level components communicate and synchronize*

This page is plain Markdown; its diagrams are Mermaid blocks that GitHub renders directly. Names match `src/trifecta`.

## Information flow

Boxes nested inside a component are internals. They have no arrows because nothing outside their container
talks to them, so each can be replaced or changed without affecting the rest of the system. In particular, the
rest of the system receives only *interpreted* sensor readings: sensor hardware, drivers and the
`SensorController` are hidden behind the `SensorInterpreter`.

Solid arrows are calls between components; dashed arrows are calls over a singleton that nearly every
component makes (`ComponentLog.singleton`).

```mermaid
flowchart LR
  Sim["Simulation (Program)<br/>loads configuration, starts and stops components"]

  subgraph SI["SensorInterpreter (singleton)"]
    direction TB
    SC["SensorController (singleton)"]
    SD["ScriptedActiveSensorDriver<br/>(one per sensor)"]
  end

  subgraph DIR["Director (singleton)"]
    direction TB
    OPT["CastOptimizer"]
  end

  subgraph ACT["Actor (one per cast member)"]
    direction TB
    SCN["Scene"]
    APP["Appearance"]
  end

  subgraph RQS["ResourceQueueSet (singleton)"]
    direction TB
    RQ["ResourceQueue<br/>(one per resource name)"]
    RI["ResourceInstance"]
  end

  LOG[("ComponentLog (singleton)")]

  Sim -->|"SetThresholds, Register sensors, Start/Stop"| SI
  Sim -->|"configure, Start/Stop"| DIR
  Sim -->|"configure, Start/Stop"| RQS
  SI -->|"ThresholdCross(T or P)"| DIR
  DIR -->|"GetTemperature, GetPressure"| SI
  DIR -->|"Start / Stop(stop_now)"| ACT
  ACT -->|"MultiPush, MultiRelease"| RQS
  RQS -->|"IsGhost, Scope, Grant<br/>(through Appearance)"| ACT

  SI -.->|Log / Trace| LOG
  DIR -.->|Log| LOG
  ACT -.->|Log| LOG
  RQS -.->|Log| LOG
  Sim -.->|Configure| LOG
```

## Locking

Each component guards its state with its own lock (a `Monitor` on `_idle`, or a `Lock`). The sequence diagrams
below show who holds which lock while calling whom. Notes read as "holds X".

`Monitor` locks are reentrant, so a callback on the same thread into a lock it already holds would not
deadlock. No such callback exists today: `Actor` never calls the `Director`.

### Sensor reading to cast change

A sensor driver thread delivers a reading. If it crosses a threshold, the interpreter notifies the Director, which
wakes its own thread to re-optimize. The Director holds its own lock during `Optimize`, so that it can iterate the
cast, but it releases it while reading the interpreter (so the interpreter-to-Director and Director-to-interpreter
directions can never wait on each other).

```mermaid
sequenceDiagram
  participant D as Sensor driver thread
  participant I as SensorInterpreter
  participant R as Director thread
  participant DL as Director
  participant A as Actor

  D->>D: tick, compute reading (holds driver lock)
  Note over D: driver lock is released before reporting
  D->>I: SensorController.SensorUpdate(reading) (no lock)
  rect rgb(235, 245, 255)
    Note over I: holds SensorInterpreter._lock
    I->>DL: ThresholdCross(value) via IConditionAware
    rect rgb(255, 245, 225)
      Note over DL: holds Director._idle (briefly)
      DL->>DL: _optimize = true, Pulse
    end
  end

  R->>R: wakes from Wait, holds Director._idle
  Note over R: releases Director._idle while reading the interpreter
  R->>I: GetTemperature(), GetPressure()
  rect rgb(235, 245, 255)
    Note over I: holds SensorInterpreter._lock (briefly)
    I-->>R: stable readings
  end
  Note over R: re-acquires Director._idle
  rect rgb(255, 245, 225)
    Note over R: holds Director._idle for all of Optimize()
    R->>R: CastOptimizer.Optimize(t, p)
    R->>A: Initiate: Actor.Start(ignorable)
    rect rgb(235, 255, 235)
      Note over A: holds Actor._idle (briefly)
      A->>A: phase = ready, start run() task
    end
    R->>A: Interrupt: Actor.Stop(stop_now, ignorable)
    rect rgb(235, 255, 235)
      Note over A: holds Actor._idle (briefly)
      A->>A: phase = stop_now, Pulse
    end
    Note over R,A: Stop(stop_now) then waits for the Actor task to exit, still holding Director._idle (not Actor._idle)
  end
```

Lock order on this path is `SensorInterpreter._lock` → `Director._idle` for notification, and
`Director._idle` → `Actor._idle` for cast changes. The reverse orders never occur. Because the Director waits for
a stopping Actor while holding its own lock, a threshold crossing that arrives meanwhile waits on
`Director._idle` while holding `SensorInterpreter._lock`, which in turn delays the sensor driver thread; it
resolves as soon as the Actor task exits.

### Resource request and grant

An Actor requests its scene's resources from its own thread, without holding its own lock. The queues call back
into the Actor to check and grant; those callbacks take only `Actor._idle`, which is never held while the
Actor calls out.

```mermaid
sequenceDiagram
  participant A as Actor thread
  participant Q as ResourceQueueSet
  participant RQ as ResourceQueue
  participant AL as Actor (callbacks)
  participant T as ResourceQueueSet thread

  Note over A: holds Actor._idle
  A->>A: phase queue_up
  Note over A: releases Actor._idle before calling out
  A->>Q: MultiPush(Appearance)
  rect rgb(235, 245, 255)
    Note over Q: holds ResourceQueueSet._multilock
    loop each resource in Scope()
      Q->>RQ: Push(Appearance)
      rect rgb(255, 245, 225)
        Note over RQ: holds ResourceQueue._lock
        RQ->>AL: IsGhost(), Grant(resource)
        rect rgb(235, 255, 235)
          Note over AL: holds Actor._idle (briefly)
          AL->>AL: Scene.Grant, and once complete: phase = work, Pulse
        end
      end
    end
  end
  A->>Q: Bump() (no other lock held)
  rect rgb(255, 245, 225)
    Note over Q: holds ResourceQueueSet._idle (briefly)
    Q->>T: Pulse
  end
  T->>RQ: Dispatch() for each queue
  rect rgb(255, 245, 225)
    Note over RQ: holds ResourceQueueSet._idle, then ResourceQueue._lock
    RQ->>AL: IsGhost(), Grant(resource)
  end
```

Lock order here is `ResourceQueueSet._multilock` → `ResourceQueue._lock` → `Actor._idle`, and
`ResourceQueueSet._idle` → `ResourceQueue._lock` → `Actor._idle`. Releasing resources (`MultiRelease`, via
`Actor.Remit`) takes `ResourceQueue._lock` → `Actor._idle` the same way, with the Actor's own lock released
first. `Actor._idle` is therefore always the innermost lock on these paths, and `ComponentLog`'s lock is a leaf
beneath every other lock.

### Shutdown

The simulator stops components in reverse of their start order: the Director (which stops its remaining
actors with `exit`, which does not wait for them), then the `ResourceQueueSet`, then the `SensorController`
(which stops each driver). Each component waits for its own thread after releasing its lock, so no stop call holds
a lock while waiting on that component's thread. The one exception is the `stop_now` wait noted above.
