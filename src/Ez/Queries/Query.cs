using System.Threading;
using System.Threading.Tasks;

namespace Ez.Queries;

public abstract class Query
{
    public abstract Task<object?> Execute(object? queryParams, CancellationToken cancellationToken = default);
    public abstract Task Compensate(object queryParams);
    
}

public abstract class Query<TParam, TResult> : Query
{
    public override Task<object?> Execute(object? queryParams, CancellationToken cancellationToken = default)
    {
        return Execute((TParam)queryParams, cancellationToken)
            .ContinueWith(t => (object)t.Result);
    }
    
    public override Task Compensate(object queryParams)
    {
        return Compensate((TParam)queryParams);
    }

    public abstract Task<TResult> Execute(TParam command, CancellationToken cancellationToken = default);
    public abstract Task Compensate(TParam command);
}
