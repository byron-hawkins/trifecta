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

public class Actor : IConditionAware {
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

  public bool IsRunning() {
    Monitor.Enter(_idle);
    try {
      return _phase != ActorPhase.exit;
    } finally { Monitor.Exit(_idle); }
  }

  public virtual void Start(bool ignorable = false) {
    ComponentLog.singleton.Log($"{_identity} start requested");

    Monitor.Enter(_idle);
    try {
      if (_phase == ActorPhase.exit) {
        _phase = ActorPhase.ready;
      } else if (_phase != ActorPhase.ready) {
        if (ignorable) return;
        throw new InvalidOperationException($"Cannot start {_identity} from phase {_phase}");
      }
      if (_onstage != null) throw new InvalidOperationException($"Cannot start {_identity} with a scene on stage (phase {_phase})");

      _appearance = new(this);
      _workflow = 0;  // ...should be already

      _self = Task.Run(() => { run(); });
    } finally { Monitor.Exit(_idle); }
  }

  public virtual void Stop(ActorPhase mode, Action? finalize = null, bool ignorable = false) {
    ComponentLog.singleton.Log($"{_identity} stop requested");

    try {
      bool stop_now = (mode == ActorPhase.stop_now);
      Task task;
      Monitor.Enter(_idle);
      try {
        if (ignorable && _phase == ActorPhase.exit) return;

        task = _self ?? throw new InvalidOperationException($"Missing background task when attempting to stop {_identity} in phase {_phase}");
        _phase = mode;
        Monitor.Pulse(_idle);
      } finally { Monitor.Exit(_idle); }
      if (stop_now) {
        task.Wait();

        Monitor.Enter(_idle);
        try {
          _self = null;
        } finally { Monitor.Exit(_idle); }
      }
    } finally {
      if (finalize is Action finalizer) finalizer();
    }
  }

  /* reserved for stop_at_cost */
  public void ThresholdCross(Temperature t) {}

  /* reserved for stop_at_cost */
  public void ThresholdCross(Pressure p) {}

  public IEnumerable<ResourceName> Scope() {
    Monitor.Enter(_idle);
    try {
      if (_onstage is Scene s) return s.Scope();
      else
        return [];
    } finally { Monitor.Exit(_idle); }
  }

  public void Grant(ResourceInstance resource) {
    Monitor.Enter(_idle);
    try {
      if (_onstage == null) throw new InvalidOperationException($"Resource granted on {_identity} with no scene on stage (phase {_phase})");

      ComponentLog.singleton.Log($"{_identity} receives resource {resource.GetName()}");

      if (_onstage.Grant(resource)) {
        switch (_phase) {
          case ActorPhase.queue_up:
          case ActorPhase.on_queue:
            _phase = ActorPhase.work;
            Monitor.Pulse(_idle);
            break;
        }
      }
    } finally { Monitor.Exit(_idle); }
  }

