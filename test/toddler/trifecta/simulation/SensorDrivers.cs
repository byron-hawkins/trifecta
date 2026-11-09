/* stub: Copilot */
using System;
using System.Collections.Generic;
using trifecta.environment;

namespace trifecta {
namespace simulation {

public class SensorDriverFactory {
  public static SensorDriverFactory singleton = new();

  public SensorDriverFactory() {}

  public static ISensorDriver scripted(string configurationPath,
                                       string scriptPath) {
    return null!;
  }

  public class SensorConfiguration {
    public void Load(string path) {}

    public int _id;
    public string _name = string.Empty;
    public TimeSpan _tickinterval;
    public SensorType _type;
    public SensorMode _mode;
  }

  private string _rootpath = string.Empty;
}

public class SensorScript<T> {
  public void Load(string path) {}

  public IReadOnlyList<Event> Events => _events;

  public struct Event {
    public TimeSpan duration;
    public T value;
  }

  private List<Event> _events = [];
}

public class ScriptedSensorDriver<T> : ISensorDriver {
  public ScriptedSensorDriver(
      SensorScript<T> _script,
      SensorDriverFactory.SensorConfiguration _configuration) {
    this._script = _script;
    this._configuration = _configuration;
  }

  public void Start() {}

  public void Stop() {}

  protected SensorScript<T> _script;
  protected SensorDriverFactory.SensorConfiguration _configuration;
}

public class ScriptedActiveSensorDriver<T> : ScriptedSensorDriver<T> {
  public ScriptedActiveSensorDriver(
      SensorScript<T> _script,
      SensorDriverFactory.SensorConfiguration _configuration)
      : base(_script, _configuration) {}
}

public class ScriptedPassiveSensorDriver<T> : ScriptedSensorDriver<T> {
  public ScriptedPassiveSensorDriver(
      SensorScript<T> _script,
      SensorDriverFactory.SensorConfiguration _configuration)
      : base(_script, _configuration) {}
}

}
}
