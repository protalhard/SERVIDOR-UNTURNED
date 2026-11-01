// DZ RÁDIO: player fixo no canto (lo-fi + synthwave Creative Commons), barras que pulam com a batida
// na capa do início, volume mais baixo fora do início e sons curtinhos ao passar o mouse e clicar.
// O navegador não deixa tocar som sozinho: a música começa no primeiro clique ou tecla do visitante.
(function () {
  'use strict';
  const GUARDA = 'dz_radio';
  const reduzir = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const salvo = (() => { try { return JSON.parse(localStorage.getItem(GUARDA)) || {}; } catch (e) { return {}; } })();
  const est = {
    i: salvo.i | 0, t: +salvo.t || 0, vol: salvo.vol ?? 0.6,
    aberto: salvo.aberto ?? innerWidth >= 1500, ligado: salvo.ligado ?? true, sfx: salvo.sfx ?? !reduzir,
  };
  let lista = [], audio, ctx, ganho, analisador, dadosFreq, sfxGanho, tocando = false, inicio = true;

  const guardar = () => { try { localStorage.setItem(GUARDA, JSON.stringify({ ...est, t: audio ? audio.currentTime : est.t })); } catch (e) {} };
  const tempo = (s) => { s = Math.max(0, Math.floor(s || 0)); return Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0'); };
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const matiz = (i) => (i * 47) % 360;

  // ---------- tela de carregamento (só na primeira página da visita) ----------
  const carga = document.getElementById('carga');
  if (carga) {
    if (sessionStorage.getItem('dz_carregou') || reduzir) carga.remove();
    else {
      const t0 = performance.now();
      const fim = () => setTimeout(() => { carga.classList.add('sai'); setTimeout(() => carga.remove(), 700); }, Math.max(0, 1100 - (performance.now() - t0)));
      if (document.readyState === 'complete') fim(); else addEventListener('load', fim);
      setTimeout(fim, 4000);
      sessionStorage.setItem('dz_carregou', '1');
    }
  }

  // ---------- montagem do player ----------
  const raiz = document.createElement('div');
  raiz.className = 'radio';
  raiz.innerHTML = `
    <section class="radio-painel" id="radio-painel" aria-label="DZ Rádio">
      <header><span class="radio-marca">DZ <b>RÁDIO</b></span><span class="radio-ao-vivo"><i></i>LO-FI · SYNTHWAVE</span>
        <button class="radio-x" data-r="fechar" aria-label="Minimizar">✕</button></header>
      <div class="radio-agora">
        <div class="radio-capa" id="radio-capa"><span>♪</span></div>
        <div class="radio-info"><strong id="radio-titulo">DZ RÁDIO</strong><small id="radio-artista">Clique em qualquer lugar pra tocar</small>
          <canvas class="radio-eq" id="radio-eq" width="132" height="18"></canvas></div>
      </div>
      <div class="radio-barra"><input type="range" id="radio-pos" min="0" max="1000" value="0" aria-label="Posição da música">
        <div class="radio-tempos"><span id="radio-t">0:00</span><span id="radio-d">0:00</span></div></div>
      <div class="radio-botoes">
        <button data-r="ant" aria-label="Anterior">⏮</button>
        <button data-r="play" class="radio-play" aria-label="Tocar ou pausar"><span class="ic-play">▶</span><span class="ic-pause">❚❚</span></button>
        <button data-r="prox" aria-label="Próxima">⏭</button>
      </div>
      <div class="radio-vol"><button data-r="mudo" aria-label="Sem som" id="radio-mudo">🔊</button>
        <input type="range" id="radio-vol" min="0" max="100" aria-label="Volume">
        <label class="radio-sfx" title="Sons ao passar o mouse e clicar"><input type="checkbox" id="radio-sfx"> sons</label></div>
      <ol class="radio-lista" id="radio-lista"></ol>
      <p class="radio-credito">Músicas Creative Commons de cada artista (<a href="/radio.json" target="_blank" rel="noopener">créditos e licenças</a>).</p>
    </section>
    <button class="radio-pilula" id="radio-pilula" aria-label="Abrir a DZ Rádio">
      <span class="radio-pp" data-r="play"><span class="ic-play">▶</span><span class="ic-pause">❚❚</span></span>
      <canvas class="radio-eq" id="radio-eq2" width="40" height="14"></canvas>
      <span class="radio-pnome" id="radio-pnome">DZ RÁDIO</span><span class="radio-abrir">☰</span>
    </button>`;
  document.body.appendChild(raiz);
  const $ = (id) => document.getElementById(id);
  const painel = $('radio-painel'), pilula = $('radio-pilula');

  function abrirPainel(v) { est.aberto = v; raiz.classList.toggle('aberto', v); guardar(); }
  abrirPainel(est.aberto);

  // ---------- áudio ----------
  audio = new Audio();
  audio.preload = 'none';
  audio.crossOrigin = 'anonymous';

  function montarAudio() {
    if (ctx) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    ctx = new AC();
    const fonte = ctx.createMediaElementSource(audio);
    analisador = ctx.createAnalyser();
    analisador.fftSize = 256; analisador.smoothingTimeConstant = 0.78;
    dadosFreq = new Uint8Array(analisador.frequencyBinCount);
    ganho = ctx.createGain();
    fonte.connect(analisador);
    fonte.connect(ganho); ganho.connect(ctx.destination);
    sfxGanho = ctx.createGain(); sfxGanho.gain.value = 1; sfxGanho.connect(ctx.destination);
    ajustarVolume(true);
  }

  // fora do início a música fica baixinha (35%) e volta ao normal na capa
  function ajustarVolume(ja) {
    const alvo = est.vol * (inicio ? 1 : 0.35);
    if (ganho) { audio.volume = 1; ganho.gain.cancelScheduledValues(ctx.currentTime); ganho.gain.setTargetAtTime(alvo, ctx.currentTime, ja ? 0.01 : 0.14); }
    else audio.volume = alvo;
    $('radio-mudo').textContent = est.vol === 0 ? '🔇' : est.vol < 0.4 ? '🔉' : '🔊';
  }

  function carregar(i, comecarEm) {
    if (!lista.length) return;
    est.i = (i + lista.length) % lista.length;
    const m = lista[est.i];
    audio.src = '/musicas/' + m.arquivo;
    if (comecarEm) audio.addEventListener('loadedmetadata', () => { try { audio.currentTime = comecarEm; } catch (e) {} }, { once: true });
    $('radio-titulo').textContent = m.titulo;
    $('radio-artista').textContent = m.artista + ' · ' + m.tipo;
    $('radio-pnome').textContent = m.titulo + ' · ' + m.artista;
    $('radio-d').textContent = tempo(m.dur);
    $('radio-capa').style.setProperty('--h', matiz(est.i));
    $('radio-capa').firstElementChild.textContent = m.artista.slice(0, 1);
    document.querySelectorAll('#radio-lista li').forEach((li, k) => li.classList.toggle('atual', k === est.i));
    const atual = document.querySelector('#radio-lista li.atual');
    if (atual && est.aberto) atual.scrollIntoView({ block: 'nearest' });
    if ('mediaSession' in navigator) navigator.mediaSession.metadata = new MediaMetadata({ title: m.titulo, artist: m.artista, album: 'DZ RÁDIO' });
    guardar();
  }

  async function tocar() {
    montarAudio();
    if (ctx && ctx.state === 'suspended') try { await ctx.resume(); } catch (e) {}
    if (!audio.src) carregar(est.i, est.t);
    try { await audio.play(); est.ligado = true; } catch (e) { /* sem gesto ainda ou arquivo faltando */ }
    guardar();
  }
  function pausar() { audio.pause(); est.ligado = false; guardar(); }
  function proxima(passo) { const tava = tocando || est.ligado; carregar(est.i + passo, 0); if (tava) tocar(); }

  audio.addEventListener('play', () => { tocando = true; raiz.classList.add('tocando'); });
  audio.addEventListener('pause', () => { tocando = false; raiz.classList.remove('tocando'); });
  audio.addEventListener('ended', () => proxima(1));
  let erros = 0;
  audio.addEventListener('error', () => { if (!audio.src || ++erros > lista.length) return; setTimeout(() => proxima(1), 400); });
  audio.addEventListener('playing', () => { erros = 0; });
  let ultimoSalvo = 0;
  audio.addEventListener('timeupdate', () => {
    const d = audio.duration || lista[est.i]?.dur || 1;
    if (!arrastando) $('radio-pos').value = Math.round(audio.currentTime / d * 1000);
    $('radio-t').textContent = tempo(audio.currentTime);
    if (audio.duration) $('radio-d').textContent = tempo(audio.duration);
    if (Date.now() - ultimoSalvo > 4000) { ultimoSalvo = Date.now(); guardar(); }
  });
  addEventListener('pagehide', guardar);

  let arrastando = false;
  const pos = $('radio-pos');
  pos.addEventListener('input', () => { arrastando = true; $('radio-t').textContent = tempo(pos.value / 1000 * (audio.duration || lista[est.i]?.dur || 0)); });
  pos.addEventListener('change', () => { arrastando = false; if (audio.duration) audio.currentTime = pos.value / 1000 * audio.duration; });
  const vol = $('radio-vol');
  vol.value = Math.round(est.vol * 100);
  vol.addEventListener('input', () => { est.vol = vol.value / 100; ajustarVolume(true); guardar(); });
  const sfx = $('radio-sfx');
  sfx.checked = est.sfx;
  sfx.addEventListener('change', () => { est.sfx = sfx.checked; guardar(); });

  raiz.addEventListener('click', (e) => {
    const b = e.target.closest('[data-r]');
    if (!b) { if (e.target.closest('#radio-pilula')) abrirPainel(true); return; }
    e.stopPropagation();
    const r = b.dataset.r;
    if (r === 'play') tocando ? pausar() : tocar();
    else if (r === 'prox') proxima(1);
    else if (r === 'ant') audio.currentTime > 4 ? (audio.currentTime = 0) : proxima(-1);
    else if (r === 'fechar') abrirPainel(false);
    else if (r === 'mudo') { est.vol = est.vol > 0 ? 0 : 0.6; vol.value = est.vol * 100; ajustarVolume(true); guardar(); }
  });
  $('radio-lista').addEventListener('click', (e) => {
    const li = e.target.closest('li');
    if (!li) return;
    carregar(+li.dataset.i, 0); tocar();
  });
  if ('mediaSession' in navigator) {
    navigator.mediaSession.setActionHandler('play', tocar);
    navigator.mediaSession.setActionHandler('pause', pausar);
    navigator.mediaSession.setActionHandler('nexttrack', () => proxima(1));
    navigator.mediaSession.setActionHandler('previoustrack', () => proxima(-1));
  }

  // primeiro gesto da visita: liga o áudio (e a música, se a pessoa não tinha pausado)
  function primeiroGesto(e) {
    removeEventListener('pointerdown', primeiroGesto, true);
    removeEventListener('keydown', primeiroGesto, true);
    montarAudio();
    if (ctx && ctx.state === 'suspended') ctx.resume();
    if (est.ligado && !(e.target && e.target.closest && e.target.closest('[data-r="play"]'))) tocar();
  }
  addEventListener('pointerdown', primeiroGesto, true);
  addEventListener('keydown', primeiroGesto, true);

  fetch('/radio.json').then((r) => r.json()).then((l) => {
    lista = l;
    $('radio-lista').innerHTML = l.map((m, k) => `<li data-i="${k}"><span class="n">${String(k + 1).padStart(2, '0')}</span>` +
      `<span class="t"><b>${esc(m.titulo)}</b> · ${esc(m.artista)}</span><span class="d">${tempo(m.dur)}</span></li>`).join('');
    carregar(Math.min(est.i, l.length - 1), est.t);
  }).catch(() => raiz.remove());

  // ---------- início x outras páginas ----------
  function verPagina() {
    const agora = document.body.classList.contains('pagina-inicio');
    if (agora !== inicio) { inicio = agora; ajustarVolume(false); }
    montarBarras();
  }
  inicio = document.body.classList.contains('pagina-inicio');
  new MutationObserver(verPagina).observe(document.body, { attributes: true, attributeFilter: ['class'] });
  new MutationObserver(montarBarras).observe(document.getElementById('app') || document.body, { childList: true });

  // ---------- barras que pulam com a batida (capa do início) ----------
  let telaBarras = null;
  function montarBarras() {
    const heroi = document.querySelector('.heroi-full');
    if (!heroi || reduzir) { telaBarras = null; return; }
    if (heroi.querySelector('.radio-horizonte')) return;
    telaBarras = document.createElement('canvas');
    telaBarras.className = 'radio-horizonte';
    telaBarras.setAttribute('aria-hidden', 'true');
    const veu = heroi.querySelector('.heroi-veu');
    veu ? veu.after(telaBarras) : heroi.prepend(telaBarras);
  }

  const niveis = new Float32Array(64);
  function bandas(n) {
    // n valores 0..1 (graves à esquerda, agudos à direita, escala log)
    const out = new Float32Array(n);
    if (!analisador || !tocando) return out;
    analisador.getByteFrequencyData(dadosFreq);
    const max = dadosFreq.length * 0.72;
    for (let k = 0; k < n; k++) {
      const a = Math.floor(Math.pow(k / n, 1.6) * max) + 1, b = Math.max(a + 1, Math.floor(Math.pow((k + 1) / n, 1.6) * max) + 1);
      let s = 0; for (let j = a; j < b; j++) s += dadosFreq[j];
      out[k] = s / (b - a) / 255;
    }
    return out;
  }

  function desenharEq(cv, n, cor) {
    const g = cv.getContext('2d'), w = cv.width, h = cv.height, b = bandas(n), lw = w / n;
    g.clearRect(0, 0, w, h);
    for (let k = 0; k < n; k++) {
      const v = tocando ? Math.max(0.12, b[k]) : 0.12;
      g.fillStyle = cor(k / n);
      g.fillRect(k * lw + 1, h - v * h, Math.max(1, lw - 2), v * h);
    }
  }

  function quadro() {
    requestAnimationFrame(quadro);
    if (document.hidden) return;
    const corEq = (x) => `hsl(${20 + x * 22}, 100%, ${55 + x * 8}%)`;
    if (est.aberto) desenharEq($('radio-eq'), 22, corEq); else desenharEq($('radio-eq2'), 7, corEq);
    if (!telaBarras || !telaBarras.isConnected) return;
    const cv = telaBarras, r = devicePixelRatio > 1 ? 1.5 : 1;
    const W = Math.round(cv.clientWidth * r), H = Math.round(cv.clientHeight * r);
    if (cv.width !== W || cv.height !== H) { cv.width = W; cv.height = H; }
    const g = cv.getContext('2d'), n = niveis.length, b = bandas(n / 2);
    g.clearRect(0, 0, W, H);
    const base = H * 0.86, larg = W / n, t = performance.now() / 1000;
    for (let k = 0; k < n; k++) {
      // espelhado: graves no meio, agudos nas pontas
      const m = Math.abs(k - (n - 1) / 2) / (n / 2), idx = Math.min(n / 2 - 1, Math.floor(m * n / 2));
      const parado = 0.035 + 0.02 * Math.sin(t * 1.3 + k * 0.5);
      const alvo = tocando ? Math.max(parado, b[idx] * (1.15 - m * 0.4)) : parado;
      niveis[k] += (alvo - niveis[k]) * (alvo > niveis[k] ? 0.55 : 0.12);
      const alt = niveis[k] * H * 0.55, x = k * larg + larg * 0.22, w = larg * 0.56;
      const gr = g.createLinearGradient(0, base - alt, 0, base);
      gr.addColorStop(0, 'rgba(255,176,32,.95)'); gr.addColorStop(0.5, 'rgba(255,90,0,.75)'); gr.addColorStop(1, 'rgba(255,90,0,0)');
      g.fillStyle = gr;
      g.fillRect(x, base - alt, w, alt);
      g.fillStyle = 'rgba(255,90,0,.10)'; // reflexo no chão
      g.fillRect(x, base + 2, w, alt * 0.25);
    }
    if (tocando) { // brilho no grave
      const grave = (b[0] + b[1] + b[2]) / 3;
      const rg = g.createRadialGradient(W / 2, base, 0, W / 2, base, W * 0.45);
      rg.addColorStop(0, `rgba(255,120,20,${0.10 + grave * 0.22})`); rg.addColorStop(1, 'rgba(255,90,0,0)');
      g.fillStyle = rg; g.fillRect(0, 0, W, H);
    }
  }
  verPagina();
  requestAnimationFrame(quadro);

  // ---------- sons da interface (sintetizados, sem arquivo) ----------
  let ultimoTick = 0, ultimoAlvo = null;
  function bip(freq, dur, volume, tipo) {
    if (!est.sfx || !ctx || ctx.state !== 'running') return;
    const o = ctx.createOscillator(), g = ctx.createGain(), t = ctx.currentTime;
    o.type = tipo; o.frequency.setValueAtTime(freq, t); o.frequency.exponentialRampToValueAtTime(freq * 0.6, t + dur);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(volume, t + 0.005); g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g); g.connect(sfxGanho); o.start(t); o.stop(t + dur + 0.02);
  }
  const ALVOS = 'a, button, .botao, .painel, .kit, .vip, summary, [role="button"], input[type="range"], .radio-lista li';
  document.addEventListener('pointerover', (e) => {
    if (e.pointerType && e.pointerType !== 'mouse') return;
    const a = e.target.closest && e.target.closest(ALVOS);
    if (!a || a === ultimoAlvo) return;
    ultimoAlvo = a;
    const agora = performance.now();
    if (agora - ultimoTick < 60) return;
    ultimoTick = agora;
    bip(1900, 0.03, 0.025, 'sine');
  });
  document.addEventListener('pointerout', (e) => { if (ultimoAlvo && !ultimoAlvo.contains(e.relatedTarget)) ultimoAlvo = null; });
  document.addEventListener('click', (e) => {
    if (e.target.closest && e.target.closest(ALVOS)) { bip(620, 0.06, 0.06, 'triangle'); setTimeout(() => bip(940, 0.04, 0.035, 'sine'), 18); }
  }, true);
})();
