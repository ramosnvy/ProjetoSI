namespace RotasEntregaSMA.Dominio.Modelos;

public enum TipoParada
{
    Coleta,
    Entrega
}

/// <summary>
/// Uma parada (coleta ou entrega) dentro da rota de um agente entregador.
/// </summary>
public class ParadaRota
{
    public required Guid PedidoId { get; init; }
    public required TipoParada Tipo { get; init; }
    public required PosicaoGeografica Posicao { get; init; }
    public DateTime? HorarioEstimado { get; set; }
}

/// <summary>
/// Rota atual de um agente entregador: sequencia ordenada de paradas de coleta/entrega.
/// </summary>
public class RotaEntregador
{
    public required Guid EntregadorId { get; init; }
    public List<ParadaRota> Paradas { get; init; } = new();
    public double DistanciaTotalKm { get; set; }

    public RotaEntregador Clonar()
    {
        return new RotaEntregador
        {
            EntregadorId = EntregadorId,
            Paradas = Paradas.Select(p => new ParadaRota
            {
                PedidoId = p.PedidoId,
                Tipo = p.Tipo,
                Posicao = p.Posicao,
                HorarioEstimado = p.HorarioEstimado
            }).ToList(),
            DistanciaTotalKm = DistanciaTotalKm
        };
    }
}
