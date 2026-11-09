/* stub: Copilot */
/* author: Byron */

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using SensorId = System.Int32;
using Temperature = System.Single;
using Pressure = System.Int32;
using trifecta.io;

namespace trifecta {
namespace environment {

public class SensorInterpreter : IConditionTypes, ISensorAware<Temperature>, ISensorAware<Pressure> {
  public static SensorInterpreter singleton = new();

  // public SensorInterpreter() {
  //   _thresholds = new();
  //   _stable = new();
  // }

  public Temperature GetTemperature() {
    using (_lock.EnterScope()) { return _stable.t; }
  }

  public Pressure GetPressure() {
    using (_lock.EnterScope()) { return _stable.p; }
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
      int t1 = FindSlot(_thresholds.t, _stable.t);
      _stable.t = reading;
      int t2 = FindSlot(_thresholds.t, _stable.t);
      if (t1 != t2) {  // TODO: also give threshold value?
        ComponentLog.singleton.Log($"Temperature sensor {id} crosses {Math.Abs(t2-t1)} thresholds to {reading}");
        foreach (IConditionAware a in _registry) a.ThresholdCross(_stable.t);
      }
    }
  }

  public void SensorUpdate(SensorId id, Pressure reading) {
    using (_lock.EnterScope()) {  // TODO: incremental update scaled by sensor relative weight
      int p1 = FindSlot(_thresholds.p, _stable.p);
      _stable.p = reading;
      int p2 = FindSlot(_thresholds.p, _stable.p);
      if (p1 != p2) {
        ComponentLog.singleton.Log($"Pressure sensor {id} crosses {Math.Abs(p2-p1)} thresholds to {reading}");
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
    public Temperature t;
    public Pressure p;
  }

  private int FindSlot<T>(T[] set, T find)
      where T : INumber<T> {
    int i = Array.FindIndex(set, (e) => e > find);
    return (i < 0) ? set.Length : i;
  }

  private Lock _lock = new();
  private Thresholds _thresholds;
  private List<IConditionAware> _registry = new();
  private List<IConditionAware> _paranoid = new();

  private Stable _stable;
}

}
}
