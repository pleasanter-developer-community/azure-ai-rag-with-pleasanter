# Azure AI Searchを利用したSemantic Kernelテキスト検索(RAG)の実装

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

### データソースはプリザンターに登録されたレコード
プリザンターに登録されたレコードの情報をインデックス化してAzure AI Searchに登録します。

#### サンプルデータ
今回は、架空のラーメン店の情報を記載したCSVファイルのデータをプリザンターに登録して利用します。

- [ramen-db.csv](/ramen-db.csv)

#### プリザンターのテーブル
プリザンターにサンプル用のテーブルを用意しておきます。

- テーブルの種類: 記録テーブル
- カラムの設定：

  |カラム名|プリザンターの項目|インデックスのフィールド名※|値の例|
  |--|--|--|--|
  |店名|Title|StoreName|麺屋昇天|
  |口コミ|Body|Reviews|「濃厚なスープと太麺が完璧に絡む。ほうれん草のトッピングが絶妙でおすすめ。」|
  |所在地|ClassA|Location|東京都瑞穂町箱根ケ崎51-51-51|
  |系統|ClassB|Style|家系|
  |おすすめメニュー|ClassC|RecomendedMenu|スペシャル豚骨ラーメン|
  |その他キーワード|ClassD|Keyword|濃厚/海苔/太麺|
  ※ インデックスのフィールド名は、Azure AI Searchでインデックスを定義する際に使用するフィールド名です。

## 2. Azureリソースの作成と設定

### 2.1 Azure OpenAIのリソースを作成する

#### リソースの作成  
Azure Portalで "Azure OpenAI" を検索し、リソースを作成します。
1. リソース名: open-ai-0227 (名前は１例です。任意の名前を付けてください)
2. 価格レベル: Standard(S0)
3. リージョン: Japan East

