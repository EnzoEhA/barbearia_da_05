using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("liberar", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors("liberar");

string connectionString = "Server=sakura.proxy.rlwy.net;Port=44682;Database=railway;User=root;Password=pICrJesDiaasNMiKdKosRErIVMfDaVIq;";

// ---------------- CLIENTES ----------------

app.MapGet("/clientes", async () =>
{
    var clientes = new List<object>();
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand("SELECT id, nome, telefone, email FROM clientes", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        clientes.Add(new
        {
            Id = reader.GetInt32("id"),
            Nome = reader.GetString("nome"),
            Telefone = reader.GetString("telefone"),
            Email = reader.GetString("email")
        });
    }
    return Results.Ok(clientes);
});

app.MapPost("/clientes", async (ClienteInput c) =>
{
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand(
        "INSERT INTO clientes (nome, telefone, email) VALUES (@nome, @telefone, @email)", conn);
    cmd.Parameters.AddWithValue("@nome", c.Nome);
    cmd.Parameters.AddWithValue("@telefone", c.Telefone);
    cmd.Parameters.AddWithValue("@email", c.Email);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok("Cadastrado!");
});

app.MapPut("/clientes/{id}", async (int id, ClienteInput c) =>
{
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand(
        "UPDATE clientes SET nome=@nome, telefone=@telefone, email=@email WHERE id=@id", conn);
    cmd.Parameters.AddWithValue("@nome", c.Nome);
    cmd.Parameters.AddWithValue("@telefone", c.Telefone);
    cmd.Parameters.AddWithValue("@email", c.Email);
    cmd.Parameters.AddWithValue("@id", id);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok("Atualizado!");
});

app.MapDelete("/clientes/{id}", async (int id) =>
{
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand("DELETE FROM clientes WHERE id=@id", conn);
    cmd.Parameters.AddWithValue("@id", id);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok("Excluído!");
});

// ---------------- BARBEIROS (leitura, pro site público mostrar as opções) ----------------

app.MapGet("/barbeiros", async () =>
{
    var barbeiros = new List<object>();
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand("SELECT id_barbeiro, nome FROM barbeiros", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        barbeiros.Add(new
        {
            Id = reader.GetInt32("id_barbeiro"),
            Nome = reader.GetString("nome")
        });
    }
    return Results.Ok(barbeiros);
});

// ---------------- SERVICOS (leitura, pro site público montar o cardápio/seleção) ----------------

app.MapGet("/servicos", async () =>
{
    var servicos = new List<object>();
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand("SELECT id_servico, nome, preco, duracao FROM servicos", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        servicos.Add(new
        {
            Id = reader.GetInt32("id_servico"),
            Nome = reader.GetString("nome"),
            Preco = reader.GetDecimal("preco"),
            Duracao = reader.GetString("duracao")
        });
    }
    return Results.Ok(servicos);
});

// ---------------- AGENDAMENTOS ----------------

// Lista agendamentos de um barbeiro numa data (usado pra saber quais horários já estão ocupados)
app.MapGet("/agendamentos", async (int idBarbeiro, string data) =>
{
    var ocupados = new List<string>();
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand(
        "SELECT horario FROM agendamentos WHERE id_barbeiro=@idBarbeiro AND data_agendamento=@data AND status <> 'cancelado'", conn);
    cmd.Parameters.AddWithValue("@idBarbeiro", idBarbeiro);
    cmd.Parameters.AddWithValue("@data", data);
    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        ocupados.Add(reader.GetTimeSpan("horario").ToString(@"hh\:mm"));
    }
    return Results.Ok(ocupados);
});

// Cria um agendamento. Se o telefone informado já for de um cliente existente, reaproveita o cadastro;
// senão, cria um cliente novo automaticamente.
app.MapPost("/agendamentos", async (AgendamentoInput a) =>
{
using var conn = new MySqlConnection(connectionString);
await conn.OpenAsync();

int idCliente;

using (var buscaCmd = new MySqlCommand("SELECT id FROM clientes WHERE telefone=@telefone LIMIT 1", conn))
{
    buscaCmd.Parameters.AddWithValue("@telefone", a.Telefone);
    var existente = await buscaCmd.ExecuteScalarAsync();

    if (existente != null)
    {
        idCliente = Convert.ToInt32(existente);
    }
    else
    {
        using var criaCmd = new MySqlCommand(
            "INSERT INTO clientes (nome, telefone, email) VALUES (@nome, @telefone, @email); SELECT LAST_INSERT_ID();", conn);
        criaCmd.Parameters.AddWithValue("@nome", a.Nome);
        criaCmd.Parameters.AddWithValue("@telefone", a.Telefone);
        criaCmd.Parameters.AddWithValue("@email", a.Email);
        idCliente = Convert.ToInt32(await criaCmd.ExecuteScalarAsync());
    }
}

using var cmd = new MySqlCommand(
    @"INSERT INTO agendamentos (id_cliente, id_barbeiro, id_servico, data_agendamento, horario, status)
          VALUES (@idCliente, @idBarbeiro, @idServico, @data, @horario, 'agendado')", conn);
cmd.Parameters.AddWithValue("@idCliente", idCliente);
cmd.Parameters.AddWithValue("@idBarbeiro", a.IdBarbeiro);
    cmd.Parameters.AddWithValue("@idServico", a.IdServico);
    cmd.Parameters.AddWithValue("@data", a.Data);
    cmd.Parameters.AddWithValue("@horario", a.Horario);

    await cmd.ExecuteNonQueryAsync();

    return Results.Ok("Agendado!");
});

app.Run();

record ClienteInput(string Nome, string Telefone, string Email);
record AgendamentoInput(string Nome, string Telefone, string Email, int IdBarbeiro, int IdServico, string Data, string Horario);