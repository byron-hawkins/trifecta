/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Linq;
using trifecta.resource;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace resource {

// ResourceQueue driven directly: ghost consumers must be discarded without
// ever losing or duplicating a resource.
[Collection("ComponentLog tests")]
public sealed class ResourceQueueGhostTests : ComponentLogTestBase {
  private static ResourceType Type(int quantity) => new ResourceType {
    name = "heater", quantity = quantity,
    tickrate = TimeSpan.FromMilliseconds(50),
    minwork = TimeSpan.FromMilliseconds(50), maxwork = TimeSpan.FromSeconds(2)
  };

  private void Run(int quantity, Action<ResourceQueue> body) {
    setup($"ResourceQueueGhostTests-{Guid.NewGuid():N}");
    try {
      body(new ResourceQueue(Type(quantity)));
    } finally {
      finish();
    }
  }

  // drain every resource the queue holds by pushing live consumers
  private static List<ResourceInstance> Drain(ResourceQueue q, int expected) {
    var got = new List<ResourceInstance>();
    for (int i = 0; i < expected + 1; i++) {
      var c = new Consumer();
      q.Push(c);
      got.AddRange(c.Granted);
    }
    return got;
  }

  [Fact]
  public void ReleaseSkipsGhostAtHeadAndGrantsLiveConsumerBehindIt() {
    Run(1, q => {
      var held = new Consumer();
      q.Push(held);
      var ghost = new Consumer();
      var live = new Consumer();
      q.Push(ghost);
      q.Push(live);
      ghost.Ghost = true;

      q.Push(held.Granted[0]);

      Assert.Empty(ghost.Granted);
      Assert.Single(live.Granted);
    });
  }

  [Fact]
  public void ReleaseWithOnlyGhostsWaitingKeepsTheResource() {
    Run(1, q => {
      var held = new Consumer();
      q.Push(held);
      var ghost = new Consumer();
      q.Push(ghost);
      ghost.Ghost = true;

      q.Push(held.Granted[0]);

      Assert.Empty(ghost.Granted);
      var later = new Consumer();
      q.Push(later);
      Assert.Single(later.Granted);
    });
  }

  [Fact]
  public void ReleaseSkipsSeveralGhostsInARow() {
    Run(1, q => {
      var held = new Consumer();
      q.Push(held);
      var ghosts = Enumerable.Range(0, 4)
                       .Select(
                           _ => new Consumer())
                       .ToArray();
      foreach (var g in ghosts)
        q.Push(g);
      var live = new Consumer();
      q.Push(live);
      foreach (var g in ghosts)
        g.Ghost = true;

      q.Push(held.Granted[0]);

      Assert.All(ghosts, g => Assert.Empty(g.Granted));
      Assert.Single(live.Granted);
    });
  }

  [Fact]
  public void GhostsInterleavedWithLiveConsumersKeepFifoOrderOfLiveOnes() {
    Run(1, q => {
      var held = new Consumer();
      q.Push(held);
      var a = new Consumer();
      var g1 = new Consumer();
      var b = new Consumer();
      q.Push(a);
      q.Push(g1);
      q.Push(b);
      g1.Ghost = true;

      q.Push(held.Granted[0]);
      Assert.Single(a.Granted);
      Assert.Empty(b.Granted);

      q.Push(a.Granted[0]);
      Assert.Single(b.Granted);
    });
  }

  [Fact]
  public void
  GhostWaitingWithNoLiveConsumerNeverConsumesAResourceAcrossReleases() {
    Run(2, q => {
      var h1 = new Consumer();
      var h2 = new Consumer();
      q.Push(h1);
      q.Push(h2);
      var g1 = new Consumer();
      var g2 = new Consumer();
      q.Push(g1);
      q.Push(g2);
      g1.Ghost = true;
      g2.Ghost = true;

      q.Push(h1.Granted[0]);
      q.Push(h2.Granted[0]);

      // both instances must still exist
      var got = Drain(q, 2);
      Assert.Equal(2, got.Count);
      Assert.Equal(2, got.Distinct().Count());
    });
  }

  [Fact]
  public void GhostPushedAsConsumerIsIgnoredAndLeavesResourceAvailable() {
    Run(1, q => {
      var ghost = new Consumer { Ghost = true };
      q.Push(ghost);
      Assert.Empty(ghost.Granted);

      var live = new Consumer();
      q.Push(live);
      Assert.Single(live.Granted);
    });
  }

  [Fact]
  public void
  ConsumerBecomingGhostBeforeDispatchIsSkippedWithoutLosingResource() {
    Run(1, q => {
      var held = new Consumer();
      q.Push(held);
      var waiting = new Consumer();
      q.Push(waiting);
      waiting.Ghost = true;
      var live = new Consumer();
      q.Push(live);

      q.Push(held.Granted[0]);
      q.Dispatch();

      Assert.Empty(waiting.Granted);
      Assert.Single(live.Granted);
    });
  }

  [Fact]
  public void ResourceCountIsConservedThroughManyGhostChurnRounds() {
    Run(3, q => {
      var holders = Enumerable.Range(0, 3)
                        .Select(
                            _ => new Consumer())
                        .ToList();
      foreach (var h in holders)
        q.Push(h);
      var all = holders.SelectMany(h => h.Granted).ToList();

      for (int round = 0; round < 20; round++) {
        var ghost = new Consumer();
        q.Push(ghost);
        ghost.Ghost = true;
        var r = all[round % all.Count];
        q.Push(r); // release one, possibly past a ghost
        var again = new Consumer();
        q.Push(again);
        Assert.Single(again.Granted); // and it must be reacquirable
        Assert.Same(r, again.Granted[0]);
      }
    });
  }

  private sealed class Consumer : IResourceConsumer {
    private readonly List<ResourceInstance> _granted = [];
    public volatile bool Ghost;

    public bool IsGhost() { return Ghost; }

    public IEnumerable<ResourceName> Scope() { return ["heater"]; }

    public void Grant(ResourceInstance resource) {
      lock (_granted) _granted.Add(resource);
    }

    public List<ResourceInstance> Granted {
      get { lock (_granted) return _granted.ToList(); }
    }
  }
}

}
}
}
