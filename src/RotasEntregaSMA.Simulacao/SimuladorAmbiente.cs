using RotasEntregaSMA.Agentes;
using RotasEntregaSMA.Agentes.Comunicacao;
using RotasEntregaSMA.Agentes.Otimizacao;
using RotasEntregaSMA.Dominio.Modelos;

namespace RotasEntregaSMA.Simulacao;

/// <summary>
/// Motor de simulacao: cria e conecta os agentes (Despachante = centro de distribuicao,
/// Entregadores, Mapa), gera pedidos dinamicamente ao longo do tempo simulado, avanca
/// o movimento dos entregadores e publica eventos para a interface web em tempo real.
/// Ao final, calcula o comparativo com a solucao estatica de referencia.
/// </summary>
public class SimuladorAmbiente
{
    private readonly BarramentoMensagens _barramento = new();
    private readonly EventosSimulacao _eventos;
    private readonly AgenteMapa _mapa = new();
    private readonly Random _rng = new();

    private AgenteDespachante _despachante = null!;
    private List<AgenteEntregador> _entregadores = new();
    private readonly List<Pedido> _todosPedidos = new();
    private PosicaoGeografica _centroDistribuicao;

    private ConfiguracaoSimulacao _config = new();
    private CancellationTokenSource? _cts;
    private Task? _execucao;

    /// <summary>Relogio interno de tempo simulado; zero no inicio de cada rodada.</summary>
    private TimeSpan _tempoSimuladoAtual;

    public bool EmExecucao { get; private set; }
    public ConfiguracaoSimulacao ConfiguracaoAtual => _config;

    public SimuladorAmbiente(EventosSimulacao eventos)
    {
        _eventos = eventos;
    }

    public IReadOnlyList<EstadoEntregador> ObterEstadoEntregadores() =>
        _entregadores.Select(e => e.Estado).ToList();

    public void Iniciar(ConfiguracaoSimulacao? config = null)
    {
        if (EmExecucao) return;

        _config = config ?? new ConfiguracaoSimulacao();
        _todosPedidos.Clear();
        _tempoSimuladoAtual = TimeSpan.Zero;
        ConfigurarAgentes();

        EmExecucao = true;
        _cts = new CancellationTokenSource();
        _execucao = ExecutarAsync(_cts.Token);
    }

    public async Task PararAsync()
    {
        if (!EmExecucao) return;

        _cts?.Cancel();
        if (_execucao is not null)
        {
            try { await _execucao; } catch (OperationCanceledException) { }
        }

        foreach (var entregador in _entregadores)
            await entregador.PararAsync();
        await _despachante.PararAsync();

        EmExecucao = false;
    }

    private void ConfigurarAgentes()
    {
        var despachanteId = Guid.NewGuid();
        _centroDistribuicao = new PosicaoGeografica(_config.TamanhoGradeKm / 2, _config.TamanhoGradeKm / 2);

        _despachante = new AgenteDespachante(despachanteId, _centroDistribuicao, _barramento, _eventos);
        _despachante.Iniciar();

        var nomes = new[] { "Ana", "Bruno", "Carla", "Diego", "Elisa", "Fabio", "Gabriela", "Hugo", "Ines", "Joao" };
        _entregadores = new List<AgenteEntregador>();

        for (int i = 0; i < _config.NumeroEntregadores; i++)
        {
            var id = Guid.NewGuid();
            var entregador = new AgenteEntregador(
                id,
                nomes[i % nomes.Length],
                _centroDistribuicao,
                _config.CapacidadeMaximaKg,
                _config.VelocidadeMediaKmH,
                _barramento,
                _mapa,
                _eventos,
                _centroDistribuicao);

            entregador.DefinirDespachante(despachanteId);
            entregador.Iniciar();

            _entregadores.Add(entregador);
            _despachante.RegistrarEntregador(id);
        }
    }

