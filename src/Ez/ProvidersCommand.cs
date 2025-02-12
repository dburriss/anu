using System;

using Spectre.Console.Cli;

namespace Ez;

public class ProvidersCommand: Command<ProvidersCommand.Settings>
{
    public override int Execute(CommandContext context, Settings settings)
    {
        var providers = context.Data as System.Collections.Generic.Dictionary<string, Type>;
        if (providers == null) throw new InvalidOperationException("Providers not found.");
        foreach (var provider in providers)
            Console.WriteLine($"{provider.Key}: {provider.Value.Name}");
        return 0;
    }

    public class Settings: CommandSettings
    {
    }
}
