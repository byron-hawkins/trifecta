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
    +EnableTrace(bool enabled) void
    +Trace(string message, string path, int line, string member) void
    +Log(string message, string path, int line, string member) void
    +Flush() void
    +Register(string componentName) LifespanToken
    +Release() void
  }
  class ITextLog {
    <<interface>>
    +Log(string message) void
  }
  class ITextLogSink {
    <<interface>>
    +Log(string message) void
    +Flush() void
  }
  class FileLog {
    LogContext _context
    string _basename
    System.IO.StreamWriter _sink
    +FileLog(LogContext context, string basename)
    +Log(string message) void
    +Flush() void
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
