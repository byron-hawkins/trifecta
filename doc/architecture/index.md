# Architecture index

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
