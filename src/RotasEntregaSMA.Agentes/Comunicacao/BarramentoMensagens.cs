using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RotasEntregaSMA.Agentes.Comunicacao;

/// <summary>
/// Barramento de mensagens em memoria: cada agente registra uma caixa de entrada (mailbox)
/// e o barramento roteia mensagens ACL entre agentes, similar ao Agent Communication Channel do JADE.
/// </summary>
public class BarramentoMensagens
{
    private readonly ConcurrentDictionary<Guid, Channel<MensagemACL>> _caixasDeEntrada = new();

    public void Registrar(Guid agenteId)
    {
        _caixasDeEntrada.TryAdd(agenteId, Channel.CreateUnbounded<MensagemACL>());
    }

    public ChannelReader<MensagemACL> ObterLeitor(Guid agenteId)
    {
        if (!_caixasDeEntrada.TryGetValue(agenteId, out var canal))
            throw new InvalidOperationException($"Agente {agenteId} nao esta registrado no barramento.");
        return canal.Reader;
    }

    public async Task EnviarAsync(MensagemACL mensagem)
    {
        if (_caixasDeEntrada.TryGetValue(mensagem.DestinatarioId, out var canal))
        {
            await canal.Writer.WriteAsync(mensagem);
        }
    }

    public async Task TransmitirAsync(IEnumerable<Guid> destinatarios, Func<Guid, MensagemACL> fabricaMensagem)
    {
        foreach (var destinatarioId in destinatarios)
        {
            await EnviarAsync(fabricaMensagem(destinatarioId));
        }
    }
}
