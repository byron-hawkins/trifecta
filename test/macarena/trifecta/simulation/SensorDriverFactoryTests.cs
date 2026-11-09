/* author: Copilot */
using trifecta.simulation;
using Xunit;

namespace trifecta {
namespace test {
namespace simulation {

public sealed class SensorDriverFactoryTests {
  [Fact]
  public void CreatesScriptedDriverFromRelativeConfigurationPaths() {
    var driver = SensorDriverFactory.scripted(
        "test/sensors/device/temperature-passive.json",
        "test/sensors/script/temperature-ascending.json");

    Assert.NotNull(driver);
  }
}

}
}
}
