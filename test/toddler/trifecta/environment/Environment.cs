/* author: Copilot */
namespace trifecta {
namespace environment {

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

public interface ISensorDriver {}

public enum SensorType { temperature, pressure }

public enum SensorMode { active, passive }

}
}
