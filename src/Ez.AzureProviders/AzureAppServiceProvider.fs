namespace Ez.AzureProviders

open System
open Ez.Core
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Orleans.Hosting


module Environment =
    let GetEnvironmentVariable name =
        Environment.GetEnvironmentVariable name
        |> Option.ofObj
type AzureAppServiceProvider(name) =
    inherit InfrastructureProvider<AzureAppServiceCommand>(name)

    override this.DeployCommandDescription = "Create ARM template for Azure App Service"
    override this.RunCommandDescription = "Run the application in Azure App Service"

    override this.Configuration(configuration: IConfigurationBuilder) =
        let env = 
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") 
            |> Option.orElseWith (fun () -> Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
            |> Option.defaultValue "Development"
        
        configuration.AddJsonFile("appsettings.json", optional = true, reloadOnChange = true)
                    .AddJsonFile($"appsettings.{env}.json", optional = true, reloadOnChange = true)
                    .AddEnvironmentVariables() |> ignore

    override this.ConfigureServices(config: IConfiguration, services: IServiceCollection) = ()

    override this.ConfigureSilos(config: IConfiguration, services: IServiceCollection, siloBuilder: ISiloBuilder) =
        siloBuilder.UseLocalhostClustering() |> ignore
        siloBuilder.AddMemoryGrainStorageAsDefault() |> ignore
        siloBuilder.UseInMemoryReminderService() |> ignore

    override this.ConfigureApplicationBuilder(config, app) = ()
