using System.Text.Json.Serialization;
using rinhaDotNetAot.Data;

namespace rinhaDotNetAot.Dto
{
    public sealed record ExtratoResponse
    {
        [JsonPropertyName("saldo")]
        public Resumo? Saldo { get; set; }

        [JsonPropertyName("ultimas_transacoes")]
        public List<ItemExtrato>? UltimasTransacoes { get; set; }

        public ExtratoResponse(List<TransacaoRecord> transacoes)
        {
            if(transacoes.Count == 0)
            {
                return;
            }
            Saldo = new Resumo(transacoes.First());
            UltimasTransacoes = transacoes.Select(t => new ItemExtrato(t)).ToList();
        }

        public record Resumo
        {
            public Resumo(TransacaoRecord transacao)
            {
                Total = transacao.UltimoSaldo;
                Limite = transacao.UltimoLimite;
                DataExtrato = transacao.DataHoraExtrato;
            }

            [JsonPropertyName("total")]
            public int Total { get; set; }

            [JsonPropertyName("limite")]
            public int Limite { get; set; }

            [JsonPropertyName("data_extrato")]
            public DateTime DataExtrato { get; set; }
        }

        public record ItemExtrato
        {
            public ItemExtrato(TransacaoRecord transacao)
            {
                Valor = transacao.Valor;
                Tipo = transacao.Tipo;
                Descricao = transacao.Descricao;
                RealizadaEm = transacao.DataHora;
            }

            [JsonPropertyName("valor")]
            public int Valor { get; set; }

            [JsonPropertyName("tipo")]
            public char Tipo { get; set; }

            [JsonPropertyName("descricao")]
            public string Descricao { get; set; }

            [JsonPropertyName("realizada_em")]
            public DateTime RealizadaEm { get; set; }
        }
    }
}
