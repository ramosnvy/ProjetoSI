using RotasEntregaSMA.Agentes.Comunicacao;
using RotasEntregaSMA.Agentes.Otimizacao;
using RotasEntregaSMA.Dominio.Modelos;

namespace RotasEntregaSMA.Agentes;

/// <summary>
/// Agente Entregador (secao 3.4): representa um entregador autonomo.
///
/// Ciclo de vida:
///   1. Aguarda no CD ate acumular MinimoLotePedidos (5) ou MaxTempoEsperaBatch expirar.
///   2. Ao partir, carrega fisicamente todos os pedidos do lote e calcula a melhor rota (ACO).
///   3. Executa as entregas. Pedidos so saem do CD com o entregador -- jamais sao coletados
///      durante o percurso.
///   4. Apos a ultima entrega, retorna automaticamente ao CD para aguardar novo lote.
///
/// Regra de negocio critica: o entregador NUNCA coleta um pedido fora do CD.
/// </summary>
public class AgenteEntregador : AgenteBase
{
    private const int MinimoLotePedidos = 2;
    private static readonly TimeSpan MaxTempoEsperaBatch = TimeSpan.FromMinutes(1);

    private readonly EventosSimulacao _eventos;
    private readonly AgenteMapa _mapa;
    private readonly OtimizadorACO _aco = new();
    private readonly object _lock = new();

    /// <summary>Todos os pedidos comprometidos com este entregador (carregados + aguardando no CD).</summary>
    private readonly Dictionary<Guid, Pedido> _pedidosAlocados = new();

    /// <summary>Pedidos aceitos no leilao que ainda aguardam coleta fisica no CD (nao partiram ainda).</summary>
    private readonly Dictionary<Guid, Pedido> _pedidosAguardandoColeta = new();

    /// <summary>Candidatos do leilao atual; descartados apos aceite ou rejeicao.</summary>
    private readonly Dictionary<Guid, Pedido> _pedidoCandidatoAtual = new();

    private readonly PosicaoGeografica _centroDistribuicao;
    private Guid? _despachanteId;
    private TimeSpan _tempoSimuladoAtual;
    private TimeSpan _inicioEsperaBatch;   // marcado quando o primeiro pedido chega ao lote atual

    public EstadoEntregador Estado { get; }

    public AgenteEntregador(Guid id, string nome, PosicaoGeografica posicaoInicial, double capacidadeMaximaKg,
        double velocidadeMediaKmH, BarramentoMensagens barramento, AgenteMapa mapa, EventosSimulacao eventos,
        PosicaoGeografica centroDistribuicao)
        : base(id, barramento)
    {
        _mapa = mapa;
        _eventos = eventos;
        _centroDistribuicao = centroDistribuicao;
        Estado = new EstadoEntregador
        {
            Id = id,
            Nome = nome,
            PosicaoAtual = posicaoInicial,
            CapacidadeMaximaKg = capacidadeMaximaKg,
            VelocidadeMediaKmH = velocidadeMediaKmH,
            Rota = new RotaEntregador { EntregadorId = id }
        };
    }

    public void DefinirDespachante(Guid despachanteId) => _despachanteId = despachanteId;

    public void AtualizarTempoSimulado(TimeSpan tempo) => _tempoSimuladoAtual = tempo;

    protected override async Task ProcessarMensagemAsync(MensagemACL mensagem)
    {
        switch (mensagem.Performativa)
        {
            case Performativa.CallForProposal:
                await ProcessarCfpAsync(mensagem);
                break;
            case Performativa.AcceptProposal:
                await ProcessarAceiteAsync(mensagem);
                break;
            case Performativa.RejectProposal:
                if (mensagem.Conteudo is ConteudoDecisao d)
                    _pedidoCandidatoAtual.Remove(d.PedidoId);
                break;
            case Performativa.Cancel:
                await ProcessarCancelamentoAsync(mensagem);
                break;
        }
    }

    private async Task ProcessarCfpAsync(MensagemACL mensagem)
    {
        var conteudo = (ConteudoCFP)mensagem.Conteudo;
        var (viavel, custo, posicao) = AvaliarInsercao(conteudo.Pedido);

        if (!viavel)
        {
            await EnviarAsync(new MensagemACL
            {
                Performativa = Performativa.Refuse,
                RemetenteId = Id,
                DestinatarioId = mensagem.RemetenteId,
                ConversationId = mensagem.ConversationId,
                Conteudo = new ConteudoRefuse(conteudo.Pedido.Id, Id, "Capacidade insuficiente ou tempo limite inviavel")
            });
            return;
        }

        RegistrarCandidato(conteudo.Pedido);

        await EnviarAsync(new MensagemACL
        {
            Performativa = Performativa.Propose,
            RemetenteId = Id,
            DestinatarioId = mensagem.RemetenteId,
            ConversationId = mensagem.ConversationId,
            Conteudo = new ConteudoProposta(conteudo.Pedido.Id, Id, custo, posicao)
        });

        _eventos.Publicar(TipoEventoSimulacao.PropostaRecebida, new { EntregadorId = Id, PedidoId = conteudo.Pedido.Id, Custo = custo });
    }

