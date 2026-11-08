*author: Copilot*

```mermaid
classDiagram
namespace trifecta.environment {
  class SensorController {
    singleton
    System.Collections.Generic.List~ISensorDriver~ _actives
    System.Collections.Generic.List~ISensorDriver~ _passives
    +SensorController(SensorInterpreter interpreter)
    +Register(ISensorDriver driver) void
    +Release(ISensorDriver driver) void
  }
  class ISensorDriver {
    <<interface>>
    +GetId() SensorId
    +GetType() SensorType
  }
  class ISensorDriverActive~T~ {
    <<interface>>
    +Register(ISensorAware~T~ aware) void
    +Release(ISensorAware~T~ aware) void
  }
  class ISensorDriverPassive~T~ {
    <<interface>>
    +GetFrequency() TimeSpan
    +Read() T
  }
}
SensorController *-- ISensorDriver : _actives
SensorController *-- ISensorDriver : _passives
ISensorDriverActive~T~ ..|> ISensorDriver
ISensorDriverPassive~T~ ..|> ISensorDriver
```
