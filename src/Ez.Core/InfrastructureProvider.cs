using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Orleans.Hosting;

using Spectre.Console.Cli;

namespace Ez.Core;

public interface InfrastructureProvider
{
    public abstract string CommandName { get; }
    public abstract string? DeployCommandDescription { get; }
    public abstract string? RunCommandDescription { get; }

    void RegisterDeployCommand(IConfigurator configurator);
    void Configuration(IConfigurationBuilder configuration);
    void ConfigureServices(IConfiguration config, IServiceCollection services);
    void ConfigureSilos(IConfiguration config, IServiceCollection services, ISiloBuilder siloBuilder);
    void ConfigureApplicationBuilder(IConfiguration config, IApplicationBuilder app);
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
    public abstract void ConfigureApplicationBuilder(IConfiguration config, IApplicationBuilder app);
}