#### モデルのデプロイ  
次の手順に従いモデルをデプロイします。
1. [Azure AI Foundry](https://ai.azure.com/) ポータルにサインインします。
2. 使用するサブスクリプションと Azure OpenAI リソースが選択されていることを確認します。
3. 左側メニューで「共有リソース」-「デプロイ」を選択します。
4. 「モデルのデプロイ」ボタンから「基本モデルをデプロイする」を選択します。
5. モデルの一覧から対象のモデルを選択肢して「確認」で内容を確認したら「リソースを作成してデプロイ」をクリックします。

今回は下記の２つのモデルを使用します。

|モデル名|用途|
|-|-|
|gpt-4o-mini|チャット補完|
|text-embedding-ada-002|埋め込みモデル。データのベクトル化|

![deploy model](/img/image-2.png)

### 2.2 Azure AI Searchのリソースを作成する

#### リソースの作成
Azure Portalで "Azure AI Search" を検索し、リソースを作成します。価格レベルは評価のためFreeとしておきます。
- リソース名: ramen-search-0227 (名前は一例です。任意の名前を付けてください)
- 価格レベル: Free
- リージョン: Japan East

#### インデックスの定義  
Azure AI Searchに格納するインデックスを定義します。

1.  作成したリソースに移動し、左側のメニューで「検索管理」-「インデックス」を選択します。
2.  右側上部の「＋インデックスの追加」ドロップダウンを開き、「インデックスの追加」を選択します。

![new-index](/img/image-10.png)
  
3. インデックス名を入力します（この例では ramen-20250301としました）。
4. 「＋フィールドの追加」ボタンから、下図を参考に必要なフィールドを追加します。

![index-fields](/img/image-11.png)

5. TextVectorを追加する際は「種類」で`Collection(Edm.Single)`を選択します。
6. ベクトルプロファイルの設定画面が出てきますので、後述「ベクトルプロファイルの設定」を参考に設定を行ってください。

![text-vector](/img/image-13.png)

#### ベクトルプロファイルの設定
ベクトルプロファイルの設定画面ではベクトルアルゴリズムとベクトル化の設定を行います。

- ベクトルアルゴリズム:  
  今回は既定値のまま変更せずに利用します。

  |項目名|値|
  |-|-|
  |アルゴリズム名|vector-config-xxxxxx(自動で付与された名前で問題ありません)|
  |Kind|hnsw|
  |双方向リンク数(m)|4|
  |efConstruction|400|
  |efSearch|500|
  |類似性メトリック|cosine|

- ベクトル化:  
  
  |項目名|値|
  |-|-|
  |名前|vectorizer-xxxxxx(自動で付与された名前で問題ありません)|
  |Kind|Azure OpenAI|
  |サブスクリプション|(Azure OpenAI のリソースを作成したサブスクリプション)|
  |Azure OpenAI Service|open-ai-0227(作成したリソースの名前)|
  |モデル デプロイ|text-embedding-ada-002|
  |認証の種類|APIキー|
  
## 3. プリザンターに登録されたデータをAzure AI Searchに登録する
プリザンターにデータが登録されたタイミングで、Azure AI Search上にインデックスが構築されるように設定します。

1. ユーザーがプリザンターでレコードを作成(または更新)します。
2. レコードの作成（更新）後のタイミングでサーバスクリプトを実行します。
3. サーバスクリプトでは、`HttpClient`の機能を使って下記の処理を行います。
   - Azure OpenAIのEnbedding APIを実行し、レコードの内容をベクトルデータに変換
   - Azure AI SearchのIndex APIを実行し、ベクトルデータとレコードの内容をインデックスとして登録
4. レコードの削除時は、Azure AI Searchへ削除対象のレコードIDを指定して対象のインデックスを削除します。


![upload-from-pleasanter](/img/seq-1.png)


#### サーバスクリプトの実装
プリザンターのサンプル用テーブルの「テーブルの管理」画面で「サーバスクリプト」タブを選択し、下記のサーバスクリプトを追加します。

1. 共通スクリプト:  
   HttpClientを使ってAPIを実行する`MyAIServiceClient`クラスを定義しています。条件を「共有」とすることで、他のサーバスクリプトが実行されるまでにこのスクリプトが実行されます。
   - タイトル: Definitions
   - 条件: 共有
   - スクリプト: 
  
```js
/**
 * MyAIServiceClient クラスは、AI検索および埋め込みサービスとの通信を行うためのクライアントを提供します。
 * 
 * @class
 */
class MyAIServiceClient {
    #aiSearchUrl = "https://{Azure AI Search Service Name}.search.windows.net/indexes('{Index Name}')/docs/search.index?api-version=2024-07-01";
    #aiSearchKey = "{Azure AI Search API Key}";
    #openAiUrl = "https://{Azure Open AI Service Name}.openai.azure.com/openai/deployments/text-embedding-ada-002/embeddings?api-version=2024-02-01";
    #openAiKey = "{Azure Open AI API Key}";
  
    /**
     * 指定されたURLとAPIキーでHTTPクライアントを初期化します。
     *
     * @param {string} url - リクエストURIとして設定するURL。
     * @param {string} key - リクエストヘッダーに追加するAPIキー。
     */
    #init(url, key) {
      httpClient.RequestHeaders.Clear();
      httpClient.ResponseHeaders.Clear();
      httpClient.RequestUri = url;
      httpClient.RequestHeaders.Add('api-key', key);
    }
  
    /**
     * 指定されたデータでPOSTリクエストを送信します。
     *
     * @param {Object} data - POSTリクエストで送信するデータ。
     * @param {Function} success - リクエストが成功した場合に実行されるコールバック関数。レスポンスを引数として受け取ります。
     * @param {Function} fail - リクエストが失敗した場合に実行されるコールバック関数。ステータスコードとレスポンスを引数として受け取ります。
     */
    #post(data, success, fail) {
      httpClient.Content = JSON.stringify(data);
      let response = httpClient.Post();
      if (httpClient.IsSuccess) {
        success(response);
      } else {
        fail(httpClient.StatusCode, response);
      }
    }
  
    /**
     * 検索URLとキーを初期化し、データを投稿してインデックスを更新します。
     *
     * @param {Object} data - 投稿するデータ。
     * @param {Function} success - 投稿が成功した場合に実行されるコールバック関数。レスポンスを引数として受け取ります。
     * @param {Function} fail - 投稿が失敗した場合に実行されるコールバック関数。ステータスコードとレスポンスを引数として受け取ります。
     */
    uploadIndex(data, success, fail) {
      this.#init(this.#aiSearchUrl, this.#aiSearchKey);
      this.#post(data, success, fail);
    }
  
    /**
     * IDでインデックスエントリを削除します。
     *
     * @param {number|string} id - 削除するインデックスエントリのID。
     * @param {function} success - 削除が成功した場合に実行されるコールバック関数。レスポンスを引数として受け取ります。
     * @param {function} fail - 削除が失敗した場合に実行されるコールバック関数。ステータスコードとレスポンスを引数として受け取ります。
     */
    deleteIndex(id, success, fail) {
      this.#init(this.#aiSearchUrl, this.#aiSearchKey);
      let data = {
        "value": [
          {
            "@search.action": "delete",
            "ID": String(id)
          }]
      };
      this.#post(data, success, fail);
    }
  
    /**
     * 提供されたデータを使用して埋め込みを作成します。
     *
     * @param {Object} data - 埋め込みを作成するために使用するデータ。
     * @param {Function} success - 埋め込みの作成が成功した場合に呼び出されるコールバック関数。レスポンスを引数として受け取ります。
     * @param {Function} fail - 埋め込みの作成が失敗した場合に呼び出されるコールバック関数。ステータスコードとレスポンスを引数として受け取ります。
     */
    createEmbedding(data, success, fail) {
      this.#init(this.#openAiUrl, this.#openAiKey);
      this.#post(data, success, fail);
    }
  }
```

2. インデックス登録用スクリプト:  
   レコードが作成または更新されたタイミングで実行されます。登録されたレコード情報をAzure AI Searchにインデックスとして登録します。インデックスにはレコードの内容をAzure OpenAI のAPIで変換したベクトルデータを含めます。
   - タイトル: Upload Index
   - 条件: 作成後および更新後
   - スクリプト:  

```js
let myAIServiceClient = new MyAIServiceClient();
//ベクトル化する文字列の設定
//各項目の値をスペースで連結
let input = {
  input: `${model.Title} ${model.Title} ${model.Body} ${model.ClassA} '
   + '${model.ClassB} ${model.ClassC} ${model.ClassD}`
};
//埋め込みサービスにより指定した文字列をベクトル化
myAIServiceClient.createEmbedding(
  input,
  //success:
  function (response) {
    let obj = $ps.JSON.parse(response);
    let vector = obj.data[0].embedding;
    //登録するIndex情報
    //埋め込みサービスから返却されたベクトル(実数値の配列)を取得し、
    //TextVectorプロパティに設定する
    let data = {
      "value":
        [
          {
            "@search.action": "upload",
            "ID": String(model.ResultId),
            "StoreName": model.Title,
            "Reviews": model.Body,
            "Location": model.ClassA,
            "Style": model.ClassB,
            "RecommendedMenu": model.ClassC,
            "Keyword": model.ClassD,
            "TextVector": vector
          }
        ]
    };
    //Indexの追加または更新を実行
    myAIServiceClient.uploadIndex(
      data,
      //success:
      function (response) {
        logs.LogInfo(response,'MyAIServiceClient.uploadIndex');
      },
      //fail:
      function (statusCode, response) {
        logs.LogUserError(`(code: ${statusCode})${response}`, 'MyAIServiceClient.uploadIndex');
      });
  },
  //fail:
  function (statusCode, response) {
    logs.LogUserError(`(code: ${statusCode})${response}`, 'MyAIServiceClient.createEmbedding');
  });
