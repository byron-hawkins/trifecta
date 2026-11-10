/* author: Copilot */
using System;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace stage {

[Collection("ComponentLog tests")]
public sealed class ActorTests : ComponentLogTestBase {
  [Fact]
  public void ConstructionLogsRoleAndEachScene() {
    var configuration = new RoleConfiguration {
      id = 17, quantity = 4, tmin = 15, pmax = 80,
      scenes =
          [
            new SceneConfiguration { duration = TimeSpan.FromSeconds(3),
                                     resources = ["heater", "pump"] },
            new SceneConfiguration { duration = TimeSpan.FromSeconds(5),
                                     resources = ["fan"] }
          ]
    };

    setup($"ActorTests-{Guid.NewGuid():N}");
    try {
      _ = new Actor(configuration);
    } finally {
      finish();
    }

    var files = FindLogFiles();
    Assert.True(files.Length == 1,
                $"Expected one actor log file; found {files.Length}.");
    var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Contains(
        lines,
        line => line.Contains("role", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("17", StringComparison.Ordinal) &&
                line.Contains("15", StringComparison.Ordinal) &&
                line.Contains("80", StringComparison.Ordinal));
    Assert.Contains(
        lines,
        line => line.Contains("00:00:03", StringComparison.Ordinal) &&
                line.Contains("heater", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("pump", StringComparison.OrdinalIgnoreCase));
    Assert.Contains(
        lines,
        line => line.Contains("00:00:05", StringComparison.Ordinal) &&
                line.Contains("fan", StringComparison.OrdinalIgnoreCase));
  }
}

}
}
}
