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
  public Actor(int index, RoleConfiguration configuration) : this(index, configuration, ResourceQueueSet.singleton) {}
  public Actor(int index, RoleConfiguration configuration, IResourceQueueSet qs) {
    ComponentLog.singleton.Log($"Constructing an Actor #{index} in role {configuration.id} with bounds T > {configuration.tmin} and P < {configuration.pmax}");

    int sequence = 0;
    foreach (SceneConfiguration scene in configuration.scenes) { _scenes.Add(new Scene(sequence++, scene, qs)); }

    _qs = qs;
    _index = index;
    _configuration = configuration;
    _identity = $"Actor {_configuration.id}/{_index}";
  }

  public void Start() {
    ComponentLog.singleton.Log($"{_identity} start requested");

    Monitor.Enter(_idle);
    try {
      if (_phase != ActorPhase.ready) throw new InvalidOperationException($"Cannot start {_identity} from phase {_phase}");
      if (_onstage != null) throw new InvalidOperationException($"Cannot start {_identity} with a scene on stage (phase {_phase})");

      _workflow = 0;  // ...should be already

      _self = Task.Run(() => { run(); });
    } finally { Monitor.Exit(_idle); }
  }

  public void Stop(ActorPhase mode, Action finalize) {
    ComponentLog.singleton.Log("{_identity} stop requested");

    Task task;
    Monitor.Enter(_idle);
    try {
      _phase = mode;
      task = _self ?? throw new InvalidOperationException("Missing background task when attempting to stop the Director");
      Monitor.Pulse(_idle);
    } finally { Monitor.Exit(_idle); }
    if (_phase == ActorPhase.stop_now) {  // not nice reading this out of the lock guard
      task.Wait();

      Monitor.Enter(_idle);
      try {
        _self = null;
      } finally { Monitor.Exit(_idle); }
    }
    finalize();
  }

  public void ThresholdCross(Temperature t) {}

  public void ThresholdCross(Pressure p) {}

  public IEnumerable<ResourceName> Scope() {
    if (_onstage is Scene s) return s.Scope();
    else
      return [];
  }

  public void Grant(ResourceInstance resource) {}

  // TODO: when to remit resources?
  private void run() {
    ComponentLog.singleton.Log($"{_identity} starts run() in phase {_phase}");

    Monitor.Enter(_idle);
    try {
      while (true) {
        ComponentLog.singleton.Log($"{_identity} loops run() in phase {_phase}");

        switch (_phase) {
          case ActorPhase.ready:
            if (_onstage != null || _workflow != 0) throw new InvalidOperationException($"{_identity} cannot be in phase {_phase} while a scene is on stage");
            _onstage = _scenes[_workflow];
            if (_onstage.IsLocal()) {
              _phase = ActorPhase.work;
              continue;
            }
            _phase = ActorPhase.queue_up;
            goto case ActorPhase.queue_up;
          case ActorPhase.queue_up:
            _qs.MultiPush(this);
            _phase = ActorPhase.on_queue;
            goto case ActorPhase.on_queue;
          case ActorPhase.on_queue:

            // check if resources granted (or will Grant() always move to .work?)
            break;
          case ActorPhase.work:
          case ActorPhase.stop_at_completion:
            work();

            if (_onstage.GetStatus() == SceneStatus.completed) {
              // remit: here?
              _onstage.Reset();
              if (++_workflow == _scenes.Count) {
                reset();
              } else {
                _onstage = _scenes[_workflow];
              }
            }
            continue;
          case ActorPhase.suspension:
          case ActorPhase.stop_at_cost: throw new NotImplementedException($"ActorPhase.{_phase}");
          case ActorPhase.stop_now:
          case ActorPhase.exit: return;
        }

        ComponentLog.singleton.Log($"Director goes idle");
        Monitor.Wait(_idle);
      }
    } finally { Monitor.Exit(_idle); }
  }

  private void work() {
    worklog("start");
    bool loggedOff = false;

    try {
      _onstage.Begin();

      Monitor.Exit(_idle); /* allow interruption */
      try {
        while (true) {
          TimeSpan duration = _onstage.GetStepDuration();

          ComponentLog.singleton.Log($"{_identity} loops work() with scene step duration {duration.Milliseconds}");

          SceneStatus status = _onstage.Execute(duration);
          // TODO: if this is an underflow step, log the work off
          switch (status) {
            case SceneStatus.completed: ComponentLog.singleton.Log($"{_identity} completes {_onstage.Identity()}"); return;
            case SceneStatus.underflow:
              loggedOff = true;
              worklog("stop");
              continue;
            case SceneStatus.dark: throw new InvalidOperationException($"Scene returned status {status} from Execute()");
            case SceneStatus.error: throw new NotImplementedException("SceneStatus.error");
          }
          Monitor.Enter(_idle); /* check phase safely */
          try {
            switch (_phase) {
              case ActorPhase.work:
              case ActorPhase.stop_at_completion: break;
              default: return;
            }
          } finally { Monitor.Exit(_idle); } /* allow interruption */
        }
      } finally { Monitor.Enter(_idle); } /* resume lockout for routing */
    } finally {
      if (!loggedOff) worklog("stop");
    }
  }

  private void worklog(string mode) { ComponentLog.singleton.Log($"{_identity} [{_onstage.Identity()}] [work: {mode}]"); }

  private void reset() {
    _workflow = 0;
    _phase = ActorPhase.ready;
    _onstage = null;
  }

  private IResourceQueueSet _qs;
  private int _index;
  private string _identity;
  private RoleConfiguration _configuration;

  private object _idle = new();
  private Task? _self = null;
  private int _workflow;
  private ActorPhase _phase = ActorPhase.ready;
  private Scene? _onstage;
  private List<Scene> _scenes = [];
}

