using System;
using System.Collections.Generic;

using Orleans;

namespace Ez;

[GenerateSerializer]
public class JobDefinition
{
    public JobDefinition(Type jobType, string jobName, IDictionary<string, object> jobParameters)
    {
        JobParameters = jobParameters;
        JobType = jobType;
        JobName = jobName;
    }

    [Id(0)]public Type JobType { get; set; }
    [Id(1)]public string JobName { get; set; }
    [Id(2)]public IDictionary<string,object> JobParameters { get; set; }
}
