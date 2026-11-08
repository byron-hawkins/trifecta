/* stub: Copilot */
/* author: Byron */

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using ThreadId = System.Int32;

namespace trifecta {
namespace io {

public class ComponentLog : ITextLog {
  public static ComponentLog singleton = new();

  ComponentLog() { _sink = new ConsoleLog(); }

  public void Configure(ITextLogSink sink) {
    using (_lock.EnterScope()) { _sink = sink; }
  }

  public void Log(string message, [CallerFilePath] string path = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "") {
    string filename = Path.GetFileName(path);
    string component = _componentNames.GetValueOrDefault(Thread.CurrentThread.ManagedThreadId, "<unknown-component-id>");
    using (_lock.EnterScope()) { _sink.Log($"[{component}.{member} {filename}.{line}]: {message}"); }
  }

  public void Flush() {
    using (_lock.EnterScope()) { _sink.Flush(); }
  }

  public LifespanToken Register(string componentName) {
    using (_lock.EnterScope()) { _componentNames[Thread.CurrentThread.ManagedThreadId] = componentName; }
    return new LifespanToken();
  }

  public void Release() {
    using (_lock.EnterScope()) {
      string? component = _componentNames[Thread.CurrentThread.ManagedThreadId];
      if (component is string name) {
        Log($"ComponentLog releases component {name}");
        _componentNames.Remove(Thread.CurrentThread.ManagedThreadId);
      } else {
        throw new InvalidOperationException($"ComponetLog failed to identify thread {Thread.CurrentThread.ManagedThreadId} at thread exit");
      }
    }
  }

  private Lock _lock = new();
  private ITextLogSink _sink;
  private Dictionary<ThreadId, string> _componentNames = new();
}

public class ConsoleLog : ITextLogSink {
  public void Log(string message) { Console.WriteLine(message); }
  public void Flush() {}
}

public class FileLog : ITextLogSink {
  public FileLog(LogContext context, string basename = "trace") {
    _context = context;
    _basename = basename;

    string? envlogdir = Environment.GetEnvironmentVariable("TRIFECTA_LOG_DIR");
    if (envlogdir is string logdir) {
      string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd.HH-mm-ss.ffff", CultureInfo.InvariantCulture);
      Directory.CreateDirectory($"{logdir}/{_context}");
      _sink = new($"{logdir}/{_context}/{_basename}.{timestamp}.log");
    } else {
      throw new InvalidOperationException("Log directory must be specified in " + "environment variable TRIFECTA_LOG_DIR");
    }
  }

  public void Log(string message) { _sink.WriteLine(message); }

  public void Flush() { _sink.Flush(); }

  private LogContext _context;
  private string _basename;
  private StreamWriter _sink;
}

public class LifespanToken : IDisposable {
  public void Dispose() { ComponentLog.singleton.Release(); }
}

}
}
