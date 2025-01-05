using Orleans;

namespace Ez;

[GenerateSerializer]
public class JobState
{
    [Id(0)] public JobDefinition? JobDefinition { get; set; }
    [Id(1)] public JobStatus? Status { get; set; }
}
