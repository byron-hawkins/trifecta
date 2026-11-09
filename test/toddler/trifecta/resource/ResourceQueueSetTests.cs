/* author: Copilot */
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using trifecta.resource;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace resource {

[Collection("ComponentLog tests")]
public sealed class ResourceQueueSetTests : ComponentLogTestBase {
  [Fact]
  public async Task StartsBumpsFiveTimesAndStopsPromptly() {
    setup($"ResourceQueueSetTests-{Guid.NewGuid():N}");
    var queueSet = ResourceQueueSet.singleton;
    try {
      queueSet.Start();
      try {
        for (var i = 0; i < 5; i++) {
          await Task.Delay(TimeSpan.FromMilliseconds(100));
          queueSet.Bump();
        }
      } finally {
        await Task.Run(queueSet.Stop).WaitAsync(TimeSpan.FromSeconds(2));
      }
    } finally {
      finish();
    }

    var files = FindLogFiles();
    Assert.True(files.Length == 1,
                $"Expected one queue set log file; found {files.Length}.");
    var log = ReadLog();
    Assert.Matches(new Regex(@"\bstart\b", RegexOptions.IgnoreCase), log);
    Assert.Equal(5, log.Split('\n').Count(
                        line => Regex.IsMatch(line, @"\bbump\b",
                                              RegexOptions.IgnoreCase)));
  }
}

}
}
}
