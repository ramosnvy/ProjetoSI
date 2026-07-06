using Microsoft.AspNetCore.SignalR;

namespace RotasEntregaSMA.Api.Hubs;

/// <summary>
/// Hub do SignalR: o servidor transmite eventos de simulacao para todos os clientes
/// conectados (item (c) dos objetivos: interface visual em tempo real). Os clientes nao
/// precisam invocar metodos neste hub -- apenas escutam o evento "evento".
/// </summary>
public class SimulacaoHub : Hub
{
}
