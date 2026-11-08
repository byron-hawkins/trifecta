using System;
using System.IO;
using System.Linq;
using trifecta.io;

namespace trifecta.test.common;

public abstract class ComponentLogTestBase
{
    protected string OutputDirectory { get; private set; } = string.Empty;
    protected string LogBase { get; private set; } = string.Empty;

    public void setup(string logbase)
    {
        LogBase = logbase;
        OutputDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "log",
            LogContext.toddler.ToString());

        ComponentLog.singleton.Configure(new FileLog(LogContext.toddler, logbase));
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
