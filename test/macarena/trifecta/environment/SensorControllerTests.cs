/* author: Copilot */
using System;
using System.Threading.Tasks;
using trifecta.environment;
using trifecta.simulation;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace environment {

[Collection("ComponentLog tests")]
public sealed class SensorControllerTests : ComponentLogTestBase {
  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  public async Task StopsPromptlyAfterRegisteringBothSensors(int cycle) {
    var temperatureDriver = SensorDriverFactory.scripted(
        "test/sensors/device/temperature-passive.json",
        "test/sensors/script/temperature-ascending.json");
    var pressureDriver = SensorDriverFactory.scripted(
        "test/sensors/device/pressure-active.json",
        "test/sensors/script/pressure-ascending.json");
    Assert.NotNull(temperatureDriver);
    Assert.NotNull(pressureDriver);

    setup($"SensorControllerStopTests-{cycle}-{Guid.NewGuid():N}");
    try {
      var controller = SensorController.singleton;
      controller.Reset();
      controller.Register(temperatureDriver);
      controller.Register(pressureDriver);
      controller.Start();
      await Task.Run(controller.Stop).WaitAsync(TimeSpan.FromSeconds(2));
      controller.Release(temperatureDriver);
      controller.Release(pressureDriver);
    } finally {
      finish();
    }
  }
}

}
}
}
