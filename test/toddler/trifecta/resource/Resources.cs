/* stub: Copilot */
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace trifecta {
namespace resource {

public class ResourceQueue {
  public void Push(IResourceConsumer consumer) {}

  public void Push(ResourceInstance resource) {}

  private Lock _lock = new();
  private Queue<ResourceInstance> _resources = new();
  private Queue<IResourceConsumer> _consumers = new();
}

public class ResourceQueueSet {
  public static ResourceQueueSet singleton = new();

  public void Start() {}

  public void Stop() {}

  public void Reset() {}

  public void Bump() {}

  public void MultiPush(IResourceConsumer consumer) {}

  private Task _self = null!;
  private readonly object _idle = new();
  private Lock _multilock = new();
  private Dictionary<ResourceName, ResourceQueue> _queues = new();
}

public class ResourceInstance {
  public ResourceDriverStatus GetStatus() { return default; }

  private IResourceDriver _driver = null!;
  private ResourceDriverStatus _status;
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

}
}
