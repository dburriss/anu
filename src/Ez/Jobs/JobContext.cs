using System;
using System.Collections.Generic;

namespace Ez.Jobs;

public interface IJobContext
{
    string JobName { get; }
    Guid RunId { get; }
    IDictionary<string,string> Data { get; }
}

public class JobContext : IJobContext
{
    public required string JobName { get; set; }
    public Guid RunId { get; set; }
    public IDictionary<string, string> Data { get; set; }
}
