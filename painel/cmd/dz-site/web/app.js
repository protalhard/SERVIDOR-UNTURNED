// Site do DAY Z BRASIL OFICIAL: páginas montadas no navegador com os dados do /api.
(() => {
  const app = document.getElementById('app');
  const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const num = n => Number(n || 0).toLocaleString('pt-BR');
  const tempo = s => { s = Number(s || 0); const h = Math.floor(s / 3600), m = Math.floor(s % 3600 / 60); return h ? `${h}h` : `${m}min`; };
  const espera = s => s >= 3600 ? `${Math.round(s / 3600)}h` : `${Math.round(s / 60)}min`;
  const api = async u => { const r = await fetch(u, { credentials: 'same-origin' }); if (!r.ok) throw new Error(r.status); return r.json(); };
  let eu = null;

  const cabeca = (selo, titulo, texto) => `<div class="cabeca"><span class="selo">${selo}</span><h1>${titulo}</h1><p>${texto}</p></div>`;
  const nomeJog = f => `<span class="tag" style="color:${esc(f.corPatente || f.cor)}">[${esc(f.patente)}]</span> ${esc(f.nome)} <span class="fraco">Lv.${f.nivel}</span>`;

  // ---------- páginas ----------
  const paginas = {
    async '' () {
      const [st, rk] = await Promise.all([api('/api/status'), api('/api/ranking')]);
      const s = st.status, top = (rk.jogadores || []).slice(0, 3);
      return `
      <section class="heroi">
        <div>
          <span class="selo">${s.online ? '● Online agora' : 'Servidor brasileiro'}</span>
          <img class="logo" src="/img/logo.png" alt="DAY Z BRASIL OFICIAL" style="margin-top:18px">
          <p class="lead">Sobrevivência <b style="color:var(--c)">PvP estilo DayZ</b> no Unturned. Dinheiro físico, ATM, lojas, mercado negro, caça, airdrops e kits que você libera matando.</p>
          <div class="acoes">
            <a class="botao" href="steam://connect/${esc(st.ip)}">▶ Jogar agora</a>
            <a class="botao fantasma" href="/servidores" data-link>Ver servidor</a>
          </div>
          <div class="chips">${['Safe Zone', 'ATM', 'Black Market', 'Caça', 'Airdrops', 'Rank Kits', 'Vault', 'TPA'].map(c => `<span class="chip">${c}</span>`).join('')}</div>
        </div>
        <div class="capa"><img src="/img/capa.jpg" alt=""></div>
      </section>

      <section class="grade g4">
        <div class="painel"><div class="rotulo">Status</div><div class="numero ${s.online ? 'laranja' : ''}">${s.online ? 'ONLINE' : 'OFFLINE'}</div></div>
        <div class="painel"><div class="rotulo">Jogadores</div><div class="numero">${st.jogadores}/${s.max || '–'}</div></div>
        <div class="painel"><div class="rotulo">Mapa</div><div class="numero" style="font-size:26px">${esc(s.mapa || '–')}</div></div>
        <div class="painel"><div class="rotulo">Sobreviventes</div><div class="numero">${num(rk.total)}</div></div>
      </section>

      <section class="secao"><h2>O que tem no <span>servidor</span></h2>
        <div class="grade g3">
          ${[
            ['🛡', 'Safe Zone', 'Sem PvP, sem zumbi, carros e bases protegidos. Volte com /safe.'],
            ['🏧', 'ATM e dinheiro físico', 'As notas do jogo caem quando você morre. Guarde no ATM.'],
            ['🏴', 'Mercado Negro', 'Armas militares e equipamento raro, mais caro e fora da safe.'],
            ['🦌', 'Caça', 'Venda carne, couro e peixe pro caçador e faça dinheiro.'],
            ['📦', 'Airdrops', 'Caixas caindo pelo mapa. Quem chegar primeiro leva.'],
            ['🎖', 'Rank Kits', 'Abates liberam kits melhores e bônus de XP.'],
            ['🧰', 'Vault', 'Cofre pessoal pra guardar o que importa.'],
            ['⚔', 'Patentes e XP', 'Suba de Recruta até Lenda. Troque XP por equipamento.'],
            ['📍', 'Home e TPA', 'Volte pra sua cama ou vá até um amigo.'],
          ].map(([i, t, d]) => `<div class="painel recurso"><div class="ic">${i}</div><h3>${t}</h3><p>${d}</p></div>`).join('')}
        </div>
      </section>

      <section class="secao"><h2>Top <span>sobreviventes</span></h2>
        ${top.length ? `<div class="podio">${top.map((f, i) => `
          <a class="painel ${i === 0 ? 'p1' : ''}" href="/jogador/${f.id}" data-link><span class="pos">#${i + 1}</span>
            <div class="nome-jog">${nomeJog(f)}</div><div class="numero laranja" style="margin-top:8px">${num(f.abates)}</div><div class="rotulo">abates</div></a>`).join('')}</div>
          <a class="botao fantasma" href="/ranking" data-link>Ranking completo</a>`
          : `<p class="fraco">O ranking começa quando os primeiros jogadores entrarem.</p>`}
      </section>`;
    },

    async servidores () {
      const st = await api('/api/status'), s = st.status;
      const pct = s.max ? Math.round(100 * st.jogadores / s.max) : 0;
      return cabeca('Status em tempo real', 'Servidores', 'Status e jogadores online, atualizados pelo plugin do servidor.') + `
      <div class="painel destaque servidor">
        <div class="foto"></div>
        <div class="info">
          <div style="display:flex;justify-content:space-between;align-items:center;gap:10px;flex-wrap:wrap">
            <span class="estado ${s.online ? 'on' : 'off'}">${s.online ? 'ONLINE' : 'OFFLINE'}</span>
            <span class="fraco mono" style="font-size:12px">atualiza a cada 15 s</span>
          </div>
          <h2 style="font:800 28px var(--sans)">DAY Z BRASIL <span style="color:var(--c)">OFICIAL</span></h2>
          <div class="fraco mono">Mapa: <b style="color:var(--titulo)">${esc(s.mapa || '–')}</b></div>
          <div class="chips" style="margin:0">${['Loot x10', 'Safe Zone', 'ATM', 'Kits', 'Vault', 'TPA', 'Home', 'Airdrop'].map(c => `<span class="chip">${c}</span>`).join('')}</div>
          <div><div style="display:flex;justify-content:space-between" class="mono"><span class="rotulo">Jogadores</span><b style="color:var(--titulo)">${st.jogadores}/${s.max || '–'}</b></div>
            <div class="barra"><i style="width:${pct}%"></i></div></div>
          <div class="ipbox"><span>${esc(st.ip)}</span><button class="botao pequeno fantasma" onclick="navigator.clipboard.writeText('${esc(st.ip)}');this.textContent='Copiado!'">Copiar</button>
            <a class="botao pequeno" href="steam://connect/${esc(st.ip)}">Conectar</a></div>
          ${s.jogadores && s.jogadores.length ? `<div><div class="rotulo" style="margin-bottom:8px">Online agora</div><div class="lista-on">${s.jogadores.map(j =>
            `<a href="/jogador/${j.id}" data-link><span style="color:${esc(j.cor)}">[${esc(j.patente)}]</span> ${esc(j.nome)}</a>`).join('')}</div></div>` : ''}
        </div>
      </div>
      <p class="fraco" style="margin-top:16px;text-align:center">Como conectar: Unturned → Jogar → Servidores → aba Internet → procure <b>DAY Z BRASIL</b>. Ou Conectar pelo IP acima.</p>`;
    },

    async ranking () {
      let por = 'abates', busca = '';
      const desenhar = async () => {
        const rk = await api(`/api/ranking?por=${por}&busca=${encodeURIComponent(busca)}`);
        const l = rk.jogadores || [];
        document.getElementById('tabela').innerHTML = l.length ? `<div class="tabela-envolve"><table>
          <thead><tr><th>#</th><th>Jogador</th><th>Kills</th><th>HS</th><th>K/D</th><th>Zumbis</th><th>Mega</th><th>Tempo</th><th>Mortes</th></tr></thead>
          <tbody>${l.map((f, i) => `<tr><td class="ord">${i + 1}</td><td><a class="nome-jog" href="/jogador/${f.id}" data-link>${nomeJog(f)}<small>${f.id}</small></a></td>
            <td>${num(f.abates)}</td><td>${num(f.cabeca)}</td><td>${f.kd.toFixed(2)}</td><td>${num(f.zumbis)}</td><td>${num(f.megas)}</td><td>${tempo(f.segundos)}</td><td>${num(f.mortes)}</td></tr>`).join('')}</tbody></table></div>`
          : `<p class="fraco carregando">Nenhum jogador encontrado.</p>`;
      };
      setTimeout(() => {
        document.querySelectorAll('.abas button').forEach(b => b.onclick = () => {
          document.querySelectorAll('.abas button').forEach(x => x.classList.remove('ativo'));
          b.classList.add('ativo'); por = b.dataset.por; desenhar();
        });
        let t; document.getElementById('busca').oninput = e => { clearTimeout(t); t = setTimeout(() => { busca = e.target.value; desenhar(); }, 250); };
        desenhar();
      });
      return cabeca('Leaderboard ao vivo', 'Ranking', 'Os melhores sobreviventes do DAY Z BRASIL. Busque por nome ou SteamID.') + `
        <div class="ferramentas"><input id="busca" placeholder="Buscar por nome ou SteamID..." autocomplete="off">
          <div class="abas">${[['abates', 'Kills'], ['kd', 'K/D'], ['xp', 'XP'], ['zumbis', 'Zumbis'], ['tempo', 'Tempo']].map(([k, t], i) =>
            `<button data-por="${k}" class="${i ? '' : 'ativo'}">${t}</button>`).join('')}</div></div>
        <div id="tabela"><p class="carregando">Carregando...</p></div>`;
    },

    async kits () {
      const d = await api('/api/kits');
      const rank = d.kits.filter(k => k.abates > 0), livres = d.kits.filter(k => !k.abates && !k.permissao);
      const meus = eu && eu.ficha ? eu.ficha.abates : null;
      const cartao = k => `<div class="painel kit ${meus !== null && meus >= k.abates ? 'destaque' : ''}">
          <div class="topo-kit"><span class="req">${k.abates ? `☠ ${k.abates} kills` : 'Livre'}</span><span class="cmd">/kit ${esc(k.nome)}</span></div>
          <h3>${esc(k.titulo || k.nome)}</h3><p class="fraco">${esc(k.descricao)}</p>
          ${meus !== null && k.abates ? `<div class="progresso">${Math.min(meus, k.abates)}/${k.abates} abates</div><div class="barra"><i style="width:${Math.min(100, 100 * meus / k.abates)}%"></i></div>` : ''}
          <div class="dados"><div><span class="rotulo">Espera</span><b>${espera(k.espera)}</b></div><div><span class="rotulo">Bônus</span><b style="color:var(--c2)">${k.bonusXp ? '+' + num(k.bonusXp) + ' XP' : '–'}</b></div></div>
        </div>`;
      return cabeca('Kits de progressão', 'Rank Kits', 'Jogue, acumule abates e libere kits melhores automaticamente, sem pagar nada. O bônus de XP cai na hora do desbloqueio.') +
        `<div class="grade g4">${rank.map(cartao).join('')}</div>
         <section class="secao"><h2>Kits <span>livres</span></h2><div class="grade g4">${livres.map(cartao).join('')}</div></section>
         <section class="secao"><h2>Loja de <span>XP</span></h2><p class="fraco" style="margin:-8px 0 16px">Troque XP por equipamento com <b class="mono">/lojaxp</b>, dentro da safe zone.</p>
         <div class="grade g3">${(d.lojaXp || []).map(p => `<div class="painel"><h3 style="font:800 20px var(--sans);text-transform:uppercase">${esc(p.nome)}</h3>
           <p class="fraco" style="margin:6px 0 12px">${esc(p.descricao)}</p><div class="numero laranja" style="font-size:24px">${num(p.preco)} XP</div></div>`).join('')}</div></section>
         <section class="secao"><h2><span>Patentes</span></h2><div class="grade g3">${(d.patentes || []).map(p =>
           `<div class="painel"><div class="tag" style="color:${esc(p.cor)};font-size:18px">[${esc(p.nome)}]</div><div class="fraco mono">a partir de ${num(p.xpMinimo)} XP</div></div>`).join('')}</div></section>`;
    },

    async loja () {
      const vips = [
        ['Vault VIP', 'Cofre 7x7 em vez de 5x5.', ['Mais espaço pra guardar', 'Tag no chat']],
        ['Vault Grande', 'Cofre 11x9.', ['Espaço de base no bolso', 'Tag no chat']],
        ['Vault Ultra', 'Cofre 15x13.', ['O maior cofre do servidor', 'Tag no chat']],
        ['Kits VIP', 'Kits extras com espera menor.', ['Kits exclusivos', 'Cargo VIP no Discord']],
      ];
      return cabeca('Loja VIP', 'Apoie o servidor', 'Os pacotes VIP vão abrir em breve, com pagamento por PIX. Todo apoio mantém o servidor online e sem lag.') +
        `<div class="grade g4">${vips.map(([t, d, l]) => `<div class="painel vip"><span class="em-breve">EM BREVE</span><h3 style="margin-top:8px">${t}</h3>
          <p class="fraco">${d}</p><ul>${l.map(x => `<li>${x}</li>`).join('')}</ul></div>`).join('')}</div>
         <p class="fraco" style="text-align:center;margin-top:24px">Quer ser avisado quando abrir? Entre no nosso Discord.</p>`;
    },

    async sobre () {
      const cmds = [
        ['/menu', 'Mostra tudo que o servidor tem'], ['/kits · /kit nome', 'Seus kits e o que falta pra cada um'],
        ['/safe', 'Volta pra safe zone (15 s parado)'], ['/home', 'Vai pra sua cama (10 s parado)'],
        ['/tpa nome · /tpaceitar', 'Teleporte até um amigo'], ['/saldo · /depositar · /sacar', 'Dinheiro no ATM'],
        ['/lojas · /loja · /comprar · /vender', 'Lojas de armas, comida, geral, caçador e mercado negro'],
        ['/perfil · /top', 'Suas estatísticas e o ranking'], ['/vault', 'Seu cofre pessoal'], ['/lojaxp', 'Troque XP por equipamento'],
      ];
      return cabeca('Sobre o servidor', 'Day Z Brasil', 'Um servidor brasileiro de Unturned feito pra quem gosta de sobrevivência de verdade: lootear, negociar, caçar e lutar.') + `
        <div class="grade g2">
          <div class="painel"><h3 style="margin-bottom:12px">Como funciona</h3>
            <p>Você nasce com um kit simples. Fora da safe zone o PvP é liberado. O dinheiro são as notas do próprio jogo: quem morre perde o que está no bolso, então guarde no ATM.</p>
            <p style="margin-top:10px">Cada abate dá XP e conta pros Rank Kits. Suba de patente, troque XP por equipamento e entre no ranking.</p>
            <p style="margin-top:10px" class="fraco">A cada 30 minutos os itens do chão são limpos, com aviso antes. O que está em caixas não some.</p></div>
          <div class="painel"><h3 style="margin-bottom:8px">Comandos</h3>${cmds.map(([c, d]) =>
            `<div class="regra"><span class="t mono" style="color:var(--c2)">${c}</span><span class="fraco" style="text-align:right">${d}</span></div>`).join('')}</div>
        </div>`;
    },

    async regras () {
      const leg = { perm: 'BAN PERMANENTE', ban: 'BAN', mute: 'MUTE / BAN', aviso: 'AVISO', var: 'PUNIÇÃO VARIADA' };
      const jogo = [['Hack, cheat, macro ou qualquer programa que dê vantagem', 'perm'], ['Abusar de bug ou exploit (reporte em ticket)', 'ban'],
        ['Construir dentro da safe zone ou bloquear lojas e ATMs', 'aviso'], ['Conta secundária pra fugir de punição', 'perm'],
        ['Racismo, ameaça real ou ofensa pesada no chat', 'mute'], ['Entrar num grupo só pra roubar ou destruir a base por dentro', 'var']];
      const disc = [['Discriminação, assédio ou exposição de dados', 'perm'], ['Divulgar outros servidores, cheats ou links suspeitos', 'ban'],
        ['Flood, spam ou correntes', 'mute'], ['Brigar com a staff no chat em vez de abrir ticket', 'aviso'], ['Conteúdo adulto ou violência real', 'perm']];
      const bloco = (t, l) => `<div class="painel" style="padding:8px 4px"><h3 style="padding:12px 16px">${t}</h3>${l.map(([r, p], i) =>
        `<div class="regra"><span class="n">${i + 1}.</span><span class="t">${r}</span><span class="pun ${p}">${leg[p]}</span></div>`).join('')}</div>`;
      return cabeca('Regras oficiais', 'Regras', 'Ao entrar no servidor ou no Discord você concorda com as regras abaixo. Fora da safe zone, PvP, raid e emboscada fazem parte do jogo.') +
        `<div class="grade g2">${bloco('🎮 Jogo', jogo)}${bloco('💬 Discord', disc)}</div>`;
    },

    async perfil () {
      if (!eu || !eu.logado) {
        return cabeca('Seu perfil', 'Perfil', 'Entre com a sua conta Steam pra ver suas estatísticas, patente e o progresso dos Rank Kits.') +
          `<div style="text-align:center"><a class="botao" href="/entrar">Entrar com Steam</a>
           <p class="fraco" style="margin-top:14px;font-size:13px">O login é feito no site oficial da Steam. Não vemos sua senha.</p></div>`;
      }
      if (!eu.ficha) return cabeca('Seu perfil', 'Perfil', 'Você ainda não jogou no servidor. Entre uma vez e suas estatísticas aparecem aqui.') +
        `<div style="text-align:center"><a class="botao" href="/servidores" data-link>Ir pro servidor</a> <a class="botao fantasma" href="/sair">Sair</a></div>`;
      return desenharPerfil(eu.ficha, eu.kits, true);
    },

    async jogador (id) {
      const [f, k] = await Promise.all([api('/api/jogador/' + encodeURIComponent(id)).catch(() => null), api('/api/kits')]);
      if (!f) return cabeca('Jogador', 'Não encontrado', 'Esse jogador ainda não tem estatísticas.');
      return desenharPerfil(f, k.kits, false);
    },
  };

  function desenharPerfil(f, kits, meu) {
    const prox = (kits || []).filter(k => k.abates > f.abates).sort((a, b) => a.abates - b.abates)[0];
    const quadro = (t, v, cor) => `<div class="painel"><div class="rotulo">${t}</div><div class="numero" ${cor ? `style="color:${cor}"` : ''}>${v}</div></div>`;
    return `<div class="painel destaque perfil-topo" style="margin-top:20px">
        <div class="avatar">${esc((f.nome || '?').trim().charAt(0).toUpperCase())}</div>
        <div style="flex:1"><div class="tag" style="color:${esc(f.corPatente)}">[${esc(f.patente)}]</div><h2>${esc(f.nome)}</h2>
          <div class="fraco mono" style="font-size:13px">${f.id} · Nível ${f.nivel} · #${f.posicao} no ranking · <a href="https://steamcommunity.com/profiles/${f.id}" target="_blank" rel="noopener">Ver na Steam</a></div></div>
        ${meu ? '<a class="botao pequeno fantasma" href="/sair">Sair</a>' : ''}
      </div>
      <div class="grade g4" style="margin-top:18px">
        ${quadro('Abates', num(f.abates), 'var(--c)')}${quadro('Mortes', num(f.mortes))}${quadro('K/D', f.kd.toFixed(2))}${quadro('HS%', f.pctCabeca + '%', 'var(--perigo)')}
        ${quadro('Zumbis', num(f.zumbis))}${quadro('Mega zumbis', num(f.megas))}${quadro('XP', num(f.xp), 'var(--c2)')}${quadro('Tempo de jogo', tempo(f.segundos))}
      </div>
      <div class="painel" style="margin-top:18px"><div class="rotulo">Progresso de rank</div>
        ${prox ? `<p style="margin:8px 0">Próximo kit: <b style="color:var(--c)">${esc(prox.titulo || prox.nome)}</b> <span class="mono fraco">/kit ${esc(prox.nome)}</span></p>
          <div class="barra"><i style="width:${Math.min(100, 100 * f.abates / prox.abates)}%"></i></div>
          <div class="mono fraco" style="margin-top:6px;font-size:12px">${f.abates}/${prox.abates} abates${prox.bonusXp ? ` · bônus de ${num(prox.bonusXp)} XP` : ''}</div>`
          : `<p style="margin-top:8px">Todos os rank kits liberados. Lenda!</p>`}
      </div>`;
  }

  // ---------- roteador ----------
  async function abrir(caminho, empilhar) {
    const partes = caminho.replace(/^\/+|\/+$/g, '').split('/');
    let nome = partes[0], arg = partes[1];
    if (empilhar) history.pushState({}, '', caminho);
    document.querySelectorAll('.menu a').forEach(a => a.classList.toggle('ativo', a.getAttribute('href') === '/' + nome));
    document.getElementById('menu').classList.remove('aberto');
    const f = paginas[nome];
    if (!f) { app.innerHTML = cabeca('404', 'Página não encontrada', 'Volte pro início.'); return; }
    app.innerHTML = '<p class="carregando">Carregando...</p>';
    try { app.innerHTML = await f(arg); } catch (e) { app.innerHTML = cabeca('Erro', 'Ops', 'Não consegui carregar agora. Tente de novo em alguns segundos.'); }
    window.scrollTo(0, 0);
  }

  document.addEventListener('click', e => {
    const a = e.target.closest('a[data-link]');
    if (!a || e.ctrlKey || e.metaKey) return;
    e.preventDefault(); abrir(a.getAttribute('href'), true);
  });
  window.addEventListener('popstate', () => abrir(location.pathname, false));
  document.getElementById('hamburguer').onclick = () => document.getElementById('menu').classList.toggle('aberto');

  (async () => {
    try { eu = await api('/api/eu'); } catch (e) { eu = null; }
    const conta = document.getElementById('botao-conta');
    if (eu && eu.logado) { conta.textContent = eu.ficha ? eu.ficha.nome : 'Meu perfil'; conta.href = '/perfil'; conta.setAttribute('data-link', ''); }
    try {
      const st = await api('/api/status');
      if (st.discord) { const d = document.getElementById('link-discord'); d.href = st.discord; d.hidden = false; }
    } catch (e) {}
    abrir(location.pathname, false);
  })();
})();
