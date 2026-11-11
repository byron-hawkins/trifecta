*author: Copilot*

```mermaid
classDiagram
namespace trifecta.resource {
  class ResourceInstance {
    IResourceDriver _driver
    +GetStatus() ResourceDriverStatus
  }
  class ResourceQueueSet {
    singleton
    Task _self
    object _idle
    System.Threading.Lock _multilock
    Dictionary~ResourceName, ResourceQueue~ _queues
    ResourceSet _resources
    +configure(ResourceSet resources) void
    +Start() void
    +Stop() void
    +Bump() void
    +MultiPush(IResourceConsumer consumer) void
  }
  class ResourceQueue {
    System.Threading.Lock _lock
    Queue~ResourceInstance~ _resources
    Queue~IResourceConsumer~ _consumers
    +Push(IResourceConsumer consumer) void
    +Push(ResourceInstance resource) void
  }
  class IResourceConsumer {
    <<interface>>
    +Scope() IEnumerable~ResourceName~
    +Grant(ResourceInstance resource) void
  }
}
ResourceQueueSet *-- ResourceQueue : _queues
ResourceQueue *-- ResourceInstance : _resources
ResourceQueue *-- IResourceConsumer : _consumers
```
