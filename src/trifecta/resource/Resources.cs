/* author: Byron */
/* stub: Copilot */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using trifecta.io;
using trifecta.stage;

namespace trifecta {
namespace resource {

public class ResourceQueue {
  public ResourceQueue(ResourceType configuration) {
    ComponentLog.singleton.Log($"Constructing a ResourceQueue for resource {configuration.name}");

    _identity = $"ResourceQueue<{configuration.name}>";
    _configuration = configuration;
    for (int i = 0; i < configuration.quantity; i++) { _resources.Enqueue(new ResourceInstance(configuration)); }
  }

  public void Push(IResourceConsumer consumer) {
    ComponentLog.singleton.Log($"{_identity} receives one consumer");

    if (consumer.IsGhost()) return;

    using (_lock.EnterScope()) {
      if (_resources.Count > 0) {
        ComponentLog.singleton.Log($"{_identity} grants quick");

        ResourceInstance resource = _resources.Dequeue();
        consumer.Grant(resource);
      } else {
        ComponentLog.singleton.Log($"{_identity} enqueues the consumer");

        _consumers.Enqueue(consumer);
      }
      Dispatch(); /* check for more */
    }
  }

  public void Push(ResourceInstance resource) {
    ComponentLog.singleton.Log($"{_identity} receives one resource {resource.GetName}");

    bool retry;
    using (_lock.EnterScope()) {
      do {
        retry = false;
        if (_consumers.Count > 0) {
          ComponentLog.singleton.Log($"{_identity} ricochet-grant on resource {resource.GetName}");

          IResourceConsumer consumer = _consumers.Dequeue();
          if (consumer.IsGhost()) {
            retry = true;
            continue;
          }
          consumer.Grant(resource);
        } else {
          ComponentLog.singleton.Log($"{_identity} enqueues resource {resource.GetName}");

          _resources.Enqueue(resource);
        }
      } while (retry);
      Dispatch(); /* check for more */
    }
  }

  public void Dispatch() {
    using (_lock.EnterScope()) {
      while (_resources.Count > 0 && _consumers.Count > 0) {
        IResourceConsumer consumer = _consumers.Dequeue();
        if (consumer.IsGhost()) { continue; }

        ResourceInstance resource = _resources.Dequeue();

        ComponentLog.singleton.Log($"{_identity} dispatch-grant on resource {resource.GetName}");

        consumer.Grant(resource);  // TODO: possible option to allow on-thread refusal, in case of race on consumer state
      }
    }
  }

  public string Identity() { return _identity; }

  private string _identity;
  private ResourceType _configuration;
  private Lock _lock = new();
  private Queue<ResourceInstance> _resources = new();
  private Queue<IResourceConsumer> _consumers = new();
}

public interface IResourceQueueSet {
  public void MultiPush(IResourceConsumer consumer);
  public void MultiRelease(IEnumerable<ResourceInstance> release);
};

public class ResourceQueueSet : IResourceQueueSet {
  public static ResourceQueueSet singleton = new();

  public void configure(ResourceSet resources) {
    ComponentLog.singleton.Log("Request to configure the ResourceQueueSet");

    if (_started) throw new InvalidOperationException("Cannot configure the ResourceQueueSet after it has been started");

    _resources = resources;
    foreach (ResourceType t in _resources.elements) { _queues[t.name] = new ResourceQueue(t); }
  }

  public void Start() {
    ComponentLog.singleton.Log("ResourceQueueSet start requested");

    Monitor.Enter(_idle);
    try {
      if (_started) throw new InvalidOperationException("Cannot restart the ResourceQueueSet");
      if (_stop) throw new InvalidOperationException("Cannot start a ResourceQueueSet that has been stopped");

      _started = true;
      _self = Task.Run(() => { run(); });
    } finally { Monitor.Exit(_idle); }
  }

