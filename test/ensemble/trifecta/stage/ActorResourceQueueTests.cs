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

// Real Actors on a real ResourceQueueSet: no mocks.
[Collection("ComponentLog tests")]
public sealed class ActorResourceQueueTests : ComponentLogTestBase {
  private const int SceneTickMs = 50;
  private const int ResourceMs = 300;
  private const int LocalMs = 150;

  public static IEnumerable<object[]> Cases() {
    foreach (var shape in new[] { "R", "L-R", "R-L" })
      foreach (var actors in new[] { 1, 2 })
        foreach (var types in new[] { 1, 2 })
          foreach (var instances in new[] { 1, 2 })
            yield return new object[] { shape, actors, types, instances };
  }

  private sealed record Mark(string Entry, DateTimeOffset At);

  [Theory]
  [MemberData(nameof(Cases))]
  public async Task ActorsCompleteTheirScenesOnSharedResources(string shape,
                                                               int actorCount,
                                                               int typeCount,
                                                               int instances) {
    var tick = TimeSpan.FromMilliseconds(SceneTickMs);
    var tolerance = TimeSpan.FromTicks(2 * tick.Ticks);
    var slack = TimeSpan.FromMilliseconds(150);
    var wait = TimeSpan.FromSeconds(10);

    var typeNames =
        Enumerable.Range(0, typeCount).Select(i => $"res{i}").ToList();
    var set = new ResourceSet {
      elements = typeNames
                     .Select(n => new ResourceType {
                       name = n, quantity = instances, tickrate = tick,
                       minwork = tick, maxwork = TimeSpan.FromSeconds(5)
                     })
                     .ToList()
    };

    // scene kinds for this shape: 'R' resource work, 'L' local work
    var kinds = shape.Split('-').Select(k => k[0]).ToArray();
    SceneConfiguration Scene(char kind) => new() {
      duration = TimeSpan.FromMilliseconds(kind == 'R' ? ResourceMs : LocalMs),
      tickrate = tick, resources = kind == 'R' ? typeNames.ToList() : []
    };

    var queues = new ResourceQueueSet();
    var roles =
        Enumerable.Range(0, actorCount)
            .Select(i => new RoleConfiguration {
              id = 70 + i, quantity = 1, scenes = kinds.Select(Scene).ToList()
            })
            .ToList();

    setup($"ActorResourceQueueTests-{Guid.NewGuid():N}");
    var actors = roles.Select(r => new Actor(0, r, queues)).ToList();
    try {
      queues.configure(set);
      queues.Start();
      try {
        foreach (var a in actors)
          a.Start();

        // every actor completes its whole scene sequence once
        var last = kinds.Length - 1;
        foreach (var r in roles)
          await WaitForLog($"Actor {r.id}/0 completes Scene: {last}", wait);
      } finally {
        await Task.WhenAll(actors.Select(StopActor));
        queues.Stop();
      }
    } finally {
      finish();
    }

    var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Mark ? First(string entry, DateTimeOffset after = default) {
      foreach (var l in lines) {
        if (!l.Contains(entry, StringComparison.Ordinal))
          continue;
        var at = Timestamp(l);
        if (at >= after)
          return new Mark(entry, at);
      }
      return null;
    }

    // per actor: the first run through the scene sequence
    var holds = new List<(DateTimeOffset Requested, DateTimeOffset Start,
                          DateTimeOffset Release)>();
    foreach (var r in roles) {
      var id = $"Actor {r.id}/0";
      var previousStop = DateTimeOffset.MinValue;
      DateTimeOffset? requested = null;
      for (var n = 0; n < kinds.Length; n++) {
        var duration =
            TimeSpan.FromMilliseconds(kinds[n] == 'R' ? ResourceMs : LocalMs);
        var start = First($"{id} [Scene: {n}] [work: start]", previousStop);
        Assert.NotNull(start);
        var stop = First($"{id} [Scene: {n}] [work: stop]", start!.At);
        Assert.NotNull(stop);

        // work period equals the scene duration (waiting is not work)
        Assert.InRange(stop!.At - start.At, duration - tolerance,
                       duration + tolerance);
        Assert.True(start.At >= previousStop, $"{id} scene {n} began early");

        if (kinds[n] == 'R') {
          // the request is the first queue_up before this work period
          var request =
              lines
                  .Where(l => l.Contains($"{id} loops run() in phase queue_up",
                                         StringComparison.Ordinal))
                  .Select(Timestamp)
                  .Where(t => t <= start.At)
                  .DefaultIfEmpty(start.At)
                  .Max();
          var release = First($"{id} Scene: {n} releases", start.At);
          Assert.NotNull(release);
          Assert.InRange(release!.At - start.At, duration - tolerance,
                         duration + tolerance);
          holds.Add((request, start.At, release.At));
          requested ??= request;
        }
        previousStop = stop.At;
      }
    }

    // capacity: at most `instances` resource-work periods overlap
    foreach (var h in holds) {
      var overlapping =
          holds.Count(o => o.Start <= h.Start && o.Release > h.Start);
      Assert.True(
          overlapping <= instances,
          $"{overlapping} actors held resources with {instances} instances");
    }

    // FIFO: with every actor needing every type, `instances` slots exist
    var free = Enumerable.Repeat(DateTimeOffset.MinValue, instances).ToList();
    // requests within 10 ms are indistinguishable in the log: order those by
    // start
    foreach (var h in holds
                 .OrderBy(x => x.Requested.Ticks /
                               TimeSpan.TicksPerMillisecond / 10)
                 .ThenBy(x => x.Start)) {
      free.Sort();
      var expected = free[0] > h.Requested ? free[0] : h.Requested;
      free[0] = expected + TimeSpan.FromMilliseconds(ResourceMs);
      Assert.InRange(h.Start - expected, -slack, slack);
    }

    // every resource granted came back to its queue
    Assert.True(lines.Count(l => l.Contains(" receives one resource",
                                            StringComparison.Ordinal)) >=
                    holds.Count * typeCount,
                "not every resource was returned to its queue");
  }

  private static async Task StopActor(Actor actor) {
    var finalized = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var stop = Task.Run(() => actor.Stop(ActorPhase.stop_now,
                                         () => finalized.TrySetResult(true)));
    await Task.WhenAll(stop, finalized.Task).WaitAsync(TimeSpan.FromSeconds(5));
  }

  private async Task WaitForLog(string entry, TimeSpan timeout) {
    var timer = Stopwatch.StartNew();
    do {
      finish();
      if (FindLogFiles().Length == 1 &&
          ReadLog().Contains(entry, StringComparison.Ordinal))
        return;
      await Task.Delay(20);
    } while (timer.Elapsed < timeout);
    Assert.Fail($"Timed out waiting for log entry '{entry}'.");
  }

  private static DateTimeOffset Timestamp(string line) {
    var end = line.IndexOf("]:", StringComparison.Ordinal);
    var start = line.LastIndexOf(' ', end);
    return DateTimeOffset.ParseExact(
        line[(start + 1)..end], "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
        CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
  }
}

}
}
}
