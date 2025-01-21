using System;
using System.Collections.Generic;

using Ez.AWS;
using Ez.Jobs;

using Spectre.Console.Cli;

namespace Ez;

public class EzSystem(string name)
{
    private bool _isLocal = true;
    private readonly List<Tuple<Type, TimeSpan>> _recurringJobs = new();
    private readonly List<EndpointTriggerDescriptor> _endpointTriggers = new();

    public static EzSystem Create(string name)
    {
        return new EzSystem(name);
    }

    public EzSystem UseLocal(bool isLocal = true)
    {
        _isLocal = isLocal;
        return this;
    }

    public EzSystem WithRecurringJob<TJob>(TimeSpan interval) where TJob: IJob
    {
        //todo: put into default feature
        _recurringJobs.Add(Tuple.Create(typeof(TJob), interval));
        return this;
    }

    public EzSystem Feature(Action<FeatureDescriptor> feature)
    {
        var descriptor = new FeatureDescriptor();
        feature(descriptor);
        
        if (descriptor.Title == null)
            descriptor.Title = "Default";
        
        if (descriptor.Description == null)
            descriptor.Description = "Default feature";
        
        return this;
    }
    
    public int Run(string[] args)
    {
        var descriptor = new SystemDescriptor(
            name,
            _isLocal,
            _recurringJobs,
            _endpointTriggers);

        var cli = new CommandApp();
        cli.Configure(config =>
        {
            config.SetApplicationName(descriptor.Name);
            config.AddCommand<LaunchCommand>("launch")
                .WithAlias("run")
                .WithDescription("Launch the system")
                .WithExample("launch", "-e", "production", "--local")
                .WithData(descriptor);

            config.AddCommand<CdkCommand>("cdk")
                .WithAlias("deploy")
                .WithDescription("Deploy the system")
                .WithExample("cdk", "-e", "production")
                .WithData(descriptor);

            #if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
            #endif
        });
        return cli.Run(args);
    }

}