    private async Task ProcessarAceiteAsync(MensagemACL mensagem)
    {
        var conteudo = (ConteudoDecisao)mensagem.Conteudo;
        if (!conteudo.Aceito || !_pedidoCandidatoAtual.TryGetValue(conteudo.PedidoId, out var pedido))
            return;

        var (viavel, _, posicao) = AvaliarInsercao(pedido);
        if (!viavel)
        {
            await EnviarAsync(new MensagemACL
            {
                Performativa = Performativa.Refuse,
                RemetenteId = Id,
                DestinatarioId = mensagem.RemetenteId,
                ConversationId = mensagem.ConversationId,
                Conteudo = new ConteudoRefuse(pedido.Id, Id, "Capacidade mudou apos a proposta")
            });
            return;
        }

        InserirPedido(pedido, posicao);
        pedido.Status = StatusPedido.Alocado;
        pedido.EntregadorAlocadoId = Id;
        _pedidoCandidatoAtual.Remove(conteudo.PedidoId);

        _eventos.Publicar(TipoEventoSimulacao.PedidoAlocado, new { EntregadorId = Id, Nome = Estado.Nome, PedidoId = pedido.Id, Rota = Estado.Rota });

        await EnviarAsync(new MensagemACL
        {
            Performativa = Performativa.Inform,
            RemetenteId = Id,
            DestinatarioId = mensagem.RemetenteId,
            ConversationId = mensagem.ConversationId,
            Conteudo = new ConteudoInforme("PedidoConfirmado", pedido.Id)
        });
    }

