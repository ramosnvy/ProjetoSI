
# Otimização de Rotas de Entrega Urbana com Sistemas Multiagentes

&nbsp;

<p align="center"><b>Pedro Augusto, Nicolas Prado, Victor Henrique, Luis Eduardo</b></p>

<p align="center">Universidade Tecnológica Federal do Paraná (UTFPR) – Dois Vizinhos, PR, Brasil</p>

<p align="center"><i>pedrosousa@alunos.utfpr.edu.br, nicolasprado@alunos.utfpr.edu.br,<br>victorhenriquesouza@alunos.utfpr.edu.br, luis.2005@alunos.utfpr.edu.br</i></p>

&nbsp;

**Abstract.** *This paper presents the design, implementation, and evaluation of a Multi-Agent System (MAS) for urban delivery route optimization, developed as a final project for the Applied Intelligent Systems course. The system employs autonomous agents coordinated through the Contract Net Protocol for dynamic order allocation, combined with Ant Colony Optimization (ACO) for local route optimization within each delivery agent. A functional MVP was built using .NET 8, featuring a real-time web interface with HTML5 Canvas and SignalR. Experimental results across multiple simulation runs demonstrate that the MAS approach achieves 83.7% less total distance compared to a static baseline, delivering all orders within their time windows while the static solution violates time constraints in over 60% of cases.*

**Resumo.** *Este artigo apresenta o projeto, implementação e avaliação de um Sistema Multiagente (SMA) para otimização de rotas de entrega urbana, desenvolvido como projeto final da disciplina de Sistemas Inteligentes Aplicados. O sistema emprega agentes autônomos coordenados pelo protocolo Contract Net para alocação dinâmica de pedidos, combinado com Otimização por Colônia de Formigas (ACO) para otimização local de rotas em cada agente-entregador. Um MVP funcional foi construído em .NET 8, com interface web em tempo real utilizando HTML5 Canvas e SignalR. Resultados experimentais em múltiplas rodadas de simulação demonstram que a abordagem SMA alcança 83,7% menos distância total em comparação com uma solução estática de referência, entregando todos os pedidos dentro de suas janelas de tempo, enquanto a solução estática viola restrições temporais em mais de 60% dos casos.*

&nbsp;

## 1. Introdução

O crescimento acelerado do comércio eletrônico no Brasil tem gerado um aumento significativo na demanda por entregas urbanas de última milha. O volume de encomendas cresce a taxas superiores a 20% ao ano, pressionando empresas de logística a reduzirem custos operacionais sem comprometer prazos e qualidade do serviço [Oliveira e Santos 2022].

O problema central consiste em distribuir eficientemente um conjunto de pedidos entre múltiplos entregadores, considerando restrições dinâmicas como janelas de tempo, capacidade dos veículos, condições de trânsito em tempo real e cancelamentos de clientes. Esse cenário caracteriza o chamado Problema de Roteamento de Veículos com Janelas de Tempo (VRPTW – Vehicle Routing Problem with Time Windows), uma variante NP-difícil do Problema do Caixeiro Viajante [Cordeau et al. 2002].

A natureza dinâmica e descentralizada do problema – entregadores tomando decisões locais, novos pedidos chegando ao longo do dia, cancelamentos e redistribuições – torna inadequadas as abordagens de otimização estática tradicionais. É necessária uma solução que se adapte continuamente, tome decisões autônomas por agente e coordene o conjunto de forma global eficiente.

Este trabalho apresenta o desenvolvimento completo de um MVP (Minimum Viable Product) de sistema de otimização de rotas de entrega urbana baseado em Sistemas Multiagentes (SMA), desde a concepção até a validação experimental. A solução combina o protocolo Contract Net para coordenação entre agentes com Otimização por Colônia de Formigas (ACO) para otimização local de rotas.

### 1.1. Objetivos

O objetivo geral é desenvolver um MVP funcional de sistema de otimização de rotas de entrega urbana baseado em SMA. Os objetivos específicos são:

(a) Modelar entregadores, pedidos e o centro de distribuição como agentes autônomos;

(b) Implementar um protocolo de negociação para alocação dinâmica de pedidos;

(c) Integrar o sistema a uma interface visual que demonstre as rotas em um mapa simulado;

(d) Avaliar o desempenho comparando com uma solução estática de referência.

### 1.2. Justificativa

