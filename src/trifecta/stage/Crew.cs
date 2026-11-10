/* author: Byron */
/* stub: Copilot */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using trifecta.environment;
using trifecta.resource;
using trifecta.io;

namespace trifecta {
namespace stage {

public class Actor : IConditionAware, IResourceConsumer {
  public Actor(RoleConfiguration configuration) {
    ComponentLog.singleton.Log($"Constructing an Actor in role {configuration.id} with bounds T > {configuration.tmin} and P < {configuration.pmax}");

    foreach (SceneConfiguration scene in configuration.scenes) { _scenes.Add(new Scene(scene)); }

    _configuration = configuration;
  }

  public void Start() {}

  public void Stop(ActorPhase mode, Action finalize) {}

  public void ThresholdCross(Temperature t) {}

  public void ThresholdCross(Pressure p) {}

  public IEnumerable<ResourceName> Scope() {
    if (_onstage is Scene s) return s.Scope();
    else
      return [];
  }

  public void Grant(ResourceInstance resource) {}

  private RoleConfiguration _configuration;
  private ActorPhase _phase;
  private Scene? _onstage;
  private List<Scene> _scenes = [];
}

public class Scene {
  public Scene(SceneConfiguration configuration) {
    ComponentLog.singleton.Log(
        $"Constructing a Scene with duration {configuration.duration} and tick-rate {configuration.tickrate} requiring resources {string.Join(",", configuration.resources)}");

    _configuration = configuration;
  }

  public SceneStatus GetStatus() { return default; }

  public SceneStatus Execute() { return default; }

  public IEnumerable<ResourceName> Scope() { return _configuration.resources; }

  private SceneConfiguration _configuration;
  private List<ResourceInstance> _resources = [];
  private SceneStatus _status;
}

public class Director : IConditionAware, ICastManager {
  public static Director singleton = new();

  public void configure(DirectorConfiguration configuration) {
    ComponentLog.singleton.Log($"Configuring the Director with transition mode {configuration.mode} and {configuration.roles.Count} Actor roles");

    if (_started) throw new InvalidOperationException("Cannot configure the Director after it has been started");

    foreach (RoleConfiguration role in configuration.roles) {
      List<Actor> members = new();
      for (int i = 0; i < role.quantity; i++) members.Add(new Actor(role));
      _cast[role.id] = members;
    }

    _configuration = configuration;
  }

  public void Start() {
    ComponentLog.singleton.Log("Director start requested");

    Monitor.Enter(_idle);
    try {
      if (_started) throw new InvalidOperationException("Cannot restart the Director");
      if (_stop) throw new InvalidOperationException("Cannot start a Director that has been stopped");

      _started = true;
      _self = Task.Run(() => { run(); });
    } finally { Monitor.Exit(_idle); }
  }

  public void Stop(bool nicely) {
    ComponentLog.singleton.Log("Director stop requested");

    Task task;
    Monitor.Enter(_idle);
    try {
      if (!_started) throw new InvalidOperationException("Cannot stop a Director that has not been started");
      if (_stop) throw new InvalidOperationException("Cannot stop a Director that has been stopped");

      _stop = true;
      task = _self ?? throw new InvalidOperationException("Missing background task when attempting to stop the Director");
      Monitor.Pulse(_idle);
    } finally { Monitor.Exit(_idle); }
    task.Wait();

    Monitor.Enter(_idle);
    try {
      // TODO: stop all actors (now unless nice)

      _self = null;
    } finally { Monitor.Exit(_idle); }
  }

  public void Reset() {
    ComponentLog.singleton.Log("Director reset requested");

    Monitor.Enter(_idle);
    try {
      if (_started && (!_stop || _self != null)) throw new InvalidOperationException("Cannot reset a Director during showtime");

      _started = _stop = false;
      _cast.Clear();
    } finally { Monitor.Exit(_idle); }
  }

  public void ThresholdCross(Temperature t) {}

  public void ThresholdCross(Pressure p) {}

  public void Interrupt(Actor actor, ActorPhase exitStrategy) {}

  public void Initiate(RoleId role) {}

  private void run() {
    Monitor.Enter(_idle);
    try {
      while (true) {
        if (_stop) return;

        // TODO: whatever Director has to do

        ComponentLog.singleton.Log("Director goes idle");
        Monitor.Wait(_idle);
      }
    } finally { Monitor.Exit(_idle); }
  }

  private bool _started = false;
  private bool _stop = false;
  private Task? _self = null;
  private object _idle = new();

  private Dictionary<RoleId, List<Actor>> _cast = [];
  private CastOptimizer _optimizer = null!;
  private DirectorConfiguration _configuration = null!;
}

public class CastOptimizer {
  public CastOptimizer(DirectorConfiguration configuration, ICastManager manager) {
    _configuration = configuration;
    _manager = manager;
  }

  public void Optimize() {}

  private ICastManager _manager;
  private DirectorConfiguration _configuration;
}

public interface ICastManager {
  void Interrupt(Actor actor, ActorPhase exitStrategy);
  void Initiate(RoleId role);
}

public enum SceneStatus {
  pending,
  running,
  completed,
  error,
}

public enum ActorPhase {
  ready,
  queue,
  work,
  suspension,
  stop_at_cost,
  stop_at_completion,
  stop_now,
  exit,
}

}
}