    private Task ProcessarCancelamentoAsync(MensagemACL mensagem)
    {
        var conteudo = (ConteudoCancelamento)mensagem.Conteudo;
        lock (_lock)
        {
            if (_pedidosAlocados.Remove(conteudo.PedidoId, out var pedido))
            {
                _pedidosAguardandoColeta.Remove(conteudo.PedidoId);
                Estado.CapacidadeOcupadaKg -= pedido.PesoKg;

                // Remove parada de entrega do pedido (so existe se o lote ja partiu)
                Estado.Rota.Paradas.RemoveAll(p => p.PedidoId == conteudo.PedidoId);

                // Se nao ha mais pedidos no lote do CD, remove a parada de retorno ao CD
                if (_pedidosAguardandoColeta.Count == 0 && !HaEntregasCarregadas())
                    Estado.Rota.Paradas.RemoveAll(p => p.Tipo == TipoParada.Coleta);

                RecalcularDistanciaTotal();
                _eventos.Publicar(TipoEventoSimulacao.PedidoCancelado, new { EntregadorId = Id, PedidoId = pedido.Id });
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Avalia a viabilidade e o custo do lance para aceitar o pedido.
    ///
    /// Custo = distancia que falta percorrer ate chegar ao CD + CD → nova entrega.
    /// Isso representa o trabalho INCREMENTAL para lidar com o novo pedido, comparavel
    /// entre agentes independentemente do tamanho de suas rotas atuais.
    ///
    /// A verificacao de tempo usa apenas o caminho CD → nova entrega a partir do momento
    /// estimado de chegada ao CD, pois as outras entregas ja estao comprometidas.
    /// </summary>
    public (bool viavel, double custo, int posicao) AvaliarInsercao(Pedido pedido)
    {
        lock (_lock)
        {
            // Capacidade: carregados + aguardando no CD
            if (Estado.CapacidadeOcupadaKg + pedido.PesoKg > Estado.CapacidadeMaximaKg)
                return (false, 0, 0);

            var distCDEntrega = _mapa.CalcularDistanciaKm(_centroDistribuicao, pedido.Entrega);
            var distAteCDPercorrida = CalcularDistanciaAteCDPercorrida();

            // Custo do lance: tempo incremental para buscar no CD e entregar
            var custo = distAteCDPercorrida + distCDEntrega;

            // Verificacao de tempo limite em tempo simulado
            var horasEstimadas = custo / Estado.VelocidadeMediaKmH;
            var tempoChegadaSimulado = _tempoSimuladoAtual + TimeSpan.FromHours(horasEstimadas);
            var tempoLimite = pedido.TempoSimuladoCriacao + pedido.TempoLimiteSimulado;

            if (tempoChegadaSimulado > tempoLimite)
                return (false, 0, 0);

            return (true, custo, 0);
        }
    }

    /// <summary>
    /// Distancia da posicao atual ate o CD, passando pela rota existente somente ate
    /// a parada de coleta (CD). Se ja estiver no CD, retorna zero.
    /// Se nao houver parada de coleta, soma a rota atual ate o ultimo ponto mais a
    /// distancia desse ponto ao CD.
    /// </summary>
    private double CalcularDistanciaAteCDPercorrida()
    {
        if (EstaNoCD()) return 0;

        double dist = 0;
        var pos = Estado.PosicaoAtual;

        foreach (var parada in Estado.Rota.Paradas)
        {
            dist += _mapa.CalcularDistanciaKm(pos, parada.Posicao);
            pos = parada.Posicao;
            if (parada.Tipo == TipoParada.Coleta)
                return dist; // chegou ao ponto CD na rota
        }

        // Sem CD na rota: termina as entregas carregadas e vai ao CD
        dist += _mapa.CalcularDistanciaKm(pos, _centroDistribuicao);
        return dist;
    }

    public void RegistrarCandidato(Pedido pedido) => _pedidoCandidatoAtual[pedido.Id] = pedido;

    /// <summary>
    /// Registra o pedido aceito no lote aguardando coleta no CD.
    /// NAO adiciona parada de entrega a rota: isso so ocorre no momento da partida do lote.
    /// Garante que a rota inclua uma parada de retorno ao CD se o agente estiver em campo.
    /// </summary>
    private void InserirPedido(Pedido pedido, int _)
    {
        lock (_lock)
        {
            Estado.CapacidadeOcupadaKg += pedido.PesoKg;
            _pedidosAlocados[pedido.Id] = pedido;
            _pedidosAguardandoColeta[pedido.Id] = pedido;

            // Marca o inicio do lote para o timer de espera
            if (_pedidosAguardandoColeta.Count == 1)
                _inicioEsperaBatch = _tempoSimuladoAtual;

            // Se o agente nao esta no CD, precisa de uma parada de retorno ao CD
            if (!EstaNoCD() && !TemParadaColeta())
            {
                Estado.Rota.Paradas.Add(new ParadaRota
                {
                    PedidoId = Guid.Empty,
                    Tipo = TipoParada.Coleta,
                    Posicao = _centroDistribuicao
                });
                RecalcularDistanciaTotal();
            }
            // Se ja esta no CD, nao ha parada a adicionar -- a partida sera decidida em Avancar()
        }
    }

    /// <summary>
    /// Verifica se o lote esta pronto para partir e, em caso positivo, carrega todos os
    /// pedidos aguardando, otimiza a rota com ACO e inicia as entregas.
    /// O lote parte quando ha MinimoLotePedidos pedidos OU quando MaxTempoEsperaBatch expirou.
    /// </summary>
    private bool TentarPartirComLote()
    {
        if (_pedidosAguardandoColeta.Count == 0) return false;

        var loteCheio = _pedidosAguardandoColeta.Count >= MinimoLotePedidos;
        var timeoutExpirou = _tempoSimuladoAtual - _inicioEsperaBatch >= MaxTempoEsperaBatch;

        if (!loteCheio && !timeoutExpirou) return false;

        // Parte com o lote atual
        foreach (var pedido in _pedidosAguardandoColeta.Values)
        {
            pedido.Status = StatusPedido.EmRota;
            Estado.Rota.Paradas.Add(new ParadaRota
            {
                PedidoId = pedido.Id,
                Tipo = TipoParada.Entrega,
                Posicao = pedido.Entrega
            });
        }
        _pedidosAguardandoColeta.Clear();

        // Otimiza toda a rota com ACO para o melhor trajeto de entrega
        ReotimizarComACO();
        RecalcularDistanciaTotal();
        return true;
    }

    private bool EstaNoCD() =>
        _mapa.CalcularDistanciaKm(Estado.PosicaoAtual, _centroDistribuicao) < 0.1;

    private bool TemParadaColeta() =>
        Estado.Rota.Paradas.Any(p => p.Tipo == TipoParada.Coleta);

    private bool HaEntregasCarregadas() =>
        Estado.Rota.Paradas.Any(p => p.Tipo == TipoParada.Entrega);

    /// <summary>
    /// Reotimiza com ACO todas as paradas de entrega da rota.
    /// Sempre busca a melhor sequencia de visita a partir da posicao atual.
    /// </summary>
    private void ReotimizarComACO()
    {
        var paradas = Estado.Rota.Paradas;
        if (paradas.Count < 2) return;

        var pontos = paradas.Select(p => p.Posicao).ToList();
        var ordem = _aco.OtimizarOrdem(Estado.PosicaoAtual, pontos);
        var paradasReordenadas = ordem.Select(indice => paradas[indice]).ToList();
        Estado.Rota.Paradas.Clear();
        Estado.Rota.Paradas.AddRange(paradasReordenadas);
    }

    private void RecalcularDistanciaTotal()
    {
        double total = 0;
        var posicao = Estado.PosicaoAtual;
        foreach (var parada in Estado.Rota.Paradas)
        {
            total += _mapa.CalcularDistanciaKm(posicao, parada.Posicao);
            posicao = parada.Posicao;
        }
        Estado.Rota.DistanciaTotalKm = total;
    }

    /// <summary>
    /// Avanca o entregador no tempo simulado:
    /// - Se estiver no CD sem rota: tenta partir com o lote atual.
    /// - Se estiver fora do CD sem rota: inicia o retorno ao CD.
    /// - Se tiver parada de Coleta (CD) como proxima: ao chegar, tenta partir com o lote.
    /// - Se tiver parada de Entrega como proxima: ao chegar, registra a entrega.
    /// </summary>
    public void Avancar(TimeSpan deltaSimulado)
    {
        lock (_lock)
        {
            // Sem rota: verifica o que fazer
            if (Estado.Rota.Paradas.Count == 0)
            {
                if (EstaNoCD())
                {
                    // No CD: tentar partir com o lote (fica parado ate o lote estar pronto)
                    if (TentarPartirComLote())
                    {
                        _eventos.Publicar(TipoEventoSimulacao.PosicaoAtualizada, new
                        {
                            EntregadorId = Id,
                            Nome = Estado.Nome,
                            Posicao = Estado.PosicaoAtual,
                            Rota = Estado.Rota
                        });
                    }
                }
                else
                {
                    // Fora do CD e sem rota: retornar ao CD
                    Estado.Rota.Paradas.Add(new ParadaRota
                    {
                        PedidoId = Guid.Empty,
                        Tipo = TipoParada.Coleta,
                        Posicao = _centroDistribuicao
                    });
                    RecalcularDistanciaTotal();
                }
                // Se ainda sem rota (aguardando lote), nao movimenta
                if (Estado.Rota.Paradas.Count == 0) return;
            }

            var proximaParada = Estado.Rota.Paradas[0];
            var distanciaAteProxima = _mapa.CalcularDistanciaKm(Estado.PosicaoAtual, proximaParada.Posicao);
            var distanciaPercorrivel = Estado.VelocidadeMediaKmH * deltaSimulado.TotalHours;

            if (distanciaPercorrivel >= distanciaAteProxima || distanciaAteProxima < 0.01)
            {
                // Chegou na parada
                Estado.PosicaoAtual = proximaParada.Posicao;
                Estado.DistanciaPercorridaKm += distanciaAteProxima;
                Estado.Rota.Paradas.RemoveAt(0);

                if (proximaParada.Tipo == TipoParada.Coleta)
                {
                    // Chegou ao CD: tenta partir com o lote acumulado
                    TentarPartirComLote();
                    // Se lote nao esta pronto, fica aguardando (rota vazia, loop voltara aqui)
                }
                else if (_pedidosAlocados.Remove(proximaParada.PedidoId, out var pedido))
                {
                    pedido.Status = StatusPedido.Entregue;
                    pedido.EntregueEm = DateTime.UtcNow;
                    Estado.CapacidadeOcupadaKg -= pedido.PesoKg;
                    Estado.PedidosEntregues++;
                    _eventos.Publicar(TipoEventoSimulacao.PedidoEntregue, new { EntregadorId = Id, PedidoId = pedido.Id });
                }

                RecalcularDistanciaTotal();
            }
            else
            {
                // Avanca proporcionalmente em direcao a proxima parada
                var fracao = distanciaPercorrivel / distanciaAteProxima;
                var novaX = Estado.PosicaoAtual.X + (proximaParada.Posicao.X - Estado.PosicaoAtual.X) * fracao;
                var novaY = Estado.PosicaoAtual.Y + (proximaParada.Posicao.Y - Estado.PosicaoAtual.Y) * fracao;
                Estado.PosicaoAtual = new PosicaoGeografica(novaX, novaY);
                Estado.DistanciaPercorridaKm += distanciaPercorrivel;
            }

            _eventos.Publicar(TipoEventoSimulacao.PosicaoAtualizada, new
            {
                EntregadorId = Id,
                Nome = Estado.Nome,
                Posicao = Estado.PosicaoAtual,
                Rota = Estado.Rota
            });
        }
    }
}
