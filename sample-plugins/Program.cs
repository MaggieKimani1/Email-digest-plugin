using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Plugins.OpenApi.Extensions;
using Microsoft.Extensions.Logging;
using sample_plugins;
using Microsoft.SemanticKernel.Plugins.MsGraph.Connectors.CredentialManagers;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Linq;

internal class Program
{
    private static string _pluginName;

    private static async Task Main(string[] args)
    {
        // Initialize the kernel
        var config = GetConfiguration();
        var kernel = args.Length > 0 && args[0] == "--enable-logging" ? InitializeKernel(config, enableLogging: true)
            : InitializeKernel(config);

        await LoadPluginAsync(kernel, config);

        var planner = InitializePlanner();
        await ExecuteGoal(kernel, planner);
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

    static async Task ExecuteGoal(Kernel kernel, FunctionCallingStepwisePlanner planner)
    {
        var promptTemplate = Environment.CurrentDirectory + $"\\ApiManifestPlugins\\{_pluginName}\\skprompt.txt";
        var goal = File.ReadAllText(promptTemplate);

        var result = await planner.ExecuteAsync(kernel, goal);

        PrintLine("--------------------");
        PrintLine($"\nResult:\n{result.FinalAnswer}\n");
        PrintLine("--------------------");
    }

    static async Task AddApiManifestPluginsAsync(Kernel kernel, IConfigurationRoot config)
    {
        var authProvider = await GetAuthProviderAsync(config);

        try
        {
            var manifestFilePath = Environment.CurrentDirectory + $"\\ApiManifestPlugins\\{_pluginName}\\apimanifest.json";
            KernelPlugin plugin =
            await kernel.ImportPluginFromApiManifestAsync(
                _pluginName,
                manifestFilePath,
                new OpenApiFunctionExecutionParameters(authCallback: authProvider.AuthenticateRequestAsync
                , serverUrlOverride: new Uri("https://graph.microsoft.com/v1.0")))
                .ConfigureAwait(false);

            PrintLine($">> {_pluginName} is created.", ConsoleColor.Green);
        }
        catch (Exception ex)
        {
            kernel.LoggerFactory.CreateLogger("Plugin Creation").LogError(ex, "Plugin creation failed. Message: {0}", ex.Message);
            throw new AggregateException($"Plugin creation failed for {_pluginName}", ex);
        }
    }

    static async Task<BearerAuthenticationProviderWithCancellationToken> GetAuthProviderAsync(IConfigurationRoot config)
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
        return authenticationProvider;
    }

    static List<string> ListAvailablePlugins()
    {
        // List the plugins available on the console
        PrintLine("Available Plugins to load:", ConsoleColor.Green);
        PrintLine("---------------------------", ConsoleColor.Green);

        var pluginIndex = 1;
        var pluginsList = new List<string>();
        var availablePlugins = Directory.GetDirectories(Environment.CurrentDirectory + "\\ApiManifestPlugins");
        foreach (var plugin in availablePlugins)
        {
            var pluginName = plugin.Split(Path.DirectorySeparatorChar).Last();
            PrintLine($"{pluginIndex}. {pluginName}");
            pluginsList.Add(pluginName);
            pluginIndex++;
        }

        PrintLine("---------------------------", ConsoleColor.Green);
        return pluginsList;
    }

    static async Task LoadPluginAsync(Kernel kernel, IConfigurationRoot config) 
    { 
        var pluginList = ListAvailablePlugins();

        // Select a plugin to load
        PrintLine("Select a plugin to load: ", ConsoleColor.Green);
        var selectedIndex = int.Parse(Console.ReadLine());
        if (selectedIndex > pluginList.Count)
        {
            throw new InvalidOperationException("Invalid selection.");
        }

        var selectedPlugin = pluginList[selectedIndex - 1];
        _pluginName = selectedPlugin;   
        await AddApiManifestPluginsAsync(kernel, config);
    }

    static void PrintLine(string message, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}