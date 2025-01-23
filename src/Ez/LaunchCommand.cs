using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

using Ez.Jobs;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Orleans.Configuration;
using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez;

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
        // USECASES
        // todo: should this just be filtered from the triggers?
        var usecases = descriptor.Features.SelectMany(f => f.UsecaseTriggers.Select(t => t.UsecaseType));
        foreach (var usecaseType in usecases.Distinct())
        {
            //check if usecase is already registered
            builder.Services.TryAddTransient(usecaseType);
        }
        
        var triggers = 
            descriptor.Features
                .SelectMany(f => f.UsecaseTriggers)
                .ToImmutableList();
        // ENDPOINT TRIGGERS
        foreach (var trigger in triggers)
        {
            builder.Services.AddKeyedTransient(typeof(UsecaseTrigger),trigger.TriggerName, (sp, key) => trigger);
        }
        
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
            // todo: add recurring jobs back
            // foreach (var job in descriptor.RecurringJobs)
            // {
            //     siloBuilder.UseRecurringJob(job.Item1, job.Item2);
            // }
        });

        // APP
        var app = builder.Build();
        var endpointTriggers = triggers.OfType<EndpointTrigger>();
        foreach (var endpointTrigger in endpointTriggers)
            switch (endpointTrigger.Method)
            {
                case "PUT":
                    app.MapPut(endpointTrigger.Path,
                        async httpContext =>
                        {
                            var usecase = (Usecase)httpContext.RequestServices.GetRequiredService(endpointTrigger.UsecaseType);
                            var trigger =
                                (UsecaseTrigger)httpContext.RequestServices.GetRequiredKeyedService(
                                    typeof(UsecaseTrigger),
                                    endpointTrigger.TriggerName);
                            await trigger.TriggerAsync(usecase, httpContext);
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
