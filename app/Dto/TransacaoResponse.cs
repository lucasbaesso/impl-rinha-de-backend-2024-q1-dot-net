using System.Text.Json.Serialization;

namespace rinhaDotNetAot.Dto
{
    public sealed record TransacaoResponse
    {
        [JsonPropertyName("limite")]
        public int Limite { get; init; }

        [JsonPropertyName("saldo")]
        public int Saldo { get; init; }

        public TransacaoResponse(int limite, int saldo)
        {
            Limite = limite;
            Saldo = saldo;
        }
    }
}
