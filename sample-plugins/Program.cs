using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Plugins.OpenApi.Extensions;
using Microsoft.Extensions.Logging;
using sample_plugins;
using Microsoft.SemanticKernel.Plugins.MsGraph.Connectors.CredentialManagers;
using Microsoft.Extensions.DependencyInjection;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var pluginNames = new[] { "MessagesPlugin"/*, "TodoPlugin", "ManagerPlugin"*/ };
        WriteSampleHeadingToConsole("MessagesPlugin", "meListMessages" /*"metodolistsListTasks"*/, new KernelArguments { { "_top", "1" } }, pluginNames);

        var config = GetConfiguration();
        var kernel = args.Length > 0 && args[0] == "--enable-logging" ? InitializeKernel(config, enableLogging: true)
            : InitializeKernel(config);

        await AddApiManifestPluginsAsync(kernel, config, pluginNames);

        PrintLine("Please submit your request: ");
        string goal = Console.ReadLine();

        PrintLine($".....Processing your request to {goal}.....");

        var planner = InitializePlanner();
        await ExecuteGoal(goal, planner, kernel);
    }

    static IConfigurationRoot GetConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();
        return configuration;
    }

    static Kernel InitializeKernel(IConfigurationRoot config, bool enableLogging = false)
    {
        var apiKey = config["AzureOpenAI:ApiKey"];
        var chatDeploymentName = config["AzureOpenAI:ChatDeploymentName"];
        var chatModelId = config["AzureOpenAI:ChatModelId"];
        var endpoint = config["AzureOpenAI:Endpoint"];

        if (apiKey == null || chatDeploymentName == null || chatModelId == null || endpoint == null)
        {
            PrintLine("Azure Endpoint, API Key, deployment name or model id not found. Skipping example...");
        }

        var builder = Kernel.CreateBuilder();
        if (enableLogging)
        {
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddFilter(level => true);
                loggingBuilder.AddConsole();
            });
        }

        return builder.AddAzureOpenAIChatCompletion(chatDeploymentName, endpoint, apiKey, chatModelId)
                     .Build();
    }

    static FunctionCallingStepwisePlanner InitializePlanner()
    {
        FunctionCallingStepwisePlannerOptions plannerConfig = new()
        {
            MaxIterations = 15,
            MaxTokens = 32000
        };

        return new FunctionCallingStepwisePlanner(plannerConfig);
    }

    static async Task ExecuteGoal(string goal, FunctionCallingStepwisePlanner planner, Kernel kernel)
    {
        var result = await planner.ExecuteAsync(kernel, goal);

        PrintLine("--------------------");
        PrintLine($"\nResult:\n{result.FinalAnswer}\n");
        PrintLine("--------------------");
    }

    static async Task AddApiManifestPluginsAsync(Kernel kernel, IConfiguration config, params string[] pluginNames)
    {
        var graphScopes = config.GetSection("MSGraph:Scopes").Get<string[]>()
            ?? throw new InvalidOperationException("Missing Scopes configuration for Microsoft Graph API.");

        LocalUserMSALCredentialManager credentialManager = await LocalUserMSALCredentialManager.CreateAsync().ConfigureAwait(false);

        var clientId = config.GetSection("MSGraph:ClientId").Get<string>();
        var tenantId = config.GetSection("MSGraph:TenantId").Get<string>();
        var scopes = config.GetSection("MSGraph:Scopes").Get<string[]>();
        var redirectUri = config.GetSection("MSGraph:RedirectUri").Get<Uri>();

        var token = await credentialManager.GetTokenAsync(clientId, tenantId, scopes, redirectUri).ConfigureAwait(false);
        BearerAuthenticationProviderWithCancellationToken authenticationProvider = new(() => Task.FromResult(token));

        foreach (var pluginName in pluginNames)
        {
            try
            {
                var manifestFilePath = Environment.CurrentDirectory + $"\\ApiManifestPlugins\\{pluginName}\\apimanifest.json";
                KernelPlugin plugin =
                await kernel.ImportPluginFromApiManifestAsync(
                    pluginName,
                    manifestFilePath,
                    new OpenApiFunctionExecutionParameters(authCallback: authenticationProvider.AuthenticateRequestAsync
                    , serverUrlOverride: new Uri("https://graph.microsoft.com/v1.0")))
                    .ConfigureAwait(false);
                PrintLine($">> {pluginName} is created.");
            }
            catch (Exception ex)
            {
                kernel.LoggerFactory.CreateLogger("Plugin Creation").LogError(ex, "Plugin creation failed. Message: {0}", ex.Message);
                throw new AggregateException($"Plugin creation failed for {pluginName}", ex);
            }
        }
    }

    static void WriteSampleHeadingToConsole(string pluginToTest, string functionToTest, KernelArguments? arguments, params string[] pluginsToLoad)
    {
        Console.WriteLine();
        PrintLine("======== [ApiManifest Plugins Sample] ========", ConsoleColor.White);
        PrintLine($"======== Loading Plugins: {string.Join(" ", pluginsToLoad)} ========", ConsoleColor.White);
        PrintLine($"======== Calling Plugin Function: {pluginToTest}.{functionToTest} with parameters {arguments?.Select(x => x.Key + " = " + x.Value).Aggregate((x, y) => x + ", " + y)} ========", ConsoleColor.White);
        Console.WriteLine();
    }

    static void PrintLine(string message, ConsoleColor color = ConsoleColor.Green)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}