using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace Ez.Queries.Triggers;

public abstract class QueryTrigger
{
    public Type QueryType { get; protected set; }
    public string TriggerName { get; protected set; }
    public Func<HttpContext, Task<object?>> InputMapper { get; protected init; }
    public Func<object?, Task<object?>> OutputMapper { get; protected init; }
    public QueryEndpointTriggerOptions Options { get; protected init; } = new();

    public virtual async Task<object?> TriggerAsync(Query query, HttpContext httpContext)
    {
        return await OutputMapper(await query.Execute(await InputMapper(httpContext)));
    }
}

public class QueryEndpointTrigger: QueryTrigger
{
    public QueryEndpointTrigger(
        Type queryType,
        string path,
        string method,
        Func<HttpContext, Task<object?>> inputMapper,
        Func<object, Task<object?>> outputMapper)
    {
        QueryType = queryType;
        Path = path;
        Method = method;
        InputMapper = inputMapper;
        OutputMapper = outputMapper;
    }

    public string Path { get; init; }
    public string Method { get; init; }
}

public class QueryTriggers<TQuery, TParams, TResult> where TQuery: Query<TParams, TResult>
{
    private readonly Dictionary<string, QueryEndpointTriggerOptions<TParams, TResult>> _endpointTriggers = new();

    public QueryTriggers<TQuery, TParams, TResult> AddGet(
        string path,
        Action<QueryEndpointTriggerOptions<TParams, TResult>>? options = null)
    {
        var trigger = new QueryEndpointTriggerOptions<TParams, TResult>();
        options?.Invoke(trigger);
        _endpointTriggers.Add(path, trigger);
        return this;
    }

    public IEnumerable<QueryTrigger> ToQueryTriggers()
    {
        foreach (KeyValuePair<string, QueryEndpointTriggerOptions<TParams, TResult>> kvp in _endpointTriggers)
        {
            Func<HttpContext, Task<object?>> inputMapper = async ctx => await kvp.Value.InputMapper(ctx);
            Func<object, Task<object?>> outputMapper = async result => await kvp.Value.OutputMapper((TResult)result);
            yield return new QueryEndpointTrigger(typeof(TQuery), kvp.Key, "GET", inputMapper, outputMapper);
        }
    }
}

public class QueryEndpointTriggerOptions<TParam, TResult>
{
    public Func<HttpContext, Task<TParam?>> InputMapper { get; set; } =
        async ctx => (TParam)(await Task.FromResult<object?>(ctx))!;

    public Func<TResult, Task<object?>> OutputMapper { get; set; } =
        async result => await Task.FromResult<object>(result!);
}

public class QueryEndpointTriggerOptions
{
    public int DefaultStatusCode { get; set; } = 200;  
}
