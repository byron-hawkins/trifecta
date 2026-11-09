/* author: Byron */
/* stub: Copilot */

using System;
using System.Collections.Generic;
using Temperature = System.Single;
using Pressure = System.Int32;
using SensorId = System.Int32;
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

public class ScriptedSensorDriver<T> : ISensorDriver {
  public ScriptedSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) {
    this._script = _script;
    this._configuration = _configuration;
  }

  public void Start() {}

  public void Stop() {}

  public SensorId GetId() { return _configuration.id; }
  public SensorType GetSensorType() { return _configuration.type; }
  public SensorMode GetSensorMode() { return _configuration.mode; }

  protected SensorScript<T> _script;
  protected SensorDriverFactory.SensorConfiguration _configuration;
}

public class ScriptedActiveSensorDriver<T> : ScriptedSensorDriver<T> {
  public ScriptedActiveSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) : base(_script, _configuration) {}
}

public class ScriptedPassiveSensorDriver<T> : ScriptedSensorDriver<T> {
  public ScriptedPassiveSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) : base(_script, _configuration) {}
}
}
}
