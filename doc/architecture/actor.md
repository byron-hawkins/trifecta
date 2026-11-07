*Author: Byron*

*Subject: outline an event model for an autonomous stage executor*

`namespace trifecta.actor`

## StageSet

### Data Structure

- class StageSet
  - contains
    - <singleton>
    - Dictionary<IStageConfiguration.IdType, IStageConfiguration> Members

## Actor

To perform a sequence of Task comprising a stage for some duration 100ms <= tx <= 5s, while responding to
changes in conditions, an autonomous Actor will operate on its own threaed and coordinate events under a
single instance-specific lock. All actions will be trivial except when performing a Task that involves
significant continuous computation. Responsiveness to concurrent events during computation will depend on
granularity of the computation steps; for example, if a Task performs computation in 10ms loop iterations,
then events relevant to the Actor may go unnoticed for as long as 10ms. For a Task that consumes one or
more resources and does not perform computation, it is assumed that the Actor remains passively available
for event notification while each resource performs work on s separate driver thread.

When not performing computation, an Actor is either busy with a trivial event while holding its lock, or
waits on its lock until some notification occurs. Implementation can most likely use a simple Monitor,
which can be held privately within Actor. Events will arrive over interface callbacks, which Actor can
implement as synchronizing on its Monitor. Before initiating a wait phase, Actor will discover any state
changes on subscribed event sources; if found, no wait will occur and Actor will directly respond.

### Data Structure

- class Actor
  - capabilities
    - environment.IConditionAware
    - resource.IDriverAware
    - resource.IResourceConsumer
  - contains
    - stage.IStageConfiguration
    - ITaskSequence
    - ActorPhase
  - reads
    - environment.SensorInterpreter
    - resource.ResourceQueueSet
  - performs
    - void Start()
    - void Exit(ActorPhase mode, System.Action Finalize)



## Task

### Data Structure

- class Task
  - contains
    - ArrayList<ITaskAware>
    - collection of available resource.ResourceInstance
    - System.Threading.TimeSpan CancelGranularity
  - performs
    - TaskStatus GetStatus()
    - TaskStatus Execute()


## Director

Attempts to optimize productivity by regulating the number of concurrent actors.

### Data Structure

- class Director
  - capabilities
    - environment.IConditionAware
    - ICastManager
  - contains
    - <singleton>
    - Dictionary<Stage, ArrayList<Actor>> Cast
    - ICastOptimizer
  - reads
    - StageSet
    - environment.SensorInterpreter
    - resource.ResourceQueueSet
    - resource.ResourcePool
  - performs
    - void Conclude(bool nicely)



## Interfaces

- interface ITaskAware
  -

- interface ICastManager
  - void Interrupt(Actor, ActorPhase)
  - void Initiate(IStageConfiguration.IdType)

- interface ICastOptimizer

- interface IStageConfiguration
  - types
    - using TypeId = int
  - performs
    - TypeId Id()
    - System.Threading.TimeSpan OccupancyLimit()


## Enumerations

- TaskStatus
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
