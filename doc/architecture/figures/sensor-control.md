*author: Copilot*

```mermaid
classDiagram
namespace trifecta.environment {
  class SensorController {
    singleton
    System.Collections.Generic.List~ISensorDriver~ _actives
    System.Collections.Generic.List~ISensorDriver~ _passives
    +Register(ISensorDriver driver) void
    +Release(ISensorDriver driver) void
    +Start() void
    +Stop() void
    +Reset() void
    +SensorUpdate(SensorId id, Temperature reading) void
    +SensorUpdate(SensorId id, Pressure reading) void
  }
  class ISensorDriver {
    <<interface>>
    +GetId() SensorId
    +GetSensorType() SensorType
    +GetSensorMode() SensorMode
    +Start() void
    +Stop() void
  }
  class IActiveSensorDriver~T~ {
    <<interface>>
    +Attach(ISensorAware~T~ aware) void
    +Detach(ISensorAware~T~ aware) void
  }
}
SensorController *-- ISensorDriver : _actives
SensorController *-- ISensorDriver : _passives
IActiveSensorDriver~T~ ..|> ISensorDriver
```
