using RotasEntregaSMA.Dominio.Modelos;

namespace RotasEntregaSMA.Agentes.Otimizacao;

public record EntregadorInicial(Guid Id, PosicaoGeografica PosicaoInicial, double VelocidadeMediaKmH, double CapacidadeMaximaKg);

public record ResultadoSolucaoEstatica(
    double DistanciaTotalKm,
    int PedidosAtendidosDentroDaJanela,
    int PedidosForaDaJanela,
    int PedidosNaoAtendidos,
    Dictionary<Guid, List<Guid>> AlocacaoPorEntregador);

/// <summary>
/// Solucao estatica de referencia (secao 2.1 / 2.4 do artigo: "OR-Tools / Comercial"),
/// usada apenas como baseline de comparacao (objetivo especifico (d)). Recebe TODOS os
/// pedidos do lote de uma vez (conhecimento antecipado completo) e faz uma unica
/// otimizacao por vizinho mais proximo com insercao mais barata, SEM realocacao
/// dinamica posterior -- ao contrario do SMA, que decide online, pedido a pedido.
/// </summary>
public class SolucaoEstaticaBaseline
{
    public ResultadoSolucaoEstatica Resolver(IReadOnlyList<Pedido> pedidos, IReadOnlyList<EntregadorInicial> entregadores)
    {
        var posicaoCorrente = entregadores.ToDictionary(e => e.Id, e => e.PosicaoInicial);
        var cargaOcupada = entregadores.ToDictionary(e => e.Id, e => 0.0);
        var alocacao = entregadores.ToDictionary(e => e.Id, e => new List<Guid>());
        var horarioCorrente = entregadores.ToDictionary(e => e.Id, e => DateTime.UtcNow);

        double distanciaTotal = 0;
        int dentroDaJanela = 0;
        int foraDaJanela = 0;
        int naoAtendidos = 0;

        // Ordena pedidos por inicio da janela, simulando o conhecimento estatico do lote completo.
        var pedidosOrdenados = pedidos.OrderBy(p => p.JanelaInicio).ToList();

        foreach (var pedido in pedidosOrdenados)
        {
            // Escolhe o entregador com menor custo de insercao (distancia da posicao atual ate a entrega)
            // entre os que tem capacidade -- heuristica gulosa classica de vizinho mais proximo.
            Guid? melhorEntregador = null;
            double melhorCusto = double.MaxValue;

            foreach (var entregador in entregadores)
            {
                if (cargaOcupada[entregador.Id] + pedido.PesoKg > entregador.CapacidadeMaximaKg) continue;

                var custo = posicaoCorrente[entregador.Id].DistanciaAte(pedido.Entrega);
                if (custo < melhorCusto)
                {
                    melhorCusto = custo;
                    melhorEntregador = entregador.Id;
                }
            }

            if (melhorEntregador is null)
            {
                naoAtendidos++;
                continue;
            }

            var id = melhorEntregador.Value;
            var entregadorEscolhido = entregadores.First(e => e.Id == id);

            distanciaTotal += melhorCusto;
            cargaOcupada[id] += pedido.PesoKg;
            alocacao[id].Add(pedido.Id);

            var horasViagem = melhorCusto / entregadorEscolhido.VelocidadeMediaKmH;
            horarioCorrente[id] = horarioCorrente[id].AddHours(horasViagem);
            posicaoCorrente[id] = pedido.Entrega;

            if (pedido.RespeitaJanela(horarioCorrente[id])) dentroDaJanela++;
            else foraDaJanela++;
        }

        return new ResultadoSolucaoEstatica(distanciaTotal, dentroDaJanela, foraDaJanela, naoAtendidos, alocacao);
    }
}
