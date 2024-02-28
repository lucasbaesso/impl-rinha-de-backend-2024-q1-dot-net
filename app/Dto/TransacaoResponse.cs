using System.Text.Json.Serialization;

namespace rinhaDotNetAot.Dto
{
    public sealed record TransacaoResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("ok")]
        public bool Ok { get; init; }

        [JsonPropertyName("limite")]
        public int Limite { get; init; }

        [JsonPropertyName("saldo")]
        public int Saldo { get; init; }

        public TransacaoResponse(int id, bool ok, int limite, int saldo)
        {
            Id = id;
            Ok = ok;
            Limite = limite;
            Saldo = saldo;
        }
    }
}
