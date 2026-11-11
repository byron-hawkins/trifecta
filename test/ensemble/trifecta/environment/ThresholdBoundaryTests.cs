/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using trifecta.environment;
using trifecta.io;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace environment {

[Collection("ComponentLog tests")]
public sealed class ThresholdBoundaryTests : ComponentLogTestBase {
  private const float TMin = 20f;
  private const int PMax = 50;

  // each step is (temperature, pressure); the role works while T > TMin and
  // P < PMax
  public static IEnumerable<object[]> Excursions() {
    // temperature reaches exactly TMin, then reverses
    yield return [
      "T up to bound, then back", new[] { (10f, 0), (TMin, 0), (10f, 0) }, true
    ];
    yield return [
      "T down to bound, then back", new[] { (30f, 0), (TMin, 0), (30f, 0) },
      true
    ];
    // pressure reaches exactly PMax, then reverses
    yield return [
      "P up to bound, then back", new[] { (30f, 40), (30f, PMax), (30f, 40) },
      true
    ];
    yield return [
      "P down to bound, then back", new[] { (30f, 60), (30f, PMax), (30f, 60) },
      true
    ];
    // the bound is only touched on the way through
    yield return
        ["T up through bound", new[] { (10f, 0), (TMin, 0), (30f, 0) }, false];
    yield return [
      "T down through bound", new[] { (30f, 0), (TMin, 0), (10f, 0) }, false
    ];
    yield return [
      "P up through bound", new[] { (30f, 40), (30f, PMax), (30f, 60) }, false
    ];
    yield return [
      "P down through bound", new[] { (30f, 60), (30f, PMax), (30f, 40) }, false
    ];
  }

  [Theory]
  [MemberData(nameof(Excursions))]
  public async Task BoundaryTouchLeavesTheRoleConsistentWithItsBounds(
      string name, (float t, int p)[] steps, bool reverses) {
    var interpreter = SensorInterpreter.singleton;
    var director = Director.singleton;
    var listener = new CrossingCounter();
    var role = new RoleConfiguration { id = 1, quantity = 1, tmin = TMin,
                                       pmax = PMax };
    var actor = new RunningActor(0, role);
    var observed = new List<(bool running, bool expected)>();

    setup($"ThresholdBoundaryTests-{Guid.NewGuid():N}");
    try {
      interpreter.SetThresholds(
          new SensorInterpreter.Thresholds { t = [TMin], p = [PMax] });
      interpreter.SensorUpdate(0, (Temperature)steps[0].t);
      interpreter.SensorUpdate(0, (Pressure)steps[0].p);

      director.Reset();
      var config = new DirectorConfiguration { mode = TransitionMode.quickstop,
                                               roles = [role] };
      director.configure(config, new() { [role.id] = [actor] });
      director.Start();
      try {
        await Settle(actor, steps[0]);
        observed.Add((actor.Running, Expected(steps[0])));

        interpreter.Register(listener);
        foreach (var step in steps.Skip(1)) {
          if (step.t != interpreter.GetTemperature())
            interpreter.SensorUpdate(0, (Temperature)step.t);
          if (step.p != interpreter.GetPressure())
            interpreter.SensorUpdate(0, (Pressure)step.p);
          await Settle(actor, step);
          observed.Add((actor.Running, Expected(step)));
        }
      } finally {
        interpreter.Release(listener);
        await Task.Run(() => director.Stop(true))
            .WaitAsync(TimeSpan.FromSeconds(2));
        interpreter.Release(director);
        interpreter.SetThresholds(
            new SensorInterpreter.Thresholds { t = [], p = [] });
      }
    } finally {
      finish();
    }

    for (int i = 0; i < observed.Count; i++) {
      Assert.True(
          observed[i].running == observed[i].expected,
          $"{name}: step {i} {steps[i]} role running={observed[i].running}, expected {observed[i].expected}");
    }

    // touching a bound and reversing is either invisible or reported twice
    if (reverses) {
      Assert.True(listener.Count is 0 or 2,
                  $"{name}: {listener.Count} crossings were reported");
    }
  }

  private static bool Expected((float t, int p)step) => step.t > TMin
                                                        && step.p < PMax;

  // gives the Director time to react, returning early once the Actor agrees
  private static async Task Settle(RunningActor actor, (float t, int p)step) {
    var until = DateTime.UtcNow + TimeSpan.FromMilliseconds(400);
    while (DateTime.UtcNow < until && actor.Running != Expected(step))
      await Task.Delay(10);
    await Task.Delay(50);
  }

  private sealed class RunningActor : Actor {
    private int _running;

    public RunningActor(int index, RoleConfiguration role)
        : base(index, role) {}

    public bool Running => Volatile.Read(ref _running) != 0;

    public override void Start(bool ignorable = false) {
      Volatile.Write(ref _running, 1);
    }

    public override void Stop(ActorPhase mode, Action? finalize = null,
                              bool ignorable = true) {
      Volatile.Write(ref _running, 0);
    }
  }

  private sealed class CrossingCounter : IConditionAware {
    private int _count;
    public int Count => Volatile.Read(ref _count);
    public void ThresholdCross(Temperature t) {
      Interlocked.Increment(ref _count);
    }
    public void ThresholdCross(Pressure p) {
      Interlocked.Increment(ref _count);
    }
  }
}

}
}
}
