using System;

namespace Ez;

public interface IJobContext
{
    string JobName { get; }
    Guid RunId { get; }
}

public class JobContext : IJobContext
{
    public required string JobName { get; set; }
    public Guid RunId { get; set; }
}
