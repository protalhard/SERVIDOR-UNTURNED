// Painel admin do site: banidos, suspeitos do DZ GUARD, reports e logs.
// Tudo que vem do jogo (nomes, motivos) entra como texto (textContent), nunca como HTML.
(function () {
  "use strict";
  const $ = (s) => document.querySelector(s);
  let eu = null, aba = "banidos", log = "admin";
  const dados = { banidos: [], suspeitos: [], reports: [] };

  function el(tag, props, ...filhos) {
    const e = document.createElement(tag);
    for (const [k, v] of Object.entries(props || {})) {
      if (k === "class") e.className = v;
      else if (k.startsWith("on")) e.addEventListener(k.slice(2), v);
      else if (v !== undefined && v !== null && v !== false) e.setAttribute(k, v);
    }
    for (const f of filhos.flat()) if (f !== null && f !== undefined && f !== false) e.append(f instanceof Node ? f : String(f));
    return e;
  }
  const data = (u) => u > 0 ? new Date(u * 1000).toLocaleString("pt-BR", { day: "2-digit", month: "2-digit", year: "2-digit", hour: "2-digit", minute: "2-digit" }) : "-";
  function tempo(s) {
    if (s < 0) return "permanente";
    const d = Math.floor(s / 86400), h = Math.floor(s % 86400 / 3600), m = Math.floor(s % 3600 / 60);
    return d > 0 ? `${d}d ${h}h` : h > 0 ? `${h}h ${m}min` : `${m}min`;
  }
  const steamLink = (id) => el("a", { href: "https://steamcommunity.com/profiles/" + id, target: "_blank", rel: "noopener" }, id);
  const fotoUrl = (f) => f && f.startsWith("spy/") ? "/api/admin/foto/" + f.slice(4) : "";
  function foto(f) {
    const u = fotoUrl(f);
    if (!u) return null;
    const i = el("img", { class: "adm-foto", src: u, alt: "foto da tela", loading: "lazy" });
    i.addEventListener("error", () => i.replaceWith(el("span", { class: "adm-tag" }, "foto nao chegou")));
    i.addEventListener("click", () => { $("#zoom img").src = u; $("#zoom").hidden = false; });
    return i;
  }
  function lista(titulo, itens) {
    if (!itens || !itens.length) return null;
    return el("details", {}, el("summary", {}, titulo + ` (${itens.length})`), el("ol", {}, itens.map((x) => el("li", {}, x))));
  }
  function aviso(t, erro) { const a = $("#aviso"); a.textContent = t; a.className = "adm-aviso" + (erro ? " erro" : ""); }

  async function api(caminho) {
    const r = await fetch(caminho, { credentials: "same-origin" });
    if (!r.ok) throw new Error(await r.text());
    return r.json();
  }
  async function acao(acao, steam, horas, motivo) {
    if (!eu.ban) { aviso("Voce nao tem a permissao Ban.", true); return false; }
    const r = await fetch("/api/admin/acao", {
      method: "POST", credentials: "same-origin",
      headers: { "Content-Type": "application/json", "X-DZ": "1" },
      body: JSON.stringify({ acao, steam, horas: Number(horas) || 0, motivo: motivo || "" }),
    });
    if (!r.ok) { aviso("Erro: " + (await r.text()), true); return false; }
    aviso("Enviado pro servidor. Atualiza em alguns segundos (o servidor precisa estar ligado).");
    setTimeout(carregar, 3000); setTimeout(carregar, 8000);
    return true;
  }
  function banirPrompt(steam, nome) {
    const motivo = prompt(`Motivo do ban de ${nome}:`, "Hack (DZ GUARD)");
    if (!motivo) return;
    const horas = prompt("Quantas horas? (0 = permanente)", "0");
    if (horas === null) return;
    acao("banir", steam, horas, motivo);
  }

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
        el("button", { class: "adm-btn ok", disabled: !eu.ban || null, onclick: () => { if (confirm(`Desbanir ${b.Nome}?`)) acao("desbanir", b.Steam); } }, "DESBANIR")),
      lista("Log / prova", ev) || el("details", {}, el("summary", {}, "Sem log registrado (ban antigo ou pelo console)")));
  }
  function cardSuspeito(s) {
    return el("article", { class: "adm-card" + (s.Pontos >= 60 ? " alerta" : "") },
      el("h3", {}, s.Nome || s.Steam, steamLink(s.Steam), el("span", { class: "adm-tag" + (s.Pontos >= 60 ? " vermelho" : "") }, s.Pontos + " pontos")),
      el("div", { class: "adm-info" },
        el("div", {}, el("b", {}, "Último alerta: "), data(s.Ultimo)),
        el("div", {}, el("b", {}, "Mais recente: "), (s.Motivos || []).slice(-1)[0] || "-")),
      el("div", { class: "adm-acoes" },
        el("button", { class: "adm-btn perigo", disabled: !eu.ban || null, onclick: () => banirPrompt(s.Steam, s.Nome) }, "BANIR"),
        el("button", { class: "adm-btn leve", disabled: !eu.ban || null, onclick: () => { if (confirm(`Apagar a suspeita de ${s.Nome}?`)) acao("ignorar", s.Steam); } }, "IGNORAR")),
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
        el("button", { class: "adm-btn perigo", disabled: !eu.ban || null, onclick: () => banirPrompt(r.Steam, r.Nome) }, "BANIR")),
      lista("O que ele estava fazendo", r.Historico));
  }

  function desenhar() {
    $("#n-banidos").textContent = dados.banidos.length;
    $("#n-suspeitos").textContent = dados.suspeitos.length;
    $("#n-reports").textContent = dados.reports.length;
    const L = $("#lista");
    L.hidden = !["banidos", "suspeitos", "reports"].includes(aba);
    $("#form-banir").hidden = aba !== "banir";
    $("#logs").hidden = aba !== "logs";
    $("#busca").hidden = aba === "banir" || aba === "logs";
    if (L.hidden) return;
    const f = { banidos: cardBanido, suspeitos: cardSuspeito, reports: cardReport }[aba];
    const itens = dados[aba].filter(filtro);
    L.replaceChildren(...(itens.length ? itens.map(f) : [el("div", { class: "adm-vazio" }, "Nada aqui por enquanto.")]));
  }

  async function carregar() {
    try {
      const [b, s, r] = await Promise.all([api("/api/admin/banidos"), api("/api/admin/suspeitos"), api("/api/admin/reports")]);
      Object.assign(dados, { banidos: b, suspeitos: s, reports: r });
      desenhar();
      if (aba === "logs") carregarLog();
    } catch (e) { aviso("Erro ao carregar: " + e.message, true); }
  }
  async function carregarLog() {
    try { $("#log-texto").textContent = (await api("/api/admin/log?q=" + log)).join("\n") || "Vazio."; }
    catch (e) { $("#log-texto").textContent = "Erro: " + e.message; }
  }

  document.querySelectorAll("#abas button").forEach((b) => b.addEventListener("click", () => {
    document.querySelectorAll("#abas button").forEach((x) => x.classList.toggle("ativa", x === b));
    aba = b.dataset.aba; desenhar(); if (aba === "logs") carregarLog();
  }));
  document.querySelectorAll("#logs .adm-sub button").forEach((b) => b.addEventListener("click", () => {
    document.querySelectorAll("#logs .adm-sub button").forEach((x) => x.classList.toggle("ativa", x === b));
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
    $("#quem").replaceChildren(el("span", {}, (eu.nome || eu.steam) + (eu.dono ? " (dono)" : eu.ban ? " (staff)" : " (staff, sem permissao de ban)")), el("a", { href: "/sair" }, "sair"));
    $("#dentro").hidden = false;
    carregar();
    setInterval(carregar, 30000);
  })();
})();
