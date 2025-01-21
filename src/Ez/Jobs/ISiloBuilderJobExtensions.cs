using System;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;

namespace Ez.Jobs;

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

    public static ISiloBuilder UseRecurringJob(this ISiloBuilder host, Type jobType, TimeSpan interval)
    {
        host.ConfigureServices(services =>
            services.AddTransient<ILifecycleParticipant<ISiloLifecycle>>(
                sp =>
                {
                    var jobName = jobType.Name;
                    return new RegisterReminderLifecycleParticipant(sp, jobType, jobName, interval);
                }));
        return host;
    }

    public static ISiloBuilder UseRecurringJob<TJob>(this ISiloBuilder host, TimeSpan interval) where TJob: IJob
    {
        var jobType = typeof(TJob);
        return host.UseRecurringJob(jobType, interval);
    }
}
