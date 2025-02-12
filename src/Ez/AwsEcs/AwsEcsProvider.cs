using Ez.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez.AWS;

public class AwsEcsProvider()
    : InfrastructureProvider<AwsEcsConfig>("aws-ecs")
{
    public override string? DeployCommandDescription { get; }
    public override string? RunCommandDescription { get; }
    public override void Configuration(IConfigurationBuilder configuration)
    {
        throw new System.NotImplementedException();
    }

    public override void ConfigureServices(IConfiguration config, IServiceCollection services)
    {
        throw new System.NotImplementedException();
    }

    public override void ConfigureSilos(IConfiguration config, IServiceCollection services, ISiloBuilder siloBuilder)
    {
        throw new System.NotImplementedException();
    }

    public override void ConfigureApplicationBuilder(IConfiguration config, IApplicationBuilder app)
    {
        throw new System.NotImplementedException();
    }
}

public class AwsEcsConfig: Command<AwsEcsConfig.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        throw new System.NotImplementedException();
    }

    public class Settings: CommandSettings
    {
    }
}
