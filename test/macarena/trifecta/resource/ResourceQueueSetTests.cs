/* author: Copilot */
using System;
using System.Diagnostics;
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
        await WaitForMessages(0, 1);
        for (var i = 1; i <= 5; i++) {
          await Task.Delay(TimeSpan.FromMilliseconds(100));
          queueSet.Bump();
          await WaitForMessages(i, i + 1);
        }
      } finally {
        await Task.Run(queueSet.Stop).WaitAsync(TimeSpan.FromSeconds(2));
      }

      finish();
      var log = ReadLog();
      Assert.Matches(new Regex(@"\bstart\b", RegexOptions.IgnoreCase), log);
      Assert.Equal(5, CountLines(log, @"\bbump\b"));
      Assert.Equal(6, CountLines(log, "finds the queues empty"));
      Assert.Equal(6, CountLines(log, "goes idle"));
    } finally {
      finish();
    }
  }

  private async Task WaitForMessages(int bumps, int idleCycles) {
    var timer = Stopwatch.StartNew();
    string log = string.Empty;
    do {
      finish();
      if (FindLogFiles().Length == 1) {
        log = ReadLog();
        if (CountLines(log, @"\bbump\b") >= bumps &&
            CountLines(log, "finds the queues empty") >= idleCycles &&
            CountLines(log, "goes idle") >= idleCycles) {
          return;
        }
      }
      await Task.Delay(20);
    } while (timer.Elapsed < TimeSpan.FromSeconds(2));

    Assert.Fail($"Timed out waiting for {bumps} bumps and {idleCycles} " +
                $"idle cycles. Log:\n{log}");
  }

  private static int CountLines(string log, string pattern) =>
      log.Split('\n').Count(line => Regex.IsMatch(line, pattern,
                                                  RegexOptions.IgnoreCase));
}

}
}
}