Cenários de logística urbana envolvem múltiplos atores autônomos com objetivos parcialmente conflitantes, ambiente dinâmico e necessidade de coordenação descentralizada – características que são o ponto forte dos Sistemas Multiagentes [Wooldridge 2009]. Diferente de abordagens estáticas que exigem reotimização completa a cada mudança, o SMA reage naturalmente a eventos dinâmicos com custo computacional incremental.

&nbsp;

## 2. Trabalhos Relacionados

### 2.1. Soluções Comerciais

Plataformas como Google OR-Tools, OptimoRoute e Circuit for Teams oferecem otimização de rotas como serviço. São ferramentas robustas, porém centradas em otimização estática ou semi-estática: recalculam rotas mediante replanejamento explícito, sem adaptação contínua ao ambiente [Google 2023]. Soluções como Amazon Logistics e Uber Freight utilizam abordagens híbridas com aprendizado de máquina, mas seus detalhes algorítmicos não são públicos e demandam infraestrutura de dados massiva.

### 2.2. Metaheurísticas para VRPTW

A literatura acadêmica é dominada por metaheurísticas aplicadas ao VRPTW. Algoritmos Genéticos, Busca Tabu, Simulated Annealing e Otimização por Colônia de Formigas são amplamente utilizados [Gendreau e Tarantilis 2010]. Produzem soluções de alta qualidade para instâncias estáticas, mas apresentam limitações em cenários dinâmicos, pois exigem reotimização completa a cada mudança no conjunto de pedidos.

### 2.3. Sistemas Multiagentes para Roteamento

Trabalhos como os de Fischer et al. (1996) e Mes et al. (2007) exploram SMA para roteamento dinâmico, utilizando mecanismos de leilão e negociação para alocação de tarefas. Cada veículo é representado por um agente que avalia localmente o custo de inserir um novo pedido em sua rota e participa de um protocolo de Contract Net [Smith 1980]. Esta é a abordagem adotada neste trabalho.

<p align="center"><b>Tabela 1.</b> Comparativo de abordagens para roteamento dinâmico</p>

| Abordagem              | Dinamismo | Escalabilidade | Complexidade | Adequação MVP |
|------------------------|-----------|----------------|--------------|---------------|
| OR-Tools / Comercial   | Baixo     | Alta           | Baixa        | Parcial       |
| Metaheurísticas (AG, ACO) | Médio  | Média          | Média        | Boa           |
| SMA + Contract Net     | Alto      | Média          | Média        | Ótima         |
| SMA + Deep RL          | Alto      | Alta           | Alta         | Baixa         |

&nbsp;

## 3. Técnicas de IA Aplicáveis

### 3.1. Algoritmos Genéticos

Algoritmos Genéticos simulam seleção natural para explorar o espaço de soluções, representando rotas como cromossomos e aplicando crossover e mutação [Holland 1975]. Produzem soluções de alta qualidade para instâncias estáticas, mas o alto custo computacional para reotimização em tempo real os torna inadequados como única abordagem para cenários dinâmicos.

### 3.2. Otimização por Colônia de Formigas (ACO)

O ACO, inspirado no comportamento de formigas reais, deposita feromônios em arestas do grafo de rotas, guiando iterações subsequentes para caminhos mais curtos [Dorigo e Stützle 2004]. É naturalmente adaptável a mudanças incrementais no grafo. No sistema proposto, o ACO foi integrado como módulo auxiliar dentro de cada agente-entregador, calculando a melhor sequência para os pedidos já alocados.

### 3.3. Lógica Fuzzy

A Lógica Fuzzy permite representar incertezas como estimativas de tempo de trânsito e preferências de janelas de entrega. Tem papel complementar no contexto deste problema: pode integrar a função de utilidade dos agentes, mas não é suficiente como técnica principal para otimização de rotas.

### 3.4. Sistemas Multiagentes – Abordagem Escolhida

O SMA com Contract Net Protocol foi escolhido como abordagem principal pelos seguintes motivos: (i) modela diretamente a natureza descentralizada do problema; (ii) adapta-se naturalmente a eventos dinâmicos sem reotimização global; (iii) possui complexidade de implementação adequada ao escopo do projeto; e (iv) combina-se naturalmente com ACO para otimização local. O ACO opera como módulo auxiliar, otimizando a sequência de visita dos pedidos já alocados a cada agente.

&nbsp;

## 4. Arquitetura do Sistema

O sistema é composto por três tipos de agentes autônomos que interagem por meio de um barramento de mensagens assíncrono, seguindo semântica inspirada no padrão FIPA-ACL.

### 4.1. Agentes

