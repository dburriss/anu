using System;
using System.Threading;
using System.Threading.Tasks;

using Ez;
using Ez.Jobs;
using Ez.Queues;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

return 
    EzSystem.Create("Ez")
        .Feature(feature =>
        {
            feature.Title = "Welcome contact";
            feature.Description = "This is an example of a recurring job.";
            feature.WithJob<WelcomeJob>(
                triggers => triggers.AddTimer(configure =>
                {
                    return configure.Enrich((_, context) => context.Data.Add("test", "data"))
                        .AutoRetry()
                        .MaxRetries(10)
                        .EveryMinutes(1);
                }));
        })
        .Feature(feature =>
        {
            feature.Title = "Create Contact";
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
        .Run(args);


public class WelcomeJob(ILogger<WelcomeJob> logger, IQueueClient queueClient) : IJob
{

    public async Task Execute(IJobContext context)
    {
        logger.LogInformation("Start working at {JobStart}...", DateTimeOffset.UtcNow);
        var msgs = queueClient.DequeueBatch("contact-created", 2);
        await foreach(var msg in msgs)
        {
            await HandleMessage(msg);
        }
        // await queueClient.RegisterHandler("contact-created", HandleMessage);
        logger.LogInformation("Done working at {JobEnd}...", DateTimeOffset.UtcNow);
    }

    private Task HandleMessage(QueueMessage message)
    {
        Console.WriteLine($"Handling message {message.Id} {message.Content} from {message.CreatedTimestamp} ");
        return Task.CompletedTask;
    }

    public Task Compensate(IJobContext context) => throw new NotImplementedException();
}

public class CreateContact : Usecase<CreateContactCmd>
{
    private readonly IQueueClient _queueClient;

    public CreateContact(IQueueClient queueClient)
    {
        _queueClient = queueClient;
    }
    
    public override async Task Execute(CreateContactCmd command, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Executing CreateContact usecase...{command.Id}");
        // todo: store event
        // todo: easy outbox?
        var msg = new QueueMessage()
        {
            Content = $"Received {command.Id}",
        };
        Console.WriteLine($"Sending message {msg.Id} at {msg.CreatedTimestamp}");
        await _queueClient.SendMessage("contact-created", msg);
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
        var id = request.RouteValues["id"]?.ToString();
        var data = await request.ReadFromJsonAsync<CreatContactData>();
        var cmd = new CreateContactCmd(id!, data!.Name, data.Email);
        return cmd;
    }
}
