const canvas = document.getElementById("mapa");
const ctx = canvas.getContext("2d");
const logEl = document.getElementById("log");

const estado = {
  gradeKm: 20,
  centro: null,
  entregadores: new Map(), // id -> { nome, posicao, rota }
  pedidosPendentes: new Map(), // id -> { coleta, entrega }
};

const cores = ["#3bd67b", "#4fa3ff", "#ff9d4d", "#c98bff", "#ff5d8f", "#5de0d6", "#ffd15c", "#ff7a7a"];
function corEntregador(index) { return cores[index % cores.length]; }

function escala(v) { return (v / estado.gradeKm) * canvas.width; }

function desenhar() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);

  // grade de fundo
  ctx.strokeStyle = "#161c2c";
  ctx.lineWidth = 1;
  for (let i = 0; i <= 10; i++) {
    const p = (i / 10) * canvas.width;
    ctx.beginPath(); ctx.moveTo(p, 0); ctx.lineTo(p, canvas.height); ctx.stroke();
    ctx.beginPath(); ctx.moveTo(0, p); ctx.lineTo(canvas.width, p); ctx.stroke();
  }

  // centro de distribuicao
  if (estado.centro) {
    ctx.fillStyle = "#f2b705";
    ctx.beginPath();
    ctx.arc(escala(estado.centro.x), escala(estado.centro.y), 8, 0, Math.PI * 2);
    ctx.fill();
  }

  // pedidos pendentes (sem entregador ainda alocado / em leilao)
  ctx.fillStyle = "#e0455f";
  for (const pedido of estado.pedidosPendentes.values()) {
    ctx.beginPath();
    ctx.arc(escala(pedido.entrega.x), escala(pedido.entrega.y), 4, 0, Math.PI * 2);
    ctx.fill();
  }

  // entregadores e suas rotas
  let idx = 0;
  for (const entregador of estado.entregadores.values()) {
    const cor = corEntregador(idx++);
    const pos = entregador.posicao;

    // linha da rota
    if (entregador.rota && entregador.rota.paradas && entregador.rota.paradas.length > 0) {
      ctx.strokeStyle = cor;
      ctx.globalAlpha = 0.6;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(escala(pos.x), escala(pos.y));
      for (const parada of entregador.rota.paradas) {
        ctx.lineTo(escala(parada.posicao.x), escala(parada.posicao.y));
      }
      ctx.stroke();
      ctx.globalAlpha = 1;

      ctx.fillStyle = cor;
      for (const parada of entregador.rota.paradas) {
        ctx.beginPath();
        ctx.arc(escala(parada.posicao.x), escala(parada.posicao.y), 3, 0, Math.PI * 2);
        ctx.fill();
      }
    }

    // entregador
    ctx.fillStyle = cor;
    ctx.beginPath();
    ctx.arc(escala(pos.x), escala(pos.y), 7, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = "#e6e9f0";
    ctx.font = "11px Segoe UI";
    ctx.fillText(entregador.nome, escala(pos.x) + 10, escala(pos.y) - 8);
  }
}

function logar(texto) {
  const linha = document.createElement("div");
  const hora = new Date().toLocaleTimeString("pt-BR");
  linha.textContent = `[${hora}] ${texto}`;
  logEl.prepend(linha);
  while (logEl.children.length > 60) logEl.removeChild(logEl.lastChild);
}

function atualizarMetricas(dados) {
  document.getElementById("mTotal").textContent = dados.totalPedidos;
  document.getElementById("mEntregues").textContent = dados.pedidosEntregues;
  document.getElementById("mPendentes").textContent = dados.pedidosPendentes;
  document.getElementById("mSemAlocacao").textContent = dados.pedidosSemAlocacao;
  document.getElementById("mDistancia").textContent = dados.distanciaTotalKm;
}

const conexao = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/simulacao")
  .withAutomaticReconnect()
  .build();

