/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using trifecta.environment;
using trifecta.io;
using trifecta.stage;
using trifecta.simulation;
using trifecta.test.common;
using Xunit;
using SensorConfiguration =
    trifecta.simulation.SensorDriverFactory.SensorConfiguration;

namespace trifecta {
namespace test {
namespace environment {

[Collection("ComponentLog tests")]
public sealed class SensorControllerTests : ComponentLogTestBase {
  [Fact]
  public async
      Task ActiveSensorsCrossThresholdsAndNotifyListenerOnlyOnCrossing() {
    var configurationDirectory =
        Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR");
    if (string.IsNullOrWhiteSpace(configurationDirectory)) {
      throw new InvalidOperationException(
          "Set TRIFECTA_CONFIG_DIR to the top-level configuration directory.");
    }

    const string temperatureConfigurationPath =
        "test/sensors/device/temperature-active.json";
    const string temperatureScriptPath =
        "test/sensors/script/temperature-ascending.json";
    const string pressureConfigurationPath =
        "test/sensors/device/pressure-active.json";
    const string pressureScriptPath =
        "test/sensors/script/pressure-ascending.json";
    var temperatureConfiguration =
        ConfigurationFileLoader.parse<SensorConfiguration>(
            Path.Combine(configurationDirectory, temperatureConfigurationPath));
    var pressureConfiguration =
        ConfigurationFileLoader.parse<SensorConfiguration>(
            Path.Combine(configurationDirectory, pressureConfigurationPath));
    Assert.Equal(SensorType.temperature, temperatureConfiguration.type);
    Assert.Equal(SensorMode.active, temperatureConfiguration.mode);
    Assert.Equal(SensorType.pressure, pressureConfiguration.type);
    Assert.Equal(SensorMode.active, pressureConfiguration.mode);

    // both scripts rise linearly over 3 s: 10 -> 50 and 1000 -> 1060; each
    // threshold below is crossed once on the way up (at 0.75 s, 1.5 s, 2.25 s),
    // always by one threshold per reading
    Temperature[] temperatureThresholds = [20f, 30f, 40f];
    Pressure[] pressureThresholds = [1015, 1030, 1045];

    var listener = new MockCondition();
    var interpreter = SensorInterpreter.singleton;

    var basename = $"SensorControllerTests-{Guid.NewGuid():N}";
    setup(basename);
    float[] temperatureCrossings;
    int[] pressureCrossings;
    try {
      interpreter.SetThresholds(new SensorInterpreter.Thresholds {
        t = (Temperature[])temperatureThresholds.Clone(),
        p = (Pressure[])pressureThresholds.Clone()
      });
      // the interpreter is a singleton: start below every threshold
      interpreter.SensorUpdate(0, (Temperature)0f);
      interpreter.SensorUpdate(0, (Pressure)0f);
      interpreter.Register(listener);

      var temperatureDriver = SensorDriverFactory.scripted(
          temperatureConfigurationPath, temperatureScriptPath);
      var pressureDriver = SensorDriverFactory.scripted(
          pressureConfigurationPath, pressureScriptPath);
      var controller = SensorController.singleton;
      controller.Reset();
      controller.Register(temperatureDriver);
      controller.Register(pressureDriver);
      controller.Start();
      try {
        var timeout = TimeSpan.FromSeconds(6);
        var temperatureTask =
            WaitForReadings("Temperature", temperatureConfiguration.id,
                            temperatureThresholds.Length, timeout, ParseFloat);
        var pressureTask =
            WaitForReadings("Pressure", pressureConfiguration.id,
                            pressureThresholds.Length, timeout, ParseInt);
        await Task.WhenAll(temperatureTask, pressureTask);
        temperatureCrossings = await temperatureTask;
        pressureCrossings = await pressureTask;
      } finally {
        await Task.Run(controller.Stop).WaitAsync(TimeSpan.FromSeconds(2));
        interpreter.Release(listener);
        interpreter.SetThresholds(
            new SensorInterpreter.Thresholds { t = [], p = [] });
      }
    } finally {
      finish();
    }

    // (a) one log message per crossing, each by a single threshold, and in
    // ascending order with the reading landing just past its threshold
    var log = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(
        3,
        log.Count(
            l => l.Contains(
                $"Temperature sensor {temperatureConfiguration.id} crosses 1 thresholds to",
                StringComparison.Ordinal)));
    Assert.Equal(
        3,
        log.Count(
            l => l.Contains(
                $"Pressure sensor {pressureConfiguration.id} crosses 1 thresholds to",
                StringComparison.Ordinal)));
    Assert.Equal(3, temperatureCrossings.Length);
    Assert.Equal(3, pressureCrossings.Length);
    for (int k = 0; k < 3; k++) {
      Assert.InRange(temperatureCrossings[k], temperatureThresholds[k],
                     temperatureThresholds[k] + 1f);
      Assert.InRange(pressureCrossings[k], pressureThresholds[k],
                     pressureThresholds[k] + 1);
    }

    // far more readings were reported than crossings
    Assert.True(
        log.Count(
            l => l.Contains(
                $"Temperature sensor {temperatureConfiguration.id} reports",
                StringComparison.Ordinal)) > 10);
    Assert.True(
        log.Count(l => l.Contains(
                      $"Pressure sensor {pressureConfiguration.id} reports",
                      StringComparison.Ordinal)) > 10);

    // (b) the listener heard exactly those crossings (many more readings
    // arrived, but only crossings notify), with the same values
    Assert.Equal(temperatureCrossings, listener.Temperatures);
    Assert.Equal(pressureCrossings, listener.Pressures);
  }

