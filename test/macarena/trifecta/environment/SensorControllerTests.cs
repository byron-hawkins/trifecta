/* author: Copilot */
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using trifecta.environment;
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
  public async Task StartLogsScriptValuesAtTheConfiguredTickRate() {
    var configurationDirectory =
        Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR");
    if (string.IsNullOrWhiteSpace(configurationDirectory)) {
      throw new InvalidOperationException(
          "Set TRIFECTA_CONFIG_DIR to the top-level configuration directory.");
    }

    const string configurationPath =
        "test/sensors/device/temperature-passive.json";
    const string scriptPath = "test/sensors/script/temperature-ascending.json";
    const string pressureConfigurationPath =
        "test/sensors/device/pressure-active.json";
    const string pressureScriptPath =
        "test/sensors/script/pressure-ascending.json";
    var configurationFile =
        Path.Combine(configurationDirectory, configurationPath);
    var scriptFile = Path.Combine(configurationDirectory, scriptPath);
    var configuration =
        ConfigurationFileLoader.parse<SensorConfiguration>(configurationFile);
    var script = ConfigurationFileLoader.parse<SensorScript<float>>(scriptFile);
    var pressureConfiguration =
        ConfigurationFileLoader.parse<SensorConfiguration>(
            Path.Combine(configurationDirectory, pressureConfigurationPath));
    var pressureScript = ConfigurationFileLoader.parse<SensorScript<int>>(
        Path.Combine(configurationDirectory, pressureScriptPath));

    Assert.True(configuration.tickinterval > TimeSpan.Zero);
    Assert.True(script.events.Count >= 2);
    var firstEvent = script.events[0];
    var secondEvent = script.events[1];
    Assert.True(firstEvent.duration > TimeSpan.Zero);
    Assert.Equal(0,
                 firstEvent.duration.Ticks % configuration.tickinterval.Ticks);

    var tickCount =
        (int)(firstEvent.duration.Ticks / configuration.tickinterval.Ticks);
    var expectedReadings =
        Enumerable.Range(0, tickCount + 1)
            .Select(index => firstEvent.value +
                             (secondEvent.value - firstEvent.value) * index /
                                 tickCount)
            .ToArray();

    Assert.Equal(SensorType.pressure, pressureConfiguration.type);
    Assert.Equal(SensorMode.active, pressureConfiguration.mode);
    Assert.True(pressureConfiguration.tickinterval > TimeSpan.Zero);
    Assert.True(pressureScript.events.Count >= 2);
    var firstPressureEvent = pressureScript.events[0];
    var secondPressureEvent = pressureScript.events[1];
    Assert.True(firstPressureEvent.duration > TimeSpan.Zero);
    Assert.Equal(0, firstPressureEvent.duration.Ticks %
                        pressureConfiguration.tickinterval.Ticks);
    var pressureTickCount = (int)(firstPressureEvent.duration.Ticks /
                                  pressureConfiguration.tickinterval.Ticks);
    Assert.Equal(0, (secondPressureEvent.value - firstPressureEvent.value) %
                        pressureTickCount);
    var expectedPressureReadings =
        Enumerable.Range(0, pressureTickCount + 1)
            .Select(index =>
                        firstPressureEvent.value +
                        (secondPressureEvent.value - firstPressureEvent.value) *
                            index / pressureTickCount)
            .ToArray();

    var basename = $"SensorControllerTests-{Guid.NewGuid():N}";
    setup(basename);
    float[] actualReadings;
    int[] actualPressureReadings;
    try {
      var driver = SensorDriverFactory.scripted(configurationPath, scriptPath);
      var pressureDriver = SensorDriverFactory.scripted(
          pressureConfigurationPath, pressureScriptPath);
      Assert.NotNull(driver);
      Assert.NotNull(pressureDriver);
      var controller = new SensorController(SensorInterpreter.singleton);
      controller.Register(driver);
      controller.Register(pressureDriver);
      controller.Start();
      var temperatureTask = WaitForReadings(
          "Temperature", configuration.id, expectedReadings.Length,
          firstEvent.duration + TimeSpan.FromSeconds(2), ParseFloat);
      var pressureTask = WaitForReadings(
          "Pressure", pressureConfiguration.id, expectedPressureReadings.Length,
          firstPressureEvent.duration + TimeSpan.FromSeconds(2), ParseInt);
      await Task.WhenAll(temperatureTask, pressureTask);
      actualReadings = await temperatureTask;
      actualPressureReadings = await pressureTask;
    } finally {
      finish();
    }

    Assert.Equal(expectedReadings.Length, actualReadings.Length);
    Assert.Equal(expectedReadings, actualReadings);
    Assert.Equal(expectedPressureReadings.Length,
                 actualPressureReadings.Length);
    Assert.Equal(expectedPressureReadings, actualPressureReadings);
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
        var log = await File.ReadAllTextAsync(logFiles.Single());
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
        .Where(line => line.Contains($"{sensorType} sensor {sensorId} ",
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
