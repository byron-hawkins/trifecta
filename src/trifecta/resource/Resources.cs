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

    _configuration = configuration;
  }

  public void Push(IResourceConsumer consumer) {
    using (_lock.EnterScope()) { _consumers.Enqueue(consumer); }
  }

  public void Push(ResourceInstance resource) {
    using (_lock.EnterScope()) {
      _resources.Enqueue(resource);

      // now bump the first consumer?
    }
  }

  private ResourceType _configuration;
  private Lock _lock = new();
  private Queue<ResourceInstance> _resources = new();
  private Queue<IResourceConsumer> _consumers = new();
}

public class ResourceQueueSet {
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
    // _multilock
    // foreach is consumer.Scope(): lock q, push
  }

  public void MultiRelease(List<ResourceInstance> release) {
    // foreach, lock q, push
  }

  private void run() {
    Monitor.Enter(_idle);
    try {
      while (true) {
        if (_stop) return;

        if (_queues.Count == 0) {
          ComponentLog.singleton.Log("ResourceQueueSet finds the queues empty");
        } else {
          ComponentLog.singleton.Log("ResourceQueueSet tries to allocate some resources on the queues");
        }

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
  ResourceInstance(ResourceType type, IResourceDriver driver) {
    _type = type;
    _driver = driver;
  }

  public ResourceDriverStatus GetStatus() { return _driver.GetStatus(); }

  public TimeSpan GetMinWork() { return _type.minwork; }
  public TimeSpan GetMaxWork() { return _type.maxwork; }

  private ResourceType _type;
  private IResourceDriver _driver;
}

public interface IResourceConsumer {
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
