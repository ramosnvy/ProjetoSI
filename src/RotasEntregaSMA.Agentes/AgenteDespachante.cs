using System.Collections.Concurrent;
using RotasEntregaSMA.Agentes.Comunicacao;
using RotasEntregaSMA.Dominio.Modelos;

namespace RotasEntregaSMA.Agentes;

/// <summary>
/// Agente Despachante (secao 3.4): representa o centro de distribuicao. Recebe novos
/// pedidos, conduz o leilao via Contract Net Protocol (Smith, 1980) -- anunciando CFPs,
/// coletando propostas dos entregadores e decidindo a alocacao vencedora -- e monitora
/// o estado geral das entregas.
/// </summary>
public class AgenteDespachante : AgenteBase
{
    private readonly EventosSimulacao _eventos;
    private readonly List<Guid> _entregadoresRegistrados = new();
    private readonly ConcurrentDictionary<Guid, LeilaoEmAndamento> _leiloesAtivos = new();
    private readonly TimeSpan _timeoutLeilao;

    public PosicaoGeografica PosicaoCentroDistribuicao { get; }

    public AgenteDespachante(Guid id, PosicaoGeografica posicaoCentroDistribuicao, BarramentoMensagens barramento,
        EventosSimulacao eventos, TimeSpan? timeoutLeilao = null)
        : base(id, barramento)
    {
        PosicaoCentroDistribuicao = posicaoCentroDistribuicao;
        _eventos = eventos;
        _timeoutLeilao = timeoutLeilao ?? TimeSpan.FromMilliseconds(350);
    }

    public void RegistrarEntregador(Guid entregadorId) => _entregadoresRegistrados.Add(entregadorId);

    /// <summary>
    /// Recebe um novo pedido do ambiente de simulacao e inicia um leilao (CFP) entre
    /// todos os agentes entregadores registrados.
    /// </summary>
    public async Task ReceberNovoPedidoAsync(Pedido pedido)
    {
        pedido.Status = StatusPedido.EmLeilao;
        var conversationId = Guid.NewGuid();
        var leilao = new LeilaoEmAndamento(pedido);
        _leiloesAtivos[conversationId] = leilao;

        _eventos.Publicar(TipoEventoSimulacao.PedidoCriado, new { PedidoId = pedido.Id, pedido.Coleta, pedido.Entrega, pedido.JanelaInicio, pedido.JanelaFim, pedido.PesoKg });
        _eventos.Publicar(TipoEventoSimulacao.LeilaoIniciado, new { PedidoId = pedido.Id, Participantes = _entregadoresRegistrados.Count });

        foreach (var entregadorId in _entregadoresRegistrados)
        {
            await EnviarAsync(new MensagemACL
            {
                Performativa = Performativa.CallForProposal,
                RemetenteId = Id,
                DestinatarioId = entregadorId,
                ConversationId = conversationId,
                Conteudo = new ConteudoCFP(pedido)
            });
        }

        _ = FinalizarLeilaoAposTimeoutAsync(conversationId);
    }

    /// <summary>
    /// Cancela um pedido ja alocado (ou em leilao), notificando o entregador responsavel.
    /// </summary>
    public async Task CancelarPedidoAsync(Pedido pedido)
    {
        pedido.Status = StatusPedido.Cancelado;
        if (pedido.EntregadorAlocadoId is Guid entregadorId)
        {
            await EnviarAsync(new MensagemACL
            {
                Performativa = Performativa.Cancel,
                RemetenteId = Id,
                DestinatarioId = entregadorId,
                ConversationId = Guid.NewGuid(),
                Conteudo = new ConteudoCancelamento(pedido.Id)
            });
        }
    }

    protected override Task ProcessarMensagemAsync(MensagemACL mensagem)
    {
        switch (mensagem.Performativa)
        {
            case Performativa.Propose:
                {
                    var conteudo = (ConteudoProposta)mensagem.Conteudo;
                    if (_leiloesAtivos.TryGetValue(mensagem.ConversationId, out var leilao))
                        leilao.Propostas.Add(conteudo);
                    break;
                }
            case Performativa.Refuse:
                // recusa registrada implicitamente pela ausencia de proposta; nada a fazer.
                break;
            case Performativa.Inform:
                // confirmacoes dos entregadores podem ser usadas para auditoria/log futuro.
                break;
        }
        return Task.CompletedTask;
    }

    private async Task FinalizarLeilaoAposTimeoutAsync(Guid conversationId)
    {
        await Task.Delay(_timeoutLeilao);

        if (!_leiloesAtivos.TryRemove(conversationId, out var leilao)) return;

        var propostas = leilao.Propostas.ToList();

        if (propostas.Count == 0)
        {
            leilao.Pedido.Status = StatusPedido.SemAlocacao;
            _eventos.Publicar(TipoEventoSimulacao.PedidoSemAlocacao, new { PedidoId = leilao.Pedido.Id });
            return;
        }

        var vencedora = propostas.OrderBy(p => p.CustoEstimadoKm).First();

        foreach (var proposta in propostas)
        {
            var aceito = proposta.EntregadorId == vencedora.EntregadorId;
            await EnviarAsync(new MensagemACL
            {
                Performativa = aceito ? Performativa.AcceptProposal : Performativa.RejectProposal,
                RemetenteId = Id,
                DestinatarioId = proposta.EntregadorId,
                ConversationId = conversationId,
                Conteudo = new ConteudoDecisao(leilao.Pedido.Id, proposta.EntregadorId, aceito)
            });
        }
    }

    private class LeilaoEmAndamento
    {
        public Pedido Pedido { get; }
        public ConcurrentBag<ConteudoProposta> Propostas { get; } = new();

        public LeilaoEmAndamento(Pedido pedido) => Pedido = pedido;
    }
}
