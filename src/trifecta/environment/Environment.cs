/* author: Byron */
/* stub: Copilot */

using SensorId = System.Int32;
using Temperature = System.Single;
using Pressure = System.Int32;

namespace trifecta.environment;

public interface IConditionTypes {}

public interface IConditionAware : IConditionTypes {
  void ThresholdCross(Temperature t);

  void ThresholdCross(Pressure p);
}

public interface IConditionParanoid : IConditionAware, IConditionTypes {
  void Change(Temperature t);

  void Change(Pressure p);
}

public interface ISensorAware<T> : IConditionTypes {
  void SensorUpdate(SensorId id, T reading);
}

public interface ISensorDriver {
  SensorId GetId();
  SensorType GetSensorType();
  SensorMode GetSensorMode();
}

public enum SensorType { temperature, pressure }

public enum SensorMode { active, passive }
