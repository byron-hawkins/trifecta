/* author: Copilot */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using trifecta.environment;
using trifecta.test.common;
using Xunit;

namespace trifecta.test.environment;

public sealed class SensorInterpreterTests : ComponentLogTestBase {
  [Fact]
  public void NotifiesAwareWhenTemperatureOrPressureCrossesAThreshold() {
    var random = Random.Shared;
    var temperatureThresholds = CreateTemperatureThresholds(random);
    var pressureThresholds = CreatePressureThresholds(random);
    var thresholds =
        new SensorInterpreter.Thresholds { t = temperatureThresholds,
                                           p = pressureThresholds };
    var interpreter = SensorInterpreter.singleton;
    var aware = new RecordingConditionAware();
    var expectedTemperatureEvents = new List<float>();
    var expectedPressureEvents = new List<int>();
    ISensorAware<float> temperatureSensor = interpreter;
    ISensorAware<int> pressureSensor = interpreter;

    interpreter.SetThresholds(thresholds);
    interpreter.Register(aware);

    try {
      ApplyChanges(temperatureSensor.SensorUpdate, temperatureThresholds,
                   GenerateReadings(random), expectedTemperatureEvents);
      ApplyChanges(pressureSensor.SensorUpdate, pressureThresholds,
                   GenerateReadings(random), expectedPressureEvents);
    } finally {
      interpreter.Release(aware);
    }

    Assert.Equal(expectedTemperatureEvents, aware.TemperatureEvents);
    Assert.Equal(expectedPressureEvents, aware.PressureEvents);
  }

  [Fact]
  public async Task SensorUpdatesCanBeReadBackAndAreLogged() {
    var basename = $"SensorInterpreterTests-{Guid.NewGuid():N}";
    setup(basename);

    var temperatureId = Random.Shared.Next(1, int.MaxValue);
    var temperature = (float)Random.Shared.Next(-100_000, 100_001);
    var pressureId = Random.Shared.Next(1, int.MaxValue);
    var pressure = Random.Shared.Next(-100_000, 100_001);

    try {
      SensorInterpreter.singleton.SensorUpdate(temperatureId, temperature);
      SensorInterpreter.singleton.SensorUpdate(pressureId, pressure);
    } finally {
      finish();
    }

    var failures = new List<string>();
    var actualTemperature = SensorInterpreter.singleton.GetTemperature();
    if (actualTemperature != temperature) {
      failures.Add(
          $"Expected temperature {temperature}, got {actualTemperature}.");
    }

    var actualPressure = SensorInterpreter.singleton.GetPressure();
    if (actualPressure != pressure) {
      failures.Add($"Expected pressure {pressure}, got {actualPressure}.");
    }

    var deadline = DateTime.UtcNow.AddSeconds(3);
    string[] logFiles = [];
    while (DateTime.UtcNow < deadline) {
      logFiles = FindLogFiles();
      if (logFiles.Length > 0) {
        break;
      }

      await Task.Delay(50);
    }

    if (logFiles.Length == 0) {
      failures.Add(
          $"Expected a log file for '{basename}' in '{OutputDirectory}'.");
    } else {
      var log = await trifecta.test.common.SharedFile.ReadAllTextAsync(logFiles[0]);
      AddMissingEntry(log, "Temperature", temperatureId, temperature.ToString(),
                      failures);
      AddMissingEntry(log, "Pressure", pressureId, pressure.ToString(),
                      failures);
    }

    Assert.True(failures.Count == 0,
                string.Join(Environment.NewLine, failures));
  }

  private static void AddMissingEntry(string log, string sensorType,
                                      int sensorId, string reading,
                                      ICollection<string> failures) {
    var hasEntry =
        log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Contains(sensorType, StringComparison.Ordinal) &&
                         line.Contains(sensorId.ToString(),
                                       StringComparison.Ordinal) &&
                         line.Contains(reading, StringComparison.Ordinal));

    if (!hasEntry) {
      failures.Add(
          $"Expected a {sensorType} update log entry containing id {sensorId} and reading {reading}.");
    }
  }

  private static float[] CreateTemperatureThresholds(Random random) {
    var count = random.Next(3, 6);
    var thresholds = new HashSet<int>();
    while (thresholds.Count < count) {
      thresholds.Add(random.Next(1, 100_000));
    }

    return thresholds.Order().Select(value => (float)value).ToArray();
  }

  private static int[] CreatePressureThresholds(Random random) {
    var count = random.Next(3, 6);
    var thresholds = new HashSet<int>();
    while (thresholds.Count < count) {
      thresholds.Add(random.Next(1, 100_000));
    }

    return thresholds.Order().ToArray();
  }

  private static int[] GenerateReadings(Random random) {
    var readings = new List<int> { 0 };
    var uniqueReadings = new HashSet<int> { 0, 100_001 };
    while (readings.Count < 30) {
      var reading = random.Next(1, 100_001);
      if (uniqueReadings.Add(reading)) {
        readings.Add(reading);
      }
    }

    readings.Add(100_001);
    return readings.ToArray();
  }

  private static void ApplyChanges<T>(Action<int, T> update, T[] thresholds,
                                      int[] readings,
                                      ICollection<T> expectedEvents)
      where T : IComparable<T> {
    T previous = (T)Convert.ChangeType(0, typeof(T));
    foreach (var rawReading in readings) {
      T reading = (T)Convert.ChangeType(rawReading, typeof(T));
      if (CrossesAnyThreshold(previous, reading, thresholds)) {
        expectedEvents.Add(reading);
      }

      update(Random.Shared.Next(1, int.MaxValue), reading);
      previous = reading;
    }
  }

  private static bool CrossesAnyThreshold<T>(T previous, T current,
                                             T[] thresholds)
      where T : IComparable<T> {
    return thresholds.Any(threshold => (previous.CompareTo(threshold) < 0 &&
                                        current.CompareTo(threshold) >= 0) ||
                                       (previous.CompareTo(threshold) > 0 &&
                                        current.CompareTo(threshold) <= 0));
  }

  private sealed class RecordingConditionAware : IConditionAware {
    public List<float> TemperatureEvents { get; } = [];
    public List<int> PressureEvents { get; } = [];

    public void ThresholdCross(float temperature) {
      TemperatureEvents.Add(temperature);
    }

    public void ThresholdCross(int pressure) { PressureEvents.Add(pressure); }
  }
}
