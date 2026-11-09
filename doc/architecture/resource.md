*Author: Byron*

*Subject: outline an event model for an application resource*

<!--- @context namespace trifecta.resource --->
`namespace trifecta.resource`

<!--- @context class ResourceQueue --->
## ResourceQueue

Configurable set of resources potentially available to the machine.

### Data Structure

<!--- @type --->
- class ResourceQueue
  - composition
    - System.Threading.Lock _lock
    - Queue<ResourceInstance> _resources
    - Queue<IResourceConsumer> _consumers
  - operations
    - void Push(IResourceConsumer consumer)
    - void Push(ResourceInstance resource)

<!--- @context class ResourceQueueSet --->
## ResourceQueueSet

Since multiple Stage types may run concurrently, and since each Stage may require multiple resources, a greedy
resource acquisition policy can fail with deadlock. Instead, a single monitor RQS.M can guard the entire set of
resource queues RQS, allowing a consumer to atomically push back on the queue of each needed resource. Mutual
waiting is avoided because it only occurs where a group of consumers having overlapping resource needs has been
enqeueued with differing precedence on at least two queues of RQS.

### Data Structure

<!--- @type --->
- class ResourceQueueSet
  - composition
    - [singleton]
    - Task _self
    - readonly object _idle
    - System.Threading.Lock _multilock
    - Dictionary<ResourceName, ResourceQueue> _queues
  - operations
    - void Start()
    - void Stop()
    - void MultiPush(IResourceConsumer consumer)

<!--- @context class ResourceInstance --->
## ResourceInstance

To avoid limitations on the kinds of resources that can be incorporated into a Stage, a resource interface
needs to provide for both passive and active usage models. Active usage occupies the calling thread for the
duration of usage, while passive usage can be trivially started, permitting the caller to perform other
operations concurrently.

<!--- @type --->
- class ResourceInstance
  - composition
    - IResourceDriver _driver
    - ResourceDriverStatus _status
  - operations
    - ResourceDriverStatus GetStatus()

## Interfaces

<!--- @type --->
- interface IResourceDriver
  - operations
    - ResourceDriverStatus GetStatus()

<!--- @type --->
- interface IPassiveResourceDriver
  - operations
    - ResourceDriverStatus Start()
    - ResourceDriverStatus Stop()

<!--- @type --->
- interface IActiveResourceDriver
  - operations
    - ResourceDriverStatus Work()

<!--- @type --->
- interface IResourceConsumer
  - operations
    - IEnumerable<ResourceName> Scope()
    - void Grant(ResourceInstance resource)

## Enumerations

<!--- @type --->
- enum ResourceMode
  - composition
    - active
    - passive

<!--- @type --->
- enum ResourceDriverStatus
  - composition
    - idle
    - working
    - error

