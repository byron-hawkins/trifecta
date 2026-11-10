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

    var received = At($"{q} receives one consumer");
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
    var a = new MockActor("A", type.name,
                          TimeSpan.FromMilliseconds(holdAMs), queues);
    var b = new MockActor("B", type.name,
                          TimeSpan.FromMilliseconds(holdBMs), queues);
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
    Assert.Equal(2, Count($"{q} receives one consumer"));
    Assert.Equal(2, Count($"{q} receives one resource"));
    var lastResourceEntry = log.Last(
        l => l.Contains($"{q} enqueues resource", StringComparison.Ordinal) ||
             l.Contains($"{q} ricochet-grant", StringComparison.Ordinal) ||
             l.Contains($"{q} dispatch-grant", StringComparison.Ordinal));
    Assert.Contains("enqueues resource", lastResourceEntry);
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
  }
}

}
}
}
