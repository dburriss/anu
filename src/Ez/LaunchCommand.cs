using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

using Ez.Jobs;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
        //builder.WebHost.UseUrls(settings.Urls.Split(';'));
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Host.UseOrleans(siloBuilder =>
        {
            CoreHostingExtensions.UseLocalhostClustering(siloBuilder);
            // set collection age limit to 2 minute
            SiloBuilderExtensions.Configure<GrainCollectionOptions>(siloBuilder,
                options =>
            {
                options.CollectionAge = TimeSpan.FromMinutes(2);
            });
            MemoryGrainStorageSiloBuilderExtensions.AddMemoryGrainStorageAsDefault(siloBuilder);
            SiloBuilderReminderMemoryExtensions.UseInMemoryReminderService(siloBuilder);

            ISiloBuilderJobExtensions.UseJobs(siloBuilder);
            foreach (var job in descriptor.RecurringJobs) ISiloBuilderJobExtensions.UseRecurringJob(siloBuilder, job.Item1, job.Item2);
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
