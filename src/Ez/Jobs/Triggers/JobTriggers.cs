using System;
using System.Collections.Generic;

namespace Ez.Jobs.Triggers;

/// <summary>
/// Fluent API for adding triggers to a job
/// </summary>
public class JobTriggers
{
    private List<TimerOptions> _timerOptions = new();

    public JobTriggers AddTimer(Func<TimerTriggerBuilder, TimerTriggerBuilder> configure)
    {
        var builder = new TimerTriggerBuilder();
        configure(builder);
        var timerOptions = builder.Build();
        _timerOptions.Add(timerOptions);
        return this;
    }
    
    public JobTriggers AddTimer(TimeSpan interval)
    {
        _timerOptions.Add(new TimerOptions { Interval = interval });
        
        return this;
    }

    public IEnumerable<JobTrigger> ToJobTriggers(Type type)
    {
        foreach (var timerOption in _timerOptions)
        {
            yield return new JobTimerTrigger(type, timerOption);
        }
    }
}

/// <summary>
/// Fluent API for configuring a timer trigger
/// </summary>
public class TimerTriggerBuilder
{
    private TimeSpan _interval = TimeSpan.FromMinutes(60);
    private int _maxRetries = 3;
    private bool _autoRetry = false;

    public TimerTriggerBuilder AutoRetry(bool autoRetry = true)
    {
        _autoRetry = autoRetry;
        return this;
    }

    public TimerTriggerBuilder MaxRetries(int maxRetries)
    {
        _autoRetry = true;
        _maxRetries = maxRetries;
        return this;
    }

    public TimerTriggerBuilder EveryMinutes(int minutes)
    {
        _interval = TimeSpan.FromMinutes(minutes);
        return this;
    }
    
    public TimerTriggerBuilder EveryHours(int hours)
    {
        _interval = TimeSpan.FromHours(hours);
        return this;
    }
    
    public TimerTriggerBuilder Cron(string cron)
    {
        throw new NotImplementedException();
        return this;
    }

    public TimerTriggerBuilder Enrich(Action<IServiceProvider, IJobContext> enrich)
    {
        return this;
    }

    public TimerOptions Build()
    {
        return new TimerOptions()
        {
            MaxRetries = _maxRetries,
            Interval = _interval,
            AutoRetry = _autoRetry
        };
    }
}

public abstract class JobTrigger
{
    public Type JobType { get; protected set; }
    public string TriggerName { get; protected set; }
}

public class JobTimerTrigger : JobTrigger
{
    public TimerOptions Options { get; }
    public JobTimerTrigger(Type jobType, TimerOptions options)
    {
        JobType = jobType;
        Options = options;
        TriggerName = $"timer__{jobType.Name}";
    }
}

public class TimerOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(60);
    public bool AutoRetry { get; set; }
}
