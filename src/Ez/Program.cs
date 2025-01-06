using System;
using System.Threading.Tasks;

using Ez;

using Microsoft.Extensions.Logging;

return 
    EzSystem.Create("Ez")
        .WithRecurringJob<SleeperJob>(TimeSpan.FromMinutes(1))
        .Run(args);

public class SleeperJob(ILogger<SleeperJob> logger) : IJob
{
    private TimeSpan _sleepTime = TimeSpan.FromMinutes(2);
    public async Task Execute(IJobContext context)
    {
        logger.LogInformation("Start working at {JobStart}...", DateTimeOffset.UtcNow);
        await Task.Delay(_sleepTime);
        logger.LogInformation("Done working at {JobEnd}...", DateTimeOffset.UtcNow);
    }

    public Task Compensate(IJobContext context) => throw new NotImplementedException();
}
