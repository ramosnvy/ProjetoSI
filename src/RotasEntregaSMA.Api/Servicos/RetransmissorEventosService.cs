using Microsoft.AspNetCore.SignalR;
using RotasEntregaSMA.Agentes.Comunicacao;
using RotasEntregaSMA.Api.Hubs;

namespace RotasEntregaSMA.Api.Servicos;

/// <summary>
/// Servico de fundo que le o canal de eventos da simulacao (agnostico de transporte) e
/// os retransmite para todos os clientes conectados via SignalR, em tempo real.
/// </summary>
public class RetransmissorEventosService : BackgroundService
{
    private readonly EventosSimulacao _eventos;
    private readonly IHubContext<SimulacaoHub> _hub;

    public RetransmissorEventosService(EventosSimulacao eventos, IHubContext<SimulacaoHub> hub)
    {
        _eventos = eventos;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evento in _eventos.Leitor.ReadAllAsync(stoppingToken))
        {
            await _hub.Clients.All.SendAsync("evento", new
            {
                tipo = evento.Tipo.ToString(),
                dados = evento.Dados,
                ocorridoEm = evento.OcorridoEm
            }, stoppingToken);
        }
    }
}