```

3. インデックス削除用スクリプト:  
   レコードが削除されたタイミングで実行されます。レコードのIDを指定してAzure AI Searchからインデックスを削除します。
   - タイトル: Delete Index
   - 条件: 削除後
   - スクリプト:

```js
let myAIServiceClient = new MyAIServiceClient();
//指定したIDのインデックスを削除
myAIServiceClient.deleteIndex(
  model.ResultId,
  function (response) {
    logs.LogInfo(response,'MyAIServiceClient.deleteIndex');
  },
  function (statusCode, response) {
    context.LogUserError(`(code: ${statusCode})${response}`, 'MyAIServiceClient.deleteIndex');
  });
```


## 4. セマンティックカーネルによるベクターストアを使用したテキスト検索の実装
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

#### ベクターストアモデルの定義
まず、下準備としてAI Searchのベクターストアに登録されているデータをC#で扱うためのモデルクラスを定義します。
各プロパティにはデータマッピング用の属性を付与します。
- VectorStoreRecordVectorAttribute: ベクトル列に付与
- VectorStoreRecordKeyAttribute: このモデルのキーとなる項目に付与
- VectorStoreRecordDataAttribute: その他、取得したいフィールドに付与

```csharp
public class Ramen
{
    [VectorStoreRecordVector]
    public ReadOnlyMemory<float> TextVector { get; init; }
        
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

#### テキスト検索結果のマッピングの定義
次に、ベクトルストアモデル(Ramenクラス)をTextSearchResultの形式に変換するためのMapperクラスを定義します。
TextSearchResultは下記のプロパティを持ちます
- Name: 取得したデータの名前
- Value: 取得したデータの内容／詳細情報
- Link: ソースとなるデータが格納されている場所(WebサイトのURLなど)

今回のサンプルでは、下記の様に値を変換しています。
- Name: StoreName（店名）
- Value: ID、StoreName以外のフィールドをJSON文字列に成型した文字列
- Link: IDを基に組み立てた、プリザンターのレコードのURL
- 
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

#### メイン処理
プログラムの流れとしては以下の様になります
1. テキスト検索用にカスタマイズしたSemantic Kernelの構築
2. 検索結果を基にプロンプトの文字列を生成するためのテンプレートを定義
3. `kernel.InvokePromptAsync`でプロンプトを実行

```csharp
static async Task Main()
{
    //アプリケーション設定の取得
    var settings = GetAppSettings();

　　//テキスト検索用にカスタマイズしたSemantic Kernelの構築
    var kernel = CreateTextSearchKernel(settings);

    //プロンプトテンプレート構築用のファクトリクラス
    //- ここではHandlebarsテンプレートエンジンを利用
    var promptTemplateFactory = new HandlebarsPromptTemplateFactory();
    //検索結果からプロンプトを生成するテンプレートの定義
    //- <Plugin名>-<Function名> でカーネルプラグインのファンクションを呼び出し
    //- 結果を {{#each this}} で反復処理
    var promptTemplate = """
        # 下記のルールに従って回答してください
        - `問合せ内容`に対して、適切な回答を提示してください
        - `検索結果`の内容を参照して回答を作成してください
        - 回答の最後に`検索結果`に含まれるリンク情報を追加してください

        # 検索結果
        {{#with (SearchPlugin-GetTextSearchResults query)}}  
            {{#each this}}  
            Name: {{Name}}
            Value: {{Value}}
            Link: {{Link}}
            -----------------
            {{/each}}  
        {{/with}}  

        # 問合せ内容
        {{query}}
        """;

    do
    {
        Console.WriteLine("Enter a query or type 'exit' to quit:");
        var input = Console.ReadLine();
        if (input == "exit")
        {
            break;
        }
        //コンソールに入力された文字列でプロンプトを実行
        var result = await kernel.InvokePromptAsync(
            promptTemplate,
            new KernelArguments() { { "query", input } },
            templateFormat: HandlebarsPromptTemplateFactory.HandlebarsTemplateFormat,
            promptTemplateFactory: promptTemplateFactory);

        Console.WriteLine(result);

    } while (true);
}
```

#### Semantic Kernelの構築
テキスト検索用にカスタマイズしたSemantic Kernelの構築の実装は下記の通りです。詳細はコード内のコメントをご確認ください。


```csharp
// テキスト検索用にカスタマイズしたSemantic Kernelの構築
private static Kernel CreateTextSearchKernel(AppSettings settings)
{
#pragma warning disable SKEXP0001, SKEXP0010 //Experimental(実験段階)であることの警告を非表示

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
```

## 6. 実行結果
それでは実行してみましょう。ちゃんと登録したデータの中から、リクエストの内容に沿った回答を返してくれていますね！

```
Enter a query or type 'exit' to quit:
つけ麵のおいしいお店をおしえてください
おいしいつけ麺のお店は以下の3軒がおすすめです：

1. **つけ麺大王 中野店**
   - **スタイル**: つけ麺系
   - **レビュー**: 評判の良いつけ麺で、看板猫がかわいいです。
   - **おすすめメニュー**: 特製つけ麺
   - **キーワード**: 濃厚なつけだれ
   - [詳細はこちら](https://my-pleasanter-xxxx.azurewebsites.net/items/61)

2. **つけ麺専門店 麺道場**
   - **スタイル**: つけ麺系
   - **レビュー**: 評判の良いつけ麺です。
   - **おすすめメニュー**: 特製つけ麺
   - **キーワード**: 濃厚なつけだれ
   - [詳細はこちら](https://my-pleasanter-xxxx.azurewebsites.net/items/52)

3. **つけ麺屋 やすべえ 中野店**
   - **スタイル**: つけ麺系
   - **レビュー**: ボリューム満点で、美味しいつけ麺が楽しめます。
   - **おすすめメニュー**: 特製つけ麺
   - **キーワード**: 濃厚、つけだれ
   - [詳細はこちら](https://my-pleasanter-xxxx.azurewebsites.net/items/44)

これらのお店はそれぞれの魅力があり、特製つけ麺はどのお店でもおすすめです。ぜひ訪れてみてください！

Enter a query or type 'exit' to quit:
こってりしたラーメンが食べたいです
こってりしたラーメンをお求めでしたら、以下の選択肢があります：

1. **背脂醤油ラーメン まつもと**
   - **スタイル**: 背脂系
   - **レビュー**: 背脂たっぷり
   - **おすすめメニュー**: 背脂醤油ラーメン
   - **キーワード**: こってり、ガッツリ
   [詳細はこちら](https://my-pleasanter-xxxx.azurewebsites.net/items/55)

2. **麺処 井の庄 中野店**
   - **スタイル**: 濃厚魚介豚骨系
   - **レビュー**: 濃厚なスープが人気
   - **おすすめメニュー**: 井の庄ラーメン
   - **キーワード**: こってり、満足感
   [詳細はこちら](https://my-pleasanter-xxxx.azurewebsites.net/items/45)

これらのラーメンは、こってりとした味わいを楽しめるものとなっていますので、ぜひ試してみてください。
```



