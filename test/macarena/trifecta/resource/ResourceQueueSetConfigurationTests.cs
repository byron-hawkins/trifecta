/* author: Copilot */
using System;
using System.IO;
using trifecta.resource;
using trifecta.simulation;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace resource {

[Collection("ComponentLog tests")]
public sealed class ResourceQueueSetConfigurationTests : ComponentLogTestBase {
  [Fact]
  public void ResourceWorkBoundsDeserializeAsMilliseconds() {
    var configurationDirectory =
        Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR");
    if (string.IsNullOrWhiteSpace(configurationDirectory)) {
      throw new InvalidOperationException(
          "Set TRIFECTA_CONFIG_DIR to the top-level configuration directory.");
    }

    var path =
        Path.Combine(configurationDirectory, "test", "stage", "resources.json");
    var configuration = ConfigurationFileLoader.parse<ResourceSet>(path);

    Assert.Equal(2, configuration.elements.Count);
    Assert.All(configuration.elements, resource => {
      Assert.Equal(TimeSpan.FromMilliseconds(50), resource.minwork);
      Assert.Equal(TimeSpan.FromMilliseconds(1000), resource.maxwork);
    });
  }

  [Fact]
  public void ConfigureLogsEachResourceType() {
    var configurationDirectory =
        Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR");
    if (string.IsNullOrWhiteSpace(configurationDirectory)) {
      throw new InvalidOperationException(
          "Set TRIFECTA_CONFIG_DIR to the top-level configuration directory.");
    }

    var path =
        Path.Combine(configurationDirectory, "test", "stage", "resources.json");
    var configuration = ConfigurationFileLoader.parse<ResourceSet>(path);
    Assert.NotEmpty(configuration.elements);

    setup($"ResourceQueueSetConfigurationTests-{Guid.NewGuid():N}");
    try {
      ResourceQueueSet.singleton.configure(configuration);
    } finally {
      finish();
    }

    var files = FindLogFiles();
    Assert.True(
        files.Length == 1,
        $"Expected one resource queue set log file; found {files.Length}.");
    var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Contains(
        lines,
        line => line.Contains("configure", StringComparison.OrdinalIgnoreCase));
    foreach (var resource in configuration.elements) {
      Assert.Contains(
          lines,
          line => line.Contains(
              $"Constructing a ResourceQueue for resource {resource.name}",
              StringComparison.OrdinalIgnoreCase));
    }
  }
}

}
}
}
