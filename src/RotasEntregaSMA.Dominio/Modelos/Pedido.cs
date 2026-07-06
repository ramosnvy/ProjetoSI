namespace RotasEntregaSMA.Dominio.Modelos;

public enum StatusPedido
{
    Novo,
    EmLeilao,
    Alocado,
    EmRota,
    Entregue,
    Cancelado,
    SemAlocacao
}

/// <summary>
/// Um pedido de entrega, com ponto de coleta (centro de distribuicao) e ponto de entrega.
/// Carrega consigo um tempo limite em tempo simulado (TempoLimiteSimulado) a partir do
/// instante de criacao (TempoSimuladoCriacao), conforme o VRPTW descrito no artigo.
/// </summary>
public class Pedido
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required PosicaoGeografica Coleta { get; init; }
    public required PosicaoGeografica Entrega { get; init; }
    public required DateTime JanelaInicio { get; init; }
    public required DateTime JanelaFim { get; init; }
    public double PesoKg { get; init; } = 1.0;
    public DateTime CriadoEm { get; init; } = DateTime.UtcNow;

    /// <summary>Instante de criacao em tempo simulado (em relacao ao inicio da simulacao).</summary>
    public TimeSpan TempoSimuladoCriacao { get; init; } = TimeSpan.Zero;

    /// <summary>Tempo maximo para entrega, contado em tempo simulado a partir da criacao.</summary>
    public TimeSpan TempoLimiteSimulado { get; init; } = TimeSpan.FromMinutes(30);

    public StatusPedido Status { get; set; } = StatusPedido.Novo;
    public Guid? EntregadorAlocadoId { get; set; }
    public DateTime? HorarioEstimadoEntrega { get; set; }
    public DateTime? EntregueEm { get; set; }

    /// <summary>
    /// Verifica se um horario proposto de entrega respeita a janela de tempo do pedido.
    /// </summary>
    public bool RespeitaJanela(DateTime horarioProposto) =>
        horarioProposto >= JanelaInicio && horarioProposto <= JanelaFim;
}
