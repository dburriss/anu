using System.ComponentModel;

using Amazon.CDK;

using Spectre.Console.Cli;

namespace Ez.AWS;

public class CdkCommand: Command<CdkCommand.Settings>
{
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
}