  [Fact]
  public async Task DirectorIsToldOnSensorThreadsAndOptimizesOnItsOwnThread() {
    var temperatureConfigurationPath =
        "test/sensors/device/temperature-active.json";
    var temperatureScriptPath =
        "test/sensors/script/temperature-ascending.json";
    var pressureConfigurationPath = "test/sensors/device/pressure-active.json";
    var pressureScriptPath = "test/sensors/script/pressure-ascending.json";
    Temperature[] temperatureThresholds = [20f, 30f, 40f];
    Pressure[] pressureThresholds = [1015, 1030, 1045];

    var interpreter = SensorInterpreter.singleton;
    var director = Director.singleton;

    setup($"SensorDirectorTests-{Guid.NewGuid():N}");
    try {
      interpreter.SetThresholds(new SensorInterpreter.Thresholds {
        t = (Temperature[])temperatureThresholds.Clone(),
        p = (Pressure[])pressureThresholds.Clone()
      });
      // the interpreter is a singleton: start below every threshold
      interpreter.SensorUpdate(0, (Temperature)0f);
      interpreter.SensorUpdate(0, (Pressure)0f);

      director.Reset();
      director.configure(new DirectorConfiguration());
      director.Start();

      var controller = SensorController.singleton;
      controller.Reset();
      controller.Register(SensorDriverFactory.scripted(
          temperatureConfigurationPath, temperatureScriptPath));
      controller.Register(SensorDriverFactory.scripted(
          pressureConfigurationPath, pressureScriptPath));
      controller.Start();
      try {
        // stop while the scripts are still rising
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(6);
        while (DateTime.UtcNow < until &&
               CountLines("has crossed a threshold") <
                   temperatureThresholds.Length + pressureThresholds.Length)
          await Task.Delay(20);
        await Task.Delay(300); // let the Director optimize the last one
      } finally {
        await Task.Run(controller.Stop).WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Run(() => director.Stop(true))
            .WaitAsync(TimeSpan.FromSeconds(2));
        interpreter.Release(director);
        interpreter.SetThresholds(
            new SensorInterpreter.Thresholds { t = [], p = [] });
      }
    } finally {
      finish();
    }
    var log = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    string Component(string line) => line[1..line.IndexOf('.')];

    var heard = log.Where(l => l.Contains("has crossed a threshold to"))
                    .Take(6)
                    .ToArray();
    Assert.Equal(6, heard.Length);
    Assert.Equal(3, heard.Count(l => l.Contains("Temperature has crossed")));
    Assert.Equal(3, heard.Count(l => l.Contains("Pressure has crossed")));

    // the Director is told on the sensor driver threads, one per sensor type
    foreach (var l in heard) {
      var expected = l.Contains("Temperature")
                         ? "Active temperature sensor driver #105"
                         : "Active pressure sensor driver #104";
      Assert.Equal(expected, Component(l));
    }

    // the optimizer never runs on a sensor thread; it runs on the Director's
    var optimizations =
        log.Where(l => l.Contains("Now optimizing the cast")).ToArray();
    Assert.NotEmpty(optimizations);
    Assert.All(optimizations, l => Assert.Equal("Director", Component(l)));

    // crossings are coalesced; Start() adds one initial optimization
    Assert.True(optimizations.Length <=
                1 + log.Count(l => l.Contains("has crossed a threshold to")));

    // after the last crossing heard, the Director optimizes at least once more
    int lastHeard =
        Array.FindLastIndex(log, l => l.Contains("has crossed a threshold to"));
    Assert.Contains(log.Skip(lastHeard + 1),
                    l => l.Contains("Now optimizing the cast"));

    int CountLines(string text) {
      var files = FindLogFiles();
      if (files.Length == 0)
        return 0;
      ComponentLog.singleton.Flush();
      return trifecta.test.common.SharedFile.ReadAllText(files.Single())
          .Split('\n')
          .Count(l => l.Contains(text));
    }
  }