  private void run() {
    using (ComponentLog.singleton.Register(_identity)) {
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
              continue;
            case ActorPhase.queue_up:
              Monitor.Exit(_idle); /* allow for potential immediate Grant() */
              try {
                _qs.MultiPush(_appearance);
              } finally { Monitor.Enter(_idle); }                              /* resume lockout for routing */
              if (_phase == ActorPhase.queue_up) _phase = ActorPhase.on_queue; /* not granted yet */
              continue;
            case ActorPhase.on_queue: break;
            case ActorPhase.work:
            case ActorPhase.stop_at_completion:
              work();

              if (_onstage.GetStatus() == SceneStatus.completed) {
                if (_onstage.IsContinuation()) {
                  if (_onstage.IsLocal()) throw new InvalidOperationException($"{_identity} local work on {_onstage.Identity()} was truncated");
                  _onstage.Remit(this);
                  _phase = ActorPhase.queue_up;
                } else {
                  _onstage.Reset(this);
                  if (++_workflow == _scenes.Count) {
                    reset();
                  } else {
                    _onstage = _scenes[_workflow];
                    if (!_onstage.IsLocal()) _phase = ActorPhase.queue_up;
                  }
                }
              }
              continue;
            case ActorPhase.suspension:
            case ActorPhase.stop_at_cost: throw new NotImplementedException($"ActorPhase.{_phase}");
            case ActorPhase.stop_now:
            case ActorPhase.exit:
              _appearance = null;
              if (_onstage is Scene abort) { abort.Reset(this); }
              reset(ActorPhase.exit);  // TODO: high-risk strategy for MultiRelease, but can it be done cleanly under iterative control?
              return;
          }

          ComponentLog.singleton.Log($"Director goes idle");
          Monitor.Wait(_idle);
        }
      } finally { Monitor.Exit(_idle); }
    }
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
              default: return;  // remit
            }
          } finally { Monitor.Exit(_idle); } /* allow interruption */
        }
      } finally { Monitor.Enter(_idle); } /* resume lockout for routing */
    } finally {
      if (!loggedOff) worklog("stop");
    }
  }

  public void Remit(List<ResourceInstance> free) {
    Monitor.Exit(_idle); /* allow interruption */
    try {
      _appearance = new(this);
      _qs.MultiRelease(free);
    } finally { Monitor.Enter(_idle); } /* resume lockout for routing */
  }

  public bool IsGhost(Appearance appearance) {
    Monitor.Enter(_idle);
    try {
      return appearance != _appearance;
    } finally { Monitor.Exit(_idle); }
  }

  public string Identity() { return _identity; }

  private void worklog(string mode) { ComponentLog.singleton.Log($"{_identity} [{_onstage.Identity()}] [work: {mode}]"); }

  private void reset(ActorPhase phase = ActorPhase.ready) {
    _workflow = 0;
    _phase = phase;
    _onstage = null;
  }

  private IResourceQueueSet _qs;
  private int _index;
  private string _identity;
  private RoleConfiguration _configuration;

  private object _idle = new();
  private Task? _self = null;
  private int _workflow;
  private ActorPhase _phase = ActorPhase.exit;
  private Scene? _onstage;
  private Appearance? _appearance = null;
  private List<Scene> _scenes = [];
}

public class Appearance : IResourceConsumer {
  public Appearance(Actor actor) { _actor = actor; }

  public bool IsGhost() { return _actor.IsGhost(this); }

  public IEnumerable<ResourceName> Scope() { return _actor.Scope(); }

  public void Grant(ResourceInstance resource) { _actor.Grant(resource); }

