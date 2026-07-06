using RotasEntregaSMA.Dominio.Modelos;

namespace RotasEntregaSMA.Agentes.Otimizacao;

/// <summary>
/// Otimizacao por Colonia de Formigas (ACO), conforme secao 3.2 do artigo: usado como
/// modulo auxiliar de otimizacao local, dentro de cada agente-entregador, para calcular
/// a melhor sequencia de visita aos pontos de entrega ja alocados a ele (secao 3.5).
/// Nao decide o QUE alocar (isso e feito pelo Contract Net Protocol) -- apenas a ORDEM.
/// </summary>
public class OtimizadorACO
{
    private readonly Random _rng = new();

    /// <summary>
    /// Encontra uma sequencia de visita de baixo custo para os pontos informados,
    /// partindo de uma posicao inicial (posicao atual do entregador). Retorna os
    /// INDICES originais de "pontos" na ordem otimizada (nao os valores), para que o
    /// chamador consiga remontar a rota sem ambiguidade caso dois pontos coincidam.
    /// </summary>
    public List<int> OtimizarOrdem(
        PosicaoGeografica posicaoInicial,
        IReadOnlyList<PosicaoGeografica> pontos,
        int numFormigas = 6,
        int numIteracoes = 15,
        double alfa = 1.0,
        double beta = 3.0,
        double taxaEvaporacao = 0.5)
    {
        var n = pontos.Count;
        if (n <= 2) return Enumerable.Range(0, n).ToList(); // pouco a ganhar reordenando 0-2 paradas

        // Nos: 0 = posicao inicial (fixa, nunca revisitada), 1..n = pontos a entregar.
        var todasPosicoes = new PosicaoGeografica[n + 1];
        todasPosicoes[0] = posicaoInicial;
        for (int i = 0; i < n; i++) todasPosicoes[i + 1] = pontos[i];

        var distancia = new double[n + 1, n + 1];
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n; j++)
                distancia[i, j] = todasPosicoes[i].DistanciaAte(todasPosicoes[j]);

        var feromonio = new double[n + 1, n + 1];
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n; j++)
                feromonio[i, j] = 1.0;

        List<int>? melhorCaminho = null;
        double melhorCusto = double.MaxValue;

        for (int iteracao = 0; iteracao < numIteracoes; iteracao++)
        {
            var caminhosDaIteracao = new List<(List<int> caminho, double custo)>();

            for (int formiga = 0; formiga < numFormigas; formiga++)
            {
                var visitado = new bool[n + 1];
                visitado[0] = true;
                var caminho = new List<int> { 0 };
                var atual = 0;

                for (int passo = 0; passo < n; passo++)
                {
                    var proximo = EscolherProximoNo(atual, visitado, feromonio, distancia, n, alfa, beta);
                    caminho.Add(proximo);
                    visitado[proximo] = true;
                    atual = proximo;
                }

                var custo = CalcularCustoCaminho(caminho, distancia);
                caminhosDaIteracao.Add((caminho, custo));

                if (custo < melhorCusto)
                {
                    melhorCusto = custo;
                    melhorCaminho = caminho;
                }
            }

            // Evaporacao
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                    feromonio[i, j] *= (1 - taxaEvaporacao);

            // Deposito de feromonio proporcional a qualidade de cada caminho da iteracao
            foreach (var (caminho, custo) in caminhosDaIteracao)
            {
                var deposito = 1.0 / (custo + 0.0001);
                for (int k = 0; k < caminho.Count - 1; k++)
                {
                    var de = caminho[k];
                    var para = caminho[k + 1];
                    feromonio[de, para] += deposito;
                    feromonio[para, de] += deposito;
                }
            }
        }

        // melhorCaminho[0] eh sempre o no 0 (posicao inicial); no i (i>=1) corresponde a pontos[i-1].
        return melhorCaminho!.Skip(1).Select(indiceNo => indiceNo - 1).ToList();
    }

    private int EscolherProximoNo(int atual, bool[] visitado, double[,] feromonio, double[,] distancia, int n, double alfa, double beta)
    {
        var candidatos = new List<int>();
        var pesos = new List<double>();
        double somaPesos = 0;

        for (int j = 0; j <= n; j++)
        {
            if (visitado[j]) continue;
            var desejabilidade = 1.0 / (distancia[atual, j] + 0.0001);
            var peso = Math.Pow(feromonio[atual, j], alfa) * Math.Pow(desejabilidade, beta);
            candidatos.Add(j);
            pesos.Add(peso);
            somaPesos += peso;
        }

        if (somaPesos <= 0 || candidatos.Count == 0)
            return candidatos.Count > 0 ? candidatos[0] : atual;

        var sorteio = _rng.NextDouble() * somaPesos;
        double acumulado = 0;
        for (int k = 0; k < candidatos.Count; k++)
        {
            acumulado += pesos[k];
            if (sorteio <= acumulado) return candidatos[k];
        }
        return candidatos[^1];
    }

    private double CalcularCustoCaminho(List<int> caminho, double[,] distancia)
    {
        double total = 0;
        for (int k = 0; k < caminho.Count - 1; k++)
            total += distancia[caminho[k], caminho[k + 1]];
        return total;
    }
}
