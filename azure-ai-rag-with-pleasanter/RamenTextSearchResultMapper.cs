using Microsoft.SemanticKernel.Data;

namespace RAGWithPleasanter;

#pragma warning disable SKEXP0001 // 種類は、評価の目的でのみ提供されています。将来の更新で変更または削除されることがあります。続行するには、この診断を非表示にします。

sealed class RamenTextSearchResultMapper : ITextSearchResultMapper

{
    public TextSearchResult MapFromResultToTextSearchResult(object result)
    {
        if (result is Ramen ramen)
        {
            var valueText = $"{{Style:\"{ramen.Style}\",Reviews:\"{ramen.Reviews}\",RecommendedMenu:\"{ramen.RecommendedMenu}\",Keyword:\"{ramen.Keyword}\"}}";
            return new TextSearchResult(value: valueText) { Name = ramen.StoreName, Link = $"https://my-test-pleasanter20240619.azurewebsites.net/items/{ramen.ID}" };
        }
        throw new ArgumentException("Invalid result type.");
    }
}

#pragma warning restore SKEXP0001