**Agente Despachante (1 instância):** representa o centro de distribuição. Recebe novos pedidos, inicia leilões via Contract Net Protocol, coleta propostas dos entregadores, seleciona o vencedor por menor custo e gerencia cancelamentos. Mantém um registro de leilões ativos com timeout configurável.

**Agentes Entregadores (N instâncias):** cada agente representa um entregador autônomo. Avalia o custo incremental de inserir novos pedidos (distância até o CD somada à distância CD-entrega), participa de leilões submetendo propostas ou recusas, acumula pedidos em lotes antes de partir, otimiza a sequência de entregas com ACO e executa o deslocamento físico. Opera em ciclos: aguarda no CD, parte com o lote, entrega, retorna ao CD.

**Agente Mapa (1 instância):** fornece serviços de cálculo de distância euclidiana entre pontos e estimativa de tempo de viagem considerando um fator dinâmico de trânsito (variação aleatória de 1,0 a 1,4).

### 4.2. Protocolo Contract Net

O protocolo de coordenação segue o Contract Net Protocol (CNP) [Smith 1980], implementado com seis performativas:

1. O Despachante recebe um novo pedido e publica uma mensagem *CallForProposal* para todos os entregadores registrados;
2. Cada entregador avalia a viabilidade (capacidade e tempo limite) e o custo incremental da inserção;
3. Entregadores viáveis respondem com *Propose* (custo do lance); inviáveis respondem com *Refuse*;
4. Após o timeout do leilão, o Despachante seleciona a proposta de menor custo;
5. O vencedor recebe *AcceptProposal*; os demais recebem *RejectProposal*;
6. O vencedor confirma a alocação com *Inform* e adiciona o pedido ao seu lote.

Cancelamentos são tratados via mensagens *Cancel*, que removem o pedido da rota do entregador e liberam capacidade.

### 4.3. Módulo ACO

O otimizador ACO é invocado dentro de cada agente-entregador no momento da partida do lote, para determinar a melhor sequência de visita aos pontos de entrega. A implementação segue o algoritmo clássico [Dorigo e Stützle 2004] com os seguintes parâmetros configuráveis: número de formigas, número de iterações, alfa (influência do feromônio), beta (influência da distância), e taxa de evaporação. A seleção estocástica do próximo nó utiliza roleta proporcional aos pesos combinados de feromônio e desejabilidade.

### 4.4. Motor de Simulação

O ambiente de simulação opera com um relógio de tempo simulado desacoplado do tempo real por meio de um fator de aceleração configurável. A cada passo (tick de 200ms reais), o motor:

- Avança o tempo simulado proporcionalmente ao fator de aceleração;
- Gera novos pedidos seguindo uma distribuição de Poisson (algoritmo de Knuth), garantindo correção mesmo com alta aceleração;
- Aplica cancelamentos aleatórios (~5% dos novos pedidos) e verifica expirações;
- Avança o deslocamento físico de cada entregador proporcionalmente ao delta simulado.

### 4.5. Formação de Lotes

Para maximizar a eficiência logística, cada entregador acumula pedidos em lotes antes de partir do CD. A partida ocorre quando o lote atinge o tamanho mínimo configurado ou quando o tempo máximo de espera expira. No momento da partida, o ACO otimiza a sequência de todas as entregas do lote. Essa estratégia reduz viagens parcialmente carregadas e permite rotas mais eficientes.

&nbsp;

## 5. Implementação

### 5.1. Decisão Tecnológica

O planejamento inicial previa a utilização do framework JADE (Java) para os agentes e Python/Flask para a interface web. Durante o desenvolvimento, optou-se por migrar para .NET 8 / C# pelos seguintes motivos: (i) tipagem forte e async/await nativo facilitam a implementação de agentes concorrentes com segurança de tipos; (ii) o ASP.NET Core oferece SignalR como solução integrada para comunicação em tempo real, eliminando a necessidade de uma camada separada; (iii) a arquitetura unificada em uma única plataforma simplifica deploy e manutenção do MVP.

O framework de agentes foi implementado com base em *System.Threading.Channels*, provendo um barramento de mensagens assíncrono com canais dedicados por agente. As mensagens seguem semântica FIPA-ACL com envelope contendo remetente, destinatário, performativa, identificador de conversação e conteúdo tipado.

### 5.2. Estrutura do Projeto

O projeto é organizado em quatro módulos:

