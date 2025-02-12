using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Ez.Jobs;
using Ez.Jobs.Triggers;
using Ez.Queries;
using Ez.Queries.Triggers;
using Ez.Queues;
using Ez.Usecases;
using Ez.Usecases.Triggers;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez;

public class LaunchCommand: Command<LaunchCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        var descriptor = context.Data as SystemDescriptor;
        if (descriptor == null) throw new InvalidOperationException("System descriptor not found.");

        var provider = descriptor.InfrastructureProvider;
        // CONFIGURATION
        var configurationBuilder = new ConfigurationBuilder();
        provider.Configuration(configurationBuilder);
        var configuration = configurationBuilder.Build();
        
        // HOST
        // setup builder and run the web api
        var applicationBuilder = WebApplication.CreateBuilder();
        applicationBuilder.Configuration.AddConfiguration(configuration);
        applicationBuilder.Services.AddSingleton<SystemDescriptor>(_ => descriptor);
        applicationBuilder.Services.AddTransient<IQueueClient, QueueClient>();
        
        provider.ConfigureServices(applicationBuilder.Configuration, applicationBuilder.Services);
        
        // ADD USECASES TO DEPENDENCY CONTAINER
        var usecases = descriptor.Features.SelectMany(f => f.UsecaseTriggers.Select(t => t.UsecaseType));
        foreach (var usecaseType in usecases.Distinct()) applicationBuilder.Services.TryAddTransient(usecaseType);

        // ADD QUERIES TO DEPENDENCY CONTAINER
        var queries = descriptor.Features.SelectMany(f => f.QueryTriggers.Select(t => t.QueryType));
        foreach (var query in queries.Distinct()) applicationBuilder.Services.TryAddTransient(query);

        // ADD TRIGGERS TO DEPENDENCY CONTAINER
        var usecaseTriggers =
            descriptor.Features
                .SelectMany(f => f.UsecaseTriggers)
                .ToImmutableList();

        foreach (var usecaseTrigger in usecaseTriggers)
            applicationBuilder.Services.AddKeyedTransient(typeof(UsecaseTrigger),
                usecaseTrigger.TriggerName,
                (sp, key) => usecaseTrigger);

        var queryTriggers =
            descriptor.Features
                .SelectMany(f => f.QueryTriggers)
                .ToImmutableList();

        foreach (var queryTrigger in queryTriggers)
            applicationBuilder.Services.AddKeyedTransient(typeof(QueryTrigger),
                queryTrigger.TriggerName,
                (sp, key) => queryTrigger);

        //builder.WebHost.UseUrls(settings.Urls.Split(';'));
        applicationBuilder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        applicationBuilder.Host.UseOrleans(siloBuilder =>
        {
            // TODO: Call provider.ConfigureSilos to configure silos
            provider.ConfigureSilos(applicationBuilder.Configuration, applicationBuilder.Services, siloBuilder);
            // set collection age limit to 2 minute
            // siloBuilder.Configure<GrainCollectionOptions>(options =>
            // {
            //     options.CollectionAge = TimeSpan.FromMinutes(2);
            // });

            siloBuilder.AddStartupTask((provider, token) =>
            {
                // QUEUE HANDLERS
                var queueTriggers = usecaseTriggers.OfType<QueueTrigger>();
                var queueClient = provider.GetRequiredService<IQueueClient>();
                foreach (var queueTrigger in queueTriggers)
                    queueClient.RegisterHandler(queueTrigger.QueueName,
                        async message =>
                        {
                            var usecase = (Usecase)provider.GetRequiredService(queueTrigger.UsecaseType);
                            await queueTrigger.TriggerAsync(usecase, message);
                        });
                return Task.CompletedTask;
            });
            siloBuilder.UseJobs();
            // ADD JOBS TO DEPENDENCY CONTAINER
            var jobTriggers = descriptor.Features.SelectMany(x => x.JobTriggers.OfType<JobTimerTrigger>());
            foreach (var jobTrigger in jobTriggers) siloBuilder.UseRecurringJob(jobTrigger.JobType, jobTrigger.Options);
        });

        // APP
        var app = applicationBuilder.Build();
        var usecaseEndpointTriggers = usecaseTriggers.OfType<UsecaseEndpointTrigger>();
        foreach (var endpointTrigger in usecaseEndpointTriggers)
            switch (endpointTrigger.Method)
            {
                case "PUT":
                    app.MapPut(endpointTrigger.Path,
                        async httpContext =>
                        {
                            var usecase =
                                (Usecase)httpContext.RequestServices.GetRequiredService(endpointTrigger.UsecaseType);
                            var trigger =
                                (UsecaseTrigger)httpContext.RequestServices.GetRequiredKeyedService(
                                    typeof(UsecaseTrigger),
                                    endpointTrigger.TriggerName);
                            await trigger.TriggerAsync(usecase, httpContext);
                            httpContext.Response.StatusCode = endpointTrigger.Options.DefaultStatusCode;
                        });
                    break;
                // todo: POST, DELETE
            }

        var queryEndpointTriggers = queryTriggers.OfType<QueryEndpointTrigger>();
        foreach (var endpointTrigger in queryEndpointTriggers)
            app.MapGet(endpointTrigger.Path,
                async httpContext =>
                {
                    var query =
                        (Query)httpContext.RequestServices.GetRequiredService(endpointTrigger.QueryType);
                    var trigger =
                        (QueryTrigger)httpContext.RequestServices.GetRequiredKeyedService(
                            typeof(QueryTrigger),
                            endpointTrigger.TriggerName);
                    var result = await trigger.TriggerAsync(query, httpContext);
                    httpContext.Response.StatusCode = endpointTrigger.Options.DefaultStatusCode;
                    await httpContext.Response.WriteAsJsonAsync(result);
                });

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
        
        provider.ConfigureApplicationBuilder(configuration, app);
        
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
        [CommandOption("-l|--local")]
        public bool IsLocal { get; set; }

        [Description("Set the verbosity level")]
        [CommandOption("-v|--verbose")]
        [DefaultValue(0)]
        public int Verbosity { get; set; }

        [Description("The urls to use for the web api")]
        [CommandOption("-u|--urls <URLS>")]
        public string Urls { get; set; } = "http://localhost:5000;https://localhost:5001";
        
        [Description("The infrastructure provider to use")]
        [CommandOption("-p|--provider <PROVIDER>")]
        public string Provider { get; set; } = "local";
    }
}

// command to list the providers
