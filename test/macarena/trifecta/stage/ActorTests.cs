/* author: Copilot */
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using trifecta.resource;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace stage {

[Collection("ComponentLog tests")]
public sealed class ActorTests : ComponentLogTestBase {
  [Fact]
  public async Task StartsAndStopsDuringLocalSceneWork() {
    var tickrate = TimeSpan.FromMilliseconds(50);
    var configuration = new RoleConfiguration {
      id = 42, quantity = 1, tmin = 0, pmax = 0,
      scenes = [new SceneConfiguration { duration = TimeSpan.FromSeconds(1),
                                         tickrate = tickrate, resources = [] }]
    };
    var finalized = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    setup($"ActorLifecycleTests-{Guid.NewGuid():N}");
    try {
      ResourceQueueSet.singleton.Reset();
      var actor = new Actor(0, configuration);
      actor.Start();
      await WaitForLogEntry("[work: start]", TimeSpan.FromSeconds(2));

      var timer = Stopwatch.StartNew();
      actor.Stop(ActorPhase.stop_now, () => finalized.TrySetResult(true));
      await finalized.Task.WaitAsync(TimeSpan.FromTicks(2 * tickrate.Ticks));

      Assert.True(timer.Elapsed <= 2 * tickrate);
      finish();
      var log = ReadLog();
      Assert.Contains("Actor 42/0 start requested", log);
      Assert.Contains("[work: start]", log);
      Assert.Contains("[work: stop]", log);
    } finally {
      finish();
    }
  }

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
      _ = new Actor(0, configuration);
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

  private async Task WaitForLogEntry(string entry, TimeSpan timeout) {
    var timer = Stopwatch.StartNew();
    do {
      finish();
      if (FindLogFiles().Length == 1 &&
          ReadLog().Contains(entry, StringComparison.OrdinalIgnoreCase)) {
        return;
      }

      await Task.Delay(10);
    } while (timer.Elapsed < timeout);

    Assert.Fail($"Timed out waiting for log entry '{entry}'.");
  }
}

}
}
}