    private async Task ExecutarAsync(CancellationToken token)
    {
        var passoReal = TimeSpan.FromMilliseconds(200);
        var deltaSimuladoPorPasso = TimeSpan.FromSeconds(passoReal.TotalSeconds * _config.FatorAceleracaoTempo);
        var duracaoReal = TimeSpan.FromSeconds(_config.DuracaoSimulacaoMinutosSimulados * 60 / _config.FatorAceleracaoTempo);
        var tempoDecorridoReal = TimeSpan.Zero;

        try
        {
            while (tempoDecorridoReal < duracaoReal)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(passoReal, token);
                tempoDecorridoReal += passoReal;
                _tempoSimuladoAtual += deltaSimuladoPorPasso;

                // Atualiza o relogio simulado em cada entregador
                foreach (var entregador in _entregadores)
                    entregador.AtualizarTempoSimulado(_tempoSimuladoAtual);

                // Gera N pedidos via distribuicao de Poisson: correto para alta aceleracao
                // onde a taxa media por tick pode superar 1 (prob > 1 num sorteio simples
                // caparia em 1; Poisson gera o numero real esperado de chegadas).
                int novosNesteTick = AmostragemPoisson(deltaSimuladoPorPasso.TotalSeconds / _config.IntervaloMedioNovoPedidoSegundosSimulados);
                for (int i = 0; i < novosNesteTick; i++)
                {
                    var pedido = GerarPedidoAleatorio();
                    _todosPedidos.Add(pedido);
                    await _despachante.ReceberNovoPedidoAsync(pedido);
                    TentarCancelarPedidoAleatorio();
                }

                // Cancela pedidos cujo tempo limite simulado foi excedido
                CancelarPedidosExpirados();

                foreach (var entregador in _entregadores)
                    entregador.Avancar(deltaSimuladoPorPasso);

                PublicarMetricas();
            }
        }
        catch (OperationCanceledException)
        {
            // parada solicitada pelo usuario
        }
    }

    /// <summary>
    /// Amostragem de distribuicao de Poisson (algoritmo de Knuth).
    /// Retorna o numero inteiro de eventos esperados para uma taxa media dada.
    /// Funciona corretamente mesmo quando a taxa media supera 1 por tick.
    /// </summary>
    private int AmostragemPoisson(double media)
    {
        if (media <= 0) return 0;
        // Para medias grandes usa aproximacao normal para evitar underflow de exp(-media)
        if (media > 30)
            return (int)Math.Max(0, Math.Round(media + Math.Sqrt(media) * (2 * _rng.NextDouble() - 1) * 1.73));
        var limiar = Math.Exp(-media);
        int k = 0;
        double p = 1.0;
        do { k++; p *= _rng.NextDouble(); } while (p > limiar);
        return k - 1;
    }

    private Pedido GerarPedidoAleatorio()
    {
        var entrega = new PosicaoGeografica(
            _rng.NextDouble() * _config.TamanhoGradeKm,
            _rng.NextDouble() * _config.TamanhoGradeKm);

        var agora = DateTime.UtcNow;
        var minutosJanela = _config.JanelaMinMinutos + _rng.NextDouble() * (_config.JanelaMaxMinutos - _config.JanelaMinMinutos);
        var tempoLimite = TimeSpan.FromMinutes(_config.TempoLimitePedidoMinutos);

        return new Pedido
        {
            Coleta = _centroDistribuicao,
            Entrega = entrega,
            JanelaInicio = agora,
            JanelaFim = agora.AddMinutes(minutosJanela),
            PesoKg = 1 + _rng.NextDouble() * 4,
            TempoSimuladoCriacao = _tempoSimuladoAtual,
            TempoLimiteSimulado = tempoLimite
        };
    }

    private void TentarCancelarPedidoAleatorio()
    {
        if (_rng.NextDouble() > 0.05) return; // ~5% de chance a cada novo pedido gerado

        var candidato = _todosPedidos.FirstOrDefault(p => p.Status == StatusPedido.Alocado);
        if (candidato is not null)
        {
            _ = _despachante.CancelarPedidoAsync(candidato);
        }
    }

    /// <summary>
    /// Marca como SemAlocacao os pedidos cujo tempo limite simulado foi excedido
    /// e que ainda nao foram entregues. Pedidos ja em rota sao cancelados ativamente.
    /// </summary>
    private void CancelarPedidosExpirados()
    {
        foreach (var pedido in _todosPedidos)
        {
            if (pedido.Status is StatusPedido.Entregue or StatusPedido.Cancelado or StatusPedido.SemAlocacao)
                continue;

            var expirou = _tempoSimuladoAtual > pedido.TempoSimuladoCriacao + pedido.TempoLimiteSimulado;
            if (!expirou) continue;

            if (pedido.Status is StatusPedido.Alocado or StatusPedido.EmRota)
                _ = _despachante.CancelarPedidoAsync(pedido);
            else
                pedido.Status = StatusPedido.SemAlocacao;
        }
    }

    private void PublicarMetricas()
    {
        var distanciaTotal = _entregadores.Sum(e => e.Estado.DistanciaPercorridaKm);
        var entregues = _entregadores.Sum(e => e.Estado.PedidosEntregues);
        var pendentes = _todosPedidos.Count(p => p.Status is StatusPedido.Novo or StatusPedido.EmLeilao or StatusPedido.Alocado or StatusPedido.EmRota);
        var semAlocacao = _todosPedidos.Count(p => p.Status is StatusPedido.SemAlocacao or StatusPedido.Cancelado);

        _eventos.Publicar(TipoEventoSimulacao.MetricasAtualizadas, new
        {
            TotalPedidos = _todosPedidos.Count,
            DistanciaTotalKm = Math.Round(distanciaTotal, 2),
            PedidosEntregues = entregues,
            PedidosPendentes = pendentes,
            PedidosSemAlocacao = semAlocacao
        });
    }

    /// <summary>
    /// Calcula o comparativo entre o desempenho observado do SMA (dinamico, online) e uma
    /// solucao estatica de referencia recalculada com conhecimento antecipado de todos os
    /// pedidos gerados na rodada -- atendendo ao objetivo especifico (d) do projeto.
    /// </summary>
    public ResultadoComparativo ObterComparativoFinal()
    {
        var entregadoresIniciais = _entregadores
            .Select(e => new EntregadorInicial(e.Id, _centroDistribuicao, e.Estado.VelocidadeMediaKmH, e.Estado.CapacidadeMaximaKg))
            .ToList();

        var baseline = new SolucaoEstaticaBaseline().Resolver(_todosPedidos, entregadoresIniciais);

        var distanciaSma = _entregadores.Sum(e => e.Estado.DistanciaPercorridaKm);
        var entreguesSma = _entregadores.Sum(e => e.Estado.PedidosEntregues);
        var semAlocacaoSma = _todosPedidos.Count(p => p.Status is StatusPedido.SemAlocacao or StatusPedido.Cancelado);

        return new ResultadoComparativo(
            TotalPedidosGerados: _todosPedidos.Count,
            DistanciaTotalSmaKm: Math.Round(distanciaSma, 2),
            PedidosEntreguesSma: entreguesSma,
            PedidosSemAlocacaoSma: semAlocacaoSma,
            DistanciaTotalEstaticaKm: Math.Round(baseline.DistanciaTotalKm, 2),
            PedidosDentroDaJanelaEstatica: baseline.PedidosAtendidosDentroDaJanela,
            PedidosForaDaJanelaEstatica: baseline.PedidosForaDaJanela,
            PedidosNaoAtendidosEstatica: baseline.PedidosNaoAtendidos);
    }
}
