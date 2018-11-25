using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Seq.Client.EventLog.Properties;

namespace Seq.Client.EventLog
{
    static class SeqApi
    {
        public static readonly HttpClient HttpClient = new HttpClient();

        public static async Task PostRawEvents(RawEvents rawEvents)
        {
            var uri = Settings.Default.SeqUri + "/api/events/raw";

            if (!string.IsNullOrWhiteSpace(Settings.Default.ApiKey))
            {
                uri += "?apiKey=" + Settings.Default.ApiKey;
            }

            var content = new StringContent(
                JsonConvert.SerializeObject(rawEvents, Formatting.None),
                Encoding.UTF8,
                "application/json");

            var result = await HttpClient.PostAsync(uri, content).ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
            {
                Serilog.Log.Error("Received failure status code {StatusCode} from Seq: {ReasonPhrase}", result.StatusCode, result.ReasonPhrase);
            }
        }
    }
}
