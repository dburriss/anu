using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

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
