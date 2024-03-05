using rinhaDotNetAot.Dto;
using rinhaDotNetAot.Data;
using System.Text.Json.Serialization;
using static rinhaDotNetAot.Dto.ExtratoResponse;
using Npgsql;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

var DB_HOST = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var DB_PORT = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var DB_USER = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var DB_PASS = Environment.GetEnvironmentVariable("DB_PASS") ?? "password";
var DB_NAME = Environment.GetEnvironmentVariable("DB_NAME") ?? "rinha";
var DB_MIN_POOL_SIZE = int.Parse(Environment.GetEnvironmentVariable("DB_MIN_POOL_SIZE") ?? "1");
var DB_MAX_POOL_SIZE = int.Parse(Environment.GetEnvironmentVariable("DB_MAX_POOL_SIZE") ?? "30");

var connectionString = @$"Host={DB_HOST};
                            Port={DB_PORT};
                            Username={DB_USER};
                            Password={DB_PASS};
                            Database={DB_NAME};
                            Pooling=true;
                            MinPoolSize={DB_MIN_POOL_SIZE};
                            MaxPoolSize={DB_MAX_POOL_SIZE};";

// do while loop to check if database is up
bool dbUp = false;
while (!dbUp)
{
    try
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            conn.Close();
            dbUp = true;
        }
    }
    catch (Exception)
    {
        Console.WriteLine("Database is not up yet, waiting 500 ms...");
        Thread.Sleep(500);
    }
}

// do a loop of 0 to size DB_MIN_POOL_SIZE and open and close a connection
List<NpgsqlConnection> connections = new List<NpgsqlConnection>();
for (int i = 0; i < DB_MIN_POOL_SIZE; i++)
{
    var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    connections.Add(conn);
}

foreach (var conn in connections)
{
    conn.Close();
}


var clientesApi = app.MapGroup("/clientes");

clientesApi.MapGet("/{id:required}/extrato", async (int id) =>
{

    // Console.WriteLine($"Extrato at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} - ID:{id}");
    // return Results.Ok();

    var sql = @"SELECT 
                    clienteId,
                    valor,
                    tipo,
                    descricao,
                    clienteNome,
                    ultimoLimite,
                    ultimoSaldo,
                    datahora,
                    now() as datahora_extrato
                FROM transacoes
                WHERE clienteid = @id
                ORDER BY id DESC
                LIMIT 10;";

    List<TransacaoRecord> transacoes = new List<TransacaoRecord>();

    using (var conn = new NpgsqlConnection(connectionString))
    {
        await conn.OpenAsync().ConfigureAwait(false);

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.PrepareAsync().ConfigureAwait(false);
        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var transacao = new TransacaoRecord(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetChar(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetDateTime(7),
                reader.GetDateTime(8)
            );
            transacoes.Add(transacao);
        }
    }
    if (transacoes.Count == 0)
    {
        return Results.NotFound();
    }
    return Results.Ok(new ExtratoResponse(transacoes));
});

clientesApi.MapPost("/{id:required}/transacoes", async (int id, TransacaoRequest request) =>
{
    // Console.WriteLine($"Transacoes at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} - ID:{id}");
    // return Results.Ok();

    if(request == null)
    {
        return Results.UnprocessableEntity("Invalid request");
    }

    char tipo = request.Tipo;
    int valor = request.Valor;
    string descricao = request.Descricao;

    if (valor <= 0)
    {
        return Results.UnprocessableEntity("Valor must be greater than 0");
    }
    //tipo must be 'd' or 'c'
    if (!tipo.Equals('d') && !tipo.Equals('c'))
    {
        return Results.UnprocessableEntity("Tipo must be 'd' or 'c'");
    }
    //descricao must be between 1 and 10 characters
    if (string.IsNullOrEmpty(descricao)  || descricao.Length > 10)
    {
        return Results.UnprocessableEntity("Descricao must be between 1 and 10 characters");
    }

    var updateSql = tipo.Equals('c') 
    ? "UPDATE clientes SET saldo = saldo + @valor WHERE id = @id RETURNING nome, saldo, limite"
    : "UPDATE clientes SET saldo = saldo - @valor WHERE id = @id AND saldo - @valor >= -limite RETURNING nome, saldo, limite";

    var sql = $@"WITH updated AS ({updateSql})
                INSERT INTO transacoes (clienteid, valor, tipo, descricao, ultimolimite, ultimosaldo, clientenome)
                    SELECT @id, @valor, @tipo, @descricao, updated.limite, updated.saldo, updated.nome
                    FROM updated
                    RETURNING ultimolimite, ultimosaldo;";

    TransacaoResponse response;
    using (var conn = new NpgsqlConnection(connectionString))
    {
        await conn.OpenAsync().ConfigureAwait(false);
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("valor", valor);
        cmd.Parameters.AddWithValue("tipo", tipo);
        cmd.Parameters.AddWithValue("descricao", descricao);

        await cmd.PrepareAsync().ConfigureAwait(false);

        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (await reader.ReadAsync().ConfigureAwait(false))
        {
            return Results.Ok(new TransacaoResponse(reader.GetInt32(0), reader.GetInt32(1)));
        }
        else
        {
            return Results.UnprocessableEntity("Limite excedido");
        }
    }
});

app.Run();

[JsonSerializable(typeof(ItemExtrato))]
[JsonSerializable(typeof(Resumo))]
[JsonSerializable(typeof(ExtratoResponse))]
[JsonSerializable(typeof(TransacaoRequest))]
[JsonSerializable(typeof(TransacaoResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}

