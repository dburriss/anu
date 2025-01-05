using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;

using LifecycleExtensions = Orleans.LifecycleExtensions;
using ServiceLifecycleStage = Orleans.ServiceLifecycleStage;

namespace Ez;

public static class ISiloBuilderJobExtensions
{
    public static ISiloBuilder UseJobs(this ISiloBuilder builder, params Assembly[] jobAssemblies)
    {
        var assemblies = jobAssemblies is null
            ? AppDomain.CurrentDomain.GetAssemblies()
            : AppDomain.CurrentDomain.GetAssemblies().Concat(jobAssemblies);
        var interfaceType = typeof(IJob);
        var jobTypes = assemblies
            .SelectMany(x => x.GetTypes())
            .Where(x => interfaceType.IsAssignableFrom(x))
            .Where(x => !x.IsInterface)
            .Where(x => !x.IsAbstract);

        builder.ConfigureServices(services =>
        {
            services.AddTransient<IJobBuilder, JobBuilder>();
            jobTypes.ToList().ForEach(x => services.AddTransient(x));
        });

        return builder;
    }

    public static ISiloBuilder AddStartupTask(
        this ISiloBuilder builder,
        Func<IServiceProvider, CancellationToken, Task> startupTask,
        int stage = ServiceLifecycleStage.Active)
    {
        builder.ConfigureServices(services =>
            services.AddTransient<Orleans.ILifecycleParticipant<ISiloLifecycle>>(
                serviceProvider =>
                    new StartupTask(
                        serviceProvider,
                        startupTask,
                        stage)));

        return builder;
    }

    public static ISiloBuilder UseRecurringJob<TJob>(this ISiloBuilder host, TimeSpan interval) where TJob: IJob
    {
        // TODO: Implement recurring job
        host.ConfigureServices(services => 
            services.AddTransient<ILifecycleParticipant<ISiloLifecycle>>(
                sp =>
                {
                    var jobType = typeof(TJob);
                    var jobName = jobType.Name;
                    return new RegisterReminderLifecycleParticipant(sp, jobType, jobName, interval);
                }));
        return host;
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
        LifecycleExtensions.Subscribe<StartupTask>(lifecycle,
            _stage,
            cancellation => _startupTask(_serviceProvider, cancellation));
    }
}

internal class RegisterReminderLifecycleParticipant(
    IServiceProvider serviceProvider,
    Type jobType,
    string jobName,
    TimeSpan period)
    : ILifecycleParticipant<ISiloLifecycle>
{
    public void Participate(ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe(
            $"StartScheduledJob_{jobName}",
            ServiceLifecycleStage.Active,
            async token =>
            {
                Console.WriteLine($"Scheduling reminder for job {jobName}...");
                // for each job, create a reminder
                var job = serviceProvider.GetService(jobType);
                if(job == null) throw new InvalidOperationException($"Job {jobType.Name} not found in service collection.");
                var grainFactory = serviceProvider.GetRequiredService<IJobBuilder>();
                var jobGrain = await grainFactory.StartJobAsync(job.GetType(), jobName);
                await jobGrain.ScheduleRecurringAsync(period);
            });
    }
}
