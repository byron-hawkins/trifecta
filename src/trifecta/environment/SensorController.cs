/* author: Byron */
/* stub: Copilot */

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Temperature = System.Single;
using Pressure = System.Int32;

namespace trifecta {
namespace environment {

public class SensorController : IConditionTypes, ISensorAware<Temperature>, ISensorAware<Pressure> {
  public static SensorController singleton = new();

  public void Register(ISensorDriver driver) {
    using (_lock.EnterScope()) {
      if (driver.GetMode() == SensorMode.active) {
        _actives.Add(driver);
      } else {
        _passives.Add(driver);
      }
    }
  }

  public void Release(ISensorDriver driver) {
    using (_lock.EnterScope()) {
      if (driver.GetMode() == SensorMode.active) {
        _actives.Remove(driver);
      } else {
        _passives.Remove(driver);
      }
    }
  }

  public void Start() {
    ComponentLog.singleton.Log("SensorController start requested");

    using (_lock.EnterScope()) {
      if (_started) throw new InvalidOperationException("Cannot restart the SensorController");
      if (_stop) throw new InvalidOperationException("Cannot start a SensorController that has been stopped");

      _started = true;
      _self = Task.Run(() => { run(); });
    }
  }

  public void Stop() {
    ComponentLog.singleton.Log("SensorController stop requested");

    using (_lock.EnterScope()) {
      if (!_started) throw new InvalidOperationException("Cannot stop a SensorController that has not been started");
      if (_stop) throw new InvalidOperationException("Cannot stop a SensorController that has been stopped");

      _self.Join();
    }
  }

  public void SensorUpdate(SensorId id, Temperature reading) {}
  public void SensorUpdate(SensorId id, Pressure reading) {}

  private void run() {
    while (true) {
      Thread.SpinWait(10);
      using (_lock.EnterScope()) {
        if (_stop) return;

        foreach (ISensorDriver passive in _passives) {
          ComponentLog.singleton.Log($"SensorController polls passive driver {passive.GetId()} for a {_passive.GetType()} value");
        }
      }
    }
  }

  private bool _started = false;
  private bool _stop = false;
  private Task? _self = null;
  private CancellationToken? _cancel = null;

  private Lock _lock = new();
  private readonly List<ISensorDriver> _actives = [];
  private readonly List<ISensorDriver> _passives = [];
}

}
}
