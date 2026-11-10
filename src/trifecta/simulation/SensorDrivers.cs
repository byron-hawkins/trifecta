/* author: Byron */
/* stub: Copilot */

using System;
using System.Collections.Generic;
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

public class ScriptedActiveSensorDriver<T> : ScriptedSensorDriver<T>, IActiveSensorDriver<T> {
  public ScriptedActiveSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) : base(_script, _configuration) {}

  public override void Start() {}

  public override void Stop() {}

  public void Attach(ISensorAware<T> aware) { _aware.Add(aware); }

  public void Detach(ISensorAware<T> unaware) { _aware.Remove(unaware); }

  private List<ISensorAware<T>> _aware = new();
}

public class ScriptedPassiveSensorDriver<T> : ScriptedSensorDriver<T> {
  public ScriptedPassiveSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) : base(_script, _configuration) {}
  public override void Start() {}

  public override void Stop() {}
}
}
}
