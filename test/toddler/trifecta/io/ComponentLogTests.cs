using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace trifecta.io.Tests;

public sealed class ComponentLogTests
{
    [Fact]
    public async Task WritesSingleMessageToLog()
    {
        var basename = $"ComponentLogTests-{Guid.NewGuid():N}";
        var outputDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "log",
            LogContext.toddler.ToString());
        var log = new FileLog(LogContext.toddler, basename);

        ComponentLog.singleton.Configure(log);
        ComponentLog.singleton.Log("Hello Log");

        var deadline = DateTime.UtcNow.AddSeconds(3);
        string[] logFiles = [];
        while (DateTime.UtcNow < deadline)
        {
            logFiles = Directory.Exists(outputDirectory)
                ? Directory.GetFiles(outputDirectory, $"{basename}.*.log")
                : [];

            if (logFiles.Length > 0)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.True(
            logFiles.Length > 0,
            $"Expected a log file named '{basename}.<timestamp>.log' in '{outputDirectory}'.");

        var contents = await File.ReadAllTextAsync(logFiles.Single());
        Assert.Contains("Hello Log", contents, StringComparison.Ordinal);
    }
}