conexao.on("evento", (evento) => {
  const d = evento.dados;
  switch (evento.tipo) {
    case "PedidoCriado":
      estado.pedidosPendentes.set(d.pedidoId, { coleta: d.coleta, entrega: d.entrega });
      logar(`Novo pedido gerado - entrega em (${d.entrega.x.toFixed(1)}, ${d.entrega.y.toFixed(1)})`);
      break;
    case "LeilaoIniciado":
      logar(`CFP (Contract Net) anunciado para ${d.participantes} entregadores`);
      break;
    case "PropostaRecebida":
      // silencioso no log para nao poluir; usado apenas para eventual depuracao
      break;
    case "PedidoAlocado":
      estado.pedidosPendentes.delete(d.pedidoId);
      atualizarEntregador(d.entregadorId, d.nome, null, d.rota);
      logar(`Pedido alocado ao entregador ${d.nome} (Accept-Proposal)`);
      break;
    case "PedidoSemAlocacao":
      logar(`Pedido sem entregador disponivel no momento (leilao sem propostas viaveis)`);
      break;
    case "PedidoCancelado":
      logar(`Pedido cancelado e removido da rota`);
      break;
    case "PosicaoAtualizada":
      atualizarEntregador(d.entregadorId, d.nome, d.posicao, d.rota);
      break;
    case "PedidoEntregue":
      estado.pedidosPendentes.delete(d.pedidoId);
      logar(`Pedido entregue com sucesso`);
      break;
    case "MetricasAtualizadas":
      atualizarMetricas(d);
      break;
  }
  desenhar();
});

function atualizarEntregador(id, nome, posicao, rota) {
  const atual = estado.entregadores.get(id) || { nome, posicao: posicao ?? { x: 0, y: 0 }, rota: null };
  if (posicao) atual.posicao = posicao;
  if (rota) atual.rota = rota;
  atual.nome = nome;
  estado.entregadores.set(id, atual);
}

conexao.start().catch((erro) => logar(`Falha ao conectar: ${erro}`));

document.getElementById("btnIniciar").addEventListener("click", async () => {
  const gradeKm = Number(document.getElementById("cfgGrade").value);
  estado.gradeKm = gradeKm;
  estado.centro = { x: gradeKm / 2, y: gradeKm / 2 };
  estado.entregadores.clear();
  estado.pedidosPendentes.clear();
  document.getElementById("comparativo").classList.add("oculto");

  const config = {
    numeroEntregadores: Number(document.getElementById("cfgEntregadores").value),
    tamanhoGradeKm: gradeKm,
    duracaoSimulacaoMinutosSimulados: Number(document.getElementById("cfgDuracao").value),
    fatorAceleracaoTempo: Number(document.getElementById("cfgAceleracao").value),
    intervaloMedioNovoPedidoSegundosSimulados: Number(document.getElementById("cfgIntervalo").value),
    tempoLimitePedidoMinutos: Number(document.getElementById("cfgTempoLimite").value),
  };

  const resposta = await fetch("/api/simulacao/iniciar", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(config),
  });

  if (resposta.ok) {
    document.getElementById("status").textContent = "Simulacao em execucao...";
    document.getElementById("btnIniciar").disabled = true;
    document.getElementById("btnParar").disabled = false;
    logar("Simulacao iniciada.");
  } else {
    logar("Nao foi possivel iniciar a simulacao (ja em execucao?)");
  }
});

document.getElementById("btnParar").addEventListener("click", async () => {
  const resposta = await fetch("/api/simulacao/parar", { method: "POST" });
  const corpo = await resposta.json();
  document.getElementById("status").textContent = "Simulacao parada.";
  document.getElementById("btnIniciar").disabled = false;
  document.getElementById("btnParar").disabled = true;
  logar("Simulacao parada. Comparativo calculado.");
  exibirComparativo(corpo.comparativo);
});

function exibirComparativo(c) {
  const tbody = document.getElementById("tabelaComparativo");
  tbody.innerHTML = `
    <tr><td>Pedidos gerados</td><td>${c.totalPedidosGerados}</td><td>${c.totalPedidosGerados}</td></tr>
    <tr><td>Distancia total percorrida (km)</td><td>${c.distanciaTotalSmaKm}</td><td>${c.distanciaTotalEstaticaKm}</td></tr>
    <tr><td>Pedidos entregues / dentro da janela</td><td>${c.pedidosEntreguesSma}</td><td>${c.pedidosDentroDaJanelaEstatica}</td></tr>
    <tr><td>Fora da janela</td><td>-</td><td>${c.pedidosForaDaJanelaEstatica}</td></tr>
    <tr><td>Sem alocacao / nao atendidos</td><td>${c.pedidosSemAlocacaoSma}</td><td>${c.pedidosNaoAtendidosEstatica}</td></tr>
  `;
  document.getElementById("comparativo").classList.remove("oculto");
}

desenhar();
