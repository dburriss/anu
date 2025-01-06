using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Amazon.CDK;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Orleans.Configuration;
using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez;

public class EzSystem(string name)
{
    private bool _isLocal = true;
    private List<Tuple<Type, TimeSpan>> _recurringJobs = new();

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
        _recurringJobs.Add(Tuple.Create(typeof(TJob), interval));   
        return this;
    }

    public int Run(string[] args)
    {
        var descriptor = new SystemDescriptor(
            Name: name,
            IsLocal: _isLocal,
            RecurringJobs: _recurringJobs);
        
        var cli = new CommandApp();
                cli.Configure(config =>
                {
                    config.SetApplicationName(descriptor.Name);
                    config.AddCommand<LaunchCommand>("launch")
                        .WithAlias("run")
                        .WithDescription("Launch the system")
                        .WithExample("launch","-e", "production", "--local")
                        .WithData(descriptor);
                    
                    config.AddCommand<CdkCommand>("cdk")
                        .WithAlias("deploy")
                        .WithDescription("Deploy the system")
                        .WithExample("cdk","-e", "production")
                        .WithData(descriptor);
                    
                    #if DEBUG
                    config.PropagateExceptions();
                    config.ValidateExamples();
                    #endif
                });
        return cli.Run(args);
    }
}

public record SystemDescriptor
(string Name, bool IsLocal, List<Tuple<Type, TimeSpan>> RecurringJobs);

public class LaunchCommand : Command<LaunchCommand.Settings>
{
    public class Settings : CommandSettings
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
            foreach (var job in descriptor.RecurringJobs)
            {
                siloBuilder.UseRecurringJob(job.Item1, job.Item2);
            }
        });
        
        // APP
        var app = builder.Build();
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
}


public class CdkCommand: Command<CdkCommand.Settings>
{
    public class Settings: CommandSettings
    {
        [Description("The environment to run the system in")]
        [CommandOption("-e|--env <ENVIRONMENT>")]
        [DefaultValue("development")]
        public string Environment { get; set; } = "development";
        
        [Description("Set the verbosity level")]
        [CommandOption("-v|--verbose")]
        [DefaultValue(0)]
        public int Verbosity { get; set; } 
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var app = new App();
        new EzStack(app,
            "EzStack",
            new StackProps
            {
                // If you don't specify 'env', this stack will be environment-agnostic.
                // Account/Region-dependent features and context lookups will not work,
                // but a single synthesized template can be deployed anywhere.

                // Uncomment the next block to specialize this stack for the AWS Account
                // and Region that are implied by the current CLI configuration.
                /*
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION"),
                }
                */

                // Uncomment the next block if you know exactly what Account and Region you
                // want to deploy the stack to.
                /*
                Env = new Amazon.CDK.Environment
                {
                    Account = "123456789012",
                    Region = "us-east-1",
                }
                */

                // For more information, see https://docs.aws.amazon.com/cdk/latest/guide/environments.html
            });
        app.Synth();
        return 0;
    }
}
