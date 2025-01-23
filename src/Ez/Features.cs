using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Ez.Jobs;

using Microsoft.AspNetCore.Http;

namespace Ez;


public class FeatureDescriptor
{
    
    public string? Title { get; set; }
    public string? Description { get; set; }
    
    // Jobs
    private readonly List<JobTrigger> _jobTriggers = new();
    public IReadOnlyList<JobTrigger> JobTriggers => _jobTriggers.ToImmutableList();
    // Usecases
    private readonly List<UsecaseTrigger> _usecaseTriggers = new();
    public IReadOnlyList<UsecaseTrigger> UsecaseTriggers => _usecaseTriggers.ToImmutableList();
    
    public void WithJob<TJob>(Func<JobTriggers, JobTriggers> trigger) where TJob: IJob
    {
        var jobTriggers = trigger(new JobTriggers());
        var triggers = jobTriggers.ToJobTriggers(typeof(TJob));
        _jobTriggers.AddRange(triggers);
    }

    public void WithUsecase<TUsecase, TCommand>(
        Func<UsecaseTriggers<TUsecase, TCommand>, UsecaseTriggers<TUsecase, TCommand>> triggers)
        where TUsecase: Usecase<TCommand>
    {
        // todo: save implementation detail of this feature
        var usecaseCommandTriggers = triggers(new UsecaseTriggers<TUsecase, TCommand>());
        var usecaseTriggers = usecaseCommandTriggers.ToUsecaseTriggers();
        
        _usecaseTriggers.AddRange(usecaseTriggers);
    }
}

// === JOBS ===

/// <summary>
/// Fluent API for adding triggers to a job
/// </summary>
public class JobTriggers
{
    private List<TimerOptions> _timerOptions = new();

    public JobTriggers AddTimer(Func<TimerTriggerBuilder, TimerTriggerBuilder> configure)
    {
        var builder = new TimerTriggerBuilder();
        configure(builder);
        var timerOptions = builder.Build();
        _timerOptions.Add(timerOptions);
        return this;
    }
    
    public JobTriggers AddTimer(TimeSpan interval)
    {
        _timerOptions.Add(new TimerOptions { Interval = interval });
        
        return this;
    }

    public IEnumerable<JobTrigger> ToJobTriggers(Type type)
    {
        foreach (var timerOption in _timerOptions)
        {
            yield return new JobTimerTrigger(type, timerOption);
        }
    }
}

/// <summary>
/// Fluent API for configuring a timer trigger
/// </summary>
public class TimerTriggerBuilder
{
    private TimeSpan _interval = TimeSpan.FromMinutes(60);
    private int _maxRetries = 3;
    private bool _autoRetry = false;

    public TimerTriggerBuilder AutoRetry(bool autoRetry = true)
    {
        _autoRetry = autoRetry;
        return this;
    }

    public TimerTriggerBuilder MaxRetries(int maxRetries)
    {
        _autoRetry = true;
        _maxRetries = maxRetries;
        return this;
    }

    public TimerTriggerBuilder EveryMinutes(int minutes)
    {
        _interval = TimeSpan.FromMinutes(minutes);
        return this;
    }
    
    public TimerTriggerBuilder EveryHours(int hours)
    {
        _interval = TimeSpan.FromHours(hours);
        return this;
    }
    
    public TimerTriggerBuilder Cron(string cron)
    {
        throw new NotImplementedException();
        return this;
    }

    public TimerTriggerBuilder Enrich(Action<IServiceProvider, IJobContext> enrich)
    {
        return this;
    }

    public TimerOptions Build()
    {
        return new TimerOptions()
        {
            MaxRetries = _maxRetries,
            Interval = _interval,
            AutoRetry = _autoRetry
        };
    }
}

public abstract class JobTrigger
{
    public Type JobType { get; protected set; }
    public string TriggerName { get; protected set; }
}

public class JobTimerTrigger : JobTrigger
{
    public TimerOptions Options { get; }
    public JobTimerTrigger(Type jobType, TimerOptions options)
    {
        JobType = jobType;
        Options = options;
        TriggerName = $"timer__{jobType.Name}";
    }
}

public class TimerOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(60);
    public bool AutoRetry { get; set; }
}

