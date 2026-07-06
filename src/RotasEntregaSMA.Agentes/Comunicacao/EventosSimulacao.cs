using System.Threading.Channels;

namespace RotasEntregaSMA.Agentes.Comunicacao;

/// <summary>
/// Tipos de evento publicados para a interface web em tempo real. Nao faz parte do
/// protocolo entre agentes (isso e o BarramentoMensagens) -- e apenas um canal de
/// observacao para a camada de apresentacao (item (c) dos objetivos do projeto).
/// </summary>
public enum TipoEventoSimulacao
{
    PedidoCriado,
    LeilaoIniciado,
    PropostaRecebida,
    PedidoAlocado,
    PedidoSemAlocacao,
    PedidoCancelado,
    PosicaoAtualizada,
    PedidoEntregue,
    MetricasAtualizadas
}

public record EventoSimulacao(TipoEventoSimulacao Tipo, object Dados, DateTime OcorridoEm);

/// <summary>
/// Publicador/assinante simples de eventos de simulacao, usado pelo Hub do SignalR
/// para transmitir atualizacoes ao navegador em tempo real.
/// </summary>
public class EventosSimulacao
{
    private readonly Channel<EventoSimulacao> _canal = Channel.CreateUnbounded<EventoSimulacao>();

    public ChannelReader<EventoSimulacao> Leitor => _canal.Reader;

    public void Publicar(TipoEventoSimulacao tipo, object dados)
    {
        _canal.Writer.TryWrite(new EventoSimulacao(tipo, dados, DateTime.UtcNow));
    }
}
