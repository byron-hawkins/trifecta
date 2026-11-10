/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
  public async Task RequestsAndReleasesResourceDuringSceneWork() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var resourceType = new ResourceType { name = "heater", minwork = sceneTick,
                                          maxwork = TimeSpan.FromSeconds(2) };
    var resource = new ResourceInstance(resourceType, new MockResourceDriver());
    var queues = new MockResourceQueueSet();
    var configuration = new RoleConfiguration {
      id = 43, quantity = 1,
      scenes = [new SceneConfiguration { duration = TimeSpan.FromSeconds(1),
                                         tickrate = sceneTick,
                                         resources = [resourceType.name] }]
    };

    setup($"ActorResourceTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      try {
        var consumer =
            await queues.Requested.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(new[] { resourceType.name }, queues.RequestedNames);
        await queues.Grant(consumer, resource)
            .WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForLogEntry("[work: start]", TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromTicks(4 * sceneTick.Ticks));
      } finally {
        var finalized = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stop =
            Task.Run(() => actor.Stop(ActorPhase.stop_now,
                                      () => finalized.TrySetResult(true)));
        await Task.WhenAll(stop, finalized.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
      }

      Assert.Equal(1, queues.RequestCount);
      var release = Assert.Single(queues.Releases);
      Assert.Same(resource, Assert.Single(release));
    } finally {
      finish();
    }
  }

  [Fact]
  public async Task ResourceWorkUnderflowHoldsResourceUntilMinimumWork() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var resourceTick = TimeSpan.FromMilliseconds(50);
    var sceneDuration = TimeSpan.FromMilliseconds(200);
    var minWork = TimeSpan.FromMilliseconds(600);
    var resourceType =
        new ResourceType { name = "heater", tickrate = resourceTick,
                           minwork = minWork,
                           maxwork = TimeSpan.FromSeconds(2) };
    var resource = new ResourceInstance(resourceType, new MockResourceDriver());
    var queues = new MockResourceQueueSet();
    var configuration = new RoleConfiguration {
      id = 45, quantity = 1,
      scenes = [new SceneConfiguration { duration = sceneDuration,
                                         tickrate = sceneTick,
                                         resources = [resourceType.name] }]
    };

    setup($"ActorResourceUnderflowTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      ResourceRelease firstRelease;
      long grantTime;
      try {
        var consumer =
            await queues.Requested.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(new[] { resourceType.name }, queues.RequestedNames);
        grantTime = await queues.Grant(consumer, resource)
                        .WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForLogEntry("Actor 45/0 [Scene: 0] [work: start]",
                              TimeSpan.FromSeconds(2));
        firstRelease =
            await queues.FirstRelease.WaitAsync(TimeSpan.FromSeconds(3));
      } finally {
        var finalized = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stop =
            Task.Run(() => actor.Stop(ActorPhase.stop_now,
                                      () => finalized.TrySetResult(true)));
        await Task.WhenAll(stop, finalized.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
      }

      Assert.Same(resource, Assert.Single(firstRelease.Resources));
      var holdTime =
          Stopwatch.GetElapsedTime(grantTime, firstRelease.Timestamp);
      var tolerance = TimeSpan.FromTicks(2 * sceneTick.Ticks);
      Assert.InRange(holdTime, minWork, resourceType.maxwork + tolerance);
      finish();
      var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
      Assert.Contains(lines,
                      line => line.Contains("Actor 45/0 completes Scene: 0",
                                            StringComparison.Ordinal));
      var start = Array.FindIndex(
          lines, line => line.Contains("Actor 45/0 [Scene: 0] [work: start]",
                                       StringComparison.Ordinal));
      Assert.True(start >= 0, "Missing underflow scene work-start log.");
      var workStop = Array.FindIndex(
          lines, start + 1,
          line => line.Contains("Actor 45/0 [Scene: 0] [work: stop]",
                                StringComparison.Ordinal));
      Assert.True(workStop > start, "Missing underflow scene work-stop log.");
      Assert.InRange(LogTimestamp(lines[workStop]) - LogTimestamp(lines[start]),
                     sceneDuration - tolerance, sceneDuration + tolerance);
    } finally {
      finish();
    }
  }

  [Fact]
  public async Task StartsAndStopsDuringLocalSceneWork() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var configuration = new RoleConfiguration {
      id = 42, quantity = 1, tmin = 0, pmax = 0,
      scenes = [new SceneConfiguration { duration = TimeSpan.FromSeconds(1),
                                         tickrate = sceneTick, resources = [] }]
    };
    var finalized = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    setup($"ActorLifecycleTests-{Guid.NewGuid():N}");
    try {
      ResourceQueueSet.singleton.Reset();
      var actor = new Actor(0, configuration);
      actor.Start();
      await WaitForLogEntry("[work: start]", TimeSpan.FromSeconds(2));
      await Task.Delay(TimeSpan.FromTicks(4 * sceneTick.Ticks));

      var timer = Stopwatch.StartNew();
      var stop = Task.Run(() => actor.Stop(ActorPhase.stop_now,
                                           () => finalized.TrySetResult(true)));
      await Task.WhenAll(stop, finalized.Task)
          .WaitAsync(TimeSpan.FromTicks(2 * sceneTick.Ticks));

      Assert.True(timer.Elapsed <= 2 * sceneTick);
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
  public async Task CompletesLocalSceneForItsFullDuration() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var duration = TimeSpan.FromSeconds(1);
    var configuration = new RoleConfiguration {
      id = 44, quantity = 1,
      scenes = [new SceneConfiguration { duration = duration,
                                         tickrate = sceneTick, resources = [] }]
    };

    setup($"ActorLocalCompletionTests-{Guid.NewGuid():N}");
    try {
      ResourceQueueSet.singleton.Reset();
      var actor = new Actor(0, configuration);
      actor.Start();
      try {
        await WaitForLogEntry("Actor 44/0 [Scene: 0] [work: stop]",
                              duration + TimeSpan.FromSeconds(2));
      } finally {
        var finalized = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stop =
            Task.Run(() => actor.Stop(ActorPhase.stop_now,
                                      () => finalized.TrySetResult(true)));
        await Task.WhenAll(stop, finalized.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
      }

      finish();
      var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
      var start = Array.FindIndex(
          lines, line => line.Contains("Actor 44/0 [Scene: 0] [work: start]",
                                       StringComparison.Ordinal));
      Assert.True(start >= 0, "Missing local scene work-start log.");
      var stopIndex = Array.FindIndex(
          lines, start + 1,
          line => line.Contains("Actor 44/0 [Scene: 0] [work: stop]",
                                StringComparison.Ordinal));
      Assert.True(stopIndex > start, "Missing local scene work-stop log.");
      Assert.Contains(lines.Skip(start + 1).Take(stopIndex - start - 1),
                      line => line.Contains("Actor 44/0 completes Scene: 0",
                                            StringComparison.Ordinal));

      var elapsed = LogTimestamp(lines[stopIndex]) - LogTimestamp(lines[start]);
      var tolerance = TimeSpan.FromTicks(2 * sceneTick.Ticks);
      Assert.InRange(elapsed, duration - tolerance, duration + tolerance);
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

  private static DateTimeOffset LogTimestamp(string line) {
    var end = line.IndexOf("]:", StringComparison.Ordinal);
    Assert.True(end >= 0, $"Missing log prefix: {line}");
    var start = line.LastIndexOf(' ', end);
    Assert.True(start >= 0, $"Missing log timestamp: {line}");
    return DateTimeOffset.ParseExact(
        line[(start + 1)..end], "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
  }

  private sealed class MockResourceDriver : IResourceDriver {
    public ResourceDriverStatus GetStatus() {
      return ResourceDriverStatus.idle;
    }
  }

  private sealed record ResourceRelease(ResourceInstance[] Resources,
                                        long Timestamp);

  private sealed class MockResourceQueueSet : IResourceQueueSet {
    private readonly object _lock = new();
    private readonly TaskCompletionSource<IResourceConsumer> _requested =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<ResourceRelease> _firstRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _requestCount;
    private string[] _requestedNames = [];
    private readonly List<ResourceInstance[]> _releases = [];

    public Task<IResourceConsumer> Requested => _requested.Task;
    public Task<ResourceRelease> FirstRelease => _firstRelease.Task;

    public int RequestCount {
      get {
        lock (_lock) { return _requestCount; }
      }
    }

    public string[] RequestedNames {
      get {
        lock (_lock) { return _requestedNames; }
      }
    }

    public ResourceInstance[][] Releases {
      get {
        lock (_lock) { return _releases.ToArray(); }
      }
    }

    public void MultiPush(IResourceConsumer consumer) {
      lock (_lock) {
        _requestCount++;
        _requestedNames = consumer.Scope().ToArray();
      }
      _requested.TrySetResult(consumer);
    }

    public Task<long> Grant(IResourceConsumer consumer,
                            ResourceInstance resource) {
      return Task.Run(() => {
        var timestamp = Stopwatch.GetTimestamp();
        consumer.Grant(resource);
        return timestamp;
      });
    }

    public void MultiRelease(List<ResourceInstance> release) {
      var snapshot = release.ToArray();
      var timestamp = Stopwatch.GetTimestamp();
      lock (_lock) { _releases.Add(snapshot); }
      _firstRelease.TrySetResult(new ResourceRelease(snapshot, timestamp));
    }
  }
}

}
}
}
