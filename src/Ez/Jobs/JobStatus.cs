using System;

using Orleans;

namespace Ez.Jobs;

[GenerateSerializer]
public class JobStatus
{
    public JobStatus(string jobName)
    {
        JobName = jobName;
    }

    [Id(0)]public string JobName { get; set; }
    [Id(1)]public JobStatusEnum CurrentStatus { get; set; } = JobStatusEnum.InActive;
    [Id(2)]public Guid? CurrentRunId { get; set; }
    [Id(3)]public DateTime? CurrentRunStartedAt { get; set; }
    [Id(4)]public TimeSpan? CurrentRunDuration { get; set; }
    
    [Id(5)]public JobStatusEnum LastStatus { get; set; }
    [Id(6)]public Guid? LastRunId { get; set; }
    [Id(7)]public DateTime? LastRunStartedAt { get; set; }
    [Id(8)]public TimeSpan? LastRunDuration { get; set; }
    [Id(9)]public DateTime? NextRun { get; set; }
}

public enum JobStatusEnum
{
    InActive,
    Active,
    Running,
    Completed,
    Failed
}
