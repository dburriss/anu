using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace Ez.Usecases;

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
