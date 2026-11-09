*Author: Byron*

*Subject: outline an event model for an autonomous stage executor*

`namespace trifecta.actor`

## StageSet

### Data Structure

- class StageSet
  - contains
    - <singleton>
    - Dictionary<IStageConfiguration.IdType, IStageConfiguration> Members

<!--- @context struct Actor --->
## Actor

To perform a sequence of Scene comprising a stage for some duration 100ms <= tx <= 5s, while responding to
changes in conditions, an autonomous Actor will operate on its own threaed and coordinate events under a
single instance-specific lock. All actions will be trivial except when performing a Scene that involves
significant continuous computation. Responsiveness to concurrent events during computation will depend on
granularity of the computation steps; for example, if a Scene performs computation in 10ms loop iterations,
then events relevant to the Actor may go unnoticed for as long as 10ms. For a Scene that consumes one or
more resources and does not perform computation, it is assumed that the Actor remains passively available
for event notification while each resource performs work on s separate driver thread.

When not performing computation, an Actor is either busy with a trivial event while holding its lock, or
waits on its lock until some notification occurs. Implementation can most likely use a simple Monitor,
which can be held privately within Actor. Events will arrive over interface callbacks, which Actor can
implement as synchronizing on its Monitor. Before initiating a wait phase, Actor will discover any state
changes on subscribed event sources; if found, no wait will occur and Actor will directly respond.

### Data Structure

<!--- @type --->
- class Actor : environment.IConditionAware, resource.IResourceConsumer
  - composition
    - Actor(ActorConfiguration configuration)
    - ActorConfiguration _configuration
    - Scene _onstage
    - ActorPhase _phase
  - operations
    - void Start()
    - void Stop(ActorPhase mode, System.Action finalize)

<!--- @context class Scene --->
## Scene

### Data Structure

<!--- @type --->
- class Scene
  - composition
    - SceneConfiguration _configuration
    - List<resource.ResourceInstance> _resources;
    - SceneStatus _status
  - operations
    - Scene(SceneConfiguration configuration)
    - SceneStatus GetStatus()
    - SceneStatus Execute()


## Director

Attempts to optimize productivity by regulating the number of concurrent actors.

### Data Structure

- class Director : environment.IConditionAware, ICastManager
  - composition
    - [singleton]
    - Dictionary<ActorTypeId, List<Actor>> _cast
    - CastOptimizer _optimizer
  - operations
    - Director(DirectorConfiguration configuration)
    - void Start()
    - void Stop(bool nicely)

<!--- @context class CastOptimizer --->
## CastOptimizer

<!--- @type --->
- class CastOptimizer
  - composition
    - TransitionMode _mode
    - ICastManager _manager
  - operations
    - CastOptimizer(TransitionMode mode, ICastManager manager)


## Interfaces

- interface ICastManager
  - void Interrupt(Actor, ActorPhase)
  - void Initiate(IStageConfiguration.IdType)

- interface ICastOptimizer


## Enumerations

- SceneStatus
  - pending
  - running
  - completed
  - error

- ActorPhase
  - ready
  - queue
  - work
  - suspension
  - exit_at_cost
  - exit_at_completion
  - exit_now
