using Newtonsoft.Json;
using System.Collections.Generic;

namespace HouseFinderWebBot.Api
{
    public class Fields
    {
        public int id { get; set; }
        public int area { get; set; }
        public int vagas { get; set; }
        
        public decimal aluguel { get; set; }
        [JsonProperty("condo_iptu")]
        public decimal condoIptu { get; set; }
        public decimal custo { get; set; }

        public string endereco { get; set; }
        [JsonProperty("regiao_nome")]
        public string regiaoNome { get; set; }
        public string cidade { get; set; }

        [JsonProperty("foto_capa")]
        public string fotoCapa { get; set; }
        public ICollection<string> photos { get; set; }
    }
}