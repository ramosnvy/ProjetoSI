namespace RotasEntregaSMA.Simulacao;

/// <summary>
/// Comparativo entre o desempenho do SMA dinamico (Contract Net Protocol, online, pedido
/// a pedido) e a solucao estatica de referencia (secao 2.1/2.4 do artigo), atendendo ao
/// objetivo especifico (d): "Avaliar o desempenho comparando com uma solucao estatica de
/// referencia".
/// </summary>
public record ResultadoComparativo(
    int TotalPedidosGerados,
    double DistanciaTotalSmaKm,
    int PedidosEntreguesSma,
    int PedidosSemAlocacaoSma,
    double DistanciaTotalEstaticaKm,
    int PedidosDentroDaJanelaEstatica,
    int PedidosForaDaJanelaEstatica,
    int PedidosNaoAtendidosEstatica);
