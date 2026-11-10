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
  public async Task CompletesResourceSceneForItsFullDuration() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var resourceTick = TimeSpan.FromMilliseconds(50);
    var duration = TimeSpan.FromSeconds(1);
    var resourceType =
        new ResourceType { name = "heater", tickrate = resourceTick,
                           minwork = TimeSpan.FromMilliseconds(50),
                           maxwork = TimeSpan.FromSeconds(2) };
    var resource = new ResourceInstance(resourceType, new MockResourceDriver());
    var queues = new MockResourceQueueSet();
    var configuration = new RoleConfiguration {
      id = 46, quantity = 1,
      scenes =
          [new SceneConfiguration { duration = duration, tickrate = sceneTick,
                                    resources = [resourceType.name] }]
    };

    setup($"ActorResourceCompletionTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      ResourceRelease firstRelease;
      try {
        var consumer =
            await queues.Requested.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(new[] { resourceType.name }, queues.RequestedNames);
        await queues.Grant(consumer, resource)
            .WaitAsync(TimeSpan.FromSeconds(2));
        firstRelease = await queues.FirstRelease.WaitAsync(
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

      Assert.Same(resource, Assert.Single(firstRelease.Resources));
      finish();
      var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
      var start = Array.FindIndex(
          lines, line => line.Contains("Actor 46/0 [Scene: 0] [work: start]",
                                       StringComparison.Ordinal));
      Assert.True(start >= 0, "Missing resource scene work-start log.");
      var completion =
          Array.FindIndex(lines, start + 1,
                          line => line.Contains("Actor 46/0 completes Scene: 0",
                                                StringComparison.Ordinal));
      Assert.True(completion > start, "Missing resource scene completion log.");
      var workStop = Array.FindIndex(
          lines, completion + 1,
          line => line.Contains("Actor 46/0 [Scene: 0] [work: stop]",
                                StringComparison.Ordinal));
      Assert.True(workStop > completion,
                  "Missing resource scene work-stop log.");
      var elapsed = LogTimestamp(lines[workStop]) - LogTimestamp(lines[start]);
      var tolerance = TimeSpan.FromTicks(2 * sceneTick.Ticks);
      Assert.InRange(elapsed, duration - tolerance, duration + tolerance);
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
  public async Task RestartsAndRepeatsLocalSceneSequence() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var configuration = new RoleConfiguration {
      id = 47, quantity = 1,
      scenes =
          [
            new SceneConfiguration { duration = TimeSpan.FromMilliseconds(200),
                                     tickrate = sceneTick, resources = [] },
            new SceneConfiguration { duration = TimeSpan.FromMilliseconds(200),
                                     tickrate = sceneTick, resources = [] }
          ]
    };

    setup($"ActorRestartTests-{Guid.NewGuid():N}");
    try {
      ResourceQueueSet.singleton.Reset();
      await RunRestartSequence(configuration, ResourceQueueSet.singleton,
                               sceneTick);
    } finally {
      finish();
    }
  }

  [Fact]
  public async Task RestartsAndRepeatsUnevenLocalAndResourceSceneSequence() {
    // scene-tick 70 ms divides neither 200 ms nor 330 ms
    var sceneTick = TimeSpan.FromMilliseconds(70);
    var heater = new ResourceType { name = "heater", tickrate = sceneTick,
                                    minwork = TimeSpan.FromMilliseconds(50),
                                    maxwork = TimeSpan.FromSeconds(2) };
    var queues = new AutoGrantQueueSet(heater);
    var configuration = new RoleConfiguration {
      id = 47, quantity = 1,
      scenes =
          [
            new SceneConfiguration { duration = TimeSpan.FromMilliseconds(200),
                                     tickrate = sceneTick, resources = [] },
            new SceneConfiguration { duration = TimeSpan.FromMilliseconds(330),
                                     tickrate = sceneTick,
                                     resources = [heater.name] }
          ]
    };

    setup($"ActorUnevenRestartTests-{Guid.NewGuid():N}");
    try {
      await RunRestartSequence(configuration, queues, sceneTick);

      // every completed resource scene must have requested and released
      var completed = CountLog("Actor 47/0 completes Scene: 1");
      Assert.True(queues.Requests.Count(r => r.Length == 1) >= completed);
      Assert.True(queues.Releases.Count(r => r.Length == 1) >= completed);
    } finally {
      finish();
    }
  }

  [Fact]
  public async
      Task RequestsAndReleasesTwoAndThenThreeResourcesInSuccessiveScenes() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    ResourceType Type(string name) =>
        new() { name = name, tickrate = sceneTick,
                minwork = TimeSpan.FromMilliseconds(50),
                maxwork = TimeSpan.FromSeconds(2) };
    var types = new[] { Type("heater"), Type("pump"), Type("fan"),
                        Type("valve"), Type("mixer") };
    var queues = new AutoGrantQueueSet(types);
    var configuration = new RoleConfiguration {
      id = 48, quantity = 1,
      scenes =
          [
            new SceneConfiguration { duration = TimeSpan.FromMilliseconds(200),
                                     tickrate = sceneTick,
                                     resources = ["heater", "pump"] },
            new SceneConfiguration { duration = TimeSpan.FromMilliseconds(300),
                                     tickrate = sceneTick,
                                     resources = ["fan", "valve", "mixer"] }
          ]
    };

    setup($"ActorTwoSceneResourceTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      try {
        await WaitForLogEntry("Actor 48/0 completes Scene: 1",
                              TimeSpan.FromSeconds(3));
      } finally {
        await StopActor(actor);
      }

      var requests = queues.Requests;
      Assert.True(requests.Length >= 2);
      Assert.Equal(new[] { "heater", "pump" }, requests[0]);
      Assert.Equal(new[] { "fan", "valve", "mixer" }, requests[1]);
      var releases = queues.Releases;
      Assert.True(releases.Length >= 2);
      Assert.Equal(new[] { "heater", "pump" }, releases[0]);
      Assert.Equal(new[] { "fan", "valve", "mixer" }, releases[1]);
    } finally {
      finish();
    }
  }

  [Theory]
  [InlineData(1000, 300, 0, 4)]
  [InlineData(500, 300, 700, 2)]
  [InlineData(500, 700, 300, 2)]
  [InlineData(800, 300, 700, 3)]
  public async Task ReleasesAndRequeuesAtLowestMaxWork(int sceneMs, int maxA,
                                                       int maxB,
                                                       int expectedHolds) {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    ResourceType Type(string name, int maxMs) =>
        new() { name = name, tickrate = sceneTick,
                minwork = TimeSpan.FromMilliseconds(50),
                maxwork = TimeSpan.FromMilliseconds(maxMs) };
    var types = new List<ResourceType> { Type("heater", maxA) };
    if (maxB > 0)
      types.Add(Type("pump", maxB));
    var lowestMax =
        TimeSpan.FromMilliseconds(types.Min(t => t.maxwork.TotalMilliseconds));
    var scene = TimeSpan.FromMilliseconds(sceneMs);
    var queues = new AutoGrantQueueSet(types.ToArray());
    var configuration =
        new RoleConfiguration { id = 49, quantity = 1,
                                scenes = [new SceneConfiguration {
                                  duration = scene, tickrate = sceneTick,
                                  resources = types.Select(t => t.name).ToList()
                                }] };

    setup($"ActorMaxWorkTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      try {
        var timer = Stopwatch.StartNew();
        while (queues.Releases.Length < expectedHolds) {
          Assert.True(timer.Elapsed < scene + TimeSpan.FromSeconds(3),
                      $"Timed out; {queues.Releases.Length} of " +
                          $"{expectedHolds} releases seen.");
          await Task.Delay(10);
        }
      } finally {
        await StopActor(actor);
      }

      finish();
      var starts =
          ReadLog()
              .Split('\n', StringSplitOptions.RemoveEmptyEntries)
              .Where(l => l.Contains("Actor 49/0 [Scene: 0] [work: start]",
                                     StringComparison.Ordinal))
              .Select(LogTimestamp)
              .ToArray();
      Assert.True(starts.Length >= expectedHolds);

      // each hold runs from [work: start] to MultiRelease, at most the lowest
      // maxwork, and together the holds cover the scene duration
      var tolerance = TimeSpan.FromTicks(2 * sceneTick.Ticks);
      var remaining = scene;
      for (var i = 0; i < expectedHolds; i++) {
        var expected = remaining < lowestMax ? remaining : lowestMax;
        var hold = queues.ReleaseTimes[i] - starts[i];
        Assert.InRange(hold, expected - tolerance, expected + tolerance);
        Assert.Equal(types.Select(t => t.name), queues.Releases[i]);
        Assert.Equal(types.Select(t => t.name), queues.Requests[i]);
        remaining -= expected;
      }
      Assert.Equal(TimeSpan.Zero, remaining);
    } finally {
      finish();
    }
  }

  private async Task RunRestartSequence(RoleConfiguration configuration,
                                        IResourceQueueSet queues,
                                        TimeSpan sceneTick) {
    var id = configuration.id;
    var workStart = $"Actor {id}/0 [Scene: 0] [work: start]";
    var completes0 = $"Actor {id}/0 completes Scene: 0";
    var completes1 = $"Actor {id}/0 completes Scene: 1";
    var wait = TimeSpan.FromSeconds(3);

    // 1. instantiate and wait
    var actor = new Actor(0, configuration, queues);
    await Task.Delay(100);
    Assert.Equal(0, CountLog("[work: start]"));

    try {
      // 2. start and allow work to complete; the sequence must repeat
      actor.Start();
      await WaitForLogCount(completes0, 2, wait);
      await WaitForLogCount(completes1, 1, wait);
      await StopActor(actor);

      // 3. wait while stopped
      await Task.Delay(200);
      var completed0 = CountLog(completes0);
      var completed1 = CountLog(completes1);
      var started = CountLog(workStart);

      // 4. start again and interrupt work with a stop
      actor.Start();
      await WaitForLogCount(workStart, started + 1, wait);
      await Task.Delay(TimeSpan.FromTicks(sceneTick.Ticks));
      await StopActor(actor);
      await Task.Delay(200);
      Assert.Equal(completed0, CountLog(completes0));
      Assert.Equal(completed1, CountLog(completes1));

      // 5. start again and allow work to complete; the sequence must repeat
      actor.Start();
      await WaitForLogCount(completes0, completed0 + 2, wait);
      await WaitForLogCount(completes1, completed1 + 1, wait);
    } finally {
      await StopActor(actor);
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

  private static async Task StopActor(Actor actor) {
    var finalized = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var stop = Task.Run(() => actor.Stop(ActorPhase.stop_now,
                                         () => finalized.TrySetResult(true)));
    await Task.WhenAll(stop, finalized.Task).WaitAsync(TimeSpan.FromSeconds(2));
  }

  private int CountLog(string entry) {
    finish();
    return ReadLog()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Count(line => line.Contains(entry, StringComparison.Ordinal));
  }

  private async Task WaitForLogCount(string entry, int count,
                                     TimeSpan timeout) {
    var timer = Stopwatch.StartNew();
    do {
      if (CountLog(entry) >= count)
        return;
      await Task.Delay(10);
    } while (timer.Elapsed < timeout);

    Assert.Fail($"Timed out waiting for {count} log entries '{entry}'.");
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

  private sealed class AutoGrantQueueSet : IResourceQueueSet {
    private readonly object _lock = new();
    private readonly Dictionary<string, ResourceInstance> _pool;
    private readonly List<string[]> _requests = [];
    private readonly List<string[]> _releases = [];
    private readonly List<DateTimeOffset> _releaseTimes = [];

    public AutoGrantQueueSet(params ResourceType[] types) {
      _pool = types.ToDictionary(
          t => t.name, t => new ResourceInstance(t, new MockResourceDriver()));
    }

    public string[][] Requests {
      get {
        lock (_lock) { return _requests.ToArray(); }
      }
    }

    public string[][] Releases {
      get {
        lock (_lock) { return _releases.ToArray(); }
      }
    }

    public DateTimeOffset[] ReleaseTimes {
      get {
        lock (_lock) { return _releaseTimes.ToArray(); }
      }
    }

    public void MultiPush(IResourceConsumer consumer) {
      var names = consumer.Scope().Select(n => n.ToString()).ToArray();
      lock (_lock) { _requests.Add(names); }
      // grant asynchronously: the Actor holds its lock while requesting
      Task.Run(() => {
        foreach (var name in names)
          consumer.Grant(_pool[name]);
      });
    }

    public void MultiRelease(List<ResourceInstance> release) {
      var names =
          release.Select(r => _pool.First(p => ReferenceEquals(p.Value, r)).Key)
              .ToArray();
      lock (_lock) {
        _releases.Add(names);
        _releaseTimes.Add(DateTimeOffset.UtcNow);
      }
      release.Clear(); // like ResourceQueueSet: release and clear go together
    }
  }

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
      release.Clear(); // like ResourceQueueSet: release and clear go together
      var timestamp = Stopwatch.GetTimestamp();
      lock (_lock) { _releases.Add(snapshot); }
      _firstRelease.TrySetResult(new ResourceRelease(snapshot, timestamp));
    }
  }
}

}
}
}
