using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Ez.Jobs;

using Microsoft.AspNetCore.Http;

namespace Ez;


public class FeatureDescriptor
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    public FeatureDescriptor WithJob<TJob>(Func<JobTriggers, JobTriggers> trigger) where TJob: IJob
    {
        // todo: save job
        return this;
    }

    public FeatureDescriptor WithUsecase<TUsecase, TCommand>(
        Func<UsecaseTriggers<TUsecase, TCommand>, UsecaseTriggers<TUsecase, TCommand>> triggers)
        where TUsecase: Usecase<TCommand>
    {
        // todo: save triggers
        return this;
    }
}

public abstract class Usecase<TCommand>
{
    public abstract Task Execute(TCommand command);

    public Task Compensate(TCommand command)
    {
        return Task.CompletedTask;
    }
}

public abstract class Trigger
{
}
public abstract class UsecaseTrigger
{
    public abstract Task TriggerAsync(HttpContext httpContext);
}
public class JobTriggers
{
    private List<Trigger> _triggers = new();

    public JobTriggers AddTimer(Func<TimerTriggerBuilder, TimerTriggerBuilder> configure)
    {
        return this;
    }
}

public class UsecaseTriggers<TUsecase, TCommand> where TUsecase: Usecase<TCommand>
{
    private Dictionary<string,UsecaseTriggerOptions<TCommand>> _putTriggerOptionsMap = new();

    public UsecaseTriggers<TUsecase, TCommand> AddPut(
        string path,
        Action<UsecaseTriggerOptions<TCommand>>? options = null)
    {
        var opt = new UsecaseTriggerOptions<TCommand>();
        options?.Invoke(opt);
        _putTriggerOptionsMap.Add(path, opt);
        return this;
    }

    public UsecaseTriggers<TUsecase, TCommand> AddQueue(string queueName)
    {
        return this;
    }
}

public class UsecaseTriggerOptions<TCommand>
{
    public Func<IServiceProvider, HttpContext, Task<TCommand?>> Mapper { get; set; } =
        async (_, ctx) => await ctx.Request.ReadFromJsonAsync<TCommand>();
    public int DefaultStatusCode { get; set; } = 200;
    public int DefaultErrorCode { get; set; } = 500;
}

public class TimerTriggerBuilder
{
    public TimerTriggerBuilder AutoRetry()
    {
        return this;
    }

    public TimerTriggerBuilder MaxFailures(int maxFailures)
    {
        return this;
    }

    public TimerTriggerBuilder EveryMinutes(int minutes)
    {
        return this;
    }

    public TimerTriggerBuilder Enrich(Action<IServiceProvider, IJobContext> enrich)
    {
        return this;
    }
}

public record SystemDescriptor(
    string Name,
    bool IsLocal,
    IReadOnlyList<Tuple<Type, TimeSpan>> RecurringJobs,
    IReadOnlyList<EndpointTriggerDescriptor> EndpointTriggers);

public record EndpointTriggerDescriptor(
    string Method,
    string Path,
    string TriggerName,
    Type TriggerType);
