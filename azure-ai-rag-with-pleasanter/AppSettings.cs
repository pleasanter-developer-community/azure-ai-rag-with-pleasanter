namespace RAGWithPleasanter;

class AppSettings
{
    public required string AzureOpenAIEndpoint { get; init; }
    public required string AzureOpenAIKey { get; init; }
    public required string AzureSearchEndpoint { get; init; }
    public required string AzureSearchKey { get; init; }
    public required string VectorStoreIndexName { get; init; }
    public required string ChatDeployment { get; init; }
    public required string EmbeddingDeployment { get; init; }
    public required string ServiceUrl { get; init; }
}
