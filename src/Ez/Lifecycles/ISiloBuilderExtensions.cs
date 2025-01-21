using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;

using ServiceLifecycleStage = Orleans.ServiceLifecycleStage;

namespace Ez.Lifecycles;

public static class ISiloBuilderExtensions
{
    public static ISiloBuilder AddStartupTask(
        this ISiloBuilder builder,
        Func<IServiceProvider, CancellationToken, Task> startupTask,
        int stage = ServiceLifecycleStage.Active)
    {
        builder.ConfigureServices(services =>
            services.AddTransient<ILifecycleParticipant<ISiloLifecycle>>(
                serviceProvider =>
                    new StartupTask(
                        serviceProvider,
                        startupTask,
                        stage)));

        return builder;
    }
    
    
}

internal class StartupTask: ILifecycleParticipant<ISiloLifecycle>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly int _stage;
    private readonly Func<IServiceProvider, CancellationToken, Task> _startupTask;

    public StartupTask(
        IServiceProvider serviceProvider,
        Func<IServiceProvider, CancellationToken, Task> startupTask,
        int stage)
    {
        _serviceProvider = serviceProvider;
        _startupTask = startupTask;
        _stage = stage;
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe<StartupTask>(_stage,
            cancellation => _startupTask(_serviceProvider, cancellation));
    }
}
