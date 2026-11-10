/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
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
      Assert.Equal(new[] { "heater", "pump" }.OrderBy(n => n),
                   requests[0].OrderBy(n => n));
      Assert.Equal(new[] { "fan", "valve", "mixer" }.OrderBy(n => n),
                   requests[1].OrderBy(n => n));
      var releases = queues.Releases;
      Assert.True(releases.Length >= 2);
      Assert.Equal(new[] { "heater", "pump" }.OrderBy(n => n),
                   releases[0].OrderBy(n => n));
      Assert.Equal(new[] { "fan", "valve", "mixer" }.OrderBy(n => n),
                   releases[1].OrderBy(n => n));
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
        Assert.Equal(types.Select(t => t.name).OrderBy(n => n),
                     queues.Releases[i].OrderBy(n => n));
        Assert.Equal(types.Select(t => t.name).OrderBy(n => n),
                     queues.Requests[i].OrderBy(n => n));
        remaining -= expected;
      }
      Assert.Equal(TimeSpan.Zero, remaining);
    } finally {
      finish();
    }
  }

  private sealed record DelayCase((int DurationMs, string[] Resources)[] Scenes,
                                  Dictionary<string, int> MaxWorkMs,
                                  Dictionary<string, int> DelaysMs);

  private static readonly Dictionary<string, DelayCase> DelayCases =
      BuildDelayCases();

  private static Dictionary<string, DelayCase> BuildDelayCases() {
    var cases = new Dictionary<string, DelayCase>();
    void Add(string name, (int, string[])[] scenes,
             Dictionary<string, int> maxWork, Dictionary<string, int> delays) =>
        cases[name] = new DelayCase(scenes, maxWork, delays);
    string[] R(params string[] r) => r;
    var roomy = new Dictionary<string, int> { ["heater"] = 2000,
                                              ["pump"] = 2000, ["fan"] = 2000 };

    Add("one-delayed", [(300, R("heater"))], roomy, new() { ["heater"] = 150 });
    Add("two-first-delayed", [(300, R("heater", "pump"))], roomy,
        new() { ["heater"] = 150 });
    Add("two-second-delayed", [(300, R("heater", "pump"))], roomy,
        new() { ["pump"] = 150 });
    Add("two-both-delayed-ascending", [(300, R("heater", "pump"))], roomy,
        new() { ["heater"] = 100, ["pump"] = 250 });
    Add("two-both-delayed-descending", [(300, R("heater", "pump"))], roomy,
        new() { ["heater"] = 250, ["pump"] = 100 });
    Add("three-middle-delayed", [(300, R("heater", "pump", "fan"))], roomy,
        new() { ["pump"] = 200 });
    Add("three-all-delayed", [(300, R("heater", "pump", "fan"))], roomy,
        new() { ["heater"] = 100, ["pump"] = 150, ["fan"] = 200 });
    Add("segmented-delayed-each-segment", [(600, R("heater"))],
        new() { ["heater"] = 300 }, new() { ["heater"] = 150 });
    Add("segmented-two-resources-lowest-max", [(700, R("heater", "pump"))],
        new() { ["heater"] = 300, ["pump"] = 700 }, new() { ["pump"] = 200 });
    Add("segmented-delayed-resource-has-higher-max",
        [(900, R("heater", "pump"))],
        new() { ["heater"] = 400, ["pump"] = 500 }, new() { ["pump"] = 120 });
    Add("local-then-delayed-resource", [(200, R()), (300, R("heater"))], roomy,
        new() { ["heater"] = 150 });
    Add("delayed-resource-then-local", [(300, R("heater")), (200, R())], roomy,
        new() { ["heater"] = 150 });
    Add("local-between-delayed-resources",
        [(300, R("heater", "pump")), (200, R()), (300, R("fan"))], roomy,
        new() { ["pump"] = 150, ["fan"] = 100 });
    Add("two-then-three-delayed",
        [(200, R("heater", "pump")), (300, R("fan", "heater", "pump"))], roomy,
        new() { ["heater"] = 120, ["fan"] = 180 });
    Add("segmented-delayed-then-local",
        [(500, R("heater", "pump")), (200, R())],
        new() { ["heater"] = 250, ["pump"] = 2000, ["fan"] = 2000 },
        new() { ["heater"] = 100, ["pump"] = 200 });
    return cases;
  }

  public static IEnumerable<object[]>
  DelayedGrantCaseNames() => DelayCases.Keys.Select(k => new object[] { k });

  [Theory]
  [MemberData(nameof(DelayedGrantCaseNames))]
  public async Task StartsWorkOnlyAfterDelayedGrantsArrive(string caseName) {
    var spec = DelayCases[caseName];
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var tolerance = TimeSpan.FromTicks(2 * sceneTick.Ticks);
    var slack =
        TimeSpan.FromMilliseconds(5); // log timestamps are truncated to ms
    var types = spec.MaxWorkMs
                    .Select(m => new ResourceType {
                      name = m.Key, tickrate = sceneTick,
                      minwork = TimeSpan.FromMilliseconds(50),
                      maxwork = TimeSpan.FromMilliseconds(m.Value)
                    })
                    .ToArray();
    var queues = new AutoGrantQueueSet(spec.DelaysMs, types);
    var configuration = new RoleConfiguration {
      id = 50, quantity = 1,
      scenes = spec.Scenes
                   .Select(sc => new SceneConfiguration {
                     duration = TimeSpan.FromMilliseconds(sc.DurationMs),
                     tickrate = sceneTick, resources = sc.Resources.ToList()
                   })
                   .ToList()
    };

    // expected work segments of one pass: (length, resource names)
    var segments = new List<(TimeSpan Length, string[] Resources)>();
    foreach (var sc in spec.Scenes) {
      var remaining = sc.DurationMs;
      var lowestMax = sc.Resources.Length == 0
                          ? int.MaxValue
                          : sc.Resources.Min(r => spec.MaxWorkMs[r]);
      while (remaining > 0) {
        var length = Math.Min(remaining, lowestMax);
        segments.Add((TimeSpan.FromMilliseconds(length), sc.Resources));
        remaining -= length;
      }
    }
    var resourceSegments = segments.Count(g => g.Resources.Length > 0);
    var passTime =
        segments.Sum(g => g.Length.TotalMilliseconds) +
        segments.Where(g => g.Resources.Length > 0)
            .Sum(g => g.Resources.Max(r => spec.DelaysMs.GetValueOrDefault(r)));

    setup($"ActorDelayedGrantTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      try {
        var timer = Stopwatch.StartNew();
        var limit =
            TimeSpan.FromMilliseconds(passTime) + TimeSpan.FromSeconds(3);
        while (queues.Releases.Length < resourceSegments ||
               CountLog("[work: stop]") < segments.Count) {
          Assert.True(
              timer.Elapsed < limit,
              $"Timed out: {queues.Releases.Length}/{resourceSegments} " +
                  $"releases, {CountLog("[work: stop]")}/" +
                  $"{segments.Count} work stops.");
          await Task.Delay(10);
        }
      } finally {
        await StopActor(actor);
      }

      finish();
      var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
      var starts = lines.Where(l => l.Contains("[work: start]"))
                       .Select(LogTimestamp)
                       .ToArray();
      var stops = lines.Where(l => l.Contains("[work: stop]"))
                      .Select(LogTimestamp)
                      .ToArray();
      Assert.True(starts.Length >= segments.Count);
      Assert.True(stops.Length >= segments.Count);

      var pushes = queues.PushTimes;
      var grants = queues.LastGrantTimes;
      var releases = queues.ReleaseTimes;
      var request = 0;
      for (var i = 0; i < segments.Count; i++) {
        var (length, resources) = segments[i];
        if (resources.Length == 0) {
          Assert.InRange(stops[i] - starts[i], length - tolerance,
                         length + tolerance);
          continue;
        }

        // waiting is not work: work begins only after the last grant, and
        // immediately after it
        var delay = TimeSpan.FromMilliseconds(
            resources.Max(r => spec.DelaysMs.GetValueOrDefault(r)));
        Assert.True(starts[i] >= grants[request] - slack,
                    $"Segment {i} started work before its last grant.");
        Assert.InRange(starts[i] - grants[request], TimeSpan.Zero - slack,
                       tolerance);
        Assert.True(grants[request] - pushes[request] >= delay - slack);

        // the hold runs from [work: start] to MultiRelease
        Assert.InRange(releases[request] - starts[i], length - tolerance,
                       length + tolerance);
        Assert.Equal(resources.OrderBy(r => r),
                     queues.Requests[request].OrderBy(r => r));
        Assert.Equal(resources.OrderBy(r => r),
                     queues.Releases[request].OrderBy(r => r));
        request++;
      }
    } finally {
      finish();
    }
  }

  // Sequence-controlled: stop_now arrives while the Actor is inside
  // MultiPush(), and the last Grant() arrives after it. The stop must win.
  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(2)]
  public async Task
  StopDuringMultiPushIsNotOverriddenByLaterGrant(int grantsBeforeStop) {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    ResourceType Type(string name) =>
        new() { name = name, tickrate = sceneTick,
                minwork = TimeSpan.FromMilliseconds(50),
                maxwork = TimeSpan.FromSeconds(2) };
    var types = new[] { Type("heater"), Type("pump"), Type("fan") };
    var queues = new GatedQueueSet(types);
    var configuration = new RoleConfiguration {
      id = 51, quantity = 1,
      scenes = [new SceneConfiguration {
        duration = TimeSpan.FromSeconds(1), tickrate = sceneTick,
        resources = types.Select(t => t.name).ToList()
      }]
    };

    setup($"ActorStopDuringPushTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      try {
        // 1. the Actor is inside MultiPush() with its lock released
        await queues.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        for (var i = 0; i < grantsBeforeStop; i++)
          queues.GrantOne(i);

        // 2. stop_now is requested; Stop() waits for the Actor to exit
        var finalized = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stop =
            Task.Run(() => actor.Stop(ActorPhase.stop_now,
                                      () => finalized.TrySetResult(true)));
        await WaitForLogEntry("Actor 51/0 stop requested",
                              TimeSpan.FromSeconds(2));
        await Task.Delay(100); // let Stop() take the lock and set the phase
        Assert.False(stop.IsCompleted);

        // 3. the remaining grants arrive after the stop request
        for (var i = grantsBeforeStop; i < types.Length; i++)
          queues.GrantOne(i);

        // 4. MultiPush() returns
        queues.Open();
        await Task.WhenAll(stop, finalized.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
      } finally {
        queues.Open();
        await StopActor(actor);
      }

      Assert.Equal(0, CountLog("[work: start]"));
      Assert.Equal(1, queues.RequestCount);
      var release = Assert.Single(queues.Releases);
      Assert.Equal(types.Length, release.Length);
    } finally {
      finish();
    }
  }

  // Sequence-controlled: MultiPush() returns with nothing granted, the Actor is
  // stopped and exits run(), and only then does a Grant() arrive. The queue set
  // calls Grant() on its own thread, so a late grant must not throw there, and
  // the resource must not be lost.
  [Fact]
  public async Task GrantAfterActorExitsDoesNotThrowOrLoseResource() {
    var sceneTick = TimeSpan.FromMilliseconds(50);
    var type = new ResourceType { name = "heater", tickrate = sceneTick,
                                  minwork = TimeSpan.FromMilliseconds(50),
                                  maxwork = TimeSpan.FromSeconds(2) };
    var queues = new GatedQueueSet(type);
    var configuration = new RoleConfiguration {
      id = 52, quantity = 1,
      scenes = [new SceneConfiguration { duration = TimeSpan.FromSeconds(1),
                                         tickrate = sceneTick,
                                         resources = [type.name] }]
    };

    setup($"ActorGrantAfterExitTests-{Guid.NewGuid():N}");
    try {
      var actor = new Actor(0, configuration, queues);
      actor.Start();
      try {
        // 1. MultiPush() returns without a grant; the Actor goes on_queue
        await queues.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        queues.Open();
        await WaitForLogEntry("Actor 52/0 loops run() in phase on_queue",
                              TimeSpan.FromSeconds(2));
      } finally {
        // 2. stop and wait for run() to exit
        await StopActor(actor);
      }

      // 3. a Grant() arrives after the Actor has exited
      var late = Record.Exception(() => queues.GrantOne(0));
      Assert.Null(late);

      // 4. the resource is neither held by a dead Actor nor lost
      Assert.Equal(0, CountLog("[work: start]"));
      Assert.Equal(1, queues.Releases.Sum(r => r.Length));
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

  // Holds MultiPush() open until the test lets it return, and grants only
  // when told to.
  private sealed class GatedQueueSet : IResourceQueueSet {
    private readonly object _lock = new();
    private readonly ResourceInstance[] _pool;
    private readonly TaskCompletionSource _entered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ManualResetEventSlim _gate = new(false);
    private IResourceConsumer? _consumer;
    private int _requestCount;
    private readonly List<ResourceInstance[]> _releases = [];

    public GatedQueueSet(params ResourceType[] types) {
      _pool =
          types.Select(t => new ResourceInstance(t, new MockResourceDriver()))
              .ToArray();
    }

    public Task Entered => _entered.Task;

    public int RequestCount {
      get {
        lock (_lock) { return _requestCount; }
      }
    }

    public ResourceInstance[][] Releases {
      get {
        lock (_lock) { return _releases.ToArray(); }
      }
    }

    public void GrantOne(int index) { _consumer!.Grant(_pool[index]); }

    public void Open() { _gate.Set(); }

    public void MultiPush(IResourceConsumer consumer) {
      lock (_lock) {
        _requestCount++;
        _consumer = consumer;
      }
      _entered.TrySetResult();
      _gate.Wait();
    }

    public void MultiRelease(IEnumerable<ResourceInstance> release) {
      var snapshot = release.ToArray();
      lock (_lock) { _releases.Add(snapshot); }
    }
  }

  private sealed class AutoGrantQueueSet : IResourceQueueSet {
    private readonly object _lock = new();
    private readonly Dictionary<string, ResourceInstance> _pool;
    private readonly List<string[]> _requests = [];
    private readonly List<string[]> _releases = [];
    private readonly List<DateTimeOffset> _releaseTimes = [];

    private readonly Dictionary<string, int> _delays;
    private readonly List<DateTimeOffset> _pushTimes = [];
    private readonly List<DateTimeOffset> _lastGrantTimes = [];

    public AutoGrantQueueSet(params ResourceType[] types)
        : this(new Dictionary<string, int>(), types) {}

    // delays: milliseconds to hold back the Grant() of a named resource on
    // every request
    public AutoGrantQueueSet(Dictionary<string, int> delays,
                             params ResourceType[] types) {
      _delays = delays;
      _pool = types.ToDictionary(
          t => t.name, t => new ResourceInstance(t, new MockResourceDriver()));
    }

    public DateTimeOffset[] PushTimes {
      get {
        lock (_lock) { return _pushTimes.ToArray(); }
      }
    }

    public DateTimeOffset[] LastGrantTimes {
      get {
        lock (_lock) { return _lastGrantTimes.ToArray(); }
      }
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
      int index;
      lock (_lock) {
        index = _requests.Count;
        _requests.Add(names);
        _pushTimes.Add(DateTimeOffset.UtcNow);
        _lastGrantTimes.Add(DateTimeOffset.UtcNow);
      }
      // grant asynchronously: the Actor must be free to receive Grant()
      foreach (var name in names) {
        var delay = _delays.GetValueOrDefault(name);
        Task.Run(async () => {
          if (delay > 0)
            await Task.Delay(delay);
          lock (_lock) {
            var now = DateTimeOffset.UtcNow;
            if (now > _lastGrantTimes[index])
              _lastGrantTimes[index] = now;
          }
          consumer.Grant(_pool[name]);
        });
      }
    }

    public void MultiRelease(IEnumerable<ResourceInstance> release) {
      var held = release.ToArray();
      var names =
          held.Select(r => _pool.First(p => ReferenceEquals(p.Value, r)).Key)
              .ToArray();
      lock (_lock) {
        _releases.Add(names);
        _releaseTimes.Add(DateTimeOffset.UtcNow);
      }
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

    public void MultiRelease(IEnumerable<ResourceInstance> release) {
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
