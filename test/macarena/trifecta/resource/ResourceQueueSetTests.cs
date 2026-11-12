/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using trifecta.resource;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace resource {

[Collection("ComponentLog tests")]
public sealed class ResourceQueueSetTests : ComponentLogTestBase {
  [Fact]
  public void SingleResourceIsGrantedThenReleasedBackToItsQueue() {
    var type = new ResourceType { name = "heater", quantity = 1,
                                  tickrate = TimeSpan.FromMilliseconds(50),
                                  minwork = TimeSpan.FromMilliseconds(50),
                                  maxwork = TimeSpan.FromSeconds(2) };
    var actor = new MockActor(type.name);
    var queues = new ResourceQueueSet();

    setup($"ResourceQueueSetTests-{Guid.NewGuid():N}");
    try {
      queues.configure(new ResourceSet { elements = [type] });
      queues.Start();
      try {
        // 3. push the single-resource request; the only resource is granted
        queues.MultiPush(actor);
        var granted = Assert.Single(actor.Granted);
        Assert.Equal(type.name, granted.GetName());

        // 4. release what was granted
        queues.MultiRelease(actor.Granted);
      } finally {
        queues.Stop();
      }
    } finally {
      finish();
    }

    var log = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var q = "ResourceQueue<heater>";
    int At(string entry) =>
        Array.FindIndex(log, l => l.Contains(entry, StringComparison.Ordinal));

    var received = At($"{q} receives consumer {actor.Identity()}");
    var quick = At($"{q} grants quick");
    var returned = At($"{q} receives one resource");
    var stored = At($"{q} enqueues resource");

    Assert.True(received >= 0, "consumer receipt not logged");
    Assert.True(quick > received, "quick grant not logged after receipt");
    Assert.True(returned > quick, "resource return not logged after grant");
    Assert.True(stored > returned, "returned resource was not re-enqueued");
    Assert.Contains(
        log,
        l => l.Contains("ResourceQueueSet pushes multiple resource requests"));
    Assert.Contains(
        log, l => l.Contains("ResourceQueueSet releases multiple resources"));
    Assert.DoesNotContain(log, l => l.Contains("ricochet-grant"));
  }

  // Two mock actors compete for one resource. The first pusher is granted at
  // once; the second waits for the first release. Each actor holds the
  // resource for its scene time and then releases it.
  [Theory]
  [InlineData(200, 200, 0)]   // B queued immediately behind A
  [InlineData(400, 100, 0)]   // long first hold, short second
  [InlineData(100, 400, 0)]   // short first hold, long second
  [InlineData(300, 150, 100)] // B arrives while A still holds
  [InlineData(100, 150, 300)] // B arrives after A has finished: no wait
  public async Task TwoActorsCompeteForOneResource(int holdAMs, int holdBMs,
                                                   int pushBDelayMs) {
    var type = new ResourceType { name = "heater", quantity = 1,
                                  tickrate = TimeSpan.FromMilliseconds(50),
                                  minwork = TimeSpan.FromMilliseconds(50),
                                  maxwork = TimeSpan.FromSeconds(2) };
    var queues = new ResourceQueueSet();
    var a = new MockActor("A", type.name, TimeSpan.FromMilliseconds(holdAMs),
                          queues);
    var b = new MockActor("B", type.name, TimeSpan.FromMilliseconds(holdBMs),
                          queues);
    var slack = TimeSpan.FromMilliseconds(100);
    var early = TimeSpan.FromMilliseconds(5); // timer vs clock granularity
    var timeout = TimeSpan.FromSeconds(5);

    setup($"ResourceQueueSetCompeteTests-{Guid.NewGuid():N}");
    try {
      queues.configure(new ResourceSet { elements = [type] });
      queues.Start();
      try {
        var begin = DateTimeOffset.UtcNow;
        queues.MultiPush(a);
        if (pushBDelayMs > 0)
          await Task.Delay(pushBDelayMs);
        var pushedB = DateTimeOffset.UtcNow;
        queues.MultiPush(b);

        // eventual completion of both
        await Task.WhenAll(a.Done, b.Done).WaitAsync(timeout);

        // A was first in line and is granted at once
        Assert.InRange(a.GrantedAt - begin, TimeSpan.Zero, slack);

        // B waits for whatever remains of A's hold, if anything
        var expectedWait =
            TimeSpan.FromMilliseconds(holdAMs) - (pushedB - a.GrantedAt);
        if (expectedWait < TimeSpan.Zero)
          expectedWait = TimeSpan.Zero;
        var waited = b.GrantedAt - pushedB;
        Assert.InRange(waited, expectedWait - slack, expectedWait + slack);

        // never held at the same time, and B started after A released
        Assert.True(b.GrantedAt >= a.ReleasedAt - TimeSpan.FromMilliseconds(5),
                    "B was granted before A released");
        Assert.InRange(a.ReleasedAt - a.GrantedAt,
                       TimeSpan.FromMilliseconds(holdAMs) - early,
                       TimeSpan.FromMilliseconds(holdAMs) + slack);
        Assert.InRange(b.ReleasedAt - b.GrantedAt,
                       TimeSpan.FromMilliseconds(holdBMs) - early,
                       TimeSpan.FromMilliseconds(holdBMs) + slack);
      } finally {
        queues.Stop();
      }
    } finally {
      finish();
    }

    // the resource is back on its queue with nobody left waiting
    var log = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var q = "ResourceQueue<heater>";
    int Count(string entry) =>
        log.Count(l => l.Contains(entry, StringComparison.Ordinal));
    Assert.Equal(2, Count($"{q} receives consumer "));
    Assert.Equal(2, Count($"{q} receives one resource"));
    var lastResourceEntry = log.Last(
        l => l.Contains($"{q} enqueues resource", StringComparison.Ordinal) ||
             l.Contains($"{q} ricochet-grant", StringComparison.Ordinal) ||
             l.Contains($"{q} dispatch-grant", StringComparison.Ordinal));
    Assert.Contains("enqueues resource", lastResourceEntry);
  }

  [Theory]
  [InlineData("heater", "valve")]
  [InlineData("valve", "heater")]
  public void ResourceExchangesFollowGrantAndReturnSequence(string first,
                                                            string second) {
    ResourceType Type(string name) =>
        new ResourceType { name = name, quantity = 1,
                           tickrate = TimeSpan.FromMilliseconds(50),
                           minwork = TimeSpan.FromMilliseconds(50),
                           maxwork = TimeSpan.FromSeconds(2) };
    var queues = new ResourceQueueSet();
    var a = new ExchangeConsumer("A", first, second);
    var b = new ExchangeConsumer("B", first, second);

    setup($"ResourceQueueSetExchangeTests-{Guid.NewGuid():N}");
    try {
      queues.configure(
          new ResourceSet { elements = [Type(first), Type(second)] });
      queues.Start();
      try {
        queues.MultiPush(a);
        Assert.Equal(2, a.Granted.Count);
        var firstResource = Assert.Single(a.Granted, r => r.GetName() == first);
        var secondResource = Assert.Single(a.Granted, r => r.GetName() == second);
        Assert.NotSame(firstResource, secondResource);
        queues.MultiPush(b);
        Assert.Empty(b.Granted);

        queues.MultiRelease([firstResource]);
        Assert.Same(firstResource, Assert.Single(b.Granted));
        queues.MultiRelease([firstResource]);
        queues.MultiRelease([secondResource]);
        Assert.Equal(2, b.Granted.Count);
        Assert.Same(secondResource,
                    Assert.Single(b.Granted, r => r.GetName() == second));
        queues.MultiRelease([secondResource]);

        var c = new ExchangeConsumer("C", first, second);
        queues.MultiPush(c);
        Assert.Equal(2, c.Granted.Count);
        Assert.Same(firstResource,
                    Assert.Single(c.Granted, r => r.GetName() == first));
        Assert.Same(secondResource,
                    Assert.Single(c.Granted, r => r.GetName() == second));
        queues.MultiRelease(c.Granted);
      } finally {
        queues.Stop();
      }
    } finally {
      finish();
    }

    var events = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(line => line[(line.IndexOf("]: ", StringComparison.Ordinal) + 3)..]
                                         .TrimEnd('\r'))
                     .Where(message => message.StartsWith(
                                "ResourceQueue<", StringComparison.Ordinal))
                     .ToArray();
    Assert.Equal(new[] {
      $"ResourceQueue<{first}> receives consumer A",
      $"ResourceQueue<{first}> grants quick",
      $"ResourceQueue<{second}> receives consumer A",
      $"ResourceQueue<{second}> grants quick",
      $"ResourceQueue<{first}> receives consumer B",
      $"ResourceQueue<{first}> enqueues consumer B",
      $"ResourceQueue<{second}> receives consumer B",
      $"ResourceQueue<{second}> enqueues consumer B",
      $"ResourceQueue<{first}> receives one resource {first}",
      $"ResourceQueue<{first}> ricochet-grant on resource {first}",
      $"ResourceQueue<{first}> receives one resource {first}",
      $"ResourceQueue<{first}> enqueues resource {first}",
      $"ResourceQueue<{second}> receives one resource {second}",
      $"ResourceQueue<{second}> ricochet-grant on resource {second}",
      $"ResourceQueue<{second}> receives one resource {second}",
      $"ResourceQueue<{second}> enqueues resource {second}",
      $"ResourceQueue<{first}> receives consumer C",
      $"ResourceQueue<{first}> grants quick",
      $"ResourceQueue<{second}> receives consumer C",
      $"ResourceQueue<{second}> grants quick",
      $"ResourceQueue<{first}> receives one resource {first}",
      $"ResourceQueue<{first}> enqueues resource {first}",
      $"ResourceQueue<{second}> receives one resource {second}",
      $"ResourceQueue<{second}> enqueues resource {second}"
    }, events);
  }

  private sealed class ExchangeConsumer : IResourceConsumer {
    private readonly string _name;
    private readonly string[] _scope;

    public ExchangeConsumer(string name, params string[] scope) {
      _name = name;
      _scope = scope;
    }

    public List<ResourceInstance> Granted { get; } = [];
    public bool IsGhost() { return false; }
    public IEnumerable<ResourceName> Scope() { return _scope; }
    public void Grant(ResourceInstance resource) { Granted.Add(resource); }
    public string Identity() { return _name; }
  }

  // start, hold, cycles, gap (local work between cycles), stopAfter (no new
  // request after this many ms from the start of the run; 0 = never)
  public sealed record Spec(int Start, int Hold, int Cycles, int Gap = 0,
                            int StopAfter = 0);

  public static IEnumerable<object[]> DemandCases() {
    Spec[] Same(int n, int start, int step, int hold, int cycles, int gap,
                int stop = 0) =>
        Enumerable.Range(0, n)
            .Select(i => new Spec(start + i * step, hold, cycles, gap, stop))
            .ToArray();
    yield return new object[] { "two-instances-two-actors-idle", 2,
                                Same(2, 0, 20, 200, 1, 0) };
    yield return new object[] { "two-instances-third-actor-waits", 2,
                                Same(3, 0, 20, 300, 1, 0) };
    yield return new object[] { "three-instances-burst-of-six", 3,
                                Same(6, 0, 20, 200, 1, 0) };
    yield return new object[] { "sustained-contention", 2,
                                Same(4, 0, 20, 150, 3, 0) };
    yield return new object[] { "local-work-reduces-demand", 2,
                                Same(4, 0, 20, 150, 3, 300) };
    yield return new object[] {
      "mixed-holds-with-local-work", 3,
      new[] { new Spec(0, 100, 2, 100), new Spec(20, 400, 2, 100),
              new Spec(40, 250, 2, 100), new Spec(60, 150, 2, 100),
              new Spec(80, 300, 2, 100) }
    };
    yield return new object[] {
      "two-actors-stop-mid-run", 2,
      new[] { new Spec(0, 100, 6, 0, 300), new Spec(20, 100, 6, 0, 300),
              new Spec(40, 100, 6, 0), new Spec(60, 100, 6, 0) }
    };
    yield return new object[] { "supply-exceeds-demand", 4,
                                Same(3, 0, 20, 200, 3, 100) };
    yield return new object[] {
      "late-arrival-after-burst", 2,
      new[] { new Spec(0, 200, 1), new Spec(20, 200, 1), new Spec(40, 200, 1),
              new Spec(900, 200, 1) }
    };
    yield return new object[] { "heavy-demand-short-gaps", 3,
                                Same(6, 0, 20, 120, 3, 50) };
  }

  // Several actors cycle request, hold, release, local work on N instances of
  // one resource type. The timeline is checked against a FIFO simulation fed
  // with the actual request times, plus two invariants: never more than N
  // held at once, and no resource idle while a request waits.
  [Theory]
  [MemberData(nameof(DemandCases))]
  public async Task ActorsShareMultipleInstancesOfOneResource(string name,
                                                              int instances,
                                                              Spec[] specs) {
    var type = new ResourceType { name = "heater", quantity = instances,
                                  tickrate = TimeSpan.FromMilliseconds(50),
                                  minwork = TimeSpan.FromMilliseconds(50),
                                  maxwork = TimeSpan.FromSeconds(2) };
    var queues = new ResourceQueueSet();
    var actors =
        specs.Select(sp => new CyclingActor(type.name, sp, queues)).ToArray();
    var slack = TimeSpan.FromMilliseconds(150);
    var early = TimeSpan.FromMilliseconds(5);

    setup($"ResourceQueueSetDemandTests-{Guid.NewGuid():N}");
    try {
      queues.configure(new ResourceSet { elements = [type] });
      queues.Start();
      try {
        var begin = DateTimeOffset.UtcNow;
        foreach (var a in actors)
          a.Begin(begin);
        await Task.WhenAll(actors.Select(a => a.Done))
            .WaitAsync(TimeSpan.FromSeconds(15));
      } finally {
        queues.Stop();
      }
    } finally {
      finish();
    }

    var all = actors.SelectMany(a => a.Requests).ToList();
    Assert.Equal(specs.Sum(sp => sp.Cycles) - actors.Sum(a => a.Skipped),
                 all.Count);
    Assert.All(all, r => Assert.True(r.Released > r.Granted));

    // never more than N held at once
    foreach (var r in all) {
      var held =
          all.Count(o => o.Granted <= r.Granted && o.Released > r.Granted);
      Assert.True(held <= instances,
                  $"{name}: {held} held at once with {instances} instances");
    }

    // FIFO simulation using the actual request times and the nominal holds
    var free = Enumerable.Repeat(DateTimeOffset.MinValue, instances).ToList();
    foreach (var r in all.OrderBy(x => x.Pushed)) {
      free.Sort();
      var granted = free[0] > r.Pushed ? free[0] : r.Pushed;
      free[0] = granted + r.Hold;
      Assert.InRange(r.Granted - granted, -slack, slack);
      Assert.True(r.Released - r.Granted >= r.Hold - early,
                  $"{name}: released before its hold ended");
    }

    // no resource idle while a request waits
    foreach (var r in all.Where(x => x.Granted - x.Pushed >
                                     TimeSpan.FromMilliseconds(100))) {
      var t = r.Pushed + TimeSpan.FromMilliseconds(30);
      var held = all.Count(o => o.Granted <= t && o.Released > t);
      Assert.True(held == instances,
                  $"{name}: request waited with {held}/{instances} held");
    }

    // every resource went back to its queue
    var log = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(
        all.Count,
        log.Count(l => l.Contains("ResourceQueue<heater> receives one resource",
                                  StringComparison.Ordinal)));
  }

  public sealed record RequestRecord(DateTimeOffset Pushed,
                                     DateTimeOffset Granted,
                                     DateTimeOffset Released, TimeSpan Hold);

  private sealed class CyclingActor : IResourceConsumer {
    private readonly object _lock = new();
    private readonly string _resource;
    private readonly Spec _spec;
    private readonly IResourceQueueSet _queues;
    private readonly List<RequestRecord> _requests = [];
    private readonly TaskCompletionSource _done =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _begin;
    private DateTimeOffset _pushed;
    private int _cycle;

    public CyclingActor(string resource, Spec spec, IResourceQueueSet queues) {
      _resource = resource;
      _spec = spec;
      _queues = queues;
    }

    public Task Done => _done.Task;
    public int Skipped { get; private set; }

    public RequestRecord[] Requests {
      get {
        lock (_lock) { return _requests.ToArray(); }
      }
    }

    public bool IsGhost() { return false; }

    public IEnumerable<ResourceName> Scope() { return [_resource]; }

    public void Begin(DateTimeOffset begin) {
      _begin = begin;
      _ = Task.Run(async () => {
        await Task.Delay(_spec.Start);
        Push();
      });
    }

    private void Push() {
      _pushed = DateTimeOffset.UtcNow;
      _queues.MultiPush(this);
    }

    // called on the queue's thread: hand the work to another thread
    public void Grant(ResourceInstance resource) {
      var granted = DateTimeOffset.UtcNow;
      _ = Task.Run(async () => {
        var hold = TimeSpan.FromMilliseconds(_spec.Hold);
        await Task.Delay(hold);
        var released = DateTimeOffset.UtcNow;
        lock (_lock) { _requests.Add(new(_pushed, granted, released, hold)); }
        _queues.MultiRelease([resource]);

        _cycle++;
        if (_cycle >= _spec.Cycles) {
          _done.TrySetResult();
          return;
        }
        await Task.Delay(_spec.Gap);
        if (_spec.StopAfter > 0 &&
            DateTimeOffset.UtcNow - _begin >=
                TimeSpan.FromMilliseconds(_spec.StopAfter)) {
          Skipped = _spec.Cycles - _cycle;
          _done.TrySetResult();
          return;
        }
        Push();
      });
    }

    public string Identity() { return "CyclingActor"; }
  }

  // Like Spec, but each actor requests its own set of resource types.
  public sealed record TSpec(int Start, int Hold, int Cycles, int Gap,
                             int StopAfter, string[] Scope);

  private static TSpec T(int start, int hold, int cycles, int gap, int stop,
                         params string[] scope) => new(start, hold, cycles, gap,
                                                       stop, scope);

  private static TSpec[] Many(int n, int step, int hold, int cycles, int gap,
                              int stop, params string[] scope) =>
      Enumerable.Range(0, n)
          .Select(i => T(i * step, hold, cycles, gap, stop, scope))
          .ToArray();

  public static IEnumerable<object[]> MultiTypeCases() {
    string[] ab = ["a", "b"];
    string[] ba = ["b", "a"];
    string[] abc = ["a", "b", "c"];
    yield return new object[] {
      "disjoint-types-no-contention", new[] { "a:1", "b:1" },
      new[] { T(0, 150, 2, 50, 0, "a"), T(10, 150, 2, 50, 0, "b") }
    };
    yield return new object[] { "three-actors-need-both-single-instances",
                                new[] { "a:1", "b:1" },
                                Many(3, 20, 150, 1, 0, 0, "a", "b") };
    yield return new object[] {
      "mixed-single-and-pair-scopes", new[] { "a:2", "b:1" },
      new[] { T(0, 200, 2, 50, 0, "a"), T(20, 200, 2, 50, 0, "a"),
              T(40, 150, 2, 50, 0, "b"), T(60, 250, 2, 50, 0, "a", "b") }
    };
    yield return new object[] { "three-types-all-needed",
                                new[] { "a:1", "b:1", "c:1" },
                                Many(3, 20, 120, 2, 0, 0, "a", "b", "c") };
    yield return new object[] { "circular-overlapping-pairs",
                                new[] { "a:1", "b:1", "c:1" },
                                new[] { T(0, 100, 3, 0, 0, "a", "b"),
                                        T(5, 100, 3, 0, 0, "b", "c"),
                                        T(10, 100, 3, 0, 0, "c", "a") } };
    yield return new object[] {
      "opposite-scope-orders", new[] { "a:1", "b:1" },
      new[] { T(0, 100, 4, 0, 0, "a", "b"), T(3, 100, 4, 0, 0, "b", "a"),
              T(6, 100, 4, 0, 0, "a", "b"), T(9, 100, 4, 0, 0, "b", "a") }
    };
    yield return new object[] { "plenty-of-supply", new[] { "a:3", "b:3" },
                                Many(3, 20, 200, 3, 100, 0, "a", "b") };
    yield return new object[] { "one-scarce-type-bottleneck",
                                new[] { "a:4", "b:1" },
                                Many(5, 20, 120, 2, 0, 0, "a", "b") };
    yield return new object[] { "local-work-reduces-demand",
                                new[] { "a:2", "b:2" },
                                Many(4, 20, 150, 3, 300, 0, "a", "b") };
    yield return new object[] {
      "two-actors-stop-mid-run", new[] { "a:2", "b:1" },
      new[] { T(0, 100, 6, 0, 300, "a", "b"), T(20, 100, 6, 0, 300, "a", "b"),
              T(40, 100, 6, 0, 0, "a", "b"), T(60, 100, 6, 0, 0, "a", "b") }
    };
    yield return new object[] {
      "late-arrival-after-burst", new[] { "a:2", "b:2" },
      new[] { T(0, 200, 1, 0, 0, "a", "b"), T(20, 200, 1, 0, 0, "a", "b"),
              T(40, 200, 1, 0, 0, "a", "b"), T(900, 200, 1, 0, 0, "a", "b") }
    };
    yield return new object[] {
      "wide-scope-among-narrow", new[] { "a:1", "b:1", "c:1", "d:1" },
      new[] { T(0, 100, 3, 50, 0, "a"), T(5, 100, 3, 50, 0, "b"),
              T(10, 100, 3, 50, 0, "c"), T(15, 100, 3, 50, 0, "d"),
              T(20, 150, 3, 50, 0, "a", "b", "c", "d") }
    };
    yield return new object[] {
      "heavy-mix-three-types", new[] { "a:2", "b:2", "c:1" },
      new[] { T(0, 150, 3, 50, 0, "a", "b"), T(10, 150, 3, 50, 0, "b", "c"),
              T(20, 100, 3, 50, 0, "a"), T(30, 200, 3, 50, 0, "c"),
              T(40, 120, 3, 50, 0, "a", "b", "c"), T(50, 80, 3, 50, 0, "b") }
    };
    yield return new object[] {
      "single-scopes-across-three-types", new[] { "a:1", "b:2", "c:3" },
      new[] { T(0, 300, 2, 50, 0, "a"), T(10, 300, 2, 50, 0, "a"),
              T(20, 200, 2, 50, 0, "b"), T(30, 200, 2, 50, 0, "b"),
              T(40, 200, 2, 50, 0, "b"), T(50, 100, 2, 50, 0, "c") }
    };
    yield return new object[] {
      "mixed-holds-with-local-work", new[] { "a:3", "b:1" },
      new[] { T(0, 100, 2, 100, 0, "a"), T(20, 400, 2, 100, 0, "a", "b"),
              T(40, 250, 2, 100, 0, "a"), T(60, 150, 2, 100, 0, "a", "b"),
              T(80, 300, 2, 100, 0, "a") }
    };
    yield return new object[] { "two-wide-two-narrow",
                                new[] { "a:1", "b:1", "c:1" },
                                new[] { T(0, 150, 2, 0, 0, "a", "b", "c"),
                                        T(10, 150, 2, 0, 0, "a", "b", "c"),
                                        T(20, 100, 2, 0, 0, "a"),
                                        T(30, 100, 2, 0, 0, "c") } };
    yield return new object[] {
      "big-burst-two-types", new[] { "a:3", "b:2" },
      new[] { T(0, 150, 1, 0, 0, "a", "b"), T(5, 150, 1, 0, 0, "a"),
              T(10, 150, 1, 0, 0, "a", "b"), T(15, 150, 1, 0, 0, "a"),
              T(20, 150, 1, 0, 0, "a", "b"), T(25, 150, 1, 0, 0, "b"),
              T(30, 150, 1, 0, 0, "a", "b"), T(35, 150, 1, 0, 0, "a") }
    };
    yield return new object[] {
      "sustained-run-with-singles-and-pairs", new[] { "a:2", "b:2" },
      new[] { T(0, 120, 4, 30, 0, "a"), T(10, 120, 4, 30, 0, "b"),
              T(20, 120, 4, 30, 0, "a", "b"), T(30, 120, 4, 30, 0, "a", "b"),
              T(40, 120, 4, 30, 0, "a"), T(50, 120, 4, 30, 0, "b") }
    };
    yield return new object[] {
      "demand-drops-then-recovers", new[] { "a:2", "b:2", "c:1" },
      new[] { T(0, 100, 8, 0, 250, "a", "b"), T(10, 100, 8, 0, 250, "b", "c"),
              T(20, 100, 8, 0, 250, "a", "c"),
              T(800, 100, 2, 0, 0, "a", "b", "c"), T(820, 100, 2, 0, 0, "a") }
    };
    yield return new object[] { "supply-exceeds-demand-four-types",
                                new[] { "a:2", "b:2", "c:2", "d:2" },
                                new[] { T(0, 150, 3, 50, 0, "a", "b"),
                                        T(10, 150, 3, 50, 0, "c", "d"),
                                        T(20, 150, 3, 50, 0, "a", "d") } };
  }

  // Actors cycle request, hold, release and local work over several resource
  // types, each type having its own number of instances. Checks:
  // completion, per-type capacity, FIFO timing from a simulation, no type
  // idle while a request that needs it waits, and every resource returned.
  [Theory]
  [MemberData(nameof(MultiTypeCases))]
  public async Task ActorsShareSeveralResourceTypes(string name, string[] types,
                                                    TSpec[] specs) {
    var quantity = types.ToDictionary(t => t.Split(':')[0],
                                      t => int.Parse(t.Split(':')[1]));
    var set =
        new ResourceSet { elements =
                              quantity
                                  .Select(kv => new ResourceType {
                                    name = kv.Key, quantity = kv.Value,
                                    tickrate = TimeSpan.FromMilliseconds(50),
                                    minwork = TimeSpan.FromMilliseconds(50),
                                    maxwork = TimeSpan.FromSeconds(2)
                                  })
                                  .ToList() };
    var queues = new ResourceQueueSet();
    var actors = specs.Select(sp => new MultiTypeActor(sp, queues)).ToArray();
    var slack = TimeSpan.FromMilliseconds(150);
    var early = TimeSpan.FromMilliseconds(5);

    setup($"ResourceQueueSetTypesTests-{Guid.NewGuid():N}");
    try {
      queues.configure(set);
      queues.Start();
      try {
        var begin = DateTimeOffset.UtcNow;
        foreach (var a in actors)
          a.Begin(begin);
        await Task.WhenAll(actors.Select(a => a.Done))
            .WaitAsync(TimeSpan.FromSeconds(20));
      } finally {
        queues.Stop();
      }
    } finally {
      finish();
    }

    var all = actors.SelectMany(a => a.Requests).ToList();
    Assert.Equal(specs.Sum(sp => sp.Cycles) - actors.Sum(a => a.Skipped),
                 all.Count);
    Assert.All(all, r => Assert.True(r.Released > r.Started));

    // per-type capacity: grants of a type outstanding at any grant time
    var events =
        all.SelectMany(r => r.Grants.Select(g => (g.Type, g.At, r.Released)))
            .ToList();
    foreach (var e in events) {
      var held = events.Count(o => o.Type == e.Type && o.At <= e.At &&
                                   o.Released > e.At);
      Assert.True(
          held <= quantity[e.Type],
          $"{name}: {held} of '{e.Type}' held, only {quantity[e.Type]} exist");
    }

    // FIFO simulation: a request starts when every type it needs has an
    // instance free; each instance is then busy until the hold ends
    var free = quantity.ToDictionary(
        kv => kv.Key,
        kv => Enumerable.Repeat(DateTimeOffset.MinValue, kv.Value).ToList());
    foreach (var r in all.OrderBy(x => x.Pushed)) {
      var start = r.Scope.Select(t => free[t].Min()).Append(r.Pushed).Max();
      foreach (var t in r.Scope) {
        free[t].Sort();
        free[t][0] = start + r.Hold;
      }
      Assert.InRange(r.Started - start, -slack, slack);
      Assert.True(r.Released - r.Started >= r.Hold - early,
                  $"{name}: released before its hold ended");
    }

    // a long wait means some needed type had every instance held
    foreach (var r in all.Where(x => x.Started - x.Pushed >
                                     TimeSpan.FromMilliseconds(100))) {
      var t = r.Pushed + TimeSpan.FromMilliseconds(30);
      var saturated = r.Scope.Any(
          type => events.Count(o => o.Type == type && o.At <= t &&
                                    o.Released > t) == quantity[type]);
      Assert.True(saturated, $"{name}: request waited with free instances");
    }

    // every resource went back to its queue
    var log = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(all.Sum(r => r.Scope.Length),
                 log.Count(l => l.Contains(" receives one resource",
                                           StringComparison.Ordinal)));
  }

  public sealed record TypedGrant(string Type, DateTimeOffset At);

  public sealed record TypedRequest(string[] Scope, DateTimeOffset Pushed,
                                    DateTimeOffset Started,
                                    DateTimeOffset Released, TimeSpan Hold,
                                    TypedGrant[] Grants);

  private sealed class MultiTypeActor : IResourceConsumer {
    private readonly object _lock = new();
    private readonly TSpec _spec;
    private readonly IResourceQueueSet _queues;
    private readonly List<TypedRequest> _requests = [];
    private readonly List<ResourceInstance> _held = [];
    private readonly List<TypedGrant> _grants = [];
    private readonly TaskCompletionSource _done =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _begin;
    private DateTimeOffset _pushed;
    private int _cycle;

    public MultiTypeActor(TSpec spec, IResourceQueueSet queues) {
      _spec = spec;
      _queues = queues;
    }

    public Task Done => _done.Task;
    public int Skipped { get; private set; }

    public TypedRequest[] Requests {
      get {
        lock (_lock) { return _requests.ToArray(); }
      }
    }

    public bool IsGhost() { return false; }

    public IEnumerable<ResourceName> Scope() { return _spec.Scope; }

    public void Begin(DateTimeOffset begin) {
      _begin = begin;
      _ = Task.Run(async () => {
        await Task.Delay(_spec.Start);
        Push();
      });
    }

    private void Push() {
      _pushed = DateTimeOffset.UtcNow;
      _queues.MultiPush(this);
    }

    // called on a queue's thread: record, and once everything has arrived hand
    // the work to another thread
    public void Grant(ResourceInstance resource) {
      var now = DateTimeOffset.UtcNow;
      ResourceInstance[]? held = null;
      TypedGrant[]? grants = null;
      lock (_lock) {
        _held.Add(resource);
        _grants.Add(new(resource.GetName(), now));
        if (_held.Count == _spec.Scope.Length) {
          held = _held.ToArray();
          grants = _grants.ToArray();
          _held.Clear();
          _grants.Clear();
        }
      }
      if (held == null)
        return;

      var started = now;
      _ = Task.Run(async () => {
        var hold = TimeSpan.FromMilliseconds(_spec.Hold);
        await Task.Delay(hold);
        var released = DateTimeOffset.UtcNow;
        lock (_lock) {
          _requests.Add(
              new(_spec.Scope, _pushed, started, released, hold, grants!));
        }
        _queues.MultiRelease(held);

        _cycle++;
        if (_cycle >= _spec.Cycles) {
          _done.TrySetResult();
          return;
        }
        await Task.Delay(_spec.Gap);
        if (_spec.StopAfter > 0 &&
            DateTimeOffset.UtcNow - _begin >=
                TimeSpan.FromMilliseconds(_spec.StopAfter)) {
          Skipped = _spec.Cycles - _cycle;
          _done.TrySetResult();
          return;
        }
        Push();
      });
    }

    public string Identity() { return "MultiTypeActor"; }
  }

  private sealed class MockActor : IResourceConsumer {
    private readonly object _lock = new();
    private readonly string _name;
    private readonly string _resource;
    private readonly TimeSpan _hold;
    private readonly IResourceQueueSet _queues;
    private readonly List<ResourceInstance> _granted = [];
    private readonly TaskCompletionSource _done =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MockActor(string name, string resource, TimeSpan hold,
                     IResourceQueueSet queues) {
      _name = name;
      _resource = resource;
      _hold = hold;
      _queues = queues;
    }

    public MockActor(string resource)
        : this("mock", resource, TimeSpan.Zero, null!) {}

    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset ReleasedAt { get; private set; }
    public Task Done => _done.Task;

    public bool IsGhost() { return false; }

    public IEnumerable<ResourceName> Scope() { return [_resource]; }

    // called on the queue's thread under its lock: never block or call back
    public void Grant(ResourceInstance resource) {
      lock (_lock) {
        _granted.Add(resource);
        GrantedAt = DateTimeOffset.UtcNow;
      }
      if (_queues == null)
        return;
      _ = Task.Run(async () => {
        await Task.Delay(_hold);
        ResourceInstance[] held;
        lock (_lock) {
          held = _granted.ToArray();
          ReleasedAt = DateTimeOffset.UtcNow;
        }
        _queues.MultiRelease(held);
        _done.TrySetResult();
      });
    }

    public ResourceInstance[] Granted {
      get {
        lock (_lock) { return _granted.ToArray(); }
      }
    }

    public string Identity() { return "MockActor"; }
  }
}

}
}
}
