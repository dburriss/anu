using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez;

public interface InfrastructureProvider
{
    public abstract string CommandName { get; }
    public abstract string? DeployCommandDescription { get; }
    public abstract string? RunCommandDescription { get; }

    void RegisterDeployCommand(IConfigurator configurator);
    void Configuration(IConfigurationBuilder configuration);
    void ConfigureServices(IConfiguration config, IServiceCollection services);
    void ConfigureSilos(IConfiguration config, IServiceCollection services, ISiloBuilder siloBuilder);
    void ConfigureApp(IConfiguration config, WebApplication app);
}

public abstract class InfrastructureProvider<TCommand>(IConfigurator configurator) : InfrastructureProvider 
    where TCommand: class, ICommand
{
    public abstract string CommandName { get; }
    public abstract string? DeployCommandDescription { get; }
    public abstract string? RunCommandDescription { get; }

    public void RegisterDeployCommand(IConfigurator configurator)
    {
        var deploy = $"deploy-{CommandName.ToLower()}";
        if (DeployCommandDescription != null)
            configurator.AddCommand<TCommand>(deploy).WithDescription(DeployCommandDescription);
        else configurator.AddCommand<TCommand>(deploy);
    }

    public abstract void Configuration(IConfigurationBuilder configuration);
    public abstract void ConfigureServices(IConfiguration config, IServiceCollection services);
    public abstract void ConfigureSilos(IConfiguration config, IServiceCollection services, ISiloBuilder siloBuilder);
    public abstract void ConfigureApp(IConfiguration config, WebApplication app);
}

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

    public override void ConfigureApp(IConfiguration config, WebApplication app)
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
