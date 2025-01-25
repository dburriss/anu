using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using Ez.Queues;

using Microsoft.AspNetCore.Http;

using Orleans.Serialization;

namespace Ez.Usecases.Triggers;

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
    
    public virtual async Task TriggerAsync(Usecase usecase, QueueMessage message)
    {
        await usecase.Execute(await Mapper(message));
    }
}

/// <summary>
/// Fluent API for configuring usecase triggers
/// </summary>
/// <typeparam name="TUsecase"></typeparam>
/// <typeparam name="TCommand"></typeparam>
public class UsecaseTriggers<TUsecase, TCommand> where TUsecase: Usecase<TCommand>
{
    private Dictionary<string,EndpointTriggerOptions<TCommand>> _putTriggerOptionsMap = new();
    private Dictionary<string, QueueTriggerOptions<TCommand>> _queueTriggerOptionsMap = new();

    public UsecaseTriggers<TUsecase, TCommand> AddPut(
        string path,
        Action<EndpointTriggerOptions<TCommand>>? options = null)
    {
        var opt = new EndpointTriggerOptions<TCommand>();
        options?.Invoke(opt);
        _putTriggerOptionsMap.Add(path, opt);
        return this;
    }

    public UsecaseTriggers<TUsecase, TCommand> AddQueue(string queueName, Action<QueueTriggerOptions<TCommand>>? options = null)
    {
        var opt = new QueueTriggerOptions<TCommand>();
        options?.Invoke(opt);
        _queueTriggerOptionsMap.Add(queueName, opt);
        return this;
    }

    public IEnumerable<UsecaseTrigger> ToUsecaseTriggers()
    {
        // Put triggers
        foreach (var (path, options) in _putTriggerOptionsMap)
        {
            var t = typeof(TUsecase);
            Func<HttpContext, Task<object>> mapper = async ctx => (await options.Mapper(ctx))!;
            var endpointOptions = new EndpointOptions(){
                DefaultStatusCode = options.DefaultStatusCode,
            };
            yield return new EndpointTrigger(t, path, "PUT", mapper, endpointOptions);
        }
        
        // Queue triggers
        foreach (var (queueName, options) in _queueTriggerOptionsMap)
        {
            var t = typeof(TUsecase);
            Func<QueueMessage, Task<object>> mapper = async msg => (await options.Mapper(msg))!;
            var opts = new QueueTriggerOptions()
            {
                BatchSize = options.BatchSize,
            };
            yield return new QueueTrigger(t, queueName, mapper, opts);
        }
        
    }
}

// === ENDPOINTS ===
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
        TriggerName = $"endpoint_trigger__{method.ToLower()}_{usecaseType.Name.ToLower()}";
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

// === QUEUES ===
/// <summary>
/// A queue trigger for a usecase
/// </summary>
public class QueueTrigger : UsecaseTrigger
{
    public string QueueName { get; }

    public QueueTrigger(Type usecaseType, string queueName, Func<QueueMessage, Task<object>> mapper, QueueTriggerOptions options)
    {
        UsecaseType = usecaseType;
        QueueName = queueName;
        TriggerName = $"queue_trigger__{queueName}_{usecaseType.Name.ToLower()}";
        Mapper = o => mapper((QueueMessage)o);
    }
    
    public override async Task TriggerAsync(Usecase usecase, QueueMessage message)
    {
        // todo: handle exceptions and retries
        await base.TriggerAsync(usecase, message);
    }
}

/// <summary>
/// Options for configuring a queue trigger
/// </summary>
public class QueueTriggerOptions
{
    public int BatchSize { get; set; } = 1;
}

/// <summary>
/// Fluent configuration of options for queue triggers
/// </summary>
/// <typeparam name="TCommand"></typeparam>
public class QueueTriggerOptions<TCommand>
{
    public Func<QueueMessage, Task<TCommand?>> Mapper { get; set; } = 
        msg => Task.FromResult(JsonSerializer.Deserialize<TCommand>(msg.Content));
    public int BatchSize { get; set; } = 1;
    
    public static implicit operator QueueTriggerOptions<object>(QueueTriggerOptions<TCommand> options)
    {
        return new QueueTriggerOptions<object>
        {
            Mapper = async ctx => await options.Mapper(ctx),
            BatchSize = options.BatchSize,
        };
    }
}
