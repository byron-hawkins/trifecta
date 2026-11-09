/* author: Copilot */
using System.Collections.Generic;
using System.Threading;

namespace trifecta.environment;

public class SensorInterpreter : IConditionTypes,
                                 ISensorAware<Temperature>,
                                 ISensorAware<Pressure> {
  public static SensorInterpreter singleton = new();

  public Temperature GetTemperature() { return default; }

  public Pressure GetPressure() { return default; }

  public void SetThresholds(Thresholds thresholds) { _thresholds = thresholds; }

  public void Register(IConditionAware aware) {}

  public void Release(IConditionAware aware) {}

  public void Paranoid(IConditionParanoid paranoid, Granularity granularity,
                       System.TimeSpan normalization) {}

  public void Relax(IConditionParanoid relax) {}

  public void SensorUpdate(SensorId id, Temperature reading) {}

  public void SensorUpdate(SensorId id, Pressure reading) {}

  public struct Thresholds : IConditionTypes {
    public Temperature[] t;
    public Pressure[] p;
  }

  public struct Granularity : IConditionTypes {
    public Temperature t;
    public Pressure p;
  }

  private Lock _lock = new();
  private Thresholds _thresholds;
  private List<IConditionAware> _registry = new();
  private List<IConditionAware> _paranoid = new();
}
