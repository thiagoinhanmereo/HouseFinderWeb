using MongoDB.Bson.Serialization.Attributes;

namespace HouseFinderWebBot
{
    public class ApartmentInfo
    {
        public const string BaseUrl = "https://www.quintoandar.com.br";

        [BsonId()]
        public int Id { get; set; }

        [BsonElement("Href")]
        [BsonRequired()]
        public string Href
        {
            get { return $"{BaseUrl}/imovel/{Id}"; }
        }

        [BsonElement("ImageRef")]
        [BsonRequired()]
        public string ImageRef { get; set; }

        [BsonElement("Rua")]
        [BsonRequired()]
        public string Rua { get; set; }

        [BsonElement("Bairro")]
        [BsonRequired()]
        public string Bairro { get; set; }

        [BsonElement("Cidade")]
        [BsonRequired()]
        public string Cidade { get; set; }

        [BsonElement("Area")]
        [BsonRequired()]
        public decimal Area { get; set; }

        [BsonElement("Aluguel")]
        [BsonRequired()]
        public decimal Aluguel { get; set; }

        [BsonElement("Total")]
        [BsonRequired()]
        public decimal Total { get; set; }
    }
}
