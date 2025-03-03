namespace Ez.AzureProviders

open System
open Ez.Core
open Spectre.Console.Cli
open Farmer
open Farmer.Builders
open Farmer.WebApp

type Settings() =
    inherit CommandSettings()
    
type AzureAppServiceCommand() =
    inherit Command<Settings>()

    override this.Execute(context: CommandContext, settings: Settings) =
        let provider = context.Data :?> InfrastructureProvider
        // need SytemDescriptor
        
        // use Farmer to generate ARM template

        // AppServicePlan
        // VNet
        // WebApp
        let mutable host = webApp {
            name "myWebApp"
            service_plan_name "myServicePlan"
            setting "myKey" "aValue"
            sku WebApp.Sku.B1
            always_on
            app_insights_off
            worker_size Medium
            number_of_workers 3
            run_from_package
            system_identity
        }

        // AppInsights
        // TableStorage
        // Monitor
        let deployment =
            arm {
                location Location.WestEurope
                add_resource host
            }
        
        deployment
        |> Writer.quickWrite "infra"
        
        0

