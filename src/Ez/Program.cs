using System;
using System.ComponentModel;

using Amazon.CDK;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Spectre.Console.Cli;

namespace Ez;

internal sealed class Program
{
    public static void Main(string[] args)
    {
        var cli = new CommandApp();
        cli.Configure(config =>
        {
            config.SetApplicationName("ez");
            config.AddCommand<LaunchCommand>("launch")
                .WithAlias("run")
                .WithDescription("Launch the system")
                .WithExample("launch","-e", "production", "--local");
            
            config.AddCommand<CdkCommand>("cdk")
                .WithAlias("deploy")
                .WithDescription("Deploy the system")
                .WithExample("cdk","-e", "production");
            
            #if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
            #endif
        });
        cli.Run(args);
    }
}

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
        public string Urls { get; set; } = "http://0.0.0.0:5001;https://0.0.0.0:5002";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        // setup builder and run the web api
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(settings.Urls.Split(';'));
        var app = builder.Build();
        app.Run();
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
