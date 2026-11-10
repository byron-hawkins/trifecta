/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using trifecta.environment;
using trifecta.simulation;
using Xunit;

namespace trifecta {
namespace test {
namespace simulation {

// Real ScriptedActiveSensorDriver<T> with a mocked ISensorAware<T>.
// Readings depend on the tick count only (not on wall-clock jitter), so the
// expected sequence is computed from the script and compared exactly.
public sealed class ScriptedActiveSensorDriverTests {
  private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(40);

  private static SensorScript<T> Script<T>(params(int ms, T value)[] events) {
    var script = new SensorScript<T>();
    foreach (var (ms, value) in events)
      script.events.Add(
          new SensorScript<T>.Event { duration = TimeSpan.FromMilliseconds(ms),
                                      value = value });
    return script;
  }

  private static SensorDriverFactory.SensorConfiguration
  Config(int id) => new() { id = id, name = $"mock-{id}", tickinterval = Tick };

  // reading expected at tick n (1-based): position in the cyclic script, the
  // value of the segment start plus the linear slide towards the next event
  private static T Expected<T>(SensorScript<T> script, int n)
      where T : INumber<T> {
    var total = script.events.Sum(e => (long)e.duration.TotalMilliseconds);
    var pos = n * (long)Tick.TotalMilliseconds % total;
    long start = 0;
    for (int i = 0; i < script.events.Count; i++) {
      var dur = (long)script.events[i].duration.TotalMilliseconds;
      if (pos < start + dur) {
        var here = script.events[i].value;
        var next = script.events[(i + 1) % script.events.Count].value;
        var ratio = (double)(pos - start) / dur;
        return here +
               T.CreateChecked(double.CreateChecked(next - here) * ratio);
      }
      start += dur;
    }
    throw new InvalidOperationException("position outside script");
  }

  private static void AssertReading<T>(T expected, T actual, int n)
      where T : INumber<T> {
    var diff = Math.Abs(double.CreateChecked(expected - actual));
    var allowed = typeof(T) == typeof(float) ? 0.001 : 0;
    Assert.True(diff <= allowed,
                $"tick {n}: expected {expected}, got {actual}");
  }

  private static async Task<List<(int Id, T Value, DateTimeOffset At)>>
  Record<T>(SensorScript<T> script, int id, int readings)
      where T : INumber<T> {
    var driver = new ScriptedActiveSensorDriver<T>(script, Config(id));
    var aware = new MockAware<T>();
    driver.Attach(aware);

    driver.Start();
    try {
      await aware.WaitFor(readings, TimeSpan.FromSeconds(10));
    } finally {
      driver.Stop();
    }

    var got = aware.Readings;
    var frozen = got.Count;
    await Task.Delay(Tick * 3);
    Assert.Equal(frozen, aware.Readings.Count); // nothing after Stop()
    return got.Take(readings).ToList();
  }

  private static async Task CheckSequence<T>(SensorScript<T> script,
                                             int readings)
      where T : INumber<T> {
    var got = await Record(script, 7, readings);

    Assert.Equal(readings, got.Count);
    Assert.All(got, r => Assert.Equal(7, r.Id));
    for (int n = 1; n <= readings; n++)
      AssertReading(Expected(script, n), got[n - 1].Value, n);

    // one update per tick: spacing is at least about a tick
    for (int n = 1; n < got.Count; n++)
      Assert.True(got[n].At - got[n - 1].At >=
                      Tick - TimeSpan.FromMilliseconds(10),
                  $"updates {n} and {n + 1} came faster than the tickrate");
  }

  [Fact]
  public async Task TemperatureRisesAndFallsBetweenTwoEvents() {
    // 200 ms up from 10 to 20, 200 ms back down; 5 ticks per segment
    await CheckSequence(Script<Temperature>((200, 10f), (200, 20f)), 12);
  }

  [Fact]
  public async Task TemperatureSlidesOverUnevenSegmentsNotAlignedToTicks() {
    // segment boundaries fall between ticks, so the remainder carries over
    await CheckSequence(Script<Temperature>((110, 5f), (70, 25f), (150, -5f)),
                        16);
  }

  [Fact]
  public async Task PressureRisesAndFallsBetweenTwoEvents() {
    await CheckSequence(Script<Pressure>((200, 100), (200, 200)), 12);
  }

  [Fact]
  public async Task PressureSlidesOverUnevenSegmentsNotAlignedToTicks() {
    await CheckSequence(Script<Pressure>((110, 1000), (70, 1300), (150, 900)),
                        16);
  }

  [Fact]
  public async Task ConstantScriptRepeatsTheSameValue() {
    var got = await Record(Script<Temperature>((100, 21.5f)), 3, 6);
    Assert.All(got, r => Assert.Equal(21.5f, r.Value));
  }

  [Fact]
  public async Task EveryAttachedAwareReceivesReadingsUntilDetached() {
    var script = Script<Temperature>((200, 10f), (200, 20f));
    var driver = new ScriptedActiveSensorDriver<Temperature>(script, Config(1));
    var kept = new MockAware<Temperature>();
    var dropped = new MockAware<Temperature>();
    driver.Attach(kept);
    driver.Attach(dropped);

    driver.Start();
    try {
      await dropped.WaitFor(3, TimeSpan.FromSeconds(5));
      driver.Detach(dropped);
      var before = dropped.Readings.Count;
      await kept.WaitFor(before + 4, TimeSpan.FromSeconds(5));
      Assert.Equal(before, dropped.Readings.Count);
    } finally {
      driver.Stop();
    }
  }

