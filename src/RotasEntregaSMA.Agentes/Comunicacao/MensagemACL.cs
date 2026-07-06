using RotasEntregaSMA.Dominio.Modelos;

namespace RotasEntregaSMA.Agentes.Comunicacao;

/// <summary>
/// Performativas do protocolo, inspiradas no padrao FIPA-ACL usado pelo JADE,
/// aplicadas aqui ao Contract Net Protocol (Smith, 1980) descrito no artigo.
/// </summary>
public enum Performativa
{
    CallForProposal,   // CFP: despachante anuncia um pedido para leilao
    Propose,           // entregador propoe um custo para atender o pedido
    Refuse,            // entregador informa que nao pode atender (capacidade/janela)
    AcceptProposal,    // despachante aceita a proposta vencedora
    RejectProposal,    // despachante rejeita as propostas perdedoras
    Inform,            // confirmacao/atualizacao de estado (ex: entrega concluida)
    Cancel             // cancelamento de um pedido ja alocado
}

/// <summary>
/// Envelope generico de mensagem ACL trocada entre agentes via o barramento.
/// </summary>
public class MensagemACL
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Performativa Performativa { get; init; }
    public required Guid RemetenteId { get; init; }
    public required Guid DestinatarioId { get; init; }
    public required Guid ConversationId { get; init; }
    public required object Conteudo { get; init; }
    public DateTime EnviadaEm { get; init; } = DateTime.UtcNow;
}

/// <summary>Conteudo de um CFP: o pedido a ser leiloado.</summary>
public record ConteudoCFP(Pedido Pedido);

/// <summary>Conteudo de uma proposta (bid) de um entregador.</summary>
public record ConteudoProposta(Guid PedidoId, Guid EntregadorId, double CustoEstimadoKm, int PosicaoInsercao);

/// <summary>Conteudo de recusa.</summary>
public record ConteudoRefuse(Guid PedidoId, Guid EntregadorId, string Motivo);

/// <summary>Conteudo da decisao final do despachante (aceite ou rejeicao).</summary>
public record ConteudoDecisao(Guid PedidoId, Guid EntregadorId, bool Aceito);

/// <summary>Conteudo de cancelamento.</summary>
public record ConteudoCancelamento(Guid PedidoId);

/// <summary>Conteudo de informe (ex.: entrega concluida, posicao atualizada).</summary>
public record ConteudoInforme(string TipoEvento, object Dados);
