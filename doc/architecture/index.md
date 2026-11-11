# Architecture

Central to the challenges of this manufacturing control system is the delicate balance between flexibility, efficiency
and correctness. Every structural limitation becomes an obstacle for the evolution of the manufacturing environment, yet
every flexible joint in the system introduces new dimensions of risk leading to potential loss of performance, or operation
of equipment in unsuitable conditions, or even a total system shutdown. For example, through aggressive integration
testing performed by Copilot over the scripted sensor inputs, a corner case was revealed in the sensor interpreter,
which transforms a stream of device input into a stepwise event broadcast. By an inconsistency between the `>` and `>=`
operators in communicating components, a sensor pattern ascending exactly to a stepwise threshold and then immediately
descending could result in only one stepwise event, omitting the reciprocal. Consequences could be severe, because
machine stages would be left running in conditions far outside their intended operating environment. On the other hand,
a simpler approach that floods the application with detailed and potentially jittery or inconsistent sensor readings
could just as easily lead to disaster scenarios.

To negotiate the competing demands of stability and adaptability in this risk prone environment, Trifecta focuses on
isolating independent system roles and narrowing the scope of influence introduced through communication and component
interaction. One example is the `trifecta.stage.Appearance` class, which acts as a proxy for a request placed on the
resource queue. Under fluctuating temperature and pressure, it is common for such resource requests to be abandoned
before they are fulfilled, leaving stale queue entries that could lead to resource handle leakage or simply a crash. Yet
just as much risk could be introduced by encumbering the resource dispatcher with the complexity of evaluating request
liveness. By mediating that component boundary with an epoch-bound `Appearance` proxy, an easy liveness evaluation on
the requester's side is made trivially available to the provider, without requiring any shared knowledge beyond a simple
boolean `IsGhost()` interface. Though by no means a final solution to the reliability demands of autonomous automated
systems, every architectural step towards elimination of artificial complexity improves integrity and efficiency leading
to a more effective and capable system.


## Technologies

At the beginning of the project, considering that it would be my first experience of any significant complexity with
C# and the .NET ecosystem, it seemed most practical and most relevant to focus on core components of the language and
the SDK. Although the simplicity allowed for a clear transition from design to implementation, the complexity of
concurrency challenges in the problem statement suggest that a framework oriented towards parallel programming could be
highly beneficial. One example that appears frequently in the academic literature is the P language along with a host of
related concurrency testing and analysis tools. Perhaps more prevalent in the .NET ecosystem are specialized testing
approaches such as Controlled Concurrency Testing (Coyote from Microsoft Research) and concurrency fuzz testing (Greybox
in ASPLOS '24), as well as Refinement Learning techniques such as the P#-Tester based on the P# language (OOPSLA '20).
Formal research has ye tto discover a sound and complete analysis at industrial scale for potential concurrency flaws,
but these tools have made major strides in focusing compute resources on the most important cases in the program under
test.

## Documents

- [globals.md](./globals.md): the global type aliases (such as `Temperature` and `Pressure`) shared by all components.
- [actor.md](./actor.md): the Director, its CastOptimizer, and the Actors and Scenes that run roles as conditions change.
- [environment.md](./environment.md): the SensorController and SensorInterpreter that turn sensor readings into threshold-crossing events.
- [resource.md](./resource.md): the resource queues and instances that grant exclusive access to shared resources.
- [simulation.md](./simulation.md): loading configuration files and driving simulated sensors from scripts.
- [stage.md](./stage.md): the configuration model for resources, roles and scenes.
- [io.md](./io.md): the component log, its sinks, and command-line I/O.
- [communication.md](./communication.md): how the top-level components exchange information and which locks they hold, with sequence diagrams.

## Diagram pages

- [actor](./render/actor.md): how the Director and CastOptimizer start and stop Actors, their Appearances and Scenes.
- [resources](./render/resources.md): how the queue set, its queues and resource instances serve resource consumers.
- [sensor-awareness](./render/sensor-awareness.md): how the interpreter notifies condition-aware listeners of threshold crossings.
- [sensor-control](./render/sensor-control.md): how the SensorController manages sensor drivers.
- [io.logging](./render/io.logging.md): the logging classes and interfaces.
