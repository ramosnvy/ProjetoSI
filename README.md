# Otimizacao de Rotas de Entrega Urbana com Sistemas Multiagentes (.NET)

Implementacao em **.NET 8** do projeto final da disciplina de Sistemas Inteligentes
Aplicados (UTFPR), a partir do artigo *"Otimizacao de Rotas de Entrega Urbana com
Sistemas Multiagentes"* (Pedro Augusto, Nicolas Prado, Victor Henrique, Luis Eduardo) e
do roteiro *"Projeto Final - Sistemas Inteligentes Aplicados"*.

O artigo original propunha o uso do **JADE (Java Agent DEvelopment Framework)** para os
agentes e uma interface web simples em **Python/Flask**. Como o pedido foi para
implementar tudo em **.NET**, o papel do JADE foi substituido por um framework de
agentes proprio, escrito em C# puro (mensageria ACL assincrona via
`System.Threading.Channels`), e a interface web foi implementada com **ASP.NET Core +
SignalR**, mantendo fielmente a arquitetura, os agentes e o protocolo descritos no
artigo (Contract Net Protocol + ACO).

## Como isso atende ao artigo, secao a secao

| Secao do artigo | Onde esta implementado |
|---|---|
| 3.4 - Tres tipos de agente (Despachante, Entregadores, Mapa) | `AgenteDespachante`, `AgenteEntregador`, `AgenteMapa` em `RotasEntregaSMA.Agentes` |
| 3.4 - Contract Net Protocol (Smith, 1980) | `AgenteDespachante` (CFP -> Propose/Refuse -> Accept/Reject) usando as performativas FIPA-like em `Comunicacao/MensagemACL.cs` |
| 3.5 - SMA + ACO como modulo auxiliar de otimizacao local | `AgenteEntregador.ReotimizarComACO()` chama `OtimizadorACO` apos cada insercao de pedido |
| 3.2 - Otimizacao por Colonia de Formigas | `RotasEntregaSMA.Agentes/Otimizacao/OtimizadorACO.cs` |
| Objetivo (a) - modelar entregadores, pedidos e centro de distribuicao como agentes | O Despachante representa o centro de distribuicao; Pedido e um objeto de dominio manipulado pelos agentes |
| Objetivo (b) - protocolo de negociacao para alocacao dinamica | Leilao via CFP, com heuristica de insercao mais barata (*cheapest insertion*) para o calculo do lance |
| Objetivo (c) - interface visual com rotas em mapa simulado | `RotasEntregaSMA.Api/wwwroot` (canvas + SignalR em tempo real) |
| Objetivo (d) - avaliar comparando com solucao estatica de referencia | `SolucaoEstaticaBaseline` (heuristica gulosa tipo OR-Tools/comercial, sem realocacao) + `SimuladorAmbiente.ObterComparativoFinal()` |
| Cronograma / MVP em 7 semanas | Escopo intencionalmente enxuto: sem Deep RL (secao 2.3, descartado no proprio artigo por ser inadequado a um MVP curto) |

## Arquitetura da solucao

```
RotasEntregaSMA.sln
src/
  RotasEntregaSMA.Dominio/      modelos de dominio (Pedido, PosicaoGeografica, RotaEntregador, ...)
  RotasEntregaSMA.Agentes/      agentes autonomos, barramento ACL, Contract Net Protocol, ACO
  RotasEntregaSMA.Simulacao/    motor de simulacao (gera pedidos, avanca o tempo, calcula metricas)
  RotasEntregaSMA.Api/          ASP.NET Core + SignalR + frontend (wwwroot) com o mapa em tempo real
```

Cada `AgenteEntregador` e cada `AgenteDespachante` processa mensagens de forma
assincrona e independente (sem estado compartilhado direto), trocando mensagens ACL
(`CallForProposal`, `Propose`, `Refuse`, `AcceptProposal`, `RejectProposal`, `Inform`,
`Cancel`) por meio do `BarramentoMensagens`, exatamente como agentes JADE trocariam
mensagens FIPA-ACL pelo Agent Communication Channel.

O `Agente Mapa` fornece distancia e tempo de deslocamento entre pontos, incluindo um
fator de transito variavel para simular condicoes dinamicas em tempo real (secao 1 do
artigo).

## Como rodar

Requer o **.NET 8 SDK** (nao pude compilar/testar este codigo neste ambiente, pois o
sandbox em que trabalho nao tem acesso ao NuGet/instalador do .NET — recomendo compilar
localmente ou com o Claude Code, que tem acesso total ao seu ambiente .NET 10 do
trabalho).

```bash
cd RotasEntregaSMA
dotnet build
dotnet run --project src/RotasEntregaSMA.Api
```

Abra `http://localhost:5080` no navegador. A pagina permite configurar o numero de
entregadores, o tamanho da grade urbana (km), a duracao simulada e o fator de
aceleracao do tempo, e exibe:

- O mapa em tempo real (centro de distribuicao, entregadores e suas rotas);
- O log de eventos do Contract Net Protocol (CFP, alocacoes, cancelamentos);
- Metricas ao vivo (pedidos entregues, pendentes, sem alocacao, distancia total);
- Ao clicar em **"Parar e Comparar"**, uma tabela comparando o desempenho do SMA
  dinamico com a solucao estatica de referencia (objetivo especifico (d)).

## Limitacoes conscientes do MVP (compativeis com o escopo de 7 semanas do artigo)

- A abordagem SMA + Deep RL (secao 2.3) foi descartada, como o proprio artigo recomenda
  para um MVP curto.
- A janela de tempo e verificada de forma simplificada (estimativa por distancia
  acumulada e velocidade media), sem modelar transito historico real.
- A solucao estatica de referencia roda com conhecimento antecipado do lote completo de
  pedidos gerados na rodada, para permitir a comparacao objetivo (d) sem exigir uma
  segunda fonte de dados externa (datasets como Solomon Benchmark/CVRPLIB podem ser
  plugados depois, se desejar aprofundar a avaliacao).
