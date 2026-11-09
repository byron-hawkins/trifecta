/* stub: Copilot */
using System;
using System.Collections.Generic;
using trifecta.environment;
using trifecta.resource;

namespace trifecta {
namespace stage {

public class Actor : IConditionAware, IResourceConsumer {
  public Actor(RoleConfiguration configuration) {
    _configuration = configuration;
  }

  public void Start() {}

  public void Stop(ActorPhase mode, Action finalize) {}

  public void ThresholdCross(Temperature t) {}

  public void ThresholdCross(Pressure p) {}

  public IEnumerable<ResourceName> Scope() { return []; }

  public void Grant(ResourceInstance resource) {}

  private RoleConfiguration _configuration;
  private ActorPhase _phase;
  private Scene? _onstage;
  private List<Scene> _scenes = [];
}

public class Scene {
  public Scene(SceneConfiguration configuration) {
    _configuration = configuration;
  }

  public SceneStatus GetStatus() { return default; }

  public SceneStatus Execute() { return default; }

  private SceneConfiguration _configuration;
  private List<ResourceInstance> _resources = [];
  private SceneStatus _status;
}

public class Director : IConditionAware, ICastManager {
  public static Director singleton = new();

  public void configure(DirectorConfiguration configuration) {}

  public void Start() {}

  public void Stop(bool nicely) {}

  public void ThresholdCross(Temperature t) {}

  public void ThresholdCross(Pressure p) {}

  public void Interrupt(Actor actor, ActorPhase exitStrategy) {}

  public void Initiate(RoleId role) {}

  private Dictionary<RoleId, List<Actor>> _cast = [];
  private CastOptimizer _optimizer = null!;
  private DirectorConfiguration _configuration = null!;
}

public class CastOptimizer {
  public CastOptimizer(DirectorConfiguration configuration,
                       ICastManager manager) {
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
  exit_at_cost,
  exit_at_completion,
  exit_now,
}

}
}
