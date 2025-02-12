using System;

using Ez.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez;

public class LocalProvider(IConfigurator configurator)
    : InfrastructureProvider<LocalCommand>(configurator)
{
    public override string CommandName => "local";
    public override string? DeployCommandDescription => null;
    public override string? RunCommandDescription => "Run the system locally";

    public override void Configuration(IConfigurationBuilder configuration)
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    }

    public override void ConfigureServices(IConfiguration config, IServiceCollection services)
    {
    }

    public override void ConfigureSilos(IConfiguration config, IServiceCollection services, ISiloBuilder siloBuilder)
    {
        siloBuilder.UseLocalhostClustering();
        siloBuilder.AddMemoryGrainStorageAsDefault();
        siloBuilder.UseInMemoryReminderService();
    }

    public override void ConfigureApplicationBuilder(IConfiguration config, IApplicationBuilder app)
    {
        
    }
}

public class LocalCommand: Command<LocalCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        Console.WriteLine("Nothing needed for running locally...");
        return 0;
    }

    public class Settings: CommandSettings
    {
    }
}
