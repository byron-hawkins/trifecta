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
  class SensorInterpreter {
    singleton
    System.Threading.Lock _lock
    Thresholds _thresholds
    System.Collections.Generic.List~IConditionAware~ _registry
    System.Collections.Generic.List~IConditionAware~ _paranoid
    +GetTemperature() Temperature
    +GetPressure() Pressure
    +SetThresholds(Thresholds thresholds) void
    +Register(IConditionAware aware) void
    +Release(IConditionAware aware) void
    +Paranoid(IConditionParanoid paranoid, Granularity granularity, TimeSpan normalization) void
    +Relax(IConditionParanoid relax) void
    +SensorUpdate(SensorId id, Temperature reading) void
    +SensorUpdate(SensorId id, Pressure reading) void
  }
  class Thresholds {
    <<struct>>
    +Temperature[] t
    +Pressure[] p
  }
  class Granularity {
    <<struct>>
    +Temperature t
    +Pressure p
  }
  class IConditionAware {
    <<interface>>
    +ThresholdCross(Temperature t) void
    +ThresholdCross(Pressure p) void
  }
  class IConditionParanoid {
    <<interface>>
    +Change(Temperature t) void
    +Change(Pressure p) void
  }
}
SensorInterpreter *-- Thresholds : _thresholds
SensorInterpreter *-- IConditionAware : _registry
SensorInterpreter *-- IConditionAware : _paranoid
IConditionParanoid ..|> IConditionAware
```
