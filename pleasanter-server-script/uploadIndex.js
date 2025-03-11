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