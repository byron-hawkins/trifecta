/* author: Copilot */
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using trifecta.io;
using trifecta.test.common;
using Xunit;

namespace trifecta.test.io;

[Collection("ComponentLog tests")]
public sealed class ComponentLogConcurrencyTests : ComponentLogTestBase
{
    [Fact]
    public async Task LogsMessagesFromFourRegisteredTasks()
    {
        setup($"ComponentLogConcurrency-{Guid.NewGuid():N}");

        var workers = Enumerable.Range(0, 4)
            .Select(index => new
            {
                Name = $"worker-{index}",
                MessageCount = Random.Shared.Next(3, 6)
            })
            .ToArray();

        var tasks = workers
            .Select(worker => Task.Run(() =>
            {
                using (ComponentLog.singleton.Register(worker.Name))
                {
                    for (var messageIndex = 0; messageIndex < worker.MessageCount; messageIndex++)
                    {
                        if (messageIndex > 0)
                        {
                            Thread.Sleep(Random.Shared.Next(10, 21));
                        }

                        ComponentLog.singleton.Log(
                            $"{worker.Name}-message-{messageIndex}");
                    }
                }
            }))
            .ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            finish();
        }

        var lines = ReadLog()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        foreach (var worker in workers)
        {
            for (var messageIndex = 0; messageIndex < worker.MessageCount; messageIndex++)
            {
                var message = $"{worker.Name}-message-{messageIndex}";
                var matchingLine = Assert.Single(
                    lines,
                    line => line.Contains(message, StringComparison.Ordinal));
                Assert.True(
                    matchingLine.Contains($"[{worker.Name}.", StringComparison.Ordinal),
                    $"Expected '{message}' to be attributed to '{worker.Name}', but got: {matchingLine}");
            }
        }

        Assert.Equal(workers.Sum(worker => worker.MessageCount), lines.Length);
    }
}