  [Fact]
  public async Task CastChangesRunOnTheDirectorThreadAsSensorsCrossLimits() {
    var temperatureConfigurationPath =
        "test/sensors/device/temperature-active.json";
    var temperatureScriptPath =
        "test/sensors/script/temperature-ascending.json";
    var pressureConfigurationPath = "test/sensors/device/pressure-active.json";
    var pressureScriptPath = "test/sensors/script/pressure-ascending.json";
    Temperature[] temperatureThresholds = [20f, 30f, 40f];
    Pressure[] pressureThresholds = [1015, 1030, 1045];

    var interpreter = SensorInterpreter.singleton;
    var director = Director.singleton;

    setup($"SensorDirectorTests-{Guid.NewGuid():N}");
    try {
      interpreter.SetThresholds(new SensorInterpreter.Thresholds {
        t = (Temperature[])temperatureThresholds.Clone(),
        p = (Pressure[])pressureThresholds.Clone()
      });
      // the interpreter is a singleton: start below every threshold
      interpreter.SensorUpdate(0, (Temperature)0f);
      interpreter.SensorUpdate(0, (Pressure)0f);

      director.Reset();
      // role 1 tolerates every condition; role 2 works only for T > 25 and
      // P < 1040, so it is out at the start, in mid-ramp, then out again
      var always = new RoleConfiguration { id = 1, quantity = 2, tmin = -100f,
                                           pmax = 5000 };
      var window = new RoleConfiguration { id = 2, quantity = 2, tmin = 25f,
                                           pmax = 1040 };
      var config = new DirectorConfiguration { mode = TransitionMode.quickstop,
                                               roles = [always, window] };
      var precast = new Dictionary<RoleId, List<Actor>>();
      foreach (var r in config.roles)
        precast[r.id] = Enumerable.Range(0, r.quantity)
                            .Select(i => (Actor) new LoggingActor(i, r))
                            .ToList();
      director.configure(config, precast);
      director.Start();

      var controller = SensorController.singleton;
      controller.Reset();
      controller.Register(SensorDriverFactory.scripted(
          temperatureConfigurationPath, temperatureScriptPath));
      controller.Register(SensorDriverFactory.scripted(
          pressureConfigurationPath, pressureScriptPath));
      controller.Start();
      try {
        // stop while the scripts are still rising
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(6);
        while (DateTime.UtcNow < until &&
               CountLines("has crossed a threshold") <
                   temperatureThresholds.Length + pressureThresholds.Length)
          await Task.Delay(20);
        await Task.Delay(300); // let the Director optimize the last one
      } finally {
        await Task.Run(controller.Stop).WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Run(() => director.Stop(true))
            .WaitAsync(TimeSpan.FromSeconds(2));
        interpreter.Release(director);
        interpreter.SetThresholds(
            new SensorInterpreter.Thresholds { t = [], p = [] });
      }
    } finally {
      finish();
    }

    var log = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    string Component(string line) => line[1..line.IndexOf('.')];

    Assert.Equal(6, log.Count(l => l.Contains("has crossed a threshold to")));
    var calls = log.Where(l => l.Contains("LoggingActor ")).ToArray();
    Assert.NotEmpty(calls);

    // while the Director runs, Actors change only on its own thread; the final
    // cleanup is made on the thread that calls Director.Stop()
    int stopRequested =
        Array.FindIndex(log, l => l.Contains("Director stop requested"));
    Assert.True(stopRequested >= 0);
    var cast = log.Take(stopRequested)
                   .Where(l => l.Contains("LoggingActor "))
                   .ToArray();
    Assert.NotEmpty(cast);
    Assert.All(cast, l => Assert.Equal("Director", Component(l)));
    Assert.All(calls.Skip(cast.Length),
               l => Assert.DoesNotContain("sensor driver", Component(l)));

    var started = calls.Where(l => l.Contains(" Start")).ToArray();
    Assert.Contains(started, l => l.Contains("LoggingActor 1/0 "));
    Assert.Contains(started, l => l.Contains("LoggingActor 1/1 "));

    // the windowed role is stopped before the ramp (initial optimize), then
    // started mid-ramp, then stopped again once pressure leaves its bound
    foreach (var member in new[] { "LoggingActor 2/0", "LoggingActor 2/1" }) {
      var own = calls.Where(l => l.Contains(member + " ")).ToArray();
      Assert.Contains(" Stop", own.First());
      int start = Array.FindIndex(own, l => l.Contains(" Start"));
      Assert.True(start > 0);
      Assert.Contains(own.Skip(start + 1), l => l.Contains(" Stop"));
    }

    // the unconditioned role is only stopped by the cleanup in Director.Stop
    int lastOptimize =
        Array.FindLastIndex(log, l => l.Contains("Now optimizing the cast"));
    for (int i = 0; i < log.Length; i++)
      if (log[i].Contains("LoggingActor 1/") && log[i].Contains(" Stop"))
        Assert.True(i > lastOptimize);

    int CountLines(string text) {
      var files = FindLogFiles();
      if (files.Length == 0)
        return 0;
      ComponentLog.singleton.Flush();
      return trifecta.test.common.SharedFile.ReadAllText(files.Single())
          .Split('\n')
          .Count(l => l.Contains(text));
    }
  }

