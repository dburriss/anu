using System;
using System.Collections.Generic;
using System.Linq;

using Ez.Core;
using Ez.Jobs;

using Spectre.Console.Cli;

namespace Ez;

public class EzSystem(string name)
{
    private readonly List<FeatureDescriptor> _features = new();
    private readonly Dictionary<string, Type> _providers  = new();

    public static EzSystem Create(string name)
    {
        return new EzSystem(name);
    }

    public EzSystem WithRecurringJob<TJob>(TimeSpan interval) where TJob: IJob
    {
        Feature(f => f.WithJob<TJob>(t => t.AddTimer(interval)));
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

        _features.Add(descriptor);
        return this;
    }

    public int Run(string[] args)
    {
        var cli = new CommandApp();
        cli.Configure(config =>
        {
            // maybe need to push the infra decision down a bit
            config.SetApplicationName(name);

            var launchCmd = config.AddCommand<LaunchCommand>("launch")
                .WithDescription("Launch the system")
                .WithExample("launch", "-e", "dev", "--local");

            var providers = new Dictionary<string, InfrastructureProvider>();
            // add local provider if no provider is specified
            if (args.Length == 0 || ContainsLocalArg(args) || _providers.Count == 0)
            {
                // add local provider to descriptors
                var infra = new LocalProvider();
                providers.Add("_local", infra);
                var descriptor = new SystemDescriptor(name, _features, infra);
                launchCmd.WithData(descriptor);
            }

            foreach (var providerKey in _providers.Keys)
            {
                //register the deployment commands
                var providerType = _providers[providerKey];
                var provider = Activator.CreateInstance(providerType, providerKey) as InfrastructureProvider;
                providers.Add(providerKey, provider!);
                provider!.RegisterDeployCommand(config);
            }

            if (args.Length > 0)
            {
                // if only 1 provider, and not --local, use it as default
                if (providers.Count == 1 || ContainsLocalArg(args))
                {
                    var provider = providers.First().Value;
                    var descriptor = new SystemDescriptor(name, _features, provider!);
                    launchCmd.WithData(descriptor);
                }
                // else look for the -p|--provider arg for the provider
                else
                {
                    var providerIndex = args.ToList().FindIndex(x => x == "-p" || x == "--provider");
                    if (providerIndex == -1 || providerIndex > args.Length - 1)
                    {
                        throw new ArgumentException("No provider specified");
                    }

                    var providerKey = args[providerIndex + 1];
                    if (!_providers.ContainsKey(providerKey))
                    {
                        throw new ArgumentException($"Provider {providerKey} not found");
                    }

                    var provider = providers[providerKey];
                    var descriptor = new SystemDescriptor(name, _features, provider!);
                    launchCmd.WithData(descriptor);
                }
            }
            // config.AddCommand<CdkCommand>("cdk")
            //     .WithAlias("deploy")
            //     .WithDescription("Deploy the system")
            //     .WithExample("cdk", "-e", "production")
            //     .WithData(descriptor);

            #if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
            #endif
        });
        
        return cli.Run(args);
    }

    public EzSystem AddProvider<TProvider>(string commandName)
    {
        _providers.Add(commandName, typeof(TProvider));
        return this;
    }
    

    private static bool ContainsLocalArg(string[] args)
    {
        return args.Contains("-l") || args.Contains("--local");
    }
}
