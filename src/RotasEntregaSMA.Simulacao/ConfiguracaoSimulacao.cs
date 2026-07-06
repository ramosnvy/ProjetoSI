namespace RotasEntregaSMA.Simulacao;

/// <summary>
/// Parametros configuraveis de uma rodada de simulacao.
/// Defaults calibrados para testes rapidos: simulacao completa em ~5 segundos reais
/// (8 minutos simulados a 100x de aceleracao, ~25 ticks de 200ms).
/// Grade de 3 km com velocidade 40 km/h garante que entregadores completem ciclos.
/// </summary>
public class ConfiguracaoSimulacao
{
    public int NumeroEntregadores { get; set; } = 2;
    public double TamanhoGradeKm { get; set; } = 3;

    /// <summary>Intervalo medio entre chegadas de novos pedidos (em segundos simulados).</summary>
    public double IntervaloMedioNovoPedidoSegundosSimulados { get; set; } = 20;

    public double DuracaoSimulacaoMinutosSimulados { get; set; } = 8;

    /// <summary>
    /// Quantos segundos simulados avancam a cada segundo real.
    /// Valor 100 → 8 minutos simulados = ~4.8 segundos reais (~25 ticks).
    /// </summary>
    public double FatorAceleracaoTempo { get; set; } = 100;

    public double CapacidadeMaximaKg { get; set; } = 20;
    public double VelocidadeMediaKmH { get; set; } = 40;

    /// <summary>Janela de tempo minima e maxima (em minutos simulados) concedida a cada pedido.</summary>
    public double JanelaMinMinutos { get; set; } = 5;
    public double JanelaMaxMinutos { get; set; } = 15;

    /// <summary>Tempo limite de entrega de cada pedido em minutos simulados.</summary>
    public double TempoLimitePedidoMinutos { get; set; } = 20;
}
