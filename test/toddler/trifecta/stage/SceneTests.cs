/* author: Copilot */
using System;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace stage {

[Collection("ComponentLog tests")]
public sealed class SceneTests : ComponentLogTestBase {
  [Fact]
  public void ConstructionLogsDurationAndRequiredResources() {
    var configuration =
        new SceneConfiguration { duration = TimeSpan.FromSeconds(3),
                                 resources = ["heater", "pump"] };

    setup($"SceneTests-{Guid.NewGuid():N}");
    try {
      _ = new Scene(configuration);
    } finally {
      finish();
    }

    var files = FindLogFiles();
    Assert.True(files.Length == 1,
                $"Expected one scene log file; found {files.Length}.");
    var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Contains(
        lines,
        line => line.Contains("00:00:03", StringComparison.Ordinal) &&
                line.Contains("heater", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("pump", StringComparison.OrdinalIgnoreCase));
  }
}

}
}
}
