namespace RAGWithPleasanter;

class AppSettings
{
    public required string AzureOpenAIEndpoint { get; init; }
    public required string AzureOpenAIKey { get; init; }
    public required string AzureSearchEndpoint { get; init; }
    public required string AzureSearchKey { get; init; }
    public required string VectorStoreName { get; init; }
    public required string ChatDeployment { get; init; }
    public required string EmbeddingDeployment { get; init; }

}

#pragma warning restore SKEXP0001 // 種類は、評価の目的でのみ提供されています。将来の更新で変更または削除されることがあります。続行するには、この診断を非表示にします。
#pragma warning restore SKEXP0010 // 種類は、評価の目的でのみ提供されています。将来の更新で変更または削除されることがあります。続行するには、この診断を非表示にします。