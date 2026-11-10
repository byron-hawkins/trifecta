/* author: Byron */
/* stub: Copilot */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using trifecta.io;
using trifecta.environment;

namespace trifecta {
namespace simulation {

public class SensorDriverFactory {
  public static SensorDriverFactory singleton = new();

  public SensorDriverFactory() {
    _rootpath = Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR") ??
                throw new InvalidOperationException("Missing TRIFECTA_CONFIG_DIR: set it to the desired top-level configuration/ directory.");
  }

  public static ISensorDriver scripted(string configurationPath, string scriptPath) {
    SensorConfiguration c = ConfigurationFileLoader.parse<SensorConfiguration>(singleton._rootpath + "/" + configurationPath);
    if (c.type == SensorType.temperature) {
      SensorScript<Temperature> s = ConfigurationFileLoader.parse<SensorScript<Temperature>>(singleton._rootpath + "/" + scriptPath);
      if (c.mode == SensorMode.active) {
        return new ScriptedActiveSensorDriver<Temperature>(s, c);
      } else {
        return new ScriptedPassiveSensorDriver<Temperature>(s, c);
      }
    } else {
      SensorScript<Pressure> s = ConfigurationFileLoader.parse<SensorScript<Pressure>>(singleton._rootpath + "/" + scriptPath);
      if (c.mode == SensorMode.active) {
        return new ScriptedActiveSensorDriver<Pressure>(s, c);
      } else {
        return new ScriptedPassiveSensorDriver<Pressure>(s, c);
      }
    }

    // return null!;
  }

  public class SensorConfiguration {
    public int id;
    public string name = string.Empty;
    public TimeSpan tickinterval;
    public SensorType type;
    public SensorMode mode;
  }

  private string _rootpath;
}

public class SensorScript<T> {
  public List<Event> events = [];

  public struct Event {
    public TimeSpan duration;
    public T value;
  }
}

public abstract class ScriptedSensorDriver<T> : ISensorDriver {
  public ScriptedSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) {
    this._script = _script;
    this._configuration = _configuration;
  }

  public abstract void Start();

  public abstract void Stop();

  public SensorId GetId() { return _configuration.id; }
  public SensorType GetSensorType() { return _configuration.type; }
  public SensorMode GetSensorMode() { return _configuration.mode; }

  protected SensorScript<T> _script;
  protected SensorDriverFactory.SensorConfiguration _configuration;
}

public class ScriptedActiveSensorDriver<T> : ScriptedSensorDriver<T>, IActiveSensorDriver<T>
    where T : INumber<T> {
  public ScriptedActiveSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) : base(_script, _configuration) {}

  public override void Start() {
    if (_script.events.Count == 0) return;

    using (_lock.EnterScope()) {
      _started = true;
      _self = Task.Run(() => { run(); });
    }
  }

  public override void Stop() {
    Task task;
    using (_lock.EnterScope()) {
      if (!_started) return;
      _stop = true;
      task = _self ?? throw new InvalidOperationException("Missing background task when attempting to stop a sensor driver");
      if (Task.CurrentId == task.Id) return; /* loopback */
    }
    task.Wait();
    using (_lock.EnterScope()) { reset(); }
  }

  public void Attach(ISensorAware<T> aware) {
    using (_lock.EnterScope()) { _aware = [.._aware, aware]; }
  }

  public void Detach(ISensorAware<T> unaware) {
    using (_lock.EnterScope()) { _aware = _aware.Where(x => x != unaware).ToArray(); }
  }

  private void run() {
    using (ComponentLog.singleton.Register($"Active {_configuration.type} sensor driver #{_configuration.id}")) {
      int point = 0;
      T reading = default;
      ISensorAware<T>[] safe_aware = [];
      TimeSpan countdown = _script.events[point].duration, duration = countdown;
      while (true) {
        Thread.Sleep(_configuration.tickinterval);
        using (_lock.EnterScope()) {
          if (_stop) {
            reset();
            return;
          }

          TimeSpan elapsed = _configuration.tickinterval;
          while (elapsed >= countdown) {
            elapsed -= countdown;
            point = next(point);
            duration = countdown = _script.events[point].duration;
          }
          countdown -= elapsed;
          reading = at(point);
          if (elapsed > TimeSpan.Zero) {
            T delta = (at(next(point)) - at(point));
            double ratio = ((((double)duration.TotalMilliseconds) - ((double)countdown.TotalMilliseconds)) / ((double)duration.TotalMilliseconds));
            double slide = (double.CreateChecked(delta) * ratio);
            reading += T.CreateChecked(slide);
          }
          safe_aware = _aware;
        }
        foreach (ISensorAware<T> aware in safe_aware) { aware.SensorUpdate(_configuration.id, reading); }
      }
    }
  }

  private T at(int point) { return _script.events[point].value; }

  private int next(int point) {
    point++;
    return (point >= _script.events.Count) ? 0 : point;
  }

  private void reset() {
    _self = null;
    _started = false;
    _stop = false;
  }

  private bool _started = false;
  private bool _stop = false;
  private Lock _lock = new();
  private Task? _self = null;
  private ISensorAware<T>[] _aware = [];
}

public class ScriptedPassiveSensorDriver<T> : ScriptedSensorDriver<T> {
  public ScriptedPassiveSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) : base(_script, _configuration) {}
  public override void Start() {}

  public override void Stop() {}
}
}
}
