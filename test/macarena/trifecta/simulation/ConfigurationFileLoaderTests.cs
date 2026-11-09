/* author: Copilot */
using System;
using System.IO;
using trifecta.environment;
using trifecta.simulation;
using Xunit;
using SensorConfiguration =
    trifecta.simulation.SensorDriverFactory.SensorConfiguration;

namespace trifecta {
namespace test {
namespace simulation {

public sealed class ConfigurationFileLoaderTests {
  [Fact]
  public void SensorJsonFilesDeserializeIntoTheirModels() {
    var configurationDirectory =
        Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR");
    if (string.IsNullOrWhiteSpace(configurationDirectory)) {
      throw new InvalidOperationException(
          "Set TRIFECTA_CONFIG_DIR to the top-level configuration directory.");
    }

    var devicePath = Path.Combine(configurationDirectory, "test", "sensors",
                                  "device", "temperature-passive.json");
    var scriptPath = Path.Combine(configurationDirectory, "test", "sensors",
                                  "script", "temperature-ascending.json");
    var configuration =
        ConfigurationFileLoader.parse<SensorConfiguration>(devicePath);
    var script = ConfigurationFileLoader.parse<SensorScript<float>>(scriptPath);

    Assert.Equal(103, configuration.id);
    Assert.Equal("temperature-passive", configuration.name);
    Assert.Equal(TimeSpan.FromMilliseconds(50), configuration.tickinterval);
    Assert.Equal(SensorType.temperature, configuration.type);
    Assert.Equal(SensorMode.passive, configuration.mode);
    Assert.Equal(2, script.Events.Count);
    Assert.Equal(TimeSpan.FromMilliseconds(3000), script.Events[0].duration);
    Assert.Equal(10.0f, script.Events[0].value);
    Assert.Equal(TimeSpan.FromMilliseconds(1000), script.Events[1].duration);
    Assert.Equal(50.0f, script.Events[1].value);
  }

  [Fact]
  public void LoadsTemperaturePassiveAscendingScript() {
    var configurationDirectory =
        Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR");
    if (string.IsNullOrWhiteSpace(configurationDirectory)) {
      throw new InvalidOperationException(
          "Set TRIFECTA_CONFIG_DIR to the top-level configuration directory.");
    }

    var filename = Path.Combine(configurationDirectory, "test", "sensors",
                                "script", "temperature-ascending.json");
    var script = ConfigurationFileLoader.parse<SensorScript<float>>(filename);

    Assert.NotNull(script);
    Assert.Equal(2, script.Events.Count);
    Assert.Equal(TimeSpan.FromMilliseconds(3000), script.Events[0].duration);
    Assert.Equal(10.0f, script.Events[0].value);
    Assert.Equal(TimeSpan.FromMilliseconds(1000), script.Events[1].duration);
    Assert.Equal(50.0f, script.Events[1].value);
  }
}

}
}
}
