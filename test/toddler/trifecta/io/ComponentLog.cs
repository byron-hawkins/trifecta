/* author: Copilot */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ThreadId = System.Int32;

namespace trifecta.io;

public class ComponentLog : ITextLog
{
    public static ComponentLog singleton = new();

    private Lock _lock = new();
    private ITextLogSink _sink = null!;
    private Dictionary<ThreadId, string> _componentNames = null!;

    public void Configure(ITextLogSink sink)
    {
    }

    public void Log(string message)
    {
    }

    public void Flush()
    {
    }

    public LifespanToken Register(string componentName)
    {
        return null!;
    }
}

public class FileLog : ITextLogSink
{
    private LogContext _context;
    private string _basename;
    private StreamWriter _sink = null!;

    public FileLog(LogContext context, string basename = "trace")
    {
        _context = context;
        _basename = basename;
    }

    public void Log(string message)
    {
    }
}

public class LifespanToken : IDisposable
{
    public void Dispose()
    {
    }
}

public interface ITextLogSink
{
    void Log(string message);
}

public interface ITextLog
{
    void Log(string message);
}

public enum LogContext
{
    toddler,
    macarena,
    ensemble,
    simulation
}
