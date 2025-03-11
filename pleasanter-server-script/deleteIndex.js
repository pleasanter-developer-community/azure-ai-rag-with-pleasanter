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