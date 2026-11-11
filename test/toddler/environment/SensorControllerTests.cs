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
using SensorConfiguration = trifecta.simulation.SensorDriverFactory.SensorConfiguration;

namespace trifecta {
namespace test {
namespace environment {

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
    const string scriptPath =
        "test/sensors/script/temperature-ascending.json";
    var configurationFile =
        Path.Combine(configurationDirectory, configurationPath);
    var scriptFile = Path.Combine(configurationDirectory, scriptPath);
    var configuration = new SensorConfiguration();
    configuration.Load(configurationFile);
    var script = new SensorScript<float>();
    script.Load(scriptFile);

    Assert.True(configuration._tickinterval > TimeSpan.Zero);
    Assert.True(script.Events.Count >= 2);
    var firstEvent = script.Events[0];
    var secondEvent = script.Events[1];
    Assert.True(firstEvent.duration > TimeSpan.Zero);
    Assert.Equal(0, firstEvent.duration.Ticks % configuration._tickinterval.Ticks);

    var tickCount =
        (int)(firstEvent.duration.Ticks / configuration._tickinterval.Ticks);
    var expectedReadings = Enumerable.Range(0, tickCount + 1)
        .Select(index => firstEvent.value +
                         (secondEvent.value - firstEvent.value) * index /
                             tickCount)
        .ToArray();

    var basename = $"SensorControllerTests-{Guid.NewGuid():N}";
    setup(basename);
    float[] actualReadings;
    try {
      var driver = SensorDriverFactory.scripted(configurationPath, scriptPath);
      var controller = new SensorController(SensorInterpreter.singleton);
      controller.Register(driver);
      controller.Start();
      actualReadings = await WaitForReadings(
          configuration._id, expectedReadings.Length,
          firstEvent.duration + TimeSpan.FromSeconds(2));
    } finally {
      finish();
    }

    Assert.Equal(expectedReadings.Length, actualReadings.Length);
    Assert.Equal(expectedReadings, actualReadings);
  }

  private async Task<float[]> WaitForReadings(int sensorId, int expectedCount,
                                             TimeSpan timeout) {
    var deadline = DateTime.UtcNow + timeout;
    float[] readings = [];
    while (DateTime.UtcNow < deadline) {
      var logFiles = FindLogFiles();
      if (logFiles.Length > 0) {
        var log = await trifecta.test.common.SharedFile.ReadAllTextAsync(logFiles.Single());
        readings = ParseTemperatureReadings(log, sensorId);
        if (readings.Length >= expectedCount) {
          return readings.Take(expectedCount).ToArray();
        }
      }

      await Task.Delay(50);
    }

    return readings;
  }

  private static float[] ParseTemperatureReadings(string log, int sensorId) {
    return log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(line => line.Contains($"Temperature sensor {sensorId}",
                                     StringComparison.Ordinal))
        .Select(line => {
          var separator = line.LastIndexOf(" to ", StringComparison.Ordinal);
          if (separator < 0 ||
              !float.TryParse(line.AsSpan(separator + 4).Trim(),
                              NumberStyles.Float, CultureInfo.CurrentCulture,
                              out var reading)) {
            return (float?)null;
          }

          return reading;
        })
        .Where(reading => reading.HasValue)
        .Select(reading => reading!.Value)
        .ToArray();
  }
}

}
}
}
