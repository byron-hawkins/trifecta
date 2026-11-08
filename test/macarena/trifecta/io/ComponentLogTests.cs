/* author: Copilot */
using System;
using trifecta.io;
using trifecta.test.common;
using Xunit;

namespace trifecta.test.io;

[Collection("ComponentLog tests")]
public sealed class ComponentLogTests : ComponentLogTestBase {
  [Fact]
  public void WritesSingleMessageToLog() {
    var basename = $"ComponentLogTests-{Guid.NewGuid():N}";
    setup(basename);
    const string componentName = "WritesSingleMessageToLog";
    using (ComponentLog.singleton.Register(componentName)) {
      ComponentLog.singleton.Log("Hello Log");
    }

    finish();

    var logFiles = FindLogFiles();
    Assert.True(
        logFiles.Length > 0,
        $"Expected a log file named '{basename}.<timestamp>.log' in '{OutputDirectory}'.");

    var contents = ReadLog();
    Assert.Contains("Hello Log", contents, StringComparison.Ordinal);
    Assert.Contains($"ComponentLog releases component {componentName}",
                    contents, StringComparison.Ordinal);
  }
}
