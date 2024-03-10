using rinhaDotNetAot.Dto;
using rinhaDotNetAot.Data;
using System.Text.Json.Serialization;
using static rinhaDotNetAot.Dto.ExtratoResponse;
using Npgsql;
using System.Diagnostics;

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
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        conn.Close();
        dbUp = true;
    }
    catch (Exception)
    {
        Console.WriteLine("Database is not up yet, waiting 500 ms...");
        Thread.Sleep(500);
    }
}

// warmup
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
List<int> ids = getIds();
foreach (var id in ids)
{
    InsertTransacao(id, 1, 'c', "warmup");
}
ResetDatabase();


var clientesApi = app.MapGroup("/clientes");

clientesApi.MapGet("/{id:required}/extrato", (int id) =>
{
    // Console.WriteLine($"Extrato at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} - ID:{id}");
    // var stopwatch = Stopwatch.StartNew();

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
        conn.Open();

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Prepare();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
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
    // stopwatch.Stop();
    // Console.WriteLine($"Execution time: {stopwatch.Elapsed.TotalMilliseconds:F3} ms");
    if (transacoes.Count == 0)
    {
        return Results.NotFound();
    }
    return Results.Ok(new ExtratoResponse(transacoes));
});

clientesApi.MapPost("/{id:required}/transacoes", (int id, TransacaoRequest request) =>
{
    // Console.WriteLine($"Transacoes at {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")} - ID:{id}");
    // var stopwatch = Stopwatch.StartNew();

    if (request == null)
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
    if (string.IsNullOrEmpty(descricao) || descricao.Length > 10)
    {
        return Results.UnprocessableEntity("Descricao must be between 1 and 10 characters");
    }

    TransacaoResponse? transacao = InsertTransacao(id, valor, tipo, descricao);

    // stopwatch.Stop();
    // Console.WriteLine($"Execution time: {stopwatch.Elapsed.TotalMilliseconds:F3} ms");
    if (transacao != null)
    {
        return Results.Ok(transacao);
    }
    else
    {
        return Results.UnprocessableEntity("Limite excedido");
    }
});

TransacaoResponse? InsertTransacao(int id, int valor, char tipo, string descricao)
{
    var updateSql = tipo.Equals('c')
    ? "UPDATE clientes SET saldo = saldo + @valor WHERE id = @id RETURNING nome, saldo, limite"
    : "UPDATE clientes SET saldo = saldo - @valor WHERE id = @id AND saldo - @valor >= -limite RETURNING nome, saldo, limite";

    var sql = $@"WITH updated AS ({updateSql})
                INSERT INTO transacoes (clienteid, valor, tipo, descricao, ultimolimite, ultimosaldo, clientenome)
                    SELECT @id, @valor, @tipo, @descricao, updated.limite, updated.saldo, updated.nome
                    FROM updated
                    RETURNING ultimolimite, ultimosaldo;";

    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("valor", valor);
    cmd.Parameters.AddWithValue("tipo", tipo);
    cmd.Parameters.AddWithValue("descricao", descricao);
    cmd.Prepare();

    using var reader = cmd.ExecuteReader();
    if (reader.Read())
    {
        int limite = reader.GetInt32(0);
        int saldo = reader.GetInt32(1);
        conn.Close();
        return new TransacaoResponse(limite, saldo);
    }
    conn.Close();
    return null;
}

void ResetDatabase()
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    var sqlDelete = new NpgsqlCommand("DELETE FROM transacoes; UPDATE clientes SET saldo = 0;", conn);
    sqlDelete.ExecuteNonQuery();
    var sqlInicial = new NpgsqlCommand(
        "INSERT INTO transacoes (clienteid, valor, tipo, descricao, clientenome, ultimolimite, ultimosaldo) " +
        "SELECT id, 0, 'c', 'inicial', nome, limite, saldo FROM clientes;", conn
    );
    sqlInicial.ExecuteNonQuery();
    conn.Close();
}

List<int> getIds()
{
    using var conn = new NpgsqlConnection(connectionString);
    conn.Open();
    var sql = new NpgsqlCommand("SELECT id FROM clientes;", conn);
    using var reader = sql.ExecuteReader();
    List<int> ids = new List<int>();
    while (reader.Read())
    {
        ids.Add(reader.GetInt32(0));
    }
    return ids;
}

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

