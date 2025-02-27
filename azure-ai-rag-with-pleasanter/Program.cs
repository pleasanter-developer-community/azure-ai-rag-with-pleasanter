using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;

namespace RAGWithPleasanter;


static class Program
{
    static async Task Main()
    {
        var settings = GetAppSettings();

        var openAiClient = new AzureOpenAIClient(
            new Uri(settings.AzureOpenAIEndpoint),
            new AzureKeyCredential(settings.AzureOpenAIKey));

        var kernel = InitializeSemanticKernel(settings, openAiClient);
        var promptTemplateFactory = new HandlebarsPromptTemplateFactory();
        var promptTemplate = """
            {{#with (SearchPlugin-GetTextSearchResults query)}}  
                {{#each this}}  
                Name: {{Name}}
                Value: {{Value}}
                Link: {{Link}}
                -----------------
                {{/each}}  
            {{/with}}  

            {{query}}

            Include citations to the relevant information where it is referenced in the response.
            """;

        do
        {
            Console.WriteLine("Enter a query or type 'exit' to quit:");
            var input = Console.ReadLine();
            if (input == "exit")
            {
                break;
            }
            var result = await kernel.InvokePromptAsync(
                promptTemplate,
                new KernelArguments() { { "query", input } },
                templateFormat: HandlebarsPromptTemplateFactory.HandlebarsTemplateFormat,
                promptTemplateFactory: promptTemplateFactory);
            Console.WriteLine(result);

        } while (true);
    }

    /// <summary>
    /// Initializes the semantic kernel with the provided settings and OpenAI client.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <param name="openAiClient">The Azure OpenAI client.</param>
    /// <returns>The initialized kernel.</returns>
    private static Kernel InitializeSemanticKernel(AppSettings settings, AzureOpenAIClient openAiClient)
    {
#pragma warning disable SKEXP0001, SKEXP0010 // 種類は、評価の目的でのみ提供されています。将来の更新で変更または削除されることがあります。続行するには、この診断を非表示にします。

        var kernelBuilder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(settings.ChatDeployment, openAiClient);
        var kernel = kernelBuilder.Build();

        var vectorStore = new AzureAISearchVectorStore(
            new SearchIndexClient(
                new Uri(settings.AzureSearchEndpoint),
                new AzureKeyCredential(settings.AzureSearchKey)));

        var collection = vectorStore.GetCollection<string, Ramen>(settings.VectorStoreIndexName);
        var embeddingGenarationService = new AzureOpenAITextEmbeddingGenerationService(settings.EmbeddingDeployment, openAiClient);
        var textSearch = new VectorStoreTextSearch<Ramen>(
            collection,
            embeddingGenarationService,
            null,
            new RamenTextSearchResultMapper(settings.ServiceUrl));

        var searchPlugin = KernelPluginFactory.CreateFromFunctions(
            "SearchPlugin", "ramen search",
            [textSearch.CreateGetTextSearchResults(searchOptions: new TextSearchOptions() { Top = 10 })]);

        kernel.Plugins.Add(searchPlugin);
        return kernel;

#pragma warning restore SKEXP0001, SKEXP0010
    }

    /// <summary>
    /// Retrieves the application settings from the configuration files.
    /// </summary>
    /// <returns>The application settings.</returns>
    private static AppSettings GetAppSettings()
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .Build();
        var settings = configuration.Get<AppSettings>()
            ?? throw new InvalidOperationException("AppSettings could not be loaded from configuration.");
        return settings;
    }
}

