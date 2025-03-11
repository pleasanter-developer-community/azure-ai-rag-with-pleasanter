
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
  