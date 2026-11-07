*Author: Byron*

*Subject: outline an event model for an application resource*

`namespace trifecta.resource`

## Resource Type

### Data Structure

- class ResourceType
  - UsageMode GetUsageMode()

## ResourceInstance

To avoid limitations on the kinds of resources that can be incorporated into a Stage, a resource interface
needs to provide for both passive and active usage models. Active usage occupies the calling thread for the
duration of usage, while passive usage can be trivially started, permitting the caller to perform other
operations concurrently.

- class ResourceInstance
  - contains
    - IResourceDriver

## ResourceQueueSet

Since multiple Stage types may run concurrently, and since each Stage may require multiple resources, a greedy
resource acquisition policy can fail with deadlock. Instead, a single monitor RQS.M can guard the entire set of
resource queues RQS, allowing a consumer to atomically push back on the queue of each needed resource. Mutual
waiting is avoided because it only occurs where a group of consumers having overlapping resource needs has been
enqeueued with differing precedence on at least two queues of RQS.

## Interfaces

- interface IResourceDriver
  - ResourceDriverStatus GetStatus()

- interface IPassiveResourceDriver
  - ResourceDriverStatus Start()
  - ResourceDriverStatus Stop()

- interface IActiveResourceDriver
  - ResourceDriverStatus Work()

## Enumerations

- UsageMode
  - active
  - passive

- ResourceDriverStatus
  - idle
  - working
  - error