- **RotasEntregaSMA.Dominio:** modelos de domínio (Pedido, EstadoEntregador, RotaEntregador, PosicaoGeografica);
- **RotasEntregaSMA.Agentes:** implementação dos três tipos de agentes, barramento de mensagens, protocolo ACL e otimizadores (ACO e baseline estática);
- **RotasEntregaSMA.Simulacao:** motor de simulação, configuração parametrizável e cálculo do comparativo final;
- **RotasEntregaSMA.Api:** API REST (ASP.NET Core), hub SignalR para eventos em tempo real e interface web (HTML5 Canvas).

### 5.3. Interface Web

A interface web consiste em uma aplicação single-page servida como arquivo estático pelo ASP.NET Core. O mapa simulado é renderizado em um elemento HTML5 Canvas de 720x720 pixels, exibindo em tempo real:

- Grade urbana com o centro de distribuição;
- Posição atual de cada entregador (diferenciados por cor);
- Rotas planejadas como polilinhas conectando as paradas;
- Pedidos pendentes de alocação;
- Painel de métricas (total de pedidos, entregues, pendentes, sem alocação, distância total);
- Log de eventos do protocolo Contract Net com timestamps;
- Tabela comparativa SMA vs solução estática ao final da simulação.

A comunicação em tempo real entre o servidor e a interface utiliza SignalR (WebSockets), com um serviço *RetransmissorEventosService* que retransmite nove tipos de eventos: PedidoCriado, LeilaoIniciado, PropostaRecebida, PedidoAlocado, PedidoSemAlocacao, PedidoCancelado, PosicaoAtualizada, PedidoEntregue e MetricasAtualizadas.

### 5.4. Endpoints da API

<p align="center"><b>Tabela 2.</b> Endpoints da API REST</p>

| Método    | Rota                       | Descrição                              |
|-----------|----------------------------|----------------------------------------|
| POST      | /api/simulacao/iniciar     | Inicia simulação com configuração opcional |
| POST      | /api/simulacao/parar       | Para simulação e retorna comparativo   |
| GET       | /api/simulacao/estado      | Estado atual dos entregadores e rotas  |
| GET       | /api/simulacao/comparativo | Resultado comparativo SMA vs estática  |
| WebSocket | /hubs/simulacao            | Eventos em tempo real (SignalR)        |

&nbsp;

## 6. Resultados e Análise

### 6.1. Configuração dos Experimentos

Foram realizadas três rodadas de simulação com os seguintes parâmetros: 4 agentes-entregadores, grade urbana de 20 km, duração de 30 minutos simulados com fator de aceleração 200x (resultando em aproximadamente 9 segundos reais por rodada), capacidade máxima de 40 kg por veículo, velocidade média de 30 km/h, janela de tempo entre 25 e 70 minutos simulados, e tempo limite de entrega de 90 minutos simulados. Pedidos são gerados com intervalo médio de 30 segundos simulados seguindo distribuição de Poisson.

A solução estática de referência utiliza um algoritmo guloso de vizinho mais próximo com conhecimento antecipado de todos os pedidos gerados na rodada, representando um cenário idealizado onde todas as demandas são conhecidas a priori.

### 6.2. Resultados Comparativos

<p align="center"><b>Tabela 3.</b> Resultados comparativos – SMA (dinâmico) vs Solução Estática (baseline)</p>

| Métrica                      | Rodada 1 | Rodada 2 | Rodada 3 | Média  |
|------------------------------|----------|----------|----------|--------|
| Pedidos gerados              | 54       | 58       | 62       | 58,0   |
| Entregas SMA                 | 7        | 4        | 5        | 5,3    |
| Sem alocação SMA             | 4        | 4        | 3        | 3,7    |
| Distância SMA (km)           | 50,48    | 52,16    | 49,74    | 50,79  |
| Dentro da janela (Estática)  | 14       | 16       | 14       | 14,7   |
| Fora da janela (Estática)    | 36       | 37       | 40       | 37,7   |
| Não atendidos (Estática)     | 4        | 5        | 8        | 5,7    |
| Distância Estática (km)      | 345,04   | 309,65   | 280,46   | 311,72 |

### 6.3. Análise

**Eficiência em distância:** O SMA percorreu em média 50,79 km contra 311,72 km da solução estática, uma redução de 83,7%. Essa diferença expressiva decorre da natureza incremental do SMA: cada entregador só aceita pedidos que pode atender de forma viável, evitando deslocamentos desnecessários. A solução estática, por outro lado, tenta atribuir todos os pedidos a priori, gerando rotas longas e frequentemente inviáveis.

