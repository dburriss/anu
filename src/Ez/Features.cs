using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Ez.Jobs;
using Ez.Jobs.Triggers;
using Ez.Queries;
using Ez.Queries.Triggers;
using Ez.Usecases;
using Ez.Usecases.Triggers;

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
    // Queries
    private readonly List<QueryTrigger> _queryTriggers = new();
    public IReadOnlyList<QueryTrigger> QueryTriggers => _queryTriggers.ToImmutableList();
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

    public void WithQuery<TQuery,TParams, TResult>(
        Func<QueryTriggers<TQuery,TParams,TResult>, QueryTriggers<TQuery,TParams,TResult>> triggers)
        where TQuery : Query<TParams, TResult>
    {
        var configuredTriggers = triggers(new QueryTriggers<TQuery,TParams,TResult>());
        var queryTriggers = configuredTriggers.ToQueryTriggers();
        _queryTriggers.AddRange(queryTriggers);
    }
}

public record SystemDescriptor(
    string Name,
    bool IsLocal,
    IReadOnlyList<FeatureDescriptor> Features);