  public void Stop() {
    ComponentLog.singleton.Log("ResourceQueueSet stop requested");

    Task task;
    Monitor.Enter(_idle);
    try {
      if (!_started) throw new InvalidOperationException("Cannot stop a ResourceQueueSet that has not been started");
      if (_stop) throw new InvalidOperationException("Cannot stop a ResourceQueueSet that has been stopped");

      _stop = true;
      task = _self ?? throw new InvalidOperationException("Missing background task when attempting to stop the ResourceQueueSet");
      Monitor.Pulse(_idle);
    } finally { Monitor.Exit(_idle); }
    task.Wait();

    Monitor.Enter(_idle);
    try {
      _self = null;
    } finally { Monitor.Exit(_idle); }
  }

  public void Bump() {
    Monitor.Enter(_idle);
    try {
      if (!_started) throw new InvalidOperationException("Cannot bump a ResourceQueueSet that has not been started");
      if (_stop) throw new InvalidOperationException("Cannot bump a ResourceQueueSet that has been stopped");

      ComponentLog.singleton.Log("Bump the ResourceQueueSet");
      Monitor.Pulse(_idle);
    } finally { Monitor.Exit(_idle); }
  }

  public void Reset() {
    ComponentLog.singleton.Log("ResourceQueueSet reset requested");

    Monitor.Enter(_idle);
    try {
      if (_started && (!_stop || _self != null)) throw new InvalidOperationException("Cannot reset a ResourceQueueSet during showtime");

      _started = _stop = false;
      _resources = null;
      _queues.Clear();
    } finally { Monitor.Exit(_idle); }
  }

  public void MultiPush(IResourceConsumer consumer) {
    ComponentLog.singleton.Log($"ResourceQueueSet pushes multiple resource requests");

    using (_multilock.EnterScope()) {
      foreach (string resource in consumer.Scope()) { _queues[resource].Push(consumer); }
    }
    Bump();
  }

  public void MultiRelease(IEnumerable<ResourceInstance> release) {
    ComponentLog.singleton.Log($"ResourceQueueSet releases multiple resources");

    foreach (ResourceInstance resource in release) { _queues[resource.GetName()].Push(resource); }
  }

  private void run() {
    Monitor.Enter(_idle);
    try {
      while (true) {
        if (_stop) return;

        ComponentLog.singleton.Log($"ResourceQueueSet dispatches {_queues.Count} resource queues");
        foreach (ResourceQueue q in _queues.Values) q.Dispatch();

        ComponentLog.singleton.Log("ResourceQueueSet goes idle");
        Monitor.Wait(_idle);
      }
    } finally { Monitor.Exit(_idle); }
  }

  private bool _started = false;
  private bool _stop = false;
  private Task? _self = null;
  private readonly object _idle = new();
  private Lock _multilock = new();

  private ResourceSet? _resources;
  private Dictionary<ResourceName, ResourceQueue> _queues = new();
}

public class ResourceInstance {
  public ResourceInstance(ResourceType type, IResourceDriver driver = null) {
    _type = type;
    _driver = driver;
  }

  public string GetName() { return _type.name; }

  public ResourceDriverStatus GetStatus() { return _driver.GetStatus(); }

  public TimeSpan GetMinWork() { return _type.minwork; }
  public TimeSpan GetMaxWork() { return _type.maxwork; }

  private ResourceType _type;
  private IResourceDriver _driver;
}

public interface IResourceConsumer {
  bool IsGhost();
  IEnumerable<ResourceName> Scope();
  void Grant(ResourceInstance resource);
}

public enum ResourceDriverStatus {
  idle,
  working,
  error,
}

public interface IResourceDriver {
  ResourceDriverStatus GetStatus();
}

public interface IPassiveResourceDriver : IResourceDriver {
  ResourceDriverStatus Work(TimeSpan duration);
}

public class PassiveResourceDriver : IPassiveResourceDriver {
  PassiveResourceDriver(TimeSpan tickrate) { _tickrate = tickrate; }

  public ResourceDriverStatus GetStatus() { return _status; }

  public ResourceDriverStatus Work(TimeSpan duration) {
    // pause _tickrate until done, checking _status for change to error or stop
    return ResourceDriverStatus.working;
  }

  private ResourceDriverStatus _status = ResourceDriverStatus.idle;
  private TimeSpan _tickrate;
}

}
}
