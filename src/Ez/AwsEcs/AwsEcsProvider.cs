using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Orleans.Hosting;

// using Spectre.Console.Cli;
//
// namespace Ez.AWS;
//
// public class AwsEcsProvider(IConfigurator configurator)
//     : InfrastructureProvider<AwsEcsConfig>(configurator)
// {
//     public override string CommandName { get; } = "aws-ecs";
//
// }
//
// public class AwsEcsConfig: Command<AwsEcsConfig.Settings>
// {
//     public override int Execute(CommandContext context, Settings settings)
//     {
//         throw new System.NotImplementedException();
//     }
//
//     public class Settings: CommandSettings
//     {
//     }
// }
