using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Orleans;
using Orleans.Runtime;

namespace Ez;

public interface IJobGrain: IGrainWithStringKey, IRemindable
{
    Task<bool> ActivateAsync(JobDefinition jobDefinition, CancellationToken cancellationToken);
    Task CancelAsync();
    Task<JobStatus> StatusAsync();
    Task CompleteAsync();
    Task FailAsync();
    Task ScheduleRecurringAsync(TimeSpan period);
}

public class JobGrain: Grain<JobState>, IJobGrain, IRemindable
{
    private readonly IGrainContextAccessor _grainContextAccessor;
    private readonly ILogger<JobGrain> _logger;
    private readonly IServiceProvider _serviceProvider;
    private Task? _task;

    public JobGrain(
        IGrainContextAccessor grainContextAccessor,
        IServiceProvider serviceProvider,
        ILogger<JobGrain> logger)
    {
        _grainContextAccessor = grainContextAccessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<bool> ActivateAsync(JobDefinition jobDefinition, CancellationToken cancellationToken)
    {
        // Do not start if already running
        if (_task != null) return Task.FromResult(false);
        // Only set definition if not set 
        if (State.JobDefinition != null) return Task.FromResult(false);
        State.JobDefinition = jobDefinition;
        State.Status.CurrentStatus = JobStatusEnum.Active;
        return Task.FromResult(true);
    }

    public Task CancelAsync()
    {
        throw new NotImplementedException();
    }

    public Task<JobStatus> StatusAsync()
    {
        State.Status.CurrentRunDuration = DateTime.UtcNow - State.Status.CurrentRunStartedAt;
        var jobStatus = State.Status;
        return Task.FromResult(jobStatus);
    }

    public async Task CompleteAsync()
    {
        if (State.Status.CurrentStatus != JobStatusEnum.Running) return;

        _task = null;
        await CompletedState();
        await WriteStateAsync();
        var reminder = await this.GetReminder($"{this.GetPrimaryKeyString()}_status_reminder");
        if (reminder != null)
            await this.UnregisterReminder(reminder);
    }

    public async Task FailAsync()
    {
        if (State.Status.CurrentStatus != JobStatusEnum.Running) return;
        await FailedState();
        _task = null;
        var reminder = await this.GetReminder($"{this.GetPrimaryKeyString()}_status_reminder");
        if (reminder != null)
            await this.UnregisterReminder(reminder);
    }

    public Task ScheduleRecurringAsync(TimeSpan period)
    {
        return this.RegisterOrUpdateReminder($"{this.GetPrimaryKeyString()}_trigger_reminder", TimeSpan.Zero, period);
    }

    Task IRemindable.ReceiveReminder(string reminderName, TickStatus status)
    {
        Console.WriteLine($"[{this.GetPrimaryKeyString()}<{State.Status.CurrentStatus}>]Received reminder {reminderName}...");
        if (reminderName == $"{this.GetPrimaryKeyString()}_status_reminder")
        {
            Console.WriteLine($"[{this.GetPrimaryKeyString()}]Updating status...");
            State.Status.CurrentRunDuration = DateTime.UtcNow - State.Status.CurrentRunStartedAt;
            return WriteStateAsync();
        }

        if (reminderName == $"{this.GetPrimaryKeyString()}_trigger_reminder")
        {
            Console.WriteLine($"[{this.GetPrimaryKeyString()}]Triggering job...");
            if (State.Status.CurrentStatus != JobStatusEnum.Active) return Task.CompletedTask;
            return TriggerJobAsync(CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    private async Task TriggerJobAsync(CancellationToken cancellationToken)
    {
        if (State.Status!.CurrentStatus == JobStatusEnum.InActive || _task != null) return;
        if (_task == null)
        {
            _task = Task.FromResult(false);
        }

        _logger.LogInformation("Starting job {JobName}...", this.GetPrimaryKeyString());
        await RunningState();
        var job = _serviceProvider.GetService(State.JobDefinition.JobType) as IJob;
        if (job == null) throw new InvalidOperationException("Job type not found.");
        _task = CreateTask(job, cancellationToken, TaskScheduler.Current);
        await this.RegisterOrUpdateReminder($"{this.GetPrimaryKeyString()}_status_reminder",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    private Task CreateTask(IJob job, CancellationToken cancellationToken, TaskScheduler taskScheduler)
    {
        return Task.Run(async () =>
            {
                try
                {
                    await job.Execute(new JobContext());
                    await InvokeGrainAsync(taskScheduler, grain => grain.CompleteAsync());
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await InvokeGrainAsync(taskScheduler, grain => grain.FailAsync());
                }
            },
            cancellationToken);
    }

    private Task InvokeGrainAsync(TaskScheduler taskScheduler, Func<IJobGrain, Task> action)
    {
        return Task.Factory.StartNew(async () =>
            {
                var grain = GrainFactory.GetGrain<IJobGrain>(this.GetPrimaryKeyString());
                await action(grain);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            taskScheduler);
    }

    private async Task RunningState()
    {
        State.Status.CurrentStatus = JobStatusEnum.Running;
        State.Status.CurrentRunId = Guid.NewGuid();
        State.Status.CurrentRunStartedAt = DateTime.UtcNow;
        await WriteStateAsync();
    }

    private async Task CompletedState()
    {
        State.Status.LastStatus = JobStatusEnum.Completed;
        State.Status.LastRunId = State.Status.CurrentRunId;
        State.Status.LastRunStartedAt = State.Status.CurrentRunStartedAt;
        State.Status.LastRunDuration = DateTime.UtcNow - State.Status.CurrentRunStartedAt;

        // reset current run
        State.Status.CurrentStatus = JobStatusEnum.Active;
        State.Status.CurrentRunId = null;
        State.Status.CurrentRunStartedAt = null;
        State.Status.CurrentRunDuration = null;

        await WriteStateAsync();
    }

    private Task FailedState()
    {
        State.Status.LastStatus = JobStatusEnum.Failed;
        State.Status.LastRunId = State.Status.CurrentRunId;
        State.Status.LastRunStartedAt = State.Status.CurrentRunStartedAt;
        State.Status.LastRunDuration = DateTime.UtcNow - State.Status.CurrentRunStartedAt;

        // reset current run
        State.Status.CurrentStatus = JobStatusEnum.Active;
        State.Status.CurrentRunId = null;
        State.Status.CurrentRunStartedAt = null;
        State.Status.CurrentRunDuration = null;

        return WriteStateAsync();
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.Status == null) State.Status = new JobStatus(this.GetPrimaryKeyString());

        await base.OnActivateAsync(cancellationToken);
    }

    protected override async Task WriteStateAsync()
    {
        await base.WriteStateAsync();
    }

    protected override async Task ReadStateAsync()
    {
        await base.ReadStateAsync();
    }
}
