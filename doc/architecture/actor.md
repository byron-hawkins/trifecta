*Author: Byron*

*Subject: outline an event model for an autonomous stage executor*

<!--- @context namespace trifecta.stage --->
`namespace trifecta.stage`

<!--- @context class Actor --->
## Actor

To perform a sequence of Scene comprising a stage for some duration 100ms <= tx <= 5s, while responding to
changes in conditions, an autonomous Actor will operate on its own thread and coordinate events under a
single instance-specific lock. All actions will be trivial except when performing a Scene that involves
significant continuous computation. Responsiveness to concurrent events during computation will depend on
granularity of the computation steps; for example, if a Scene performs computation in 10ms loop iterations,
then events relevant to the Actor may go unnoticed for as long as 10ms. For a Scene that consumes one or
more resources and does not perform computation, it is assumed that the Actor remains passively available
for event notification while each resource performs work on a separate driver thread.

When not performing computation, an Actor is either busy with a trivial event while holding its lock, or
waits on its lock until some notification occurs. Implementation can most likely use a simple Monitor,
which can be held privately within Actor. Events will arrive over interface callbacks, which Actor can
implement as synchronizing on its Monitor. Before initiating a wait phase, Actor will discover any state
changes on subscribed event sources; if found, no wait will occur and Actor will directly respond.

### Data Structure

<!--- @type --->
- class Actor : environment.IConditionAware
  - composition
    - RoleConfiguration _configuration
    - ActorPhase _phase
    - Scene? _onstage
    - List<Scene> _scenes
  - operations
    - Actor(int index, RoleConfiguration configuration)
    - Actor(int index, RoleConfiguration configuration, resource.IResourceQueueSet qs)
    - bool IsRunning()
    - void Start()
    - void Stop(ActorPhase mode, System.Action finalize)
    - void ThresholdCross(Temperature t)
    - void ThresholdCross(Pressure p)
    - IEnumerable<ResourceName> Scope()
    - void Grant(resource.ResourceInstance resource)
    - void Remit(List<resource.ResourceInstance> free)
    - bool IsGhost(Appearance appearance)
    - string Identity()

<!--- @context class Appearance --->
## Appearance

Each time an Actor queues for resources it presents a fresh Appearance, which is what the resource queues hold
and to which they `Grant()` the resource.. When an Actor leaves a queue (for example, on stop) it simply
discards its Appearance; a queue that later finds a stale Appearance recognizes it as a ghost and skips it,
precluding the need to remove abandoned resource requests from the queue.

### Data Structure

<!--- @type --->
- class Appearance : resource.IResourceConsumer
  - composition
    - Actor _actor
  - operations
    - Appearance(Actor actor)
    - bool IsGhost()
    - IEnumerable<ResourceName> Scope()
    - void Grant(resource.ResourceInstance resource)
    - string Identity()

<!--- @context class Scene --->
## Scene

### Data Structure

<!--- @type --->
- class Scene
  - composition
    - SceneConfiguration _configuration
    - List<resource.ResourceInstance> _resources
    - SceneStatus _status
  - operations
    - Scene(int index, SceneConfiguration configuration)
    - Scene(int index, SceneConfiguration configuration, resource.IResourceQueueSet qs)
    - SceneStatus GetStatus()
    - bool IsContinuation()
    - void Begin()
    - SceneStatus Execute()
    - bool Grant(resource.ResourceInstance resource)
    - void Reset(Actor actor)
    - void Remit(Actor actor)
    - IEnumerable<ResourceName> Scope()
    - bool IsLocal()
    - TimeSpan GetStepDuration()
    - string Identity()

<!--- @context class Director --->
## Director

Attempts to optimize productivity by regulating the number of concurrent actors.

### Data Structure

<!--- @type --->
- class Director : environment.IConditionAware, ICastManager
  - composition
    - [singleton]
    - Dictionary<RoleId, List<Actor>> _cast
    - CastOptimizer _optimizer
    - DirectorConfiguration _configuration
  - operations
    - void configure(DirectorConfiguration configuration)
    - void Start()
    - void Stop(bool nicely)
    - void Reset()
    - void ThresholdCross(Temperature t)
    - void ThresholdCross(Pressure p)
    - void Interrupt(RoleId role, ActorPhase exitStrategy)
    - void Initiate(RoleId role)

<!--- @context class CastOptimizer --->
## CastOptimizer

<!--- @type --->
- class CastOptimizer
  - composition
    - ICastManager _manager
    - DirectorConfiguration _configuration
  - operations
    - CastOptimizer(DirectorConfiguration configuration, ICastManager manager)
    - void Optimize()

## Interfaces

<!--- @type --->
- interface ICastManager
  - operations
    - void Interrupt(Actor actor, ActorPhase exitStrategy)
    - void Initiate(RoleId role)

## Enumerations

<!--- @type --->
- enum SceneStatus
  - composition
    - dark
    - working
    - underflow
    - completed
    - error

<!--- @type --->
- enum ActorPhase
  - composition
    - ready
    - queue_up
    - on_queue
    - work
    - suspension
    - stop_at_cost
    - stop_at_completion
    - stop_now
    - exit

## Diagrams

<!--- @diagram [figures/actor.md] [class Actor, class Appearance, class Scene, class Director, class CastOptimizer, enum ActorPhase, enum SceneStatus] --->
