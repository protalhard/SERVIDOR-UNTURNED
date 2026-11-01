// Painel admin do DAY Z BRASIL: dashboard, mensagens (jogo e Discord), enquetes, wipe,
// jogadores, banidos, suspeitos, reports, logs e staff.
// Tudo que vem do jogo ou do Discord (nomes, motivos) entra como texto (textContent), nunca como HTML.
(function () {
  "use strict";
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
  let eu = null, aba = "dashboard", log = "admin";
  let painel = null, jogadores = [];
  const dados = { banidos: [], suspeitos: [], reports: [] };
  let staff = { staff: [], permissoes: [] };
  const nomesPerm = { Kick: "Kick", Ban: "Banir/desbanir", Tp: "Ir até", Trazer: "Trazer", Curar: "Curar", Matar: "Matar",
    Congelar: "Congelar", Spy: "Foto da tela", Itens: "Spawnar itens", Visao: "Visão/espião", Deus: "Imortal", Mundo: "Anúncios e mensagens",
    Airdrop: "Airdrop", Reiniciar: "Reiniciar servidor", Precos: "Preços das lojas", Kits: "Qualquer kit sem espera", Skills: "Skills" };
  const titulos = { dashboard: ["Dashboard", "Tudo do servidor, do Discord e do site num lugar só."],
    mensagens: ["Mensagens", "Mande avisos e enquetes pro chat do jogo e pro canal do Discord que quiser."],
    wipe: ["Wipe", "Pergunte pra galera, escolha o que apagar e marque a hora."],
    jogadores: ["Jogadores", "Todo mundo que já entrou no servidor."], banidos: ["Banidos", "Bans com motivo, prova e foto."],
    suspeitos: ["Suspeitos", "O que o DZ GUARD (anti-cheat) achou estranho."], reports: ["Reports", "Denúncias feitas com /report no jogo."],
    banir: ["Banir por Steam ID", "Funciona com o jogador online ou offline."], logs: ["Logs", "Ações da staff, anti-cheat e raids."],
    staff: ["Staff", "Quem é da staff e o que cada um pode fazer."] };

  const ICONES = {
    painel: '<path d="M3 13h8V3H3zm0 8h8v-6H3zm10 0h8V11h-8zm0-18v6h8V3z"/>',
    msg: '<path d="M4 4h16v12H7l-3 3z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/>',
    wipe: '<path d="M20 12a8 8 0 1 1-2.3-5.7M20 4v5h-5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>',
    gente: '<circle cx="9" cy="8" r="3.2" fill="none" stroke="currentColor" stroke-width="2"/><path d="M3 20c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5M16 4.5a3 3 0 0 1 0 6M18 14.5c2 .6 3 2.6 3 5.5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>',
    ban: '<circle cx="12" cy="12" r="8.5" fill="none" stroke="currentColor" stroke-width="2"/><path d="M6 6l12 12" stroke="currentColor" stroke-width="2"/>',
    olho: '<path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="12" cy="12" r="3" fill="currentColor"/>',
    flag: '<path d="M5 21V4m0 0h12l-2 4 2 4H5" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/>',
    martelo: '<path d="M14 4l6 6-3 3-6-6zM11.5 9.5L4 17l3 3 7.5-7.5" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/>',
    log: '<path d="M6 3h9l4 4v14H6zM9 12h7M9 16h7M9 8h3" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/>',
    escudo: '<path d="M12 3l8 3v6c0 4.5-3.4 8-8 9-4.6-1-8-4.5-8-9V6z" fill="none" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/>',
  };
  function icone(nome) {
    const s = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    s.setAttribute("viewBox", "0 0 24 24"); s.setAttribute("fill", "currentColor"); s.setAttribute("aria-hidden", "true");
    s.innerHTML = ICONES[nome] || ""; // ícones fixos daqui do código
    return s;
  }

  function el(tag, props, ...filhos) {
    const e = document.createElement(tag);
    for (const [k, v] of Object.entries(props || {})) {
      if (k === "class") e.className = v;
      else if (k.startsWith("on")) e.addEventListener(k.slice(2), v);
      else if (v !== undefined && v !== null && v !== false) e.setAttribute(k, v === true ? "" : v);
    }
    for (const f of filhos.flat()) if (f !== null && f !== undefined && f !== false) e.append(f instanceof Node ? f : String(f));
    return e;
  }
  const data = (u) => u > 0 ? new Date(u * 1000).toLocaleString("pt-BR", { day: "2-digit", month: "2-digit", year: "2-digit", hour: "2-digit", minute: "2-digit" }) : "-";
  const dataLonga = (u) => new Date(u * 1000).toLocaleString("pt-BR", { weekday: "long", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
  function tempo(s) {
    if (s < 0) return "permanente";
    const d = Math.floor(s / 86400), h = Math.floor(s % 86400 / 3600), m = Math.floor(s % 3600 / 60);
    return d > 0 ? `${d}d ${h}h` : h > 0 ? `${h}h ${m}min` : `${m}min`;
  }
  const tamanho = (b) => b > 1e9 ? (b / 1e9).toFixed(1) + " GB" : b > 1e6 ? (b / 1e6).toFixed(1) + " MB" : Math.max(1, Math.round(b / 1e3)) + " KB";
  const steamLink = (id) => el("a", { href: "https://steamcommunity.com/profiles/" + id, target: "_blank", rel: "noopener" }, id);
  const fotoUrl = (f) => f && f.startsWith("spy/") ? "/api/admin/foto/" + f.slice(4) : "";
  function foto(f) {
    const u = fotoUrl(f);
    if (!u) return null;
    const i = el("img", { class: "adm-foto", src: u, alt: "foto da tela", loading: "lazy" });
    i.addEventListener("error", () => i.replaceWith(el("span", { class: "adm-tag" }, "foto não chegou")));
    i.addEventListener("click", () => { $("#zoom img").src = u; $("#zoom").hidden = false; });
    return i;
  }
  function lista(titulo, itens) {
    if (!itens || !itens.length) return null;
    return el("details", {}, el("summary", {}, titulo + ` (${itens.length})`), el("ol", {}, itens.map((x) => el("li", {}, x))));
  }
  let avisoTimer = 0;
  function aviso(t, erro) {
    const a = $("#aviso");
    a.textContent = t; a.className = "adm-aviso" + (erro ? " erro" : ""); a.hidden = false;
    clearTimeout(avisoTimer); avisoTimer = setTimeout(() => { a.hidden = true; }, erro ? 12000 : 7000);
    window.scrollTo({ top: 0, behavior: "smooth" });
  }
  const vazio = (t) => el("p", { class: "adm-vazio" }, t);

  async function api(caminho) {
    const r = await fetch(caminho, { credentials: "same-origin" });
    if (!r.ok) throw new Error(await r.text());
    return r.json();
  }
  async function post(caminho, corpo) {
    const r = await fetch(caminho, { method: "POST", credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-DZ": "1" }, body: JSON.stringify(corpo) });
    const t = await r.text();
    if (!r.ok) throw new Error(t.trim() || "erro " + r.status);
    try { return JSON.parse(t); } catch { return {}; }
  }
  async function acao(acao, steam, horas, motivo) {
    if ((acao === "perm" || acao === "staff_remover") ? !eu.dono : !eu.ban) { aviso("Você não tem a permissão Ban.", true); return false; }
    try { await post("/api/admin/acao", { acao, steam, horas: Number(horas) || 0, motivo: motivo || "" }); }
    catch (e) { aviso("Erro: " + e.message, true); return false; }
    aviso(acao === "perm" ? "Enviado. Muda em alguns segundos (o servidor precisa estar ligado)." : "Enviado pro servidor. Atualiza em alguns segundos (o servidor precisa estar ligado).");
    setTimeout(carregarListas, 3000); setTimeout(carregarListas, 8000);
    return true;
  }
  function banirPrompt(steam, nome) {
    const motivo = prompt(`Motivo do ban de ${nome}:`, "Hack (DZ GUARD)");
    if (!motivo) return;
    const horas = prompt("Quantas horas? (0 = permanente)", "0");
    if (horas === null) return;
    acao("banir", steam, horas, motivo);
  }


  // ---------------- DESEMPENHO AO VIVO ----------------
  const NS = "http://www.w3.org/2000/svg";
  function svg(tag, attrs, ...filhos) {
    const e = document.createElementNS(NS, tag);
    for (const [k, v] of Object.entries(attrs || {})) e.setAttribute(k, v);
    filhos.forEach((f) => e.append(f));
    return e;
  }
  const R = 50, CIRC = 2 * Math.PI * R, ARCO = CIRC * 0.75;
  let idGrad = 0;
  function medidorArco(rotuloBaixo) {
    const id = "g" + (++idGrad);
    const grad = svg("linearGradient", { id, x1: "0", y1: "1", x2: "1", y2: "0" },
      svg("stop", { offset: "0", "stop-color": "#FF5A00" }), svg("stop", { offset: "1", "stop-color": "#FFB020" }));
    const comum = { cx: "60", cy: "60", r: String(R), fill: "none", "stroke-width": "10", "stroke-linecap": "round", transform: "rotate(135 60 60)" };
    const trilho = svg("circle", { ...comum, class: "trilho", "stroke-dasharray": `${ARCO} ${CIRC}` });
    const valor = svg("circle", { ...comum, class: "valor", stroke: `url(#${id})`, "stroke-dasharray": `0 ${CIRC}` });
    const num = el("strong", {}, "–");
    const caixa = el("div", { class: "adm-gauge" }, svg("svg", { viewBox: "0 0 120 120" }, svg("defs", {}, grad), trilho, valor),
      el("div", { class: "adm-gauge-num" }, num, el("span", {}, rotuloBaixo)));
    return { caixa, definir(pct, texto) { valor.setAttribute("stroke-dasharray", `${ARCO * Math.max(0, Math.min(100, pct)) / 100} ${CIRC}`); num.textContent = texto; } };
  }
  function termometro() {
    const seta = el("i"); const t = el("div", { class: "adm-termo" }, seta);
    return { caixa: t, definir(p) { seta.style.bottom = Math.max(0, Math.min(100, p)) + "%"; } };
  }
  function faisca(cores) {
    const s = svg("svg", { class: "adm-faisca", viewBox: "0 0 300 42", preserveAspectRatio: "none" });
    const linhas = cores.map((c) => {
      const area = svg("path", { class: "area", fill: c }), linha = svg("path", { fill: "none", stroke: c, "stroke-width": "2", "vector-effect": "non-scaling-stroke" });
      s.append(area, linha); return { area, linha };
    });
    return { caixa: s, definir(series, max) {
      series.forEach((v, k) => {
        if (!v || v.length < 2) { linhas[k].linha.setAttribute("d", ""); linhas[k].area.setAttribute("d", ""); return; }
        const m = max || Math.max(1, ...series.flat());
        const pts = v.map((y, i) => `${(i / (v.length - 1) * 300).toFixed(1)},${(40 - y / m * 36).toFixed(1)}`);
        linhas[k].linha.setAttribute("d", "M" + pts.join(" L"));
        linhas[k].area.setAttribute("d", "M0,42 L" + pts.join(" L") + " L300,42 Z");
      });
    } };
  }
  function nivel(p) { return p >= 85 ? ["carga alta", "alta"] : p >= 60 ? ["carga média", "media"] : ["normal", ""]; }
  const gb = (b) => (b / 1073741824).toLocaleString("pt-BR", { maximumFractionDigits: 1, minimumFractionDigits: 1 });
  function velocidade(bps) {
    const bits = bps * 8;
    return bits >= 1e6 ? (bits / 1e6).toLocaleString("pt-BR", { maximumFractionDigits: 1 }) + " Mb/s" : (bits / 1e3).toLocaleString("pt-BR", { maximumFractionDigits: 1 }) + " kb/s";
  }
  function duracao(s) {
    const d = Math.floor(s / 86400), h = Math.floor(s % 86400 / 3600), m = Math.floor(s % 3600 / 60);
    return d ? `${d}d ${h}h` : h ? `${h}h ${m}min` : `${m}min`;
  }
  let med = null;
  function prepararDesempenho() {
    const card = (titulo, sub) => {
      const pct = el("strong", {}, "–"), etq = el("em", {}, ""), subt = el("small", {}, sub || "");
      const meio = el("div", { class: "adm-med-meio" });
      const c = el("article", { class: "adm-med" }, el("div", { class: "adm-med-topo" }, el("div", {}, el("b", {}, titulo), subt), el("div", { class: "adm-med-pct" }, pct, etq)), meio);
      return { c, pct, etq, subt, meio };
    };
    const cpu = card("CPU", ""), ram = card("Memória RAM", ""), disco = card("Disco", ""), rede = card("Rede", "tráfego da máquina");
    const gCpu = medidorArco("% uso"), tCpu = termometro(), fCpu = faisca(["#FF5A00"]);
    cpu.meio.append(gCpu.caixa, tCpu.caixa); cpu.c.append(fCpu.caixa);
    const quad = Array.from({ length: 24 }, () => el("span"));
    const ramNum = el("strong", {}, "–"), ramTot = el("span", {}, ""), ramPct = el("span", {}, "");
    ram.meio.append(el("div", { class: "adm-ram-caixa" }, el("div", { class: "adm-ram" }, quad), el("div", { class: "adm-ram-num" }, ramNum, ramTot), ramPct));
    const fRam = faisca(["#FFB020"]); ram.c.append(fRam.caixa);
    const gDisco = medidorArco("% usado"), tDisco = termometro();
    disco.meio.append(gDisco.caixa, tDisco.caixa);
    const desce = el("strong", { class: "desce" }, "–"), sobe = el("strong", { class: "sobe" }, "–");
    rede.meio.append(el("div", { class: "adm-rede" }, el("div", {}, el("small", {}, "↓ BAIXANDO"), desce), el("div", {}, el("small", {}, "↑ ENVIANDO"), sobe)));
    const fRede = faisca(["#FFB020", "#FF5A00"]); rede.c.append(fRede.caixa);
    $("#desemp").replaceChildren(cpu.c, ram.c, disco.c, rede.c);
    med = { cpu, ram, disco, rede, gCpu, tCpu, fCpu, quad, ramNum, ramTot, ramPct, fRam, gDisco, tDisco, desce, sobe, fRede };
  }
  function cabecalho(c, p) {
    const [t, cls] = nivel(p);
    c.pct.textContent = Math.round(p) + "%"; c.etq.textContent = t; c.etq.className = cls; c.c.classList.toggle("quente", p >= 85);
  }
  function atualizarDesempenho(l) {
    if (!med || !l || !l.quando) return;
    const m = med;
    m.cpu.subt.textContent = `${l.modeloCpu || "processador"} · ${l.nucleos} núcleo(s)`;
    cabecalho(m.cpu, l.cpu); m.gCpu.definir(l.cpu, String(Math.round(l.cpu))); m.tCpu.definir(l.cpu); m.fCpu.definir([l.histCpu], 100);
    const pr = l.ramTotal ? 100 * l.ramUsada / l.ramTotal : 0;
    cabecalho(m.ram, pr); m.ram.subt.textContent = gb(l.ramTotal) + " GB instalados";
    const acesos = Math.round(pr / 100 * m.quad.length);
    m.quad.forEach((q, i) => { q.style.background = i < acesos ? `hsl(${Math.max(0, 28 - i * 1.1)}, 100%, ${55 - i * 0.4}%)` : ""; });
    m.ramNum.textContent = gb(l.ramUsada); m.ramTot.textContent = "/ " + gb(l.ramTotal) + " GB"; m.ramPct.textContent = Math.round(pr) + "% em uso";
    m.fRam.definir([l.histRam], 100);
    const pd = l.discoTotal ? 100 * l.discoUsado / l.discoTotal : 0;
    cabecalho(m.disco, pd); m.disco.subt.textContent = `${gb(l.discoUsado)} de ${gb(l.discoTotal)} GB`;
    m.gDisco.definir(pd, String(Math.round(pd))); m.tDisco.definir(pd);
    m.desce.textContent = velocidade(l.redeDesce); m.sobe.textContent = velocidade(l.redeSobe);
    m.rede.pct.textContent = ""; m.rede.etq.textContent = "";
    m.fRede.definir([l.histDesce, l.histSobe]);
    const jogo = l.jogoRodando
      ? [["Unturned", "rodando"], ["CPU do jogo", Math.round(l.jogoCpu) + "% de 1 núcleo"], ["RAM do jogo", gb(l.jogoRam) + " GB"], ["Jogo ligado há", duracao(l.jogoLigado)]]
      : [["Unturned", "parado"]];
    jogo.push(["Máquina ligada há", duracao(l.ligada)], ["Carga", (l.carga || []).join(" · ")]);
    $("#desemp-jogo").replaceChildren(...jogo.map(([k, v]) => el("span", {}, k + ": ", el("b", {}, v))));
  }
  let falhasMaquina = 0;
  async function carregarMaquina() {
    try { atualizarDesempenho(await api("/api/admin/maquina")); falhasMaquina = 0; $("#aovivo").className = "adm-aovivo"; }
    catch { if (++falhasMaquina > 2) $("#aovivo").className = "adm-aovivo parado"; }
  }

  // ---------------- DASHBOARD ----------------
  function tile(rotulo, valor, sub, cor, irPara) {
    return el("div", { class: "adm-tile" + (cor ? " " + cor : "") + (irPara ? " clica" : ""), onclick: irPara ? () => abrir(irPara) : null },
      el("small", {}, rotulo), el("strong", {}, valor), sub ? el("em", {}, sub) : null);
  }
  function desenharDashboard() {
    const p = painel; if (!p) return;
    const st = p.status || {}, d = p.discord || {}, c = p.contagem || {};
    const on = (st.jogadores || []).length;
    $("#tiles").replaceChildren(
      tile("Servidor", st.online ? "Online" : "Offline", st.online ? `${on}/${st.max} jogando · ${st.mapa || ""}` : "reiniciando ou desligado", st.online ? "verde" : "vermelho"),
      tile("Raid agora", p.raid.aberto ? "Liberado" : "Bloqueado", p.raid.texto, p.raid.aberto ? "laranja" : ""),
      tile("Discord", p.botOnline ? String(d.membros || 0) : "—", p.botOnline ? `membros · ${d.online || 0} online` : "bot desligado", p.botOnline ? "" : "vermelho"),
      tile("Tickets abertos", String((d.tickets || []).length), "no Discord", (d.tickets || []).length ? "laranja" : ""),
      tile("Jogadores", String(c.jogadores || 0), "já entraram", "", "jogadores"),
      tile("Banidos", String(c.banidos || 0), "", "", "banidos"),
      tile("Suspeitos", String(c.suspeitos || 0), "DZ GUARD", c.suspeitos ? "vermelho" : "", "suspeitos"),
      tile("Reports", String(c.reports || 0), "/report", "", "reports"));

    desenharAgendado($("#b-agendado"), true);

    const O = $("#b-online");
    O.replaceChildren(on ? el("div", { class: "adm-itens-lista" }, st.jogadores.map((j) =>
      el("div", { class: "adm-li" }, el("span", { class: "adm-ponto on" }), el("span", { class: "cresce" }, j.nome),
        el("span", { class: "adm-tag" }, j.patente || ""), el("span", { class: "mono" }, "Lv." + j.nivel)))) : vazio(st.online ? "Ninguém online agora." : "Servidor offline."));

    const D = $("#b-discord");
    const ds = [];
    if (!p.botOnline) ds.push(el("p", { class: "adm-erro" }, "O bot do Discord não respondeu nos últimos minutos."));
    else {
      ds.push(el("div", { class: "adm-acoes-linha" }, el("span", { class: "adm-tag verde" }, "bot online"),
        d.convite ? el("a", { href: d.convite, target: "_blank", rel: "noopener", class: "mono pequeno" }, d.convite) : null));
      ds.push(el("h3", { class: "pequeno fraco mono" }, "TICKETS ABERTOS"));
      ds.push((d.tickets || []).length ? el("div", { class: "adm-itens-lista" }, d.tickets.map((t) =>
        el("a", { class: "adm-li", href: t.link, target: "_blank", rel: "noopener" }, "🎫", el("span", { class: "cresce" }, t.nome), el("span", { class: "mono" }, "abrir ↗")))) : vazio("Nenhum ticket aberto."));
      ds.push(el("h3", { class: "pequeno fraco mono" }, "SALAS DE VOZ PARTICULARES"));
      ds.push((d.salas || []).length ? el("div", { class: "adm-itens-lista" }, d.salas.map((s) =>
        el("div", { class: "adm-li" }, "🎯", el("span", { class: "cresce" }, s.nome), el("span", { class: "mono" }, s.pessoas + " pessoa(s)")))) : vazio("Nenhuma sala aberta."));
    }
    D.replaceChildren(...ds);

    const E = $("#b-enquetes");
    E.replaceChildren(...((d.enquetes || []).length ? d.enquetes.slice(0, 3).map(cardEnquete) : [vazio("Nenhuma enquete ainda. Crie em Mensagens ou Wipe.")]));

    const B = $("#b-backups");
    B.replaceChildren((p.backups || []).length ? el("div", { class: "adm-itens-lista" }, p.backups.map((b) =>
      el("div", { class: "adm-li" }, "💾", el("span", { class: "cresce mono" }, b.nome), el("span", { class: "mono" }, tamanho(b.tamanho)), el("span", { class: "mono" }, data(b.quando))))) : vazio("Nenhum backup ainda. O automático roda todo dia às 5h."));

    const H = $("#b-historico");
    H.replaceChildren(historico(p.historico));
  }
  function historico(h) {
    if (!h || !h.length) return vazio("Nenhum wipe ou reinício pelo painel ainda.");
    return el("div", { class: "adm-itens-lista" }, h.map((x) => el("div", { class: "adm-li" },
      x.tipo === "wipe" ? "🔄" : "♻️",
      el("span", { class: "cresce" }, (x.tipo === "wipe" ? "Wipe" : "Reinício") + (x.itens ? ": " + x.itens.map((k) => NOMES_WIPE[k] || k).join(", ") : "")),
      el("span", { class: "adm-tag " + (x.ok ? "verde" : "vermelho") }, x.ok ? "ok" : "erro"),
      el("span", { class: "mono" }, data(x.inicio)))));
  }
  function cardEnquete(e) {
    const max = Math.max(1, ...e.respostas.map((r) => r.votos));
    return el("div", { class: "adm-enquete" },
      el("h3", {}, e.pergunta, el("span", { class: "adm-tag " + (e.encerrada ? "cinza" : "verde") }, e.encerrada ? "encerrada" : "aberta")),
      e.respostas.map((r) => el("div", { class: "adm-barra-voto" },
        el("i", { style: `width:${Math.round(r.votos / max * 100)}%` }), el("span", {}, r.texto), el("b", {}, String(r.votos)))),
      el("div", { class: "adm-acoes-linha pequeno fraco" }, `${e.total} voto(s) · #${e.canalNome} · `,
        e.encerrada ? "terminou " + data(e.termina) : "termina " + data(e.termina), " · ",
        el("a", { href: e.link, target: "_blank", rel: "noopener" }, "ver no Discord ↗")));
  }

  let contador = 0;
  function desenharAgendado(alvo, resumo) {
    const ac = painel && painel.acao;
    alvo.className = "adm-bloco" + (ac ? " destaque" : "");
    if (!ac) {
      const filhos = [el("h2", {}, "Próximo wipe ou reinício"), vazio("Nada agendado.")];
      if (eu.dono && resumo) filhos.push(el("div", { class: "adm-acoes-linha" },
        el("button", { class: "botao fantasma pequeno", onclick: () => reiniciar(1) }, "♻️ Reiniciar em 1 min"),
        el("button", { class: "botao fantasma pequeno", onclick: () => reiniciar(5) }, "Em 5 min"),
        el("button", { class: "botao fantasma pequeno", onclick: () => abrir("wipe") }, "🔄 Marcar wipe")));
      alvo.replaceChildren(...filhos);
      return;
    }
    const cont = el("div", { class: "adm-contagem" });
    const nome = ac.tipo === "wipe" ? "WIPE MARCADO" : "REINÍCIO MARCADO";
    alvo.replaceChildren(el("h2", {}, nome), el("p", {}, dataLonga(ac.quando)), cont,
      ac.itens ? el("p", { class: "pequeno" }, "Vai apagar: " + Object.keys(ac.itens).map((k) => NOMES_WIPE[k] || k).join(", ")) : null,
      el("p", { class: "pequeno fraco" }, "Marcado por " + (ac.porNome || ac.por) + " em " + data(ac.criado)),
      eu.dono ? el("div", { class: "adm-acoes-linha" }, el("button", { class: "botao fantasma pequeno", onclick: cancelar }, "Cancelar")) : null);
    const tic = () => {
      const s = ac.quando - Math.floor(Date.now() / 1000);
      cont.textContent = s > 0 ? `${Math.floor(s / 86400) ? Math.floor(s / 86400) + "d " : ""}${String(Math.floor(s % 86400 / 3600)).padStart(2, "0")}:${String(Math.floor(s % 3600 / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}` : "executando…";
    };
    tic(); clearInterval(contador); contador = setInterval(() => { if (!cont.isConnected) return; tic(); }, 1000);
  }
  async function reiniciar(min) {
    if (!confirm(`Reiniciar o servidor em ${min} minuto(s)? Os jogadores recebem aviso no chat.`)) return;
    try { await post("/api/admin/servidor", { acao: "reiniciar", quando: Math.floor(Date.now() / 1000) + min * 60 }); aviso("Reinício marcado."); carregarPainel(); }
    catch (e) { aviso("Erro: " + e.message, true); }
  }
  async function cancelar() {
    if (!confirm("Cancelar o que está agendado?")) return;
    try { await post("/api/admin/servidor", { acao: "cancelar" }); aviso("Cancelado. O jogo recebeu o aviso."); carregarPainel(); }
    catch (e) { aviso("Erro: " + e.message, true); }
  }

  // ---------------- canais do Discord ----------------
  function preencherCanais(sel, padrao) {
    const d = (painel && painel.discord) || {};
    const atual = sel.value || padrao || "";
    const grupos = new Map();
    for (const c of d.canais || []) {
      if (c.tipo === "voz") continue;
      if (!grupos.has(c.categoria)) grupos.set(c.categoria, []);
      grupos.get(c.categoria).push(c);
    }
    const opts = [el("option", { value: "" }, (d.canais || []).length ? "Escolha o canal…" : "Bot sem canais (desligado?)")];
    for (const [cat, cs] of grupos) opts.push(el("optgroup", { label: cat || "Sem categoria" }, cs.map((c) => el("option", { value: c.id }, "# " + c.nome))));
    sel.replaceChildren(...opts);
    if (atual) sel.value = atual;
  }
  const nomeCanal = (id) => (((painel && painel.discord) || {}).canais || []).find((c) => c.id === id)?.nome || "";

  // ---------------- MENSAGENS ----------------
  const CORES = ["#FF5A00", "#FFB020", "#5BE37D", "#3BA4FF", "#9B6BFF", "#FF4D4D", "#C9CDD3"];
  let tipoMsg = "mensagem", corMsg = CORES[0];
  function linhaOpcao(caixa, valor, aoMudar) {
    const i = el("input", { maxlength: "55", value: valor || "", placeholder: "Opção", oninput: aoMudar });
    const l = el("div", { class: "adm-opcao" }, i, el("button", { type: "button", title: "Tirar", onclick: () => { if (caixa.children.length > 2) { l.remove(); aoMudar && aoMudar(); } } }, "×"));
    caixa.append(l);
  }
  const opcoesDe = (caixa) => $$("input", caixa).map((i) => i.value.trim()).filter(Boolean);
  function prepararMensagens() {
    const f = $("#form-msg");
    const op = $("#msg-opcoes");
    ["Sim", "Não"].forEach((v) => linhaOpcao(op, v, previa));
    $("#mais-opcao").addEventListener("click", () => { if (op.children.length < 10) linhaOpcao(op, "", previa); });
    $("#msg-cores").replaceChildren(...CORES.map((c) => el("button", { type: "button", style: "background:" + c, class: c === corMsg ? "ativa" : "", "aria-label": "cor " + c,
      onclick: (ev) => { corMsg = c; $$("#msg-cores button").forEach((b) => b.classList.toggle("ativa", b === ev.currentTarget)); previa(); } })));
    $$("#msg-tipo button").forEach((b) => b.addEventListener("click", () => {
      tipoMsg = b.dataset.tipo; $$("#msg-tipo button").forEach((x) => x.classList.toggle("ativa", x === b)); formatoMsg();
    }));
    f.addEventListener("input", previa);
    f.addEventListener("change", () => { formatoMsg(); });
    f.addEventListener("submit", enviarMensagem);
    formatoMsg();
  }
  function formatoMsg() {
    const f = $("#form-msg");
    $$("[data-tipos]", f).forEach((x) => { x.hidden = !x.dataset.tipos.split(" ").includes(tipoMsg); });
    $$("[data-so=discord]", f).forEach((x) => { x.hidden = !f.discord.checked; });
    $("#lbl-titulo").textContent = tipoMsg === "enquete" ? "Pergunta" : "Título";
    $("#lbl-texto").textContent = tipoMsg === "enquete" ? "Texto junto (opcional)" : tipoMsg === "aviso" ? "Texto do aviso" : "Mensagem";
    previa();
  }
  function previa() {
    const f = $("#form-msg"); if (!f) return;
    const titulo = f.titulo.value.trim(), texto = f.texto.value.trim(), men = f.mencao.value;
    const onde = [];
    if (f.jogo.checked) onde.push("chat do jogo");
    if (f.discord.checked) onde.push(f.canal.value ? "#" + nomeCanal(f.canal.value) : "Discord (escolha o canal)");
    $("#prev-onde").textContent = onde.length ? "Vai para: " + onde.join(" e ") : "Escolha pelo menos um destino.";
    const corpo = el("div", { class: "corpo" }, el("div", { class: "quem" }, "DZ Brasil", el("small", {}, "APP")));
    if (men) corpo.append(el("div", {}, el("span", { class: "men" }, "@" + men)));
    if (tipoMsg === "mensagem") corpo.append(el("div", { class: "txt" }, texto || "Sua mensagem aparece aqui."));
    if (tipoMsg === "aviso") {
      const img = f.imagem.value.trim();
      corpo.append(el("div", { class: "embed", style: "border-left-color:" + corMsg }, el("b", {}, titulo || "Título do aviso"),
        el("div", { class: "txt" }, texto || "Texto do aviso."), img.startsWith("https://") ? el("img", { src: img, alt: "" }) : null));
    }
    if (tipoMsg === "enquete") {
      if (texto) corpo.append(el("div", { class: "txt" }, texto));
      corpo.append(el("div", { class: "poll" }, el("b", {}, titulo || "Pergunta da enquete"), opcoesDe($("#msg-opcoes")).map((o) => el("div", {}, o)),
        el("small", { class: "fraco" }, `${f.horas.selectedOptions[0]?.textContent || ""}${f.multi.checked ? " · várias respostas" : ""}`)));
    }
    $("#previa").replaceChildren(el("img", { class: "av", src: "/img/icon64.png", alt: "" }), corpo);
    $("#previa").hidden = !f.discord.checked;
    let jogo = "";
    if (f.jogo.checked) jogo = tipoMsg === "enquete" ? `ENQUETE no Discord: ${titulo || "…"} (vote la!)` : titulo && texto ? `${titulo} - ${texto}` : (titulo || texto || "…");
    $("#previa-jogo").replaceChildren(...(jogo ? [el("b", {}, "[DZ] "), el("em", {}, "ANUNCIO: "), jogo.replace(/\n/g, " ").slice(0, 240)] : []));
  }
  async function enviarMensagem(ev) {
    ev.preventDefault();
    const f = ev.target, R = $("#msg-resultado");
    const corpo = { jogo: f.jogo.checked, discord: f.discord.checked, canal: f.canal.value, tipo: tipoMsg,
      titulo: f.titulo.value, texto: f.texto.value, cor: corMsg, imagem: f.imagem.value, mencao: f.mencao.value,
      opcoes: opcoesDe($("#msg-opcoes")), horas: Number(f.horas.value), multi: f.multi.checked };
    if (corpo.discord && !corpo.canal) { aviso("Escolha o canal do Discord.", true); return; }
    if (corpo.mencao === "everyone" && !confirm("Marcar @everyone avisa TODO mundo do Discord. Enviar?")) return;
    const bt = $("button[type=submit]", f); bt.disabled = true; bt.textContent = "Enviando…";
    try {
      const r = await post("/api/admin/mensagem", corpo);
      R.replaceChildren(...resultadoEnvio(r));
      const okD = !r.discord || r.discord.ok, okJ = !r.jogo || r.jogo.ok;
      if (okD && okJ) { aviso("Mensagem enviada."); f.titulo.value = ""; f.texto.value = ""; f.imagem.value = ""; previa(); setTimeout(carregarPainel, 4000); }
      else aviso("Parte do envio falhou. Veja embaixo do botão.", true);
    } catch (e) { aviso("Erro: " + e.message, true); }
    finally { bt.disabled = false; bt.textContent = "Enviar"; }
  }
  function resultadoEnvio(r) {
    const out = [];
    if (r.jogo) out.push(el("div", { class: "adm-li" }, "🎮", el("span", { class: "cresce" }, r.jogo.ok ? (r.jogo.online ? "Enviado pro chat do jogo." : "Na fila do jogo: aparece quando o servidor ligar.") : r.jogo.erro),
      el("span", { class: "adm-tag " + (r.jogo.ok ? "verde" : "vermelho") }, r.jogo.ok ? "ok" : "erro")));
    if (r.discord) out.push(el("div", { class: "adm-li" }, "💬", el("span", { class: "cresce" }, r.discord.ok ? (r.discord.pendente ? "Na fila do bot." : "Postado no Discord.") : r.discord.erro),
      r.discord.link ? el("a", { href: r.discord.link, target: "_blank", rel: "noopener", class: "mono" }, "ver ↗") : null,
      el("span", { class: "adm-tag " + (r.discord.ok ? "verde" : "vermelho") }, r.discord.ok ? "ok" : "erro")));
    return out;
  }

  // ---------------- WIPE ----------------
  const NOMES_WIPE = { bases: "Bases e construções", veiculos: "Veículos", inventario: "Inventário e roupas", skills: "Skills",
    personagem: "Personagem inteiro", banco: "Banco", ranking: "Ranking", cofres: "Cofres", kits: "Espera dos kits" };
  const ITENS_WIPE = [
    ["bases", "Bases e construções", "Tudo que foi construído no mapa, inclusive as construções das safes feitas com /montar."],
    ["veiculos", "Veículos", "Carros, helis e barcos que estão no mapa."],
    ["inventario", "Inventário e roupas", "O que cada jogador carrega e veste."],
    ["skills", "Skills", "Os níveis de habilidade de todo mundo."],
    ["personagem", "Personagem inteiro", "Vida, posição, inventário e skills. Todo mundo nasce do zero."],
    ["banco", "Dinheiro do banco", "O saldo guardado no banco."],
    ["ranking", "Ranking e estatísticas", "Abates, mortes, XP e tempo jogado (site e Discord)."],
    ["cofres", "Cofres (vaults)", "Os cofres virtuais dos jogadores e VIPs."],
    ["kits", "Espera dos kits", "Libera os kits de novo pra todo mundo."],
  ];
  const PRESETS = { bases: ["bases", "veiculos"], completo: ["bases", "veiculos", "personagem", "banco", "cofres", "kits"], limpar: [] };
  function prepararWipe() {
    const op = $("#w-opcoes");
    ["Sim, bora wipe!", "Esperar mais 1 semana", "Não, tá bom assim"].forEach((v) => linhaOpcao(op, v));
    $("#w-itens").replaceChildren(...ITENS_WIPE.map(([k, n, d]) => el("label", { class: "adm-item" },
      el("input", { type: "checkbox", name: "item", value: k, onchange: marcarPersonagem }), el("span", {}, el("b", {}, n), el("small", {}, d)))));
    $$("#w-preset button").forEach((b) => b.addEventListener("click", () => {
      const ks = PRESETS[b.dataset.preset]; $$("#w-itens input").forEach((i) => { i.checked = ks.includes(i.value); }); marcarPersonagem();
    }));
    $$("#w-rapido button").forEach((b) => b.addEventListener("click", () => {
      let t = new Date(Date.now() + (Number(b.dataset.min) || 0) * 60000 + 60000);
      if (b.dataset.amanha) { t = new Date(); t.setDate(t.getDate() + 1); t.setHours(18, 0, 0, 0); }
      $("#form-wipe").quando.value = paraInput(t);
    }));
    const amanha = new Date(); amanha.setDate(amanha.getDate() + 1); amanha.setHours(18, 0, 0, 0);
    $("#form-wipe").quando.value = paraInput(amanha);
    $("#form-enquete-wipe").addEventListener("submit", enviarEnqueteWipe);
    $("#form-wipe").addEventListener("submit", marcarWipe);
    $("#form-wipe").anunciar.addEventListener("change", (e) => { $("#w-canal-anuncio").closest("label").hidden = !e.target.checked; });
  }
  function marcarPersonagem() {
    const p = $("#w-itens input[value=personagem]");
    ["inventario", "skills"].forEach((k) => { const i = $(`#w-itens input[value=${k}]`); if (p.checked) i.checked = true; i.disabled = p.checked; });
  }
  const paraInput = (t) => new Date(t.getTime() - t.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  function desenharWipe() {
    if (!painel) return;
    desenharAgendado($("#w-agendado"), false);
    const cb = painel.canaisBot || {};
    preencherCanais($("#w-canal-enquete"), cb.wipe || cb.anuncios);
    preencherCanais($("#w-canal-anuncio"), cb.wipe || cb.anuncios);
    const enq = ((painel.discord || {}).enquetes || []).filter((e) => e.rotulo === "wipe");
    $("#w-votos").replaceChildren(...(enq.length ? [el("h3", { class: "pequeno fraco mono" }, "VOTOS"), ...enq.slice(0, 2).map(cardEnquete)] : []));
    $("#w-historico").replaceChildren(historico((painel.historico || []).filter((x) => x.tipo === "wipe")));
    $("#w-botao").disabled = !eu.dono || !!painel.acao;
    $("#w-so-dono").hidden = eu.dono;
  }
  async function enviarEnqueteWipe(ev) {
    ev.preventDefault();
    const f = ev.target;
    if (!eu.mundo) { aviso("Você não tem permissão de anúncio.", true); return; }
    if (!f.canal.value) { aviso("Escolha o canal da enquete.", true); return; }
    try {
      const r = await post("/api/admin/mensagem", { jogo: f.jogo.checked, discord: true, canal: f.canal.value, tipo: "enquete",
        titulo: f.titulo.value, texto: "Votem aí pra gente decidir o wipe! 👇", opcoes: opcoesDe($("#w-opcoes")), horas: Number(f.horas.value), mencao: "everyone", rotulo: "wipe" });
      $("#w-res-enquete").replaceChildren(...resultadoEnvio(r));
      if (r.discord && r.discord.ok) { aviso("Enquete enviada. Os votos aparecem aqui em até 1 minuto."); setTimeout(carregarPainel, 5000); }
    } catch (e) { aviso("Erro: " + e.message, true); }
  }
  async function marcarWipe(ev) {
    ev.preventDefault();
    const f = ev.target;
    const itens = {}; $$("#w-itens input:checked").forEach((i) => { itens[i.value] = true; });
    if (!Object.keys(itens).length) { aviso("Marque o que vai ser apagado.", true); return; }
    const quando = Math.floor(new Date(f.quando.value).getTime() / 1000);
    if (!quando) { aviso("Escolha a data e a hora.", true); return; }
    if (f.confirmar.value.trim().toUpperCase() !== "WIPE") { aviso("Digite WIPE no campo de confirmação.", true); return; }
    const lista = Object.keys(itens).map((k) => "• " + NOMES_WIPE[k]).join("\n");
    if (!confirm(`Marcar WIPE para ${dataLonga(quando)}?\n\nVai apagar:\n${lista}\n\nFaz backup antes.`)) return;
    try {
      const r = await post("/api/admin/servidor", { acao: "wipe", quando, itens, motivo: f.motivo.value, confirmar: f.confirmar.value,
        anunciar: f.anunciar.checked, canal: f.canal.value });
      aviso("Wipe marcado para " + dataLonga(r.quando) + "." + (r.discord && !r.discord.ok ? " (Discord: " + r.discord.erro + ")" : ""));
      f.confirmar.value = "";
      carregarPainel();
    } catch (e) { aviso("Erro: " + e.message, true); }
  }

  // ---------------- JOGADORES ----------------
  function desenharJogadores() {
    const L = $("#lista");
    const q = $("#busca").value.trim().toLowerCase();
    const l = jogadores.filter((j) => !q || j.nome.toLowerCase().includes(q) || j.id === q);
    if (!l.length) { L.replaceChildren(vazio("Ninguém encontrado.")); return; }
    const th = (t, num) => el("th", { class: num ? "num" : "" }, t);
    L.replaceChildren(el("div", { class: "adm-tabela-envolve" }, el("table", { class: "adm-tabela" },
      el("thead", {}, el("tr", {}, th("Jogador"), th("Patente"), th("Nível", 1), th("Abates", 1), th("Mortes", 1), th("K/D", 1), th("Zumbis", 1), th("Tempo", 1), th("Steam"), th(""))),
      el("tbody", {}, l.slice(0, 300).map((j) => el("tr", {},
        el("td", {}, el("span", { class: "adm-ponto" + (j.online ? " on" : ""), title: j.online ? "online" : "offline" }), j.nome),
        el("td", {}, el("span", { class: "adm-tag" }, j.patente)), el("td", { class: "num" }, j.nivel), el("td", { class: "num" }, j.abates),
        el("td", { class: "num" }, j.mortes), el("td", { class: "num" }, j.kd.toFixed(2)), el("td", { class: "num" }, j.zumbis),
        el("td", { class: "num" }, tempo(j.segundos)), el("td", {}, steamLink(j.id)),
        el("td", {}, eu.ban ? el("button", { class: "botao perigo pequeno", onclick: () => banirPrompt(j.id, j.nome) }, "Banir") : null)))))));
  }

  // ---------------- banidos, suspeitos, reports, staff ----------------
  function filtro(x) {
    const q = $("#busca").value.trim().toLowerCase();
    return !q || (x.Nome || "").toLowerCase().includes(q) || (x.Steam || "") === q;
  }
  function cardBanido(b) {
    const ev = (b.Evidencia || "").split(/ \|\| | \| /).filter(Boolean);
    return el("article", { class: "adm-card" },
      el("h3", {}, b.Nome || b.Steam, steamLink(b.Steam), el("span", { class: "adm-tag vermelho" }, b.Duracao < 0 ? "PERMANENTE" : "falta " + tempo(b.Resta))),
      el("div", { class: "adm-info" },
        el("div", {}, el("b", {}, "Motivo: "), b.Motivo || "-"),
        el("div", {}, el("b", {}, "Quem baniu: "), b.JuizNome || "-", "  ·  ", data(b.Banido)),
        el("div", {}, el("b", {}, "Duração: "), b.Duracao < 0 ? "permanente" : tempo(b.Duracao)),
        foto(b.Foto)),
      el("div", { class: "adm-acoes" },
        el("button", { class: "botao ok pequeno", disabled: !eu.ban || null, onclick: () => { if (confirm(`Desbanir ${b.Nome}?`)) acao("desbanir", b.Steam); } }, "DESBANIR")),
      lista("Log / prova", ev) || el("details", {}, el("summary", {}, "Sem log registrado (ban antigo ou pelo console)")));
  }
  function cardSuspeito(s) {
    return el("article", { class: "adm-card" + (s.Pontos >= 60 ? " alerta" : "") },
      el("h3", {}, s.Nome || s.Steam, steamLink(s.Steam), el("span", { class: "adm-tag" + (s.Pontos >= 60 ? " vermelho" : "") }, s.Pontos + " pontos")),
      el("div", { class: "adm-info" },
        el("div", {}, el("b", {}, "Último alerta: "), data(s.Ultimo)),
        el("div", {}, el("b", {}, "Mais recente: "), (s.Motivos || []).slice(-1)[0] || "-")),
      el("div", { class: "adm-acoes" },
        el("button", { class: "botao perigo pequeno", disabled: !eu.ban || null, onclick: () => banirPrompt(s.Steam, s.Nome) }, "BANIR"),
        el("button", { class: "botao fantasma pequeno", disabled: !eu.ban || null, onclick: () => { if (confirm(`Apagar a suspeita de ${s.Nome}?`)) acao("ignorar", s.Steam); } }, "IGNORAR")),
      lista("O que o DZ GUARD viu", (s.Motivos || []).slice().reverse()));
  }
  function cardReport(r) {
    return el("article", { class: "adm-card" },
      el("h3", {}, r.Nome || r.Steam, steamLink(r.Steam), el("span", { class: "adm-tag" }, data(r.Quando))),
      el("div", { class: "adm-info" },
        el("div", {}, el("b", {}, "Reportado por: "), r.QuemNome || r.QuemSteam),
        el("div", {}, el("b", {}, "Motivo: "), r.Motivo || "-"),
        foto(r.Foto)),
      el("div", { class: "adm-acoes" },
        el("button", { class: "botao perigo pequeno", disabled: !eu.ban || null, onclick: () => banirPrompt(r.Steam, r.Nome) }, "BANIR")),
      lista("O que ele estava fazendo", r.Historico));
  }
  function cardStaff(m) {
    const tem = new Set(m.Perms || []);
    return el("article", { class: "adm-card" },
      el("h3", {}, m.Nome || m.Steam, steamLink(m.Steam), el("span", { class: "adm-tag" }, tem.size + " permissões")),
      el("div", { class: "adm-perms" }, staff.permissoes.map((p) => el("label", { class: "adm-perm" + (tem.has(p) ? " on" : "") },
        el("input", { type: "checkbox", checked: tem.has(p) || null, onchange: (ev) => acao("perm", m.Steam, ev.target.checked ? 1 : 0, p) }),
        nomesPerm[p] || p))),
      el("div", { class: "adm-acoes" },
        el("button", { class: "botao perigo pequeno", onclick: () => { if (confirm(`Tirar ${m.Nome} da staff?`)) acao("staff_remover", m.Steam); } }, "TIRAR DA STAFF")));
  }
  function desenharStaff() {
    const S = $("#staff");
    const novo = el("form", { class: "adm-bloco adm-form adm-estreito" },
      el("h2", {}, "Colocar alguém na staff"),
      el("label", { class: "adm-campo" }, "Steam ID ", el("input", { name: "steam", placeholder: "7656119...", required: true, pattern: "7656[0-9]{13}" })),
      el("p", { class: "adm-nota" }, "Entra com a permissão de pegar kits. Depois marque o resto abaixo."),
      el("button", { class: "botao ok", type: "submit" }, "ADICIONAR"));
    novo.addEventListener("submit", (ev) => { ev.preventDefault(); acao("perm", new FormData(novo).get("steam"), 1, "Kits"); novo.reset(); });
    const itens = staff.staff.filter(filtro);
    S.replaceChildren(novo, ...(itens.length ? itens.map(cardStaff) : [vazio("Ninguém na staff ainda.")]));
  }

  // ---------------- navegação e carga ----------------
  function abrir(nova) {
    aba = nova;
    if (location.hash.slice(1) !== aba) history.replaceState(null, "", "#" + aba);
    $$("#abas button").forEach((x) => x.classList.toggle("ativa", x.dataset.aba === aba));
    const [t, s] = titulos[aba] || [aba, ""];
    $("#titulo").textContent = t; $("#subtitulo").textContent = s;
    const ehLista = ["banidos", "suspeitos", "reports", "jogadores"].includes(aba);
    $$("[data-painel]").forEach((p) => { p.hidden = !(p.dataset.painel === aba || (ehLista && p.dataset.painel === "lista")); });
    desenhar();
    if (aba === "logs") carregarLog();
    if (aba === "jogadores") carregarJogadores();
    if (aba === "mensagens" || aba === "wipe" || aba === "dashboard") carregarPainel();
  }
  function desenhar() {
    $("#n-banidos").textContent = dados.banidos.length || "";
    $("#n-suspeitos").textContent = dados.suspeitos.length || "";
    $("#n-reports").textContent = dados.reports.length || "";
    $("#n-jogadores").textContent = painel ? painel.contagem.jogadores || "" : "";
    if (aba === "dashboard") desenharDashboard();
    if (aba === "wipe") desenharWipe();
    if (aba === "mensagens" && painel) { preencherCanais($("#msg-canal"), (painel.canaisBot || {}).anuncios); previa(); }
    if (aba === "staff") desenharStaff();
    if (aba === "jogadores") desenharJogadores();
    const f = { banidos: cardBanido, suspeitos: cardSuspeito, reports: cardReport }[aba];
    if (f) {
      const itens = dados[aba].filter(filtro);
      $("#lista").replaceChildren(...(itens.length ? itens.map(f) : [vazio("Nada aqui por enquanto.")]));
    }
  }
  function pilulas() {
    const st = (painel && painel.status) || {};
    const s = $("#pilula-servidor"), b = $("#pilula-bot");
    s.className = "adm-pilula " + (st.online ? "on" : "off");
    s.textContent = st.online ? `Servidor ${(st.jogadores || []).length}/${st.max}` : "Servidor offline";
    b.className = "adm-pilula " + (painel && painel.botOnline ? "on" : "off");
    b.textContent = painel && painel.botOnline ? "Bot Discord" : "Bot offline";
  }
  async function carregarPainel() {
    try { painel = await api("/api/admin/painel"); pilulas(); desenhar(); }
    catch (e) { aviso("Erro ao carregar o painel: " + e.message, true); }
  }
  async function carregarListas() {
    try {
      const [b, s, r] = await Promise.all([api("/api/admin/banidos"), api("/api/admin/suspeitos"), api("/api/admin/reports")]);
      Object.assign(dados, { banidos: b, suspeitos: s, reports: r });
      if (eu.dono) staff = await api("/api/admin/staff");
      desenhar();
    } catch (e) { aviso("Erro ao carregar: " + e.message, true); }
  }
  async function carregarJogadores() {
    try { jogadores = await api("/api/admin/jogadores"); if (aba === "jogadores") desenharJogadores(); }
    catch (e) { aviso("Erro: " + e.message, true); }
  }
  async function carregarLog() {
    try { $("#log-texto").textContent = (await api("/api/admin/log?q=" + log)).join("\n") || "Vazio."; }
    catch (e) { $("#log-texto").textContent = "Erro: " + e.message; }
  }

  $$("[data-i]").forEach((i) => i.append(icone(i.dataset.i)));
  $$("#abas button").forEach((b) => b.addEventListener("click", () => abrir(b.dataset.aba)));
  $$("#sub-logs button").forEach((b) => b.addEventListener("click", () => {
    $$("#sub-logs button").forEach((x) => x.classList.toggle("ativa", x === b));
    log = b.dataset.log; carregarLog();
  }));
  $("#busca").addEventListener("input", desenhar);
  $("#zoom").addEventListener("click", () => { $("#zoom").hidden = true; });
  $("#form-banir").addEventListener("submit", async (ev) => {
    ev.preventDefault();
    const f = new FormData(ev.target);
    if (!confirm(`Banir ${f.get("steam")}?`)) return;
    if (await acao("banir", f.get("steam"), f.get("horas"), f.get("motivo"))) ev.target.reset();
  });

  (async function iniciar() {
    eu = await api("/api/admin/eu").catch(() => ({}));
    if (!eu.staff) {
      $("#fora").hidden = false;
      $("#naostaff").hidden = !eu.steam;
      return;
    }
    $("#quem").replaceChildren(el("b", {}, eu.nome || eu.steam), el("span", { class: "fraco" }, eu.dono ? "dono" : eu.ban ? "staff" : "staff (sem ban)"), el("a", { href: "/sair" }, "sair"));
    $$("#abas button[data-precisa]").forEach((b) => { b.hidden = !eu[b.dataset.precisa]; });
    $("#dentro").hidden = false;
    prepararMensagens();
    prepararWipe();
    prepararDesempenho();
    carregarMaquina();
    setInterval(() => { if (aba === "dashboard" && !document.hidden) carregarMaquina(); }, 2000);
    const inicial = (location.hash || "").slice(1);
    abrir(titulos[inicial] ? inicial : "dashboard");
    window.addEventListener("hashchange", () => { const h = location.hash.slice(1); if (titulos[h] && h !== aba) abrir(h); });
    carregarListas();
    setInterval(carregarListas, 30000);
    setInterval(() => { if (["dashboard", "wipe"].includes(aba) && !document.hidden) carregarPainel(); }, 20000);
  })();
})();
