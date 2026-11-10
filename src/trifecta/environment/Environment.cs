/* author: Byron */
/* stub: Copilot */

namespace trifecta.environment;

public interface IConditionAware {
  void ThresholdCross(Temperature t);

  void ThresholdCross(Pressure p);
}

public interface IConditionParanoid : IConditionAware {
  void Change(Temperature t);

  void Change(Pressure p);
}

public interface ISensorAware<T> {
  void SensorUpdate(SensorId id, T reading);
}

public interface ISensorDriver {
  SensorId GetId();
  SensorType GetSensorType();
  SensorMode GetSensorMode();
  void Start() {}
  void Stop() {}
}

public interface IActiveSensorDriver<T> : ISensorDriver {
  void Attach(ISensorAware<T> aware);
  void Detach(ISensorAware<T> aware);
}

public enum SensorType { temperature, pressure }

public enum SensorMode { active, passive }
