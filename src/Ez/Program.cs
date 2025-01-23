using System;
using System.Threading;
using System.Threading.Tasks;

using Ez;
using Ez.Jobs;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

return 
    EzSystem.Create("Ez")
        .Feature(feature =>
        {
            feature.Title = "Example Job";
            feature.Description = "This is an example of a recurring job.";
            feature.WithJob<SleeperJob>(
                triggers => triggers.AddTimer(configure =>
                {
                    return configure.Enrich((_, context) => context.Data.Add("test", "data"))
                        .AutoRetry()
                        .MaxFailures(3)
                        .EveryMinutes(2);
                }));
        })
        .Feature(feature =>
        {
            feature.Title = "Example usecase";
            feature.Description = "This is an example of accepting a command";
            feature.WithUsecase<CreateContact, CreateContactCmd>(
                triggers =>
                {
                    return triggers
                        .AddPut("/contacts/{id:guid}", 
                            opt =>
                            {
                                opt.DefaultStatusCode = StatusCodes.Status201Created;
                                opt.Mapper = async ctx => await ctx.GetCreateContactCommand();
                            })
                        .AddQueue("create-contacts-queue");
                });
        })
        // .WithRecurringJob<SleeperJob>(TimeSpan.FromHours(1))
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

public class CreateContact : Usecase<CreateContactCmd>
{
    public override Task Execute(CreateContactCmd command, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Executing CreateContact usecase...{command.Id}");
        return Task.CompletedTask;
    }
}

public record CreateContactCmd(string Id, string Name, string Email);
public static class HttpContextExtensions
{
    public class CreatContactData
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
    public static async Task<CreateContactCmd> GetCreateContactCommand(this HttpContext context)
    {
        var request = context.Request;
        var id = request.Query["id"];
        var data = await request.ReadFromJsonAsync<CreatContactData>();
        return new CreateContactCmd(id!, data!.Name, data.Email);
    }
}
