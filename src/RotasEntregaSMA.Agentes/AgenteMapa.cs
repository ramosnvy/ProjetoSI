using RotasEntregaSMA.Dominio.Modelos;

namespace RotasEntregaSMA.Agentes;

/// <summary>
/// Agente Mapa: fornece informacoes de distancia e tempo de deslocamento entre pontos
/// do grafo urbano simulado, conforme descrito na secao 3.4 do artigo. Aplica um fator
/// de transito variavel para introduzir dinamismo realista no calculo de tempo.
/// </summary>
public class AgenteMapa
{
    private readonly Random _rng;

    public AgenteMapa(int seed = 0)
    {
        _rng = seed == 0 ? new Random() : new Random(seed);
    }

    /// <summary>Distancia em quilometros entre dois pontos (euclidiana sobre a grade simulada).</summary>
    public double CalcularDistanciaKm(PosicaoGeografica origem, PosicaoGeografica destino) =>
        origem.DistanciaAte(destino);

    /// <summary>
    /// Fator de transito atual (1.0 = fluxo livre, acima disso = congestionamento).
    /// Varia de forma pseudo-aleatoria para simular condicoes de transito em tempo real,
    /// conforme mencionado nas restricoes dinamicas do problema (secao 1).
    /// </summary>
    public double FatorTransitoAtual() => 1.0 + _rng.NextDouble() * 0.4;

    /// <summary>Tempo estimado de deslocamento entre dois pontos, dada uma velocidade media.</summary>
    public TimeSpan CalcularTempoViagem(PosicaoGeografica origem, PosicaoGeografica destino, double velocidadeMediaKmH)
    {
        var distanciaKm = CalcularDistanciaKm(origem, destino);
        var horasComTransito = (distanciaKm / velocidadeMediaKmH) * FatorTransitoAtual();
        return TimeSpan.FromHours(horasComTransito);
    }
}
