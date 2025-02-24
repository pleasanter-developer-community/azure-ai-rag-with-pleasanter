# Azure Open AI と Azure AI Search でRAGを構築する

## はじめに

ここでは、[Microsoft Learn](https://learn.microsoft.com/ja-jp/azure/search/retrieval-augmented-generation-overview)に記載されている下記のパターンに倣ってシステムを構築していきます。

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
> ※ 引用元: [Azure AI Search での取得拡張生成 (RAG)](https://learn.microsoft.com/ja-jp/azure/search/retrieval-augmented-generation-overview)
  
### Consoleアプリ & Semantic Kernel
このサンプルでは、図の「App UX」と「App Server,Orchestrator」の部分を.NETコンソールアプリケーション(C#)で代用します。コードはSemantic Kernelのクラスライブラリを利用して実装します。

### データソースはプリザンターのDB（SQL Database）
データソースには、[プリザンター](https://pleasanter.org/)に登録されたデータを利用します。

- プリザンターはAzure AppService及びAzure SQL Databaseを利用したサーバーレス構成でインストールされているものとします。
  - 参考: [インストーラでプリザンターをAzure AppServiceにサーバレス構成でインストールする](https://pleasanter.org/ja/manual/getting-started-installer-pleasanter-azure)


## 事前準備
サンプルデータとして下記のデータをプリザンターにインポートしておきます。

## Azure AI Searchにデータをインポートする


- [Azure OpenAI Service のドキュメント](https://learn.microsoft.com/ja-jp/azure/ai-services/openai/)



- [マネージド ID を使用して Azure SQL へのインデクサー接続を設定する](https://learn.microsoft.com/ja-jp/azure/search/search-howto-managed-identities-sql)

- [Azure SQL データベースのデータにインデックスを付ける](https://learn.microsoft.com/ja-jp/azure/search/search-how-to-index-sql-database?tabs=portal-check-indexer)