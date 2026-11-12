*author: Copilot*

```mermaid
classDiagram
namespace trifecta.stage {
  class Actor {
    RoleConfiguration _configuration
    ActorPhase _phase
    Scene? _onstage
    List~Scene~ _scenes
    +Actor(int index, RoleConfiguration configuration)
    +Actor(int index, RoleConfiguration configuration, resource.IResourceQueueSet qs)
    +IsRunning() bool
    +Start() void
    +Stop(ActorPhase mode, System.Action finalize) void
    +ThresholdCross(Temperature t) void
    +ThresholdCross(Pressure p) void
    +Scope() IEnumerable~ResourceName~
    +Grant(resource.ResourceInstance resource) void
    +Remit(List~resource.ResourceInstance~ free) void
    +IsGhost(Appearance appearance) bool
    +Identity() string
  }
  class Appearance {
    Actor _actor
    +Appearance(Actor actor)
    +IsGhost() bool
    +Scope() IEnumerable~ResourceName~
    +Grant(resource.ResourceInstance resource) void
    +Identity() string
  }
  class Scene {
    SceneConfiguration _configuration
    List~resource.ResourceInstance~ _resources
    SceneStatus _status
    +Scene(int index, SceneConfiguration configuration)
    +Scene(int index, SceneConfiguration configuration, resource.IResourceQueueSet qs)
    +GetStatus() SceneStatus
    +IsContinuation() bool
    +Begin() void
    +Execute() SceneStatus
    +Grant(resource.ResourceInstance resource) bool
    +Reset(Actor actor) void
    +Remit(Actor actor) void
    +Scope() IEnumerable~ResourceName~
    +IsLocal() bool
    +GetStepDuration() TimeSpan
    +Identity() string
  }
  class Director {
    singleton
    Dictionary~RoleId, List~Actor~~ _cast
    CastOptimizer _optimizer
    DirectorConfiguration _configuration
    +configure(DirectorConfiguration configuration) void
    +Start() void
    +Stop(bool nicely) void
    +Reset() void
    +ThresholdCross(Temperature t) void
    +ThresholdCross(Pressure p) void
    +Interrupt(RoleId role, ActorPhase exitStrategy) void
    +Initiate(RoleId role) void
  }
  class CastOptimizer {
    ICastManager _manager
    DirectorConfiguration _configuration
    +CastOptimizer(DirectorConfiguration configuration, ICastManager manager)
    +Optimize() void
  }
  class ActorPhase {
    <<enumeration>>
    ready
    queue_up
    on_queue
    work
    suspension
    stop_at_cost
    stop_at_completion
    stop_now
    exit
  }
  class SceneStatus {
    <<enumeration>>
    dark
    working
    underflow
    completed
    error
  }
}
Actor *-- ActorPhase : _phase
Actor *-- Scene : _onstage
Actor *-- Scene : _scenes
Appearance *-- Actor : _actor
Scene *-- SceneStatus : _status
Director *-- Actor : _cast
Director *-- CastOptimizer : _optimizer
```
