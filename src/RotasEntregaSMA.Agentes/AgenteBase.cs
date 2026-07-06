using RotasEntregaSMA.Agentes.Comunicacao;

namespace RotasEntregaSMA.Agentes;

/// <summary>
/// Base para todo agente autonomo do sistema (item (a) dos objetivos do projeto).
/// Cada agente possui uma caixa de entrada propria no barramento e processa mensagens
/// ACL de forma assincrona e independente, sem estado compartilhado direto com outros
/// agentes -- toda coordenacao ocorre por troca de mensagens (Wooldridge, 2009).
/// </summary>
public abstract class AgenteBase
{
    public Guid Id { get; }
    protected readonly BarramentoMensagens Barramento;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    protected AgenteBase(Guid id, BarramentoMensagens barramento)
    {
        Id = id;
        Barramento = barramento;
        Barramento.Registrar(Id);
    }

    public void Iniciar()
    {
        _cts = new CancellationTokenSource();
        _loop = ExecutarLoopAsync(_cts.Token);
    }

    public async Task PararAsync()
    {
        _cts?.Cancel();
        if (_loop is not null)
        {
            try { await _loop; } catch (OperationCanceledException) { }
        }
    }

    private async Task ExecutarLoopAsync(CancellationToken token)
    {
        var leitor = Barramento.ObterLeitor(Id);
        try
        {
            await foreach (var mensagem in leitor.ReadAllAsync(token))
            {
                await ProcessarMensagemAsync(mensagem);
            }
        }
        catch (OperationCanceledException)
        {
            // encerramento normal do agente
        }
    }

    protected abstract Task ProcessarMensagemAsync(MensagemACL mensagem);

    protected Task EnviarAsync(MensagemACL mensagem) => Barramento.EnviarAsync(mensagem);
}
