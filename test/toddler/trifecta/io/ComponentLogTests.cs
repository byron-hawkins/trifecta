using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using trifecta.io;
using trifecta.test.common;
using Xunit;

namespace trifecta.test.io;

public sealed class ComponentLogTests : ComponentLogTestBase
{
    [Fact]
    public async Task WritesSingleMessageToLog()
    {
        var basename = $"ComponentLogTests-{Guid.NewGuid():N}";
        setup(basename);
        ComponentLog.singleton.Log("Hello Log");
        finish();

        var deadline = DateTime.UtcNow.AddSeconds(3);
        string[] logFiles = [];
        while (DateTime.UtcNow < deadline)
        {
            logFiles = FindLogFiles();

            if (logFiles.Length > 0)
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.True(
            logFiles.Length > 0,
            $"Expected a log file named '{basename}.<timestamp>.log' in '{OutputDirectory}'.");

        var contents = await File.ReadAllTextAsync(logFiles.Single());
        Assert.Contains("Hello Log", contents, StringComparison.Ordinal);
    }
}
