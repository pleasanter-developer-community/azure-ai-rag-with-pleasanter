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

        var kernel = CreateTextSearchKernel(settings);
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
    private static Kernel CreateTextSearchKernel(AppSettings settings)
    {
#pragma warning disable SKEXP0001, SKEXP0010 //Experimental(実験段階)であることの警告を非表示にします。

        // Azure OpenAIクライアントのインスタンスを生成
        var openAiClient = new AzureOpenAIClient(
            new Uri(settings.AzureOpenAIEndpoint),
            new AzureKeyCredential(settings.AzureOpenAIKey));

        //ChatCompletionのデプロイ(gpt-4o-mini)を紐づけし、Semantic Kernelのインスタンスを生成
        var kernelBuilder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(settings.ChatDeployment, openAiClient);
        var kernel = kernelBuilder.Build();

        // Azure AI Searchのベクトルストアのインスタンスを生成
        var vectorStore = new AzureAISearchVectorStore(
            new SearchIndexClient(
                new Uri(settings.AzureSearchEndpoint),
                new AzureKeyCredential(settings.AzureSearchKey)));

        // ベクトルストアからコレクションを取得
        var collection = vectorStore.GetCollection<string, Ramen>(settings.VectorStoreIndexName);

        //テキスト埋め込み生成サービスのインスタンスを生成
        var embeddingGenarationService 
            = new AzureOpenAITextEmbeddingGenerationService(settings.EmbeddingDeployment, openAiClient);

        //VectorStoreTextSearch オブジェクトの生成
        var textSearch = new VectorStoreTextSearch<Ramen>(
            collection,
            embeddingGenarationService,
            null,
            new RamenTextSearchResultMapper(settings.ServiceUrl));

        //VectorStoreTextSearchオブジェクトからファンクション`GetTextSearchResult` を生成
        //そのファンクションを実行するカーネルプラグインを作成
        var searchPlugin = KernelPluginFactory.CreateFromFunctions(
            "SearchPlugin", "ramen search",
            [textSearch.CreateGetTextSearchResults(searchOptions: new TextSearchOptions() { Top = 10 })]);

        //プラグインをカーネルに追加
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