  // records which component's thread enters Start() and Stop()
  private sealed class LoggingActor : Actor {
    private readonly string _name;

    public LoggingActor(int index, RoleConfiguration role) : base(index, role) {
      _name = $"{role.id}/{index}";
    }

    public override void Start(bool ignorable = false) {
      ComponentLog.singleton.Log($"LoggingActor {_name} Start");
    }

    public override void Stop(ActorPhase mode, Action? finalize = null,
                              bool ignorable = true) {
      ComponentLog.singleton.Log($"LoggingActor {_name} Stop");
    }
  }

  private sealed class MockCondition : IConditionAware {
    private readonly object _lock = new();
    private readonly List<Temperature> _temperatures = [];
    private readonly List<Pressure> _pressures = [];

    public void ThresholdCross(Temperature t) {
      lock (_lock) _temperatures.Add(t);
    }

    public void ThresholdCross(Pressure p) { lock (_lock) _pressures.Add(p); }

    public Temperature[] Temperatures {
      get { lock (_lock) return _temperatures.ToArray(); }
    }

    public Pressure[] Pressures {
      get { lock (_lock) return _pressures.ToArray(); }
    }
  }

  private async Task<T[]> WaitForReadings<T>(string sensorType, int sensorId,
                                             int expectedCount,
                                             TimeSpan timeout,
                                             Func<string, T> parse) {
    var deadline = DateTime.UtcNow + timeout;
    T[] readings = [];
    while (DateTime.UtcNow < deadline) {
      var logFiles = FindLogFiles();
      if (logFiles.Length > 0) {
        var log = await trifecta.test.common.SharedFile.ReadAllTextAsync(logFiles.Single());
        readings = ParseReadings(log, sensorType, sensorId, parse);
        if (readings.Length >= expectedCount) {
          return readings.Take(expectedCount).ToArray();
        }
      }

      await Task.Delay(50);
    }

    return readings;
  }

  private static T[] ParseReadings<T>(string log, string sensorType,
                                      int sensorId, Func<string, T> parse) {
    return log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.Contains($"{sensorType} sensor {sensorId} crosses ",
                                     StringComparison.Ordinal))
        .Select(line => {
          var separator = line.LastIndexOf(" to ", StringComparison.Ordinal);
          if (separator < 0) {
            throw new FormatException($"Invalid sensor log entry: {line}");
          }

          return parse(line[(separator + 4)..].Trim());
        })
        .ToArray();
  }

  private static float ParseFloat(string value) =>
      float.Parse(value, NumberStyles.Float, CultureInfo.CurrentCulture);

  private static int ParseInt(string value) =>
      int.Parse(value, NumberStyles.Integer, CultureInfo.CurrentCulture);
}

}
}
}
