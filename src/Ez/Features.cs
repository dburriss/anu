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
    private JobTriggers _jobTriggers = new();
    public string? Title { get; set; }
    public string? Description { get; set; }
    private readonly List<UsecaseTrigger> _usecaseTriggers = new();
    public IReadOnlyList<UsecaseTrigger> UsecaseTriggers => _usecaseTriggers.ToImmutableList();
    
    public void WithJob<TJob>(Func<JobTriggers, JobTriggers> trigger) where TJob: IJob
    {
        _jobTriggers = trigger(_jobTriggers);
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

public class JobTriggers
{
    public JobTriggers AddTimer(Func<TimerTriggerBuilder, TimerTriggerBuilder> configure)
    {
        return this;
    }
}

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

public class EndpointTrigger : UsecaseTrigger
{
    private readonly EndpointOptions _options;
    public string Path { get; }
    public string Method { get; }
    
    public EndpointTrigger(Type usecaseType, string path, string method, Func<HttpContext, Task<object>> mapper, EndpointOptions options)
    {
        _options = options;
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
        httpContext.Response.StatusCode = _options.DefaultStatusCode;
    }
}

public class EndpointOptions
{
    public int DefaultStatusCode { get; set; } = 200;
}

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
/// Configuration options for endpoint triggers
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
    IReadOnlyList<FeatureDescriptor> Features);

