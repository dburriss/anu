using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Orleans;

namespace Ez;
public interface IJobBuilder
{
    Task<IJobGrain> StartJobAsync(Type jobType, string? name);
    Task<IJobGrain> StartJobAsync<TJob>(string? name);
}

public class JobBuilder: IJobBuilder
{
    private readonly IGrainFactory _grainFactory;
    private readonly Func<string,string> _jobName;

    public JobBuilder(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
        _jobName = jobName => $"job__{jobName}";
    }
        
    public async Task<IJobGrain> StartJobAsync(Type jobType, string? name = null)
    {
        var jobName = name == null ? _jobName(jobType.Name) : _jobName(name);
        var jobGrain = _grainFactory.GetGrain<IJobGrain>(jobName);
        var jobDefinition = new JobDefinition(jobType, jobName, new Dictionary<string, object>());
        await jobGrain.ActivateAsync(jobDefinition, CancellationToken.None);
        return jobGrain;
    }
    
    public async Task<IJobGrain> StartJobAsync<TJob>(string? name = null)
    {
        return await StartJobAsync(typeof(TJob), name);
    }
}

public static class JobBuilderExtensions
{
    /// <summary>
    /// Provides an IJobBuilder which can be used to prepare and execute a job. Name defaults to the job type name.
    ///
    /// <para name="grainFactory"></para>
    /// <returns></returns>
    public static Task<IJobGrain> CreateJobGrain<TJob>(this IGrainFactory grainFactory, string? name = null) 
    {
        var builder = new JobBuilder(grainFactory);
        return builder.StartJobAsync<TJob>();
    }
    
    /// <summary>
    ///     Returns a job instance.
    /// </summary>
    /// <param name="grainFactory"></param>
    /// <param name="jobName">The name of the job</param>
    /// <returns></returns>
    public static IJobGrain GetJobGrain(this IGrainFactory grainFactory, string jobName)
    {
        return grainFactory.GetGrain<IJobGrain>($"job__{jobName}");
    }
}

