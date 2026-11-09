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
    _rootpath = Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR");
    if (_rootpath == null) { throw new InvalidOperationException("Missing TRIFECTA_CONFIG_DIR: set it to the desired top-level configuration/ directory."); }
  }

  public static ISensorDriver scripted(string configurationPath, string scriptPath) {
    SensorConfiguration c = ConfigurationFileLoader.parse<SensorConfiguration>(_rootpath + "/" + configurationPath);
    if (c.type == SensorType.temperature) {
      SensorScript<Temperature> s = ConfigurationFileLoader.parse<SensorScript<Temperature>>(_rootpath + "/" + scriptPath);
      if (c.mode == SensorMode.passive) { return new ScriptedPassiveSensorDriver<Temperature>(s, c); }
    }

    return null!;
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
  public IReadOnlyList<Event> Events => _events;

  public struct Event {
    public TimeSpan duration;
    public T value;
  }

  private List<Event> _events = [];
}

public class ScriptedSensorDriver<T> : ISensorDriver {
  public ScriptedSensorDriver(SensorScript<T> _script, SensorDriverFactory.SensorConfiguration _configuration) {
    this._script = _script;
    this._configuration = _configuration;
  }

  public void Start() {}

  public void Stop() {}

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
