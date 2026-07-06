namespace RotasEntregaSMA.Dominio.Modelos;

/// <summary>
/// Estado publico (para fins de leitura/visualizacao) de um agente entregador.
/// </summary>
public class EstadoEntregador
{
    public required Guid Id { get; init; }
    public required string Nome { get; init; }
    public PosicaoGeografica PosicaoAtual { get; set; }
    public double CapacidadeMaximaKg { get; init; } = 30.0;
    public double CapacidadeOcupadaKg { get; set; }
    public double VelocidadeMediaKmH { get; init; } = 25.0;
    public RotaEntregador Rota { get; set; } = null!;
    public int PedidosEntregues { get; set; }
    public double DistanciaPercorridaKm { get; set; }

    public double CapacidadeDisponivelKg => CapacidadeMaximaKg - CapacidadeOcupadaKg;
}
