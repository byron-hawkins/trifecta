# Architecture index

Each page below is a rendered architecture document, with its generated diagram.

## Rendered documents

- [actor](./render/actor.md): the Director, its CastOptimizer, and the Actors and Scenes that run roles as conditions change.
- [resources](./render/resources.md): the resource queues and instances that grant exclusive access to shared resources.
- [sensor-awareness](./render/sensor-awareness.md): the SensorInterpreter and how it notifies condition-aware listeners of threshold crossings.
- [sensor-control](./render/sensor-control.md): the SensorController and how it manages sensor drivers.
- [io.logging](./render/io.logging.md): the component log, its sinks, and the logging interfaces.

## Plain Markdown

- [communication.md](./communication.md): how the top-level components exchange information and which locks they hold, with sequence diagrams (not generated, so it has no rendered form).
