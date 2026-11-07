*Author: Byron*

*Subject: outline an event model for an autonomous stage executor*

`namespace trifecta.actor`

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




## Task

### Data Structure

- class Task
  - contains
    - registry of ITaskAware
    - collection of available resource.ResourceInstance
  - performs
    - TaskStatus GetStatus()
    - TaskStatus Execute()


## Interfaces

- interface ITaskAware
  -


## Enumerations

- TaskStatus
  - pending
  - running
  - completed
  - error
