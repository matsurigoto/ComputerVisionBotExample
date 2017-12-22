using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using RestSharp;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;

namespace ComputerVisionBotApplication.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            string response = string.Empty;
            if(activity.Attachments != null)
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var attachment = activity.Attachments.First();
                    if ((activity.ChannelId.Equals("skype", StringComparison.InvariantCultureIgnoreCase) || activity.ChannelId.Equals("msteams", StringComparison.InvariantCultureIgnoreCase))
                        && new Uri(attachment.ContentUrl).Host.EndsWith("skype.com"))
                    {
                        var token = await new MicrosoftAppCredentials().GetTokenAsync();
                        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    }

                    var responseMessage = await httpClient.GetAsync(attachment.ContentUrl);
                    var data = await responseMessage.Content.ReadAsByteArrayAsync();
                    response = await RecognizeText(data);
                }
            }

            await context.PostAsync($"{response}");
            context.Wait(MessageReceivedAsync);
        }

        private string key = "your_key";

        private async Task<string> RecognizeText(byte[] file)
        {
            var uri = "/vision/v1.0/recognizeText?handwriting=true";

            var result = string.Empty;
            var client = new RestClient("https://southeastasia.api.cognitive.microsoft.com");
            var request = new RestRequest(uri, Method.POST);
            request.AddHeader("Ocp-Apim-Subscription-Key", key);
            request.AddParameter("application/octet-stream", file, ParameterType.RequestBody);
            var response = await client.ExecuteTaskAsync(request);

            if (response.IsSuccessful)
            {
                result = await TextOperations(response.Headers[1].Value.ToString());
            }
            return result;
        }

        private async Task<string> TextOperations(string operationsUrl)
        {
            var result = string.Empty;
            var client = new RestClient(operationsUrl);
            var request = new RestRequest("", Method.GET);
            request.AddHeader("Ocp-Apim-Subscription-Key", key);
            var response = await client.ExecuteTaskAsync<RootObject>(request);

            return response.Data.recognitionResult.lines.Aggregate(result, (current, item) => current + (" " + item.text));
        }
    }

    public class Word
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
    }

    public class Line
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public List<Word> words { get; set; }
    }

    public class RecognitionResult
    {
        public List<Line> lines { get; set; }
    }

    public class RootObject
    {
        public string status { get; set; }
        public RecognitionResult recognitionResult { get; set; }
    }
}