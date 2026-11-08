/* author: Copilot */
using System;
using System.IO;
using System.Linq;
using trifecta.io;
using Xunit;

namespace trifecta.test.common;

[CollectionDefinition("ComponentLog tests", DisableParallelization = true)]
public sealed class ComponentLogTestCollection
{
}

public abstract class ComponentLogTestBase
{
    protected string OutputDirectory { get; private set; } = string.Empty;
    protected string LogBase { get; private set; } = string.Empty;

    public void setup(string logbase)
    {
        var logDirectory = Environment.GetEnvironmentVariable("TRIFECTA_LOG_DIR");
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            throw new InvalidOperationException(
                "Set TRIFECTA_LOG_DIR to the desired top-level log directory.");
        }

        LogBase = logbase;
        OutputDirectory = Path.Combine(logDirectory, LogContext.macarena.ToString());
        ComponentLog.singleton.Configure(new FileLog(LogContext.macarena, logbase));
    }

    public void finish()
    {
        ComponentLog.singleton.Flush();
    }

    protected string[] FindLogFiles()
    {
        return Directory.Exists(OutputDirectory)
            ? Directory.GetFiles(OutputDirectory, $"{LogBase}.*.log")
            : [];
    }

    protected string ReadLog()
    {
        return File.ReadAllText(FindLogFiles().Single());
    }
}
