using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Ez;
using Ez.Jobs;
using Ez.Queries;
using Ez.Queues;
using Ez.Usecases;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

return 
    EzSystem.Create("Ez")
        .Feature(feature =>
        {
            feature.Title = "Creating Customers";
            feature.Description = "Signup of new customers";
            feature.WithUsecase<AcceptCustomerSignup, CreateCustomerCmd>(
                triggers =>
                {
                    return triggers
                        .AddPut("/signups/{id:guid}",
                            opt =>
                            {
                                opt.DefaultStatusCode = StatusCodes.Status201Created;
                                opt.Mapper = async ctx => await ctx.GetCreateCustomerCommand();
                            });
                }); 
            feature.WithUsecase<CreateCustomer, CreateCustomerCmd>(
                triggers =>
                {
                    return triggers
                        .AddPut("/customers/{id:guid}", 
                            opt =>
                            {
                                opt.Mapper = async ctx => await ctx.GetCreateCustomerCommand();
                            })
                        .AddQueue("create-customer-queue");
                });
            feature.WithQuery<CustomerQuery, Guid, string>(
                triggers => 
                    triggers.AddGet("/customers/{id:guid}", 
                        opt => 
                        {
                            opt.InputMapper = async ctx => Guid.Parse(ctx.Request.RouteValues["id"]?.ToString() ?? string.Empty);
                            opt.OutputMapper = async result => result;
                        }));
        })
        .Feature(feature =>
                {
                    feature.Title = "Welcome emails";
                    feature.Description = "A batch job to send emails to new customers.";
                    feature.WithJob<BatchWelcomeEmailJob>(
                        triggers => triggers.AddTimer(configure =>
                        {
                            return configure.Enrich((_, context) => context.Data.Add("test", "data"))
                                .AutoRetry()
                                .MaxRetries(10)
                                .EveryMinutes(1);
                        }));
                })
        .Run(args);


// todo: have a way to go from a endpoint to queue?
public class AcceptCustomerSignup(IQueueClient queueClient, ILogger<AcceptCustomerSignup> logger)
    : Usecase<CreateCustomerCmd>
{
    public override async Task Execute(CreateCustomerCmd command, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Signup accepted...{Id}", command.Id);
        // todo: store event
        // todo: easy outbox?
        var msg = new QueueMessage()
        {
            Content = JsonSerializer.Serialize(command) 
        };
        var queue = "create-customer-queue";
        logger.LogInformation("Message {Id} placed on {Queue} at {Timestamp}", msg.Id, queue, msg.CreatedTimestamp);
        await queueClient.SendMessage(queue, msg);
    }
}

public class CreateCustomer(IQueueClient queueClient, ILogger<CreateCustomer> logger): Usecase<CreateCustomerCmd>
{
    public override async Task Execute(CreateCustomerCmd command, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Executing CreateContact usecase...{command.Id}");
        logger.LogInformation("Creating contact...{Id}", command.Id);
        // todo: store event
        // todo: easy outbox?
        var msg = new QueueMessage()
        {
            Content = JsonSerializer.Serialize(command) 
        };
        var queue = "customer-created-queue";
        logger.LogInformation("Message {Id} placed on {Queue} at {Timestamp}", msg.Id, queue, msg.CreatedTimestamp);
        await queueClient.SendMessage(queue, msg);
    }
}

public record CreateCustomerCmd(string Id, string Name, string Email);
public static class HttpContextExtensions
{
    public class CreatContactData
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
    public static async Task<CreateCustomerCmd> GetCreateCustomerCommand(this HttpContext context)
    {
        var request = context.Request;
        var id = request.RouteValues["id"]?.ToString();
        var data = await request.ReadFromJsonAsync<CreatContactData>();
        var cmd = new CreateCustomerCmd(id!, data!.Name, data.Email);
        return cmd;
    }
}

public class BatchWelcomeEmailJob(ILogger<BatchWelcomeEmailJob> logger, IQueueClient queueClient) : IJob
{
    public async Task Execute(IJobContext context)
    {
        var queue = "customer-created-queue";
        logger.LogInformation("Start working at {JobStart}...", DateTimeOffset.UtcNow);
        var messages = queueClient.DequeueBatch(queue, 2);
        await foreach(var msg in messages)
        {
            await HandleMessage(msg);
        }
        // await queueClient.RegisterHandler("contact-created", HandleMessage);
        logger.LogInformation("Done working at {JobEnd}...", DateTimeOffset.UtcNow);
    }

    private Task HandleMessage(QueueMessage message)
    {
        var createdCustomer = JsonSerializer.Deserialize<CreateCustomerCmd>(message.Content);
        Console.WriteLine($"Email sent to {createdCustomer?.Email}");
        return Task.CompletedTask;
    }

    public Task Compensate(IJobContext context) => Task.CompletedTask;
}

public class CustomerQuery : Query<Guid,string>
{
    public override Task<string> Execute(Guid id, CancellationToken cancellationToken = default)
    {
        
        return Task.FromResult($"Bob");
    }

    public override Task Compensate(Guid id)
    {
        return Task.CompletedTask;
    }
}
