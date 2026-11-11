*author: Copilot*

# Collaboration transcript — 2026-11-11

## Summary

Work covered actor/queue locking, the macarena and ensemble test suites, the
simulator and its configurations, a reverse (C# to SMD) compiler and a forward
compiler, synchronizing the architecture Markdown with `src/`, new
communication diagrams, and a substantial README.

## Decisions and design

- **Actor-queue lock inversion.** Copilot identified, during ensemble testing,
  a lock inversion between the Actor and the resource queues: the Actor held
  its own lock across `ResourceQueueSet.MultiPush`/`MultiRelease`, while the
  queues call back into the Actor (`Grant()`), so the two locks could be taken
  in opposite orders. A related hazard was a late `Grant()` reaching an Actor
  that had already stopped or exited.
- **Queue token / cancel.** Copilot suggested a queue token with a cancel
  step. The user and Copilot eventually settled on a simpler variant that
  avoids a locked cancel phase: each queueing creates an `Appearance` (the
  consumer the queues see). `IsGhost()` is true once the Actor has left that
  push, and the queues skip ghosts. The Actor no longer holds its lock across
  calls into the queue set. The cancel-token design was dropped.
- **Known residual window.** `IsGhost()` and `Grant()` each take the Actor
  lock separately, so the check and the grant are not atomic. A re-check inside
  `Grant()` (with `Grant()` returning `bool`) is recorded in `temp/todo.md` as
  optional and was not implemented.
- **Lock order** (verified from the code, no inversion found):
  `SensorInterpreter._lock` -> `Director._idle` -> `Actor._idle`;
  `ResourceQueueSet._multilock` or `_idle` -> `ResourceQueue._lock` ->
  `Actor._idle`. `Actor._idle` is innermost and `ComponentLog` is a leaf.
  Documented in `doc/architecture/communication.md`.
- **Latency note.** `Director` holds `Director._idle` while
  `Actor.Stop(stop_now)` waits for the Actor task, so a threshold crossing
  arriving meanwhile delays the sensor driver thread until the Actor exits.
  Not a deadlock.
- **Boundary crossings.** A sensor that reaches a bound exactly and reverses
  must produce either two crossings or none; one crossing would leave a role
  active outside its bounds. An ensemble test (`ThresholdBoundaryTests`) covers
  it. The project's requirements make the bounds hard limits.
- **Simulator.** `simulation/` is a CLI app: prints the log path at startup,
  stops on Enter, takes an optional manifest argument. Default configuration
  is `configuration/simulation`; `configuration/quick-stage` is an unrelated
  variant (one scene per role, 50/75/100 ms). Consecutive scenes of a role must
  differ in resource *set*. Sensor scripts were rebalanced so the three roles
  are active for roughly equal time (about 1.0 : 0.95 : 0.8 by combination
  reachability; stage_3 cannot run without stage_1).
- **Reverse and forward compilers.** Architecture Markdown was made to match
  `src/` by name only: types that no longer exist were removed and renamed
  members fixed; no members were added or removed, and private members such as
  `_lock` were not added. Signature differences (parameter lists) are
  deliberately left out as noise. `Appearance : IResourceConsumer` was added
  to `actor.md`.
- Passive sensor drivers, passive resource drivers, the `finishup` and
  `costmodel` modes, and the `stop_at_cost`/`suspension` phases are interfaced
  but not implemented; the README says so.

## Outputs and changes

- `meta/generate/reverse-smd/` — Roslyn reverse compiler; writes
  `doc/architecture/reverse/*.smd`.
- `meta/generate/forward/diagram_and_render.py` — forward compiler; writes
  `doc/architecture/bin/*.smd`, `figures/`, and `render/`.
- `doc/architecture/{actor,environment,resource,io}.md` — updated to match
  `src/`. `bin/`, `figures/`, `render/` regenerated, including the new actor
  and resources diagrams.
- `doc/architecture/communication.md` — information-flow diagram (internals
  nested without arrows, `SensorController` and drivers inside
  `SensorInterpreter`) and two sequence diagrams of lock use, plus shutdown.
- `doc/architecture/index.md` — index of the architecture documents and
  diagram pages.
- `README.md` — simulator section first (with how the JSON configuration
  works), then the architecture/BNF/SMD/compiler section, then running the
  tests, then "Interfaced but not implemented".
- `configuration/simulation/` and `configuration/quick-stage/` — manifests
  and configuration files.
- Tests: toddler, macarena, and ensemble suites; ghost/appearance, boundary,
  and queue-set tests among them.

## Validation and current status

- Reverse compile: 47 of 47 documented types found, no diagnostics. All SMDs
  validate against `architecture-schema.json`.
- Full macarena suite: 103 passed. Full ensemble suite: 35 passed (run after
  the latest `Crew.cs` and `SensorInterpreter` changes).
- Mermaid diagrams were rendered with the Mermaid command-line tool; they
  have not been viewed on GitHub or in a browser.

## Follow-up notes

- Optional `Grant()` re-check and `bool` return (see `temp/todo.md`).
- Add the ensemble test command to the README; consider Windows concerns
  (environment variable syntax, about 15 ms timer resolution, line endings).
- `Actor.run()` idle message says "Director goes idle" (copy-paste slip).
- The semantics document says rendered output goes to `render/io.md`, but the
  forward compiler names files by diagram basename.
- The reverse compiler's `bin/`/`obj/` output must stay out of the repository
  (there is no `.gitignore` yet).
- The `@diagram` directive in `resource.md` ends in `-->` rather than `--->`.
