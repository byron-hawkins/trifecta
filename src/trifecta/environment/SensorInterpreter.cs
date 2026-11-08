/* stub: Copilot */
/* author: Byron */

using System.Collections.Generic;
using System.Threading;
using SensorId = System.Int32;
using Temperature = System.Single;
using Pressure = System.Int32;

namespace trifecta {
namespace environment {

public class SensorInterpreter : IConditionTypes, ISensorAware<Temperature>, ISensorAware<Pressure> {
  public static SensorInterpreter singleton = new();

  // public SensorInterpreter() {
  //   _thresholds = new();
  //   _stable = new();
  // }

  public Temperature GetTemperature() {
    using (_lock.EnterScope()) { return _stable.temperature; }
  }

  public Pressure GetPressure() {
    using (_lock.EnterScope()) { return _stable.pressure; }
  }

  public void SetThresholds(Thresholds thresholds) {
    using (_lock.EnterScope()) {
      _thresholds = thresholds;
      Array.Sort(_thresholds.t);
      Array.Sort(_thresholds.p);
    }
  }

  public void Register(IConditionAware aware) {
    using (_lock.EnterScope()) { _registry.Add(aware); }
  }

  public void Release(IConditionAware aware) {
    using (_lock.EnterScope()) { _registry.Remove(aware); }
  }

  public void Paranoid(IConditionParanoid paranoid, Granularity granularity, System.TimeSpan normalization) {
    using (_lock.EnterScope()) { _paranoid.Add(paranoid); }  // TODO: bundle granularity and normalization
  }

  public void Relax(IConditionParanoid relax) {
    using (_lock.EnterScope()) { _paranoid.Remove(relax); }
  }

  public void SensorUpdate(SensorId id, Temperature reading) {
    // TODO: buffer at some interval to avoid chatter
    using (_lock.EnterScope()) {  // TODO: incremental update scaled by sensor relative weight
      Temperature t1 = Array.FindIndex(_thresholds.t, (f) => f < _stable.t);
      _stable.t = reading;
      Temperature t2 = Array.FindIndex(_thresholds.t, (f) => f < _stable.t);
      if (t1 != t2) {  // TODO: also give threshold value?
        foreach (IConditionAware a in _registry) a.ThresholdCross(_stable.t);
      }
    }
  }

  public void SensorUpdate(SensorId id, Pressure reading) {
    using (_lock.EnterScope()) {  // TODO: incremental update scaled by sensor relative weight
      Pressure p1 = Array.FindIndex(_thresholds.p, (i) => i < _stable.p);
      _stable.p = reading;
      Pressure p2 = Array.FindIndex(_thresholds.p, (i) => i < _stable.p);
      if (p1 != p2) {
        foreach (IConditionAware a in _registry) a.ThresholdCross(_stable.p);
      }
    }
  }

  public struct Thresholds : IConditionTypes {
    public Temperature[] t;
    public Pressure[] p;
  }

  public struct Granularity : IConditionTypes {
    public Temperature t;
    public Pressure p;
  }

  private struct Stable {
    Temperature t;
    Pressure p;
  }

  private Lock _lock = new();
  private Thresholds _thresholds;
  private List<IConditionAware> _registry = new();
  private List<IConditionAware> _paranoid = new();

  private Stable _stable;
}

}
}
