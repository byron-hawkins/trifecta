*author: Copilot*

```mermaid
classDiagram
namespace trifecta.io {
  class ComponentLog {
    singleton
    System.Threading.Lock _lock
    ITextLogSink _sink
    System.Dictionary~ThreadId, string~ _componentNames
    +Configure(ITextLogSink sink) void
    +Register(string componentName) LifespanToken
  }
  class ITextLog {
    <<interface>>
    +Log(string message) void
  }
  class ITextLogSink {
    <<interface>>
    +Log(string message) void
  }
  class FileLog {
    LogContext _context
    string _basename
    System.IO.StreamWriter _sink
    +FileLog(LogContext context, string basename)
  }
  class LogContext {
    <<enumeration>>
    toddler
    macarena
    ensemble
    simulation
  }
}
ComponentLog *-- ITextLogSink : _sink
ComponentLog ..|> ITextLog
FileLog *-- LogContext : _context
FileLog ..|> ITextLogSink
```
