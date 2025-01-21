using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Ez.AWS;
using Ez.Jobs;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Orleans.Configuration;
using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez;

public class EzSystem(string name)
{
    private bool _isLocal = true;
    private readonly List<Tuple<Type, TimeSpan>> _recurringJobs = new();
    private readonly List<EndpointTriggerDescriptor> _endpointTriggers = new();

    public static EzSystem Create(string name)
    {
        return new EzSystem(name);
    }

    public EzSystem UseLocal(bool isLocal = true)
    {
        _isLocal = isLocal;
        return this;
    }

    public EzSystem WithRecurringJob<TJob>(TimeSpan interval) where TJob: IJob
    {
        //todo: put into default feature
        _recurringJobs.Add(Tuple.Create(typeof(TJob), interval));
        return this;
    }

    public EzSystem Feature(Action<FeatureDescriptor> feature)
    {
        var descriptor = new FeatureDescriptor();
        feature(descriptor);
        
        if (descriptor.Title == null)
            descriptor.Title = "Default";
        
        if (descriptor.Description == null)
            descriptor.Description = "Default feature";
        
        return this;
    }
    
    public int Run(string[] args)
    {
        var descriptor = new SystemDescriptor(
            name,
            _isLocal,
            _recurringJobs,
            _endpointTriggers);

        var cli = new CommandApp();
        cli.Configure(config =>
        {
            config.SetApplicationName(descriptor.Name);
            config.AddCommand<LaunchCommand>("launch")
                .WithAlias("run")
                .WithDescription("Launch the system")
                .WithExample("launch", "-e", "production", "--local")
                .WithData(descriptor);

            config.AddCommand<CdkCommand>("cdk")
                .WithAlias("deploy")
                .WithDescription("Deploy the system")
                .WithExample("cdk", "-e", "production")
                .WithData(descriptor);

            #if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
            #endif
        });
        return cli.Run(args);
    }

}

public class FeatureDescriptor
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    public FeatureDescriptor WithJob<TJob>(Func<JobTriggers, JobTriggers> trigger) where TJob: IJob
    {
        // todo: save job
        return this;
    }

    public FeatureDescriptor WithUsecase<TUsecase, TCommand>(
        Func<UsecaseTriggers<TUsecase, TCommand>, UsecaseTriggers<TUsecase, TCommand>> triggers)
        where TUsecase: Usecase<TCommand>
    {
        // todo: save triggers
        return this;
    }
}

public abstract class Usecase<TCommand>
{
    public abstract Task Execute(TCommand command);

    public Task Compensate(TCommand command)
    {
        return Task.CompletedTask;
    }
}

public abstract class Trigger
{
}
public abstract class UsecaseTrigger
{
    public abstract Task TriggerAsync(HttpContext httpContext);
}
public class JobTriggers
{
    private List<Trigger> _triggers = new();

    public JobTriggers AddTimer(Func<TimerTriggerBuilder, TimerTriggerBuilder> configure)
    {
        return this;
    }
}

public class UsecaseTriggers<TUsecase, TCommand> where TUsecase: Usecase<TCommand>
{
    private Dictionary<string,UsecaseTriggerOptions<TCommand>> _putTriggerOptionsMap = new();

    public UsecaseTriggers<TUsecase, TCommand> AddPut(
        string path,
        Action<UsecaseTriggerOptions<TCommand>>? options = null)
    {
        var opt = new UsecaseTriggerOptions<TCommand>();
        options?.Invoke(opt);
        _putTriggerOptionsMap.Add(path, opt);
        return this;
    }

    public UsecaseTriggers<TUsecase, TCommand> AddQueue(string queueName)
    {
        return this;
    }
}

public class UsecaseTriggerOptions<TCommand>
{
    public Func<IServiceProvider, HttpContext, Task<TCommand?>> Mapper { get; set; } =
        async (_, ctx) => await ctx.Request.ReadFromJsonAsync<TCommand>();
    public int DefaultStatusCode { get; set; } = 200;
    public int DefaultErrorCode { get; set; } = 500;
}

public class TimerTriggerBuilder
{
    public TimerTriggerBuilder AutoRetry()
    {
        return this;
    }

    public TimerTriggerBuilder MaxFailures(int maxFailures)
    {
        return this;
    }

    public TimerTriggerBuilder EveryMinutes(int minutes)
    {
        return this;
    }

    public TimerTriggerBuilder Enrich(Action<IServiceProvider, IJobContext> enrich)
    {
        return this;
    }
}

public record SystemDescriptor(
    string Name,
    bool IsLocal,
    IReadOnlyList<Tuple<Type, TimeSpan>> RecurringJobs,
    IReadOnlyList<EndpointTriggerDescriptor> EndpointTriggers);

public record EndpointTriggerDescriptor(
    string Method,
    string Path,
    string TriggerName,
    Type TriggerType);

public class LaunchCommand: Command<LaunchCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        var descriptor = context.Data as SystemDescriptor;
        if (descriptor == null) throw new InvalidOperationException("System descriptor not found.");
        // HOST

        // setup builder and run the web api
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<SystemDescriptor>(_ => descriptor);
        //builder.WebHost.UseUrls(settings.Urls.Split(';'));
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Host.UseOrleans(siloBuilder =>
        {
            siloBuilder.UseLocalhostClustering();
            // set collection age limit to 2 minute
            siloBuilder.Configure<GrainCollectionOptions>(options =>
            {
                options.CollectionAge = TimeSpan.FromMinutes(2);
            });
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.UseInMemoryReminderService();

            siloBuilder.UseJobs();
            foreach (var job in descriptor.RecurringJobs) siloBuilder.UseRecurringJob(job.Item1, job.Item2);
        });

        // APP
        var app = builder.Build();
        foreach (var endpointTrigger in descriptor.EndpointTriggers)
            switch (endpointTrigger.Method)
            {
                case "PUT":
                    app.MapPut(endpointTrigger.Path,
                        async httpContext =>
                        {
                            try
                            {
                                var trigger =
                                    (UsecaseTrigger)httpContext.RequestServices.GetRequiredKeyedService(endpointTrigger.TriggerType,
                                        endpointTrigger.TriggerName);
                                await trigger.TriggerAsync(httpContext);
                            }
                            catch (Exception ex)
                            {
                                httpContext.Response.StatusCode = 500;
                                // return problem details
                                
                            }

                        });
                    break;
            }
        // app.MapGet("/jobs/test/{name}", async (IGrainFactory grains, string name) =>
        // {
        //     var jobGrain = await grains.CreateJobGrain<TestJob>(name);
        //     var status = await jobGrain.StatusAsync();
        //     return Results.Ok(new { status });
        // });

        // app.MapGet("/job/{id:guid}", async (IGrainFactory grains, Guid id) =>
        // {
        //     
        // });

        app.UseHttpsRedirection();
        app.Run();
        app.DisposeAsync();
        return 0;
    }

    public class Settings: CommandSettings
    {
        [Description("The environment to run the system in")]
        [CommandOption("-e|--env <ENVIRONMENT>")]
        [DefaultValue("development")]
        public string Environment { get; set; } = "development";

        [Description("Run the system locally")]
        [CommandOption("--local")]
        public bool IsLocal { get; set; }

        [Description("Set the verbosity level")]
        [CommandOption("-v|--verbose")]
        [DefaultValue(0)]
        public int Verbosity { get; set; }

        [Description("The urls to use for the web api")]
        [CommandOption("-u|--urls <URLS>")]
        public string Urls { get; set; } = "http://localhost:5000;https://localhost:5001";
    }
}