  [Fact]
  public async Task EmptyScriptNeverReports() {
    var driver =
        new ScriptedActiveSensorDriver<Pressure>(Script<Pressure>(), Config(2));
    var aware = new MockAware<Pressure>();
    driver.Attach(aware);

    driver.Start();
    await Task.Delay(Tick * 3);
    driver.Stop();

    Assert.Empty(aware.Readings);
  }

  [Fact]
  public async Task StopWithoutStartIsHarmless() {
    var driver = new ScriptedActiveSensorDriver<Pressure>(
        Script<Pressure>((100, 1)), Config(2));
    driver.Stop();
    await Task.CompletedTask;
  }

  [Fact]
  public async Task DriverReportsAgainAfterStopAndRestart() {
    var script = Script<Temperature>((200, 10f), (200, 20f));
    var driver = new ScriptedActiveSensorDriver<Temperature>(script, Config(4));
    var aware = new MockAware<Temperature>();
    driver.Attach(aware);

    driver.Start();
    await aware.WaitFor(2, TimeSpan.FromSeconds(5));
    driver.Stop();
    var first = aware.Readings.Count;

    driver.Start();
    try {
      await aware.WaitFor(first + 2, TimeSpan.FromSeconds(3));
    } finally {
      driver.Stop();
    }
    Assert.True(aware.Readings.Count >= first + 2);
  }

  [Fact]
  public async Task DetachFromInsideACallbackDoesNotBlockOrThrow() {
    var script = Script<Temperature>((200, 10f), (200, 20f));
    var driver = new ScriptedActiveSensorDriver<Temperature>(script, Config(5));
    var other = new MockAware<Temperature>();
    var selfDetaching = new MockAware<Temperature>();
    selfDetaching.OnUpdate = () => driver.Detach(selfDetaching);
    driver.Attach(selfDetaching);
    driver.Attach(other);

    driver.Start();
    try {
      await other.WaitFor(5, TimeSpan.FromSeconds(5));
    } finally {
      driver.Stop();
    }

    Assert.Equal(1, selfDetaching.Readings.Count);
    Assert.True(other.Readings.Count >= 5);
  }

  [Fact]
  public async
      Task AttachAndDetachDuringARunNeverThrowOrLoseTheStableListener() {
    var script = Script<Temperature>((200, 10f), (200, 20f));
    var driver = new ScriptedActiveSensorDriver<Temperature>(script, Config(6));
    var stable = new MockAware<Temperature>();
    driver.Attach(stable);

    driver.Start();
    try {
      var churn = Task.Run(() => {
        for (int i = 0; i < 20000; i++) {
          var a = new MockAware<Temperature>();
          driver.Attach(a);
          driver.Detach(a);
        }
      });
      await churn.WaitAsync(TimeSpan.FromSeconds(10));
      await stable.WaitFor(5, TimeSpan.FromSeconds(5));
    } finally {
      driver.Stop();
    }

    Assert.True(stable.Readings.Count >= 5);
  }

  [Fact]
  public async Task StopFromInsideACallbackDoesNotDeadlockAndAllowsRestart() {
    var script = Script<Temperature>((200, 10f), (200, 20f));
    var driver = new ScriptedActiveSensorDriver<Temperature>(script, Config(8));
    var aware = new MockAware<Temperature>();
    aware.OnUpdate = () => {
      if (aware.Readings.Count == 3)
        driver.Stop();
    };
    driver.Attach(aware);

    driver.Start();
    // Stop() inside the callback must return; the loop then ends by itself
    await Task.Delay(Tick * 12);
    Assert.Equal(3, aware.Readings.Count);

    // a clean restart reports again; and an external Stop() joins normally
    aware.OnUpdate = null;
    driver.Start();
    try {
      await aware.WaitFor(5, TimeSpan.FromSeconds(3));
    } finally {
      var stopped = Task.Run(driver.Stop);
      await stopped.WaitAsync(TimeSpan.FromSeconds(3));
    }
    Assert.True(aware.Readings.Count >= 5);
  }

  private sealed class MockAware<T> : ISensorAware<T> {
    private readonly object _lock = new();
    private readonly List<(int, T, DateTimeOffset)> _readings = [];

    public Action? OnUpdate;

    public void SensorUpdate(SensorId id, T reading) {
      lock (_lock) _readings.Add((id, reading, DateTimeOffset.UtcNow));
      OnUpdate?.Invoke();
    }

    public List<(int Id, T Value, DateTimeOffset At)> Readings {
      get { lock (_lock) return _readings.ToList(); }
    }

    public async Task WaitFor(int count, TimeSpan timeout) {
      var until = DateTimeOffset.UtcNow + timeout;
      while (Readings.Count < count && DateTimeOffset.UtcNow < until)
        await Task.Delay(5);
    }
  }
}

}
}
}
