> [!WARNING]
> このREADME.mdは書きかけです。

# Azure OpenAI と Azure AI Search でRAGを構築する

## 1. はじめに

ここでは、[Azure AI Search での取得拡張生成 (RAG) : Microsoft Learn](https://learn.microsoft.com/ja-jp/azure/search/retrieval-augmented-generation-overview)に記載されている下記のパターンに倣ってシステムを構築していきます。

### Azure AI Search のカスタム RAG パターン
> パターンの大まかな概要は次のとおりです。
>
> 1. ユーザーの質問または要求 (プロンプト) から始めます。
> 2. Azure AI Search に送信して、関連情報を見つけます。
> 3. 上位の検索結果を LLM に返します。
> 4. LLM の自然言語理解と推論機能を使用して、最初のプロンプトに対する応答を生成します。
> 
> Azure AI Search が LLM プロンプトに入力を提供しますが、モデルのトレーニングはしません。  RAG アーキテクチャでは、追加のトレーニングはありません。 LLM はパブリック データを使用して事前トレーニングされますが、取得コンポーネント (この場合は Azure AI 検索) からの情報によって拡張された応答を生成します。
> 
> ![image-1](img/image-1.png)
> - ユーザー エクスペリエンスのためのアプリ UX (Web アプリ)
> - アプリ サーバーまたはオーケストレーター (統合と調整レイヤー)
> - Azure AI Search (情報取得システム)
> - Azure OpenAI (生成 AI 用の LLM)
>
> ※ 引用元: [Azure AI Search での取得拡張生成 (RAG) : Microsoft Learn](https://learn.microsoft.com/ja-jp/azure/search/retrieval-augmented-generation-overview)
  
### Consoleアプリ & Semantic Kernel
このサンプルでは、図の「App UX」と「App Server,Orchestrator」の部分を.NETコンソールアプリケーション(C#)で代用します。コードはSemantic Kernelのクラスライブラリを利用して実装します。

### データソースはプリザンターのDB（SQL Database）
データソースには、[プリザンター](https://pleasanter.org/)に登録されたデータを利用します。

- プリザンターはAzure AppService及びAzure SQL Databaseを利用したサーバーレス構成でインストールされているものとします。
  - 参考: [インストーラでプリザンターをAzure AppServiceにサーバレス構成でインストールする](https://pleasanter.org/ja/manual/getting-started-installer-pleasanter-azure)


## 2. 取りこむデータの準備
### サンプルデータ
サンプルデータとして下記のデータをプリザンターにインポートしておきます。

- [ramen-db.csv](/ramen-db.csv)  
  (中野にあるラーメン屋のデータベースを想定した架空のデータをCopilotに作ってもらいました。実在する店舗名もありますが、あくまで架空のデータとなります)

### Viewを作成する
プリザンターの特定のテーブルからデータを取得するためのViewを作成します。
以下のSQLを使って、dbo.Resultsテーブルから必要なデータを取得し、各カラムに適切な名前を付けたViewを作成します。

今回は、「SiteId」で絞ることでプリザンターに登録されている全データから今回のサンプルデータのみをインデックス化するようにしています。

```sql
CREATE VIEW dbo.ramen
AS
SELECT
	ResultId AS ID
	, Title AS StoreName
	, Body AS Reviews
	, ClassA AS Location
	, ClassB AS Style
	, ClassC AS RecommendedMenu
	, ClassD AS Keyword
	, Title + ' ' + Body + ' ' + ClassA + ' ' + ClassB + ' ' + ClassC + ' ' + ClassD AS CombinedField
	, UpdatedTime
FROM            dbo.Results
WHERE           (SiteId = 2)

```

#### カラムの対応表
  
|テーブルのカラム名|表示名|Viewのカラム名|
|-|-|-|
|ResultId|ID|ID|
|Title|店舗名|StoreName|
|Body|口コミ|Reviews|
|ClassA|所在地|Location|
|ClassB|系統|Style|
|ClassC|おすすめメニュー|RecommendedMenu|
|ClassD|その他キーワード|Keyword|
|*|-|CombinedField|
|UpdatedTime|更新日時|UpdatedTime|

> [!Note]
> ConvinedFieldには各カラムの値を文字列結合した値を格納しておきます。このカラムはベクトル化列として利用します。
 
## 3. Azureサービスの準備
### Azure AI Searchのリソースを作成する

1. Azure Portalで "Azure AI Search" を検索し、リソースを作成します。価格レベルは評価のためFreeとしておきます。
   - リソース名: ramen-search
   - 価格レベル: Free
   - リージョン: Japan East

2. マネージドIDを有効にする
   作成したリソースに移動し、左側のメニューで"ID"を選んで「システム割り当て済み」の状態をONにしておきます

### Azure OpenAIのリソースを作成する
1. Azure Portalで "Azure OpenAI" を検索し、リソースを作成します。リージョンによって利用できないLLMがありますので必要に応じて選択してください。
   - リソース名: open-ai-searvice
   - 価格レベル: Standard(S0)
   - リージョン: Japan East

2. アクセス制御でAzure AI Searchのロールを割り当てる
   - 作成したリソースに移動し、左側のメニューで"アクセス制御(IAM)"を選びます
   - 「ロールの割り当ての追加」ボタンをクリック
   - ロールに「Cognitive Services OpenAI User」を選択
   - アクセスの割当先に「マネージドID」を選択
   - メンバーの追加でAI Search（ramen-search）のマネージドIDを選択

3. ネットワークセキュリティの構成
   必要に応じてネットワークセキュリティの構成を行います。  
   参考: [Azure OpenAI Service リソースを作成してデプロイする: Microsoft Learn](https://learn.microsoft.com/ja-jp/azure/ai-services/openai/how-to/create-resource?pivots=web-portal)

4. モデルをデプロイする  
   モデルをデプロイするには、次の手順に従います。
   
   - [Azure AI Foundry](https://ai.azure.com/) ポータルにサインインします。
   - 使用するサブスクリプションと Azure OpenAI リソースが選択されていることを確認します。
   - 左側メニューで「共有リソース」-「デプロイ」を選択します。
   - 「モデルのデプロイ」ボタンから「基本モデルをデプロイする」を選択します。
   - モデルの一覧から対象のモデルを選択肢して「確認」で内容を確認したら「リソースを作成してデプロイ」をクリックします。
  
   今回は下記の２つのモデルを使用します。
   |モデル名|用途|
   |-|-|
   |gpt-4o-mini|チャット補完|
   |text-embedding-ada-002|埋め込みモデル。データのベクトル化|

![deploy model](/img/image-2.png)


## 4. Azure AI Searchにデータをインポートする

下記ドキュメントを参考にサンプルデータのインポートとベクター化を行います。

- [マネージド ID を使用して Azure SQL へのインデクサー接続を設定する : Microsoft Learn](https://learn.microsoft.com/ja-jp/azure/search/search-howto-managed-identities-sql)
- [Azure SQL データベースのデータにインデックスを付ける : Microsoft Learn](https://learn.microsoft.com/ja-jp/azure/search/search-how-to-index-sql-database?tabs=portal-check-indexer)

### SQL Databaseへのロール割り当て
[マネージド ID を使用して Azure SQL へのインデクサー接続を設定する:Microsoft Learn](https://github.com/pleasanter-developer-community/azure-ai-rag-with-pleasanter)を参考にして、AI Searchのインスタンスに、データソースとなるSQL Databaseへの読み取り権限を付与します。

- Azure PortalでSQL Databaseサーバーのリソースに移動し、左側のメニューで"アクセス制御(IAM)"を選びます
- 「ロールの割り当ての追加」ボタンをクリック
- ロールに「閲覧者」を選択
- アクセスの割当先に「マネージドID」を選択
- メンバーの追加でAI Search（ramen-search）のマネージドIDを選択

また、Visual Studioや SQLServer Management Studio 等のツールでSQL Databaseに接続し、下記SQLでAI SearchのマネージドIDに対してデータベースへのアクセス権限を付与します。

```sql
CREATE USER [ramen-search] FROM EXTERNAL PROVIDER;
EXEC sp_addrolemember 'db_datareader', [ramen-search];
```

### データのインポートとベクター化

- Azure PortalでAI Searchのリソースへ移動し、「概要」画面上部の「データのインポートとベクター化」ボタンをクリックします。
  
![alt text](/img/image-3.png) 

- データへの接続
  - Azure SQLアカウントの種類：SQLデータベース
  - サーバー：(プリザンターのDBサーバー)
  - データベース: (プリザンターのDB名)
  - テーブルまたはビュー: 表示(View)
  - 認証オプションを選択する: マネージドIDを使用して認証する（システム割り当て）
  - ビューの名前: ramen
  - 変更の追跡: ☑　
    - 高ウォーターマーク変更ポリシー
    - 高基準値列：UpdatedTime

> [!Warning]
> #### 変更の追跡と高ウォーターマークポリシーの高基準値列について
> [Azure SQL データベースのデータにインデックスを付ける : Microsoft Learn](https://learn.microsoft.com/ja-jp/azure/search/search-how-to-index-sql-database?tabs=portal-check-indexer) には下記のような記載があります。
> 
> 高ウォーターマーク変更検出ポリシーは、行が最後に更新されたときのバージョンまたは時刻を取得する、テーブルまたはビューの "高基準" 列に依存します。 
> ビューを使う場合は、高ウォーターマークポリシーを使用する必要があります。 
>
> 今回はビューを利用するため、高ウォーターマーク変更ポリシーのみが利用可能となります。高基準値列にはUpdatedTimeを指定しましたが、rowversion列を指定することを強く推奨するとの事です。

- テキストをベクトル化する
  - ベクトル化する列: CombindField
  - Kind: Azure OpenAI
  - サブスクリプション: (Azure OpenAIを作成したサブスクリプション)
  - Azure OpenAI Service: (作成したAzure OpenAI名)
  - モデルデプロイ: (ベクトル化に使用するLLMモデルのデプロイ名（今回は text-embedding-ada-002）)
  
![alt text](/img/image-5.png)

- インデックス作成のスケジュール
  - スケジュール: インデックスを更新する頻度を選択します。（今回は動作検証に利用するだけなので「一度だけ」としておきます）

![alt text](/img/image-6.png)

- オブジェクト名のプレフィックスを任意の名前に変更してインポートを実行します。この名前はこの後「インデックス名」として利用します。

![alt text](/img/image-7.png)

インポートが完了すると、Azure AI Searchのリソースにインデックス作成されます。
![alt text](/img/image-8.png)


## 5. セマンティックカーネルによるベクターストアを使用したテキスト検索の実装
ここからはAIサービスを利用したクライアント側の実装例を見ていきます。
サンプルでは下記のドキュメントを参考にセマンティックカーネルによるテキスト検索（RAG）の実装を行っています。

> [!WARNING]
> セマンティック カーネル テキスト検索機能はプレビュー段階であり、破壊的変更を必要とする機能強化は、リリース前の限られた状況で引き続き発生する可能性があります。

- [セマンティック カーネル テキスト検索とは](https://learn.microsoft.com/ja-jp/semantic-kernel/concepts/text-search/?pivots=programming-language-csharp)
- [セマンティック カーネル テキスト検索でベクター ストアを使用する方法](https://learn.microsoft.com/ja-jp/semantic-kernel/concepts/text-search/text-search-vector-stores?pivots=programming-language-csharp)

### 依存ライブラリ
サンプルでは、下記のライブラリを参照しています。

|Package|説明|
|-|-|
|Microsoft.Extensions.Configuration.Binder|アプリケーション設定取得用|
|Microsoft.Extensions.Configuration.Json|アプリケーション設定取得用|
|Microsoft.SemanticKernel|セマンティックカーネルのライブラリ本体|
|Microsoft.SemanticKernel.Connectors.AzureOpenAI|Azure OpenAI用コネクタ|
|Microsoft.SemanticKernel.PromptTemplates.Handlebars|PromptTemplateの生成|
|Microsoft.SemanticKernel.Connectors.AzureAISearch|Azure AI Search用コネクタ（こちらはブレビュー版につき実装が大きく変わる可能性があります。）|

### アプリケーション設定
サンプルを実行するには、アプリケーション設定ファイルが必要です。プロジェクトフォルダ(azure-ai-rag-with-pleasanter)配下に `appsettings.development.json` ファイルを作成し、Azureサービスの各種設定を記入してください。
 
```json
{
  "AzureOpenAIEndpoint": "{Azure OpenAI のエンドポイント}",
  "AzureOpenAIKey": "{Azure OpenAIのキー}",
  "AzureSearchEndpoint": "{Azure AI Searchのエンドポイント}",
  "AzureSearchKey": "{Azure AI Searchのキー}",
  "ChatDeployment": "gpt-4o-mini",
  "VectorStoreIndexName": "{AI Searchのインデックス名}",
  "EmbeddingDeployment": "text-embedding-ada-002",
  "ServiceUrl": "{検索結果のLink生成に使う、プリザンターのURL}"
}
```

### コードの解説

1. ベクターストアモデルの定義

```csharp
public class Ramen
{
    [VectorStoreRecordVector]
    public ReadOnlyMemory<float> text_vector { get; init; }
        
    [VectorStoreRecordKey]
    public required string ID { get; init; }

    [VectorStoreRecordData]
    public required string StoreName { get; init; }

    [VectorStoreRecordData]
    public required string Reviews { get; init; }

    [VectorStoreRecordData]
    public required string Location { get; init; }

    [VectorStoreRecordData]
    public required string Style { get; init; }

    [VectorStoreRecordData]
    public required string RecommendedMenu { get; init; }

    [VectorStoreRecordData]
    public required string Keyword { get; init; }
}
```

2. テキスト検索結果のマッピングを定義

```csharp
sealed class RamenTextSearchResultMapper : ITextSearchResultMapper

{
    public TextSearchResult MapFromResultToTextSearchResult(object result)
    {
        if (result is Ramen ramen)
        {
            var valueText = $"{{Style:\"{ramen.Style}\",Reviews:\"{ramen.Reviews}\",RecommendedMenu:\"{ramen.RecommendedMenu}\",Keyword:\"{ramen.Keyword}\"}}";
            return new TextSearchResult(value: valueText) { Name = ramen.StoreName, Link = $"{ServiceUrl}/items/{ramen.ID}" };
        }
        throw new ArgumentException("Invalid result type.");
    }
}
```

2. Azure OpenAI Clientの作成

```csharp
var openAiClient = new AzureOpenAIClient(
    new Uri(settings.AzureOpenAIEndpoint),
    new AzureKeyCredential(settings.AzureOpenAIKey));
```

3. Semantic Kernelの構築

```csharp
private static Kernel InitializeSemanticKernel(AppSettings settings, AzureOpenAIClient openAiClient)
{
#pragma warning disable SKEXP0001, SKEXP0010

    //ChatCompletionのデプロイ(gpt-4o-mini)を紐づけし、Semantic Kernelのインスタンスを生成
    var kernelBuilder = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(settings.ChatDeployment, openAiClient);
    var kernel = kernelBuilder.Build();

    //AI Searchのベクトルストアへのアクセスするためのオブジェクトを生成
    var vectorStore = new AzureAISearchVectorStore(
        new SearchIndexClient(
            new Uri(settings.AzureSearchEndpoint),
            new AzureKeyCredential(settings.AzureSearchKey)));

    //ベクトルストアからコレクションを取得
    var collection = vectorStore.GetCollection<string, Ramen>(settings.VectorStoreName);

    //テキスト埋め込み生成サービスの作成
    var embeddingGenarationService = new AzureOpenAITextEmbeddingGenerationService(settings.EmbeddingDeployment, openAiClient);

    //VectorStoreTextSearch オブジェクトの生成
    var textSearch = new VectorStoreTextSearch<Ramen>(
        collection,
        embeddingGenarationService,
        null,
        new RamenTextSearchResultMapper());

    var searchPlugin = KernelPluginFactory.CreateFromFunctions(
        "SearchPlugin", "ramen search",
        [textSearch.CreateGetTextSearchResults(searchOptions: new TextSearchOptions() { Top = 10 })]);

    kernel.Plugins.Add(searchPlugin);
    return kernel;

#pragma warning restore SKEXP0001, SKEXP0010
}
```