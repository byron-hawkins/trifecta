using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Threading;
using ThreadId = System.Int32;

namespace trifecta.io;

public class ComponentLog : ITextLog {
  public static ComponentLog singleton = new();

  ComponentLog() { _sink = new ConsoleLog(); }

  public void Configure(ITextLogSink sink) { _sink = sink; }

  public void Log(string message) {
    using (_lock.EnterScope()) { _sink.Log(message); }
  }

  public LifespanToken Register(string componentName) {
    _componentNames[Thread.CurrentThread.ManagedThreadId] = componentName;
    return new LifespanToken();
  }

  public void Release() { _componentNames.Remove(Thread.CurrentThread.ManagedThreadId); }

  private Lock _lock = new();
  private ITextLogSink _sink;
  private Dictionary<ThreadId, string> _componentNames = new();
}

public class ConsoleLog : ITextLogSink {
  public void Log(string message) { Console.WriteLine(message); }
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

  private LogContext _context;
  private string _basename;
  private StreamWriter _sink;
}

public class LifespanToken : IDisposable {
  public void Dispose() { ComponentLog.singleton.Release(); }
}
