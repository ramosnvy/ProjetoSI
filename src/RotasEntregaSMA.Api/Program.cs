using RotasEntregaSMA.Agentes.Comunicacao;
using RotasEntregaSMA.Api.Hubs;
using RotasEntregaSMA.Api.Servicos;
using RotasEntregaSMA.Simulacao;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EventosSimulacao>();
builder.Services.AddSingleton<SimuladorAmbiente>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<RetransmissorEventosService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

app.MapHub<SimulacaoHub>("/hubs/simulacao");

app.MapPost("/api/simulacao/iniciar", (SimuladorAmbiente simulador, ConfiguracaoSimulacao? config) =>
{
    if (simulador.EmExecucao)
        return Results.Conflict(new { mensagem = "A simulacao ja esta em execucao." });

    simulador.Iniciar(config);
    return Results.Ok(new { mensagem = "Simulacao iniciada.", configuracao = simulador.ConfiguracaoAtual });
});

app.MapPost("/api/simulacao/parar", async (SimuladorAmbiente simulador) =>
{
    await simulador.PararAsync();
    return Results.Ok(new { mensagem = "Simulacao parada.", comparativo = simulador.ObterComparativoFinal() });
});

app.MapGet("/api/simulacao/estado", (SimuladorAmbiente simulador) =>
{
    return Results.Ok(new
    {
        emExecucao = simulador.EmExecucao,
        entregadores = simulador.ObterEstadoEntregadores().Select(e => new
        {
            e.Id,
            e.Nome,
            e.PosicaoAtual,
            e.CapacidadeMaximaKg,
            e.CapacidadeOcupadaKg,
            e.PedidosEntregues,
            e.DistanciaPercorridaKm,
            paradas = e.Rota.Paradas
        })
    });
});

app.MapGet("/api/simulacao/comparativo", (SimuladorAmbiente simulador) =>
    Results.Ok(simulador.ObterComparativoFinal()));

app.Run();