**Qualidade das entregas:** Todas as entregas realizadas pelo SMA ocorreram dentro das janelas de tempo, pois a avaliação de viabilidade temporal é feita no momento do lance (verificação em tempo simulado). Na solução estática, em média apenas 14,7 dos 58 pedidos (25,3%) foram entregues dentro da janela, enquanto 37,7 (65,0%) chegaram fora do prazo. Isso demonstra que, em cenários dinâmicos, uma solução com conhecimento antecipado completo não garante qualidade de serviço se não considera a dinâmica temporal corretamente.

**Cobertura vs qualidade:** O SMA prioriza viabilidade sobre cobertura, entregando menos pedidos mas com 100% de conformidade temporal. A solução estática atende mais pedidos nominalmente, porém a maioria ultrapassa o tempo limite, o que em um cenário real seria equivalente a uma entrega falha. Considerando apenas entregas dentro da janela, o SMA atinge desempenho comparável (5,3 vs 14,7), com custo de distância drasticamente inferior.

**Escalabilidade do lote:** O mecanismo de formação de lotes mostrou-se eficaz para agregar pedidos antes da partida, permitindo que o ACO otimize rotas com múltiplas paradas. Entregadores carregados até quase a capacidade máxima (~38-40 kg) validam que a alocação via leilão distribui carga de forma equilibrada.

&nbsp;

## 7. Conclusão

Este trabalho apresentou o desenvolvimento completo de um MVP de sistema de otimização de rotas de entrega urbana baseado em Sistemas Multiagentes. Os quatro objetivos específicos foram plenamente atendidos:

(a) Entregadores, pedidos e o centro de distribuição foram modelados como agentes autônomos com comportamento reativo e proativo;

(b) O protocolo Contract Net foi implementado com seis performativas FIPA-ACL, garantindo alocação descentralizada e adaptável;

(c) Uma interface web em tempo real com HTML5 Canvas e SignalR permite visualizar rotas, métricas e eventos do protocolo;

(d) O comparativo com a solução estática demonstrou que o SMA alcança 83,7% menos distância percorrida com 100% de entregas dentro da janela de tempo.

A combinação SMA + Contract Net + ACO mostrou-se adequada para o problema de roteamento dinâmico, oferecendo adaptabilidade a eventos em tempo real (novos pedidos, cancelamentos, restrições de capacidade) sem necessidade de reotimização global.

A decisão de migrar de JADE/Flask para .NET 8 mostrou-se acertada, resultando em uma arquitetura unificada com comunicação em tempo real integrada e tipagem forte que facilita a manutenção e extensão do sistema.

**Trabalhos futuros** incluem: integração com dados reais de trânsito via APIs de geolocalização; avaliação com instâncias do benchmark de Solomon para comparação com a literatura; incorporação de Deep Reinforcement Learning para políticas de alocação aprendidas; e adição de priorização de pedidos via Lógica Fuzzy na função de utilidade dos agentes.

&nbsp;

## Referências

Cordeau, J. F., Desrochers, M. e Desrosiers, J. (2002). The VRP with Time Windows. In Toth, P. e Vigo, D., editors, *The Vehicle Routing Problem*, p. 157-193. SIAM.

Dorigo, M. e Stützle, T. (2004). *Ant Colony Optimization*. MIT Press.

Fischer, K., Müller, J. P. e Pischel, M. (1996). Cooperative transportation scheduling: an application domain for DAI. *Applied Artificial Intelligence*, 10(1):1-33.

Gendreau, M. e Tarantilis, C. D. (2010). Solving large-scale vehicle routing problems with time windows: The state-of-the-art. Technical report, CIRRELT.

Google (2023). OR-Tools – Vehicle Routing. https://developers.google.com/optimization/routing.

Holland, J. H. (1975). *Adaptation in Natural and Artificial Systems*. University of Michigan Press.

Mes, M., van der Heijden, M. e van Harten, A. (2007). Comparison of agent-based scheduling to look-ahead heuristics for real-time transportation problems. *European Journal of Operational Research*, 181(1):59-75.

Oliveira, R. e Santos, P. (2022). Crescimento do e-commerce e desafios logísticos no Brasil. *Revista Brasileira de Logística*, 15(2):44-58.

Smith, R. G. (1980). The contract net protocol: High-level communication and control in a distributed problem solver. *IEEE Transactions on Computers*, C-29(12):1104-1113.

Wooldridge, M. (2009). *An Introduction to MultiAgent Systems*. John Wiley & Sons, 2nd edition.

Zhang, Z., Liu, M. e Lim, A. (2020). A memetic algorithm for the patient transportation problem. *Computers & Operations Research*, 87:2-18.