/*
  ready,
  queue_up,
  on_queue,
  work,
  suspension,
  stop_at_cost,
  stop_at_completion,
  stop_now,
  exit,
*/

public class Scene {
  public Scene(int index, SceneConfiguration configuration) : this(index, configuration, ResourceQueueSet.singleton) {}
  public Scene(int index, SceneConfiguration configuration, IResourceQueueSet qs) {
    ComponentLog.singleton.Log(
        $"Constructing a Scene at index {index} with duration {configuration.duration} and tick-rate {configuration.tickrate} requiring resources {string.Join(",", configuration.resources)}");

    _qs = qs;
    _index = index;
    _configuration = configuration;
    _status = SceneStatus.dark;
    _identity = $"Scene: {_index}";
  }

  public SceneStatus GetStatus() { return _status; }

  public SceneStatus Execute(TimeSpan duration) {
    switch (_status) {
      case SceneStatus.working:
      case SceneStatus.underflow: break;
      default: throw new InvalidOperationException($"Cannot perform work on a scene with status {_status}");
    }

    Thread.Sleep(duration);

    if (_period.remaining == TimeSpan.Zero) {
      if (_period.underflow == TimeSpan.Zero) throw new InvalidOperationException("Scene cannot execute for 0ms");
      _period.underflow = TimeSpan.Zero;
      return apply(SceneStatus.completed);
    }

    _period.remaining -= duration;
    if (_period.remaining == TimeSpan.Zero) {
      if (_period.underflow == TimeSpan.Zero) return apply(SceneStatus.completed);
      else
        return apply(SceneStatus.underflow);
    }
    return apply(SceneStatus.working);
  }

  public void Begin() {
    switch (_status) {
      case SceneStatus.dark:
      case SceneStatus.completed: break;
      default: throw new InvalidOperationException($"Cannot begin work on a scene with status {_status}");
    }

    apply(SceneStatus.working);

    _period.Reset();
    _period.remaining = _configuration.duration;
    foreach (ResourceInstance resource in _resources) {
      if (resource.GetMinWork() > _period.remaining) {
        _period.underflow += (resource.GetMinWork() - _period.remaining);
        _period.remaining = resource.GetMinWork();
      } else if (_period.remaining > resource.GetMaxWork()) {
        _period.remaining = resource.GetMaxWork();
      }
    }

    ComponentLog.singleton.Log(
        $"{Identity()} exits Begin() with _period.remaining {_period.remaining.TotalMilliseconds} and _period.underflow {_period.underflow.TotalMilliseconds}");
  }

  public void Reset() {
    _status = SceneStatus.dark;
    _period.Reset();
    _qs.MultiRelease(_resources);
  }

  public IEnumerable<ResourceName> Scope() { return _configuration.resources; }

  public bool IsLocal() { return _configuration.resources.Count == 0; }

  public TimeSpan GetStepDuration() {
    if (_period.remaining == TimeSpan.Zero) {
      if (_period.underflow == TimeSpan.Zero) throw new InvalidOperationException("Scene cannot execute for 0ms");
      return _period.underflow;
    }

    TimeSpan step = _configuration.tickrate;
    if (step > _period.remaining) step = _period.remaining;
    return step;
  }

  public string Identity() { return _identity; }

  private SceneStatus apply(SceneStatus s) { return (_status = s); }

  private struct Period {
    public TimeSpan underflow;
    public TimeSpan remaining;

    public void Reset() { underflow = remaining = TimeSpan.Zero; }
  }

  private IResourceQueueSet _qs;
  private int _index;
  private string _identity;
  private Period _period = new();
  private SceneConfiguration _configuration;
  private List<ResourceInstance> _resources = [];
  private SceneStatus _status;
}

public class Director : IConditionAware, ICastManager {
  public static Director singleton = new();

  public void configure(DirectorConfiguration configuration) {
    ComponentLog.singleton.Log($"Configuring the Director with transition mode {configuration.mode} and {configuration.roles.Count} Actor roles");

    if (_started) throw new InvalidOperationException("Cannot configure the Director after it has been started");

    int index = 0;
    foreach (RoleConfiguration role in configuration.roles) {
      List<Actor> members = new();
      for (int i = 0; i < role.quantity; i++) members.Add(new Actor(index++, role));
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
    ComponentLog.singleton.Log($"Director stop requested");

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
  dark,
  working,
  underflow,
  completed,
  error,
}

public enum ActorPhase {
  ready,
  queue_up,
  on_queue,
  work,
  suspension,
  stop_at_cost,
  stop_at_completion,
  stop_now,
  exit,
}

}
}