// === USECASES ===
public abstract class Usecase{
    public abstract Task Execute(object command, CancellationToken cancellationToken = default);
    public abstract Task Compensate(object command);
}

public abstract class Usecase<TCommand> : Usecase
{
    public override Task Execute(object command, CancellationToken cancellationToken = default)
    {
        return Execute((TCommand)command, cancellationToken);
    }
    
    public override Task Compensate(object command)
    {
        return Compensate((TCommand)command);
    }

    public abstract Task Execute(TCommand command, CancellationToken cancellationToken = default);

    public Task Compensate(TCommand command)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A trigger for a usecase
/// </summary>
public abstract class UsecaseTrigger
{
    public Type UsecaseType { get; protected set; }
    public string TriggerName { get; protected set; }
    public Func<object, Task<object>> Mapper { get; protected set; } = Task.FromResult;
    
    public virtual async Task TriggerAsync(Usecase usecase, HttpContext httpContext)
    {
        await usecase.Execute(await Mapper(httpContext));
    }
}

/// <summary>
/// A endpoint trigger for a usecase
/// </summary>
public class EndpointTrigger : UsecaseTrigger
{
    public EndpointOptions Options { get; }
    public string Path { get; }
    public string Method { get; }

    public EndpointTrigger(Type usecaseType, string path, string method, Func<HttpContext, Task<object>> mapper, EndpointOptions options)
    {
        Options = options;
        UsecaseType = usecaseType;
        Func<object,Task<object>> mapperFunc = async (o) => await mapper((HttpContext)o);
        Path = path;
        Method = method;
        TriggerName = $"endpoint__{method.ToLower()}_{usecaseType.Name}";
        Mapper = mapperFunc;
    }

    public override async Task TriggerAsync(Usecase usecase, HttpContext httpContext)
    {
        await base.TriggerAsync(usecase, httpContext);
        httpContext.Response.StatusCode = Options.DefaultStatusCode;
    }
}

/// <summary>
/// Options for configuring an endpoint trigger
/// </summary>
public class EndpointOptions
{
    public int DefaultStatusCode { get; set; } = 200;
}

/// <summary>
/// Fluent API for configuring usecase triggers
/// </summary>
/// <typeparam name="TUsecase"></typeparam>
/// <typeparam name="TCommand"></typeparam>
public class UsecaseTriggers<TUsecase, TCommand> where TUsecase: Usecase<TCommand>
{
    private Dictionary<string,EndpointTriggerOptions<TCommand>> _putTriggerOptionsMap = new();

    public UsecaseTriggers<TUsecase, TCommand> AddPut(
        string path,
        Action<EndpointTriggerOptions<TCommand>>? options = null)
    {
        var opt = new EndpointTriggerOptions<TCommand>();
        options?.Invoke(opt);
        _putTriggerOptionsMap.Add(path, opt);
        return this;
    }

    public UsecaseTriggers<TUsecase, TCommand> AddQueue(string queueName)
    {
        return this;
    }

    public IEnumerable<UsecaseTrigger> ToUsecaseTriggers()
    {
        foreach (var (path, options) in _putTriggerOptionsMap)
        {
            var t = typeof(TUsecase);
            Func<HttpContext, Task<object>> mapper = async ctx => (await options.Mapper(ctx))!;
            var endpointOptions = new EndpointOptions(){
                DefaultStatusCode = options.DefaultStatusCode,
            };
            yield return new EndpointTrigger(t, path, "PUT", mapper, endpointOptions);
        }
    }
}

/// <summary>
/// Fluent configuration of options for endpoint triggers
/// </summary>
/// <typeparam name="TCommand"></typeparam>
public class EndpointTriggerOptions<TCommand>
{
    public Func<HttpContext, Task<TCommand?>> Mapper { get; set; } =
        async (ctx) => await ctx.Request.ReadFromJsonAsync<TCommand>();
    public int DefaultStatusCode { get; set; } = 200;
    
    public static implicit operator EndpointTriggerOptions<object>(EndpointTriggerOptions<TCommand> options)
    {
        return new EndpointTriggerOptions<object>
        {
            Mapper = async ctx => await options.Mapper(ctx),
            DefaultStatusCode = options.DefaultStatusCode,
        };
    }
}

public record SystemDescriptor(
    string Name,
    bool IsLocal,
    IReadOnlyList<FeatureDescriptor> Features);

