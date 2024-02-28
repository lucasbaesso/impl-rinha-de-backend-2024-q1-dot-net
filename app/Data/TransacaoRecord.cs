namespace rinhaDotNetAot.Data
{
    public readonly record struct TransacaoRecord
    (
        int ClienteId,
        int Valor,
        char Tipo,
        string Descricao,
        string ClienteNome,
        int UltimoLimite,
        int UltimoSaldo,
        DateTime DataHora,
        DateTime DataHoraExtrato
    );
}
