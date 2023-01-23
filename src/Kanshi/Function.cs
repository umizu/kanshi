using System.Text;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Kanshi;

public class Function
{
    /// <summary>
    /// Grabs a random kanji from the S3 bucket object and sends it to Discord
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task FunctionHandler(CloudWatchEvent<object> input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Received @ {input.Time}");

        var s3Client = new AmazonS3Client();
        var response = await s3Client.GetObjectAsync("kanshi", "joyo.json");
        using var memoryStream = new MemoryStream();
        response.ResponseStream.CopyTo(memoryStream);
        var content = Encoding.UTF8.GetString(memoryStream.ToArray());
        var kanjis = JsonConvert.DeserializeObject<List<string>>(content)!;

        var randomIndex = new Random().Next(0, kanjis.Count);
        var randomKanji = kanjis[randomIndex];

        var httpClient = new HttpClient() { BaseAddress = new Uri("https://discord.com/api/webhooks/") };
        var jsonBody = new StringContent(JsonConvert.SerializeObject(new
        {
            embeds = new List<object>
            {
                new
                {
                    description = $"{randomKanji}\n\n[`jp`](https://dictionary.goo.ne.jp/word/kanji/{randomKanji}/) | [`en`](https://www.japandict.com/kanji/{randomKanji})"
                }
            }
        }), Encoding.UTF8, "application/json");

        var resp = await httpClient.PostAsync(
            Environment.GetEnvironmentVariable("webhook_path"),
            jsonBody);

        if (!resp.IsSuccessStatusCode)
            context.Logger.LogError($"Failed to send message to Discord. Status code: {resp.StatusCode}");
    }
}
