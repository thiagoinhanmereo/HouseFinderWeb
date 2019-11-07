using Newtonsoft.Json;

namespace HouseFinderWebBot.Api
{
    public class Status
    {
        public string rid { get; set; }

        [JsonProperty("time-ms")]
        public decimal timeMs { get; set; }
    }
}