  private Actor _actor;
}

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

  public bool IsContinuation() { return _period.total > TimeSpan.Zero; }

  public void Begin() {
    switch (_status) {
      case SceneStatus.dark:
      case SceneStatus.completed: break;
      default: throw new InvalidOperationException($"Cannot begin work on a scene with status {_status}");
    }

    apply(SceneStatus.working);

    if (_period.total > TimeSpan.Zero) {
      _period.remaining = _period.total;
    } else {
      _period.remaining = _period.total = _configuration.duration;
    }
    foreach (ResourceInstance resource in _resources) {
      if (resource.GetMinWork() > _period.remaining) {
        _period.underflow += (resource.GetMinWork() - _period.remaining);
      } else if (_period.remaining > resource.GetMaxWork()) {
        _period.remaining = resource.GetMaxWork();
      }
    }

    ComponentLog.singleton.Log(
        $"{Identity()} exits Begin() with _period.remaining {_period.remaining.TotalMilliseconds} and _period.underflow {_period.underflow.TotalMilliseconds}");
  }

  public SceneStatus Execute(TimeSpan duration) {
    switch (_status) {
      case SceneStatus.working:
      case SceneStatus.underflow: break;
      default: throw new InvalidOperationException($"Cannot perform work on a scene with status {_status}");
    }

    Thread.Sleep(duration); /* fake work */

    if (_period.remaining == TimeSpan.Zero) {
      if (_period.underflow == TimeSpan.Zero) throw new InvalidOperationException("Scene cannot execute for 0ms");
      _period.underflow = TimeSpan.Zero;
      return apply(SceneStatus.completed);
    }

    _period.remaining -= duration;
    _period.total -= duration;
    if (_period.remaining == TimeSpan.Zero) {
      if (_period.underflow == TimeSpan.Zero) return apply(SceneStatus.completed);
      else
        return apply(SceneStatus.underflow);
    }
    return apply(SceneStatus.working);
  }

  public bool Grant(ResourceInstance resource) {
    _resources.Add(resource);  // TODO: validate unique, validate this resource is required
    return (_resources.Count == _configuration.resources.Count);
  }

  public void Reset(Actor actor) {
    _status = SceneStatus.dark;
    _period.Reset();
    Remit(actor);
  }

  public void Remit(Actor actor) {
    if (_resources.Count > 0) {
      ComponentLog.singleton.Log($"{actor.Identity()} {Identity()} releases {_resources.Count} resources");

      List<ResourceInstance> swap = _resources;
      _resources = new();
      actor.Remit(swap);
    }
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
    public TimeSpan total;

    public void Reset() { underflow = remaining = total = TimeSpan.Zero; }
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

  public void configure(DirectorConfiguration configuration, Dictionary<RoleId, List<Actor>>? precast = null) {
    ComponentLog.singleton.Log($"Configuring the Director with transition mode {configuration.mode} and {configuration.roles.Count} Actor roles");

    if (_started) throw new InvalidOperationException("Cannot configure the Director after it has been started");

    if (precast is Dictionary<RoleId, List<Actor>> pre) {
      _cast = pre;
    } else {
      int index = 0;
      foreach (RoleConfiguration role in configuration.roles) {
        List<Actor> members = new();
        for (int i = 0; i < role.quantity; i++) members.Add(new Actor(index++, role));
        _cast[role.id] = members;
      }
    }

    _configuration = configuration;
    _optimizer = new(configuration, this);
    SensorInterpreter.singleton.Register(this);
  }

  public void Start() {
    ComponentLog.singleton.Log("Director start requested");

    Monitor.Enter(_idle);
    try {
      if (_started) throw new InvalidOperationException("Cannot restart the Director");
      if (_stop) throw new InvalidOperationException("Cannot start a Director that has been stopped");

      _started = true;
      _optimize = true;
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
      foreach (List<Actor> role in _cast.Values) {
        foreach (Actor a in role) { a.Stop(ActorPhase.exit, null, true); }
      }

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

  public void ThresholdCross(Temperature t) {
    ComponentLog.singleton.Log($"Temperature has crossed a threshold to {t}: delegating to the cast optimizer");

    Monitor.Enter(_idle);
    try {
      _optimize = true;
      Monitor.Pulse(_idle);
    } finally { Monitor.Exit(_idle); }
  }

  public void ThresholdCross(Pressure p) {
    ComponentLog.singleton.Log($"Pressure has crossed a threshold to {p}: delegating to the cast optimizer");

    Monitor.Enter(_idle);
    try {
      _optimize = true;
      Monitor.Pulse(_idle);
    } finally { Monitor.Exit(_idle); }
  }

  public void Interrupt(RoleId role, ActorPhase exitStrategy) {
    foreach (Actor interrupt in _cast[role]) interrupt.Stop(exitStrategy, null, true);
  }

  public void Initiate(RoleId role) {
    foreach (Actor initiate in _cast[role]) initiate.Start(true);
  }

  private void run() {
    using (ComponentLog.singleton.Register("Director")) {
      Monitor.Enter(_idle);
      try {
        while (true) {
          if (_stop) return;

          if (_optimize) {
            Temperature t;
            Pressure p;

            Monitor.Exit(_idle);
            try {
              t = SensorInterpreter.singleton.GetTemperature();
              p = SensorInterpreter.singleton.GetPressure();
            } finally { Monitor.Enter(_idle); }

            _optimizer.Optimize(t, p);
            _optimize = false;
          }

          ComponentLog.singleton.Log("Director goes idle");
          Monitor.Wait(_idle);
        }
      } finally { Monitor.Exit(_idle); }
    }
  }

  private bool _started = false;
  private bool _stop = false;
  private bool _optimize = false;
  private Task? _self = null;
  private object _idle = new();

  private Dictionary<RoleId, List<Actor>> _cast = [];
  private CastOptimizer? _optimizer = null;
  private DirectorConfiguration _configuration = null!;
}

public class CastOptimizer {
  public CastOptimizer(DirectorConfiguration configuration, ICastManager manager) {
    _configuration = configuration;
    _manager = manager;
  }

  public void Optimize(Temperature t, Pressure p) {
    ComponentLog.singleton.Log($"Now optimizing the cast for temperature {t} and pressure {p}");

    foreach (RoleConfiguration role in _configuration.roles) {
      if (t > role.tmin && p < role.pmax) {
        _manager.Initiate(role.id);
      } else {
        if (_configuration.mode != TransitionMode.quickstop) throw new NotImplementedException($"TransitionMode {_configuration.mode}");

        _manager.Interrupt(role.id, ActorPhase.stop_now);
      }
    }
  }

  private ICastManager _manager;
  private DirectorConfiguration _configuration;
}

public interface ICastManager {
  void Interrupt(RoleId role, ActorPhase exitStrategy);
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
