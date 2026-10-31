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

  const nomeMapa = m => String(m || '').replace(/([a-z])(\d)/i, '$1 $2').toUpperCase();
  const slides = fotos => `<div class="slides">${fotos.map((f, i) => `<div class="slide${i ? '' : ' ativo'}" style="background-image:url(/img/mapa/${f}.webp)"></div>`).join('')}</div>`;

  function cartaoServidor(st, aberto) {
    const s = st.status, pct = s.max ? Math.round(100 * st.jogadores / s.max) : 0;
    return `<div class="painel destaque cartao-srv">
      <div class="srv-foto">${slides(['farol', 'ponte', 'palmeiras', 'colinas'])}
        <span class="estado ${s.online ? 'on' : 'off'}">${s.online ? 'ONLINE' : 'OFFLINE'}</span>
        <div class="srv-base"><h3>${esc(nomeMapa(s.mapa) || 'CALIFORNIA 2')}</h3>
          <div class="ipbox"><span>${esc(st.ip)}</span><button class="botao pequeno fantasma" onclick="navigator.clipboard.writeText('${esc(st.ip)}');this.textContent='Copiado!'">Copiar</button>
            <a class="botao pequeno" href="steam://connect/${esc(st.ip)}">⚡ Conectar</a></div></div>
      </div>
      <div class="srv-corpo">
        <div class="chips" style="margin:0">${['Loot x10', 'Safe Zone', 'ATM', 'Kits', 'Vault', 'TPA', 'Home', 'Airdrop', 'Mercado negro'].map(c => `<span class="chip">${c}</span>`).join('')}</div>
        <div class="painel jogadores"><div style="display:flex;justify-content:space-between;align-items:center"><span class="rotulo">👥 Jogadores</span><span class="fraco mono" style="font-size:11px">ATUALIZA A CADA 15 S</span></div>
          <div class="numero">${st.jogadores}<small>/${s.max || '–'}</small></div><div class="barra"><i style="width:${pct}%"></i></div></div>
      </div>
      <details class="detalhes-srv"${aberto ? ' open' : ''}><summary>Ver jogadores online (${st.jogadores})</summary>
        ${s.jogadores && s.jogadores.length ? `<div class="grade-on">${s.jogadores.map(j =>
          `<a href="/jogador/${j.id}" data-link><i></i><span style="color:${esc(j.cor)}">[${esc(j.patente)}]</span> ${esc(j.nome)}${j.nivel ? ` <small>Lv.${j.nivel}</small>` : ''}</a>`).join('')}</div>`
          : '<p class="fraco">Ninguém online agora. Seja o primeiro!</p>'}
      </details>
    </div>`;
  }

  // ---------- páginas ----------
  const paginas = {
    async '' () {
      const [st, rk] = await Promise.all([api('/api/status'), api('/api/ranking')]);
      const s = st.status, top = (rk.jogadores || []).slice(0, 3);
      const ticker = ['Kits de rank grátis', 'Safe zone protegida', 'ATM e dinheiro físico', 'Mercado negro', 'Airdrops pelo mapa', 'Caça e pesca', 'Vault pessoal', 'Ranking ao vivo', 'Mapa California 2'];
      const recursos = [
        ['mercado', '🛡', 'Safe Zone', 'Lojas, ATM e nada de PvP. Carros e bases protegidos. Volte com /safe.'],
        ['lanchonete', '🏧', 'ATM e dinheiro físico', 'As notas do jogo caem quando você morre. Guarde no banco.'],
        ['bunker', '🏴', 'Mercado Negro', 'Armas militares e equipamento raro, caro e fora da safe.'],
        ['lago', '🦌', 'Caça e pesca', 'Venda carne, couro e peixe pro caçador e faça dinheiro.'],
        ['colinas', '📦', 'Airdrops', 'Caixas caindo pelo mapa. Quem chegar primeiro leva.'],
        ['deserto', '🎖', 'Rank Kits', 'Abates liberam kits melhores e bônus de XP. Sem pagar nada.'],
        ['torre-noite', '⚔', 'Patentes e XP', 'Suba de Recruta até Lenda e troque XP por equipamento.'],
        ['vila', '📍', 'Home e TPA', 'Volte pra sua cama ou vá até um amigo.'],
        ['neblina', '🧰', 'Vault', 'Cofre pessoal pra guardar o que importa.'],
      ];
      return `
      <section class="heroi-full">
        ${slides(['ponte-por-do-sol', 'farol', 'floresta', 'ponte', 'palmeiras'])}
        <div class="heroi-video" id="heroi-video"><div id="yt-heroi"></div></div>
        <div class="heroi-veu"></div>
        <div class="envolve heroi-conteudo">
          <span class="pilula">${s.online ? `<i class="ponto"></i> ONLINE <b>|</b> ${st.jogadores}/${s.max || 50} jogando agora` : 'Servidor brasileiro de Unturned'}</span>
          <h1 class="titulo-heroi"><span>DAY Z</span><em>BRASIL</em></h1>
          <div class="slogan">Sobrevivência · PvP · ${esc(nomeMapa(s.mapa) || 'California 2')}</div>
          <p class="lead">Sobrevivência <b>PvP estilo DayZ</b> no Unturned. Dinheiro físico, ATM, lojas, mercado negro, caça, airdrops e kits que você libera matando.</p>
          <div class="acoes">
            <a class="botao" href="steam://connect/${esc(st.ip)}">▷ Jogar agora</a>
            <a class="botao fantasma" href="/kits" data-link>Rank Kits</a>
            <a class="botao fantasma" href="/sobre" data-link>&gt;_ Comandos</a>
          </div>
        </div>
        <div class="letreiro" aria-hidden="true"><div class="faixa">${[...ticker, ...ticker].map(t => `<span>${t}</span><i>◆</i>`).join('')}</div></div>
      </section>

      <section class="secao"><h2>Nosso <span>servidor</span></h2>${cartaoServidor(st)}</section>

      <section class="grade g4 numeros">
        <div class="painel"><div class="rotulo">Status</div><div class="numero ${s.online ? 'laranja' : ''}">${s.online ? 'ONLINE' : 'OFFLINE'}</div></div>
        <div class="painel"><div class="rotulo">Jogadores</div><div class="numero">${st.jogadores}/${s.max || '–'}</div></div>
        <div class="painel"><div class="rotulo">Mapa</div><div class="numero" style="font-size:26px">${esc(nomeMapa(s.mapa) || '–')}</div></div>
        <div class="painel"><div class="rotulo">Sobreviventes</div><div class="numero" data-conta="${Number(rk.total) || 0}">0</div></div>
      </section>

      <section class="secao"><h2>O que tem no <span>servidor</span></h2>
        <div class="grade g3">${recursos.map(([f, i, t, d]) => `<div class="painel recurso-foto" style="--foto:url(/img/mapa/${f}.webp)">
          <div class="ic">${i}</div><h3>${t}</h3><p>${d}</p></div>`).join('')}</div>
      </section>

      <section class="secao"><h2>O mapa: <span>California 2</span></h2>
        <div class="galeria">${[['ponte-por-do-sol', 'A ponte ao pôr do sol'], ['farol', 'O farol da costa'], ['mercado', 'Lojas na cidade'], ['palmeiras', 'Praias e palmeiras'], ['ponte', 'San Francisco'], ['floresta', 'Florestas do norte']]
          .map(([f, t]) => `<figure><img src="/img/mapa/${f}.webp" alt="${t}" loading="lazy"><figcaption>${t}</figcaption></figure>`).join('')}</div>
        <p class="credito">Imagens do mapa California 2, feito pelo California Team (Steam Workshop). Vídeo da capa: California 2 Environmental Teaser, Smartly Dressed Games.</p>
      </section>

      <section class="secao"><h2>Top <span>sobreviventes</span></h2>
        ${top.length ? `<div class="podio">${top.map((f, i) => `
          <a class="painel ${i === 0 ? 'p1' : ''}" href="/jogador/${f.id}" data-link><span class="pos">#${i + 1}</span>
            <div class="nome-jog">${nomeJog(f)}</div><div class="numero laranja" style="margin-top:8px">${num(f.abates)}</div><div class="rotulo">abates</div></a>`).join('')}</div>
          <a class="botao fantasma" href="/ranking" data-link>Ranking completo</a>`
          : `<p class="fraco">O ranking começa quando os primeiros jogadores entrarem.</p>`}
      </section>

      <section class="secao"><h2>Como <span>começar</span></h2>
        <div class="passos">
          <div class="passo painel"><h3>Abra o Unturned</h3><p>Na Steam, abra o Unturned e vá em Jogar → Servidores → aba Internet.</p></div>
          <div class="passo painel"><h3>Ache o DAY Z BRASIL</h3><p>Procure por DAY Z BRASIL ou use o IP <b class="mono">${esc(st.ip)}</b> no Conectar.</p></div>
          <div class="passo painel"><h3>Sobreviva</h3><p>Pegue o <b class="mono">/kit inicial</b>, leia o <b class="mono">/tutorial</b> e saia da safe zone pra lootear.</p></div>
        </div>
      </section>

      <section class="secao"><h2>Perguntas <span>frequentes</span></h2>
        <div class="perguntas">
          <details><summary>Precisa pagar pra jogar?</summary><p>Não. O servidor é grátis. Os kits de rank são liberados por abates, sem pagar nada.</p></details>
          <details><summary>O que acontece com meu dinheiro quando eu morro?</summary><p>As notas no inventário caem com você. Guarde no ATM com /depositar e saque com /sacar quando precisar.</p></details>
          <details><summary>Como funciona a safe zone?</summary><p>Dentro dela não tem PvP, não dá pra construir e carros e bases ficam protegidos. Use /safe pra voltar (fique parado alguns segundos).</p></details>
          <details><summary>Como ganho XP e subo de patente?</summary><p>Matando jogadores (mais na cabeça), zumbis e megas, e jogando. Veja com /perfil e gaste XP na /lojaxp.</p></details>
          <details><summary>Perdi minhas coisas por bug. E agora?</summary><p>Abra um ticket no nosso Discord, no canal de suporte, com print ou vídeo. A staff responde por lá.</p></details>
        </div>
        <div class="caixa-final painel com-foto" style="--foto:url(/img/mapa/ponte-por-do-sol.webp)">
          <div><h2>Bora <span class="laranja">sobreviver</span>?</h2><p>Entre no servidor e no nosso Discord pra achar grupo, ver o ranking e saber dos eventos.</p></div>
          <div class="acoes"><a class="botao" href="steam://connect/${esc(st.ip)}">⚡ Jogar agora</a>${st.discord ? `<a class="botao fantasma" href="${esc(st.discord)}" target="_blank" rel="noopener">Discord</a>` : ''}</div>
        </div>
      </section>`;
    },

    async servidores () {
      const st = await api('/api/status');
      return cabeca('Status em tempo real', 'Servidor <em>ao vivo</em>', 'Status e jogadores online, atualizados pelo plugin do servidor.') + cartaoServidor(st, true) +
        `<p class="fraco" style="margin-top:16px;text-align:center">Como conectar: Unturned → Jogar → Servidores → aba Internet → procure <b>DAY Z BRASIL</b>. Ou Conectar pelo IP acima.</p>`;
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
      return cabeca('Leaderboard ao vivo', 'Os melhores <em>sobreviventes</em>', 'Os melhores sobreviventes do DAY Z BRASIL. Busque por nome ou SteamID.') + `
        <div class="ferramentas"><input id="busca" placeholder="Buscar por nome ou SteamID..." autocomplete="off">
          <div class="abas">${[['abates', 'Kills'], ['kd', 'K/D'], ['xp', 'XP'], ['zumbis', 'Zumbis'], ['tempo', 'Tempo']].map(([k, t], i) =>
            `<button data-por="${k}" class="${i ? '' : 'ativo'}">${t}</button>`).join('')}</div></div>
        <div id="tabela"><p class="carregando">Carregando...</p></div>`;
    },

    async kits () {
      const d = await api('/api/kits');
      const rank = d.kits.filter(k => k.abates > 0), livres = d.kits.filter(k => !k.abates && !k.permissao);
      const meus = eu && eu.ficha ? eu.ficha.abates : null;
      const fotosKit = ['deserto', 'lago', 'vila', 'mercado', 'colinas', 'bunker', 'torre-noite', 'farol', 'ponte', 'floresta', 'palmeiras', 'lanchonete'];
      let nFoto = 0;
      const cartao = k => `<div class="painel kit ${meus !== null && meus >= k.abates ? 'destaque' : ''}">
          <div class="kit-foto" style="background-image:url(/img/mapa/${fotosKit[nFoto++ % fotosKit.length]}.webp)"><span class="kit-nome">${esc((k.titulo || k.nome).toUpperCase())}</span></div>
          <div class="topo-kit"><span class="req">${k.abates ? `☠ ${k.abates} kills` : 'Livre'}</span><span class="cmd">/kit ${esc(k.nome)}</span></div>
          <h3>${esc(k.titulo || k.nome)}</h3><p class="fraco">${esc(k.descricao)}</p>
          ${meus !== null && k.abates ? `<div class="progresso">${Math.min(meus, k.abates)}/${k.abates} abates</div><div class="barra"><i style="width:${Math.min(100, 100 * meus / k.abates)}%"></i></div>` : ''}
          <div class="dados"><div><span class="rotulo">Espera</span><b>${espera(k.espera)}</b></div><div><span class="rotulo">Bônus</span><b style="color:var(--c2)">${k.bonusXp ? '+' + num(k.bonusXp) + ' XP' : '–'}</b></div></div>
        </div>`;
      return cabeca('Kits de progressão', 'Mate e libere <em>kits melhores</em>', 'Jogue, acumule abates e libere kits melhores automaticamente, sem pagar nada. O bônus de XP cai na hora do desbloqueio.') +
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
        ['Vault VIP', 'Cofre 7x7 em vez de 5x5.', ['Mais espaço pra guardar', 'Tag no chat'], 'neblina'],
        ['Vault Grande', 'Cofre 11x9.', ['Espaço de base no bolso', 'Tag no chat'], 'bunker'],
        ['Vault Ultra', 'Cofre 15x13.', ['O maior cofre do servidor', 'Tag no chat'], 'torre-noite'],
        ['Kits VIP', 'Kits extras com espera menor.', ['Kits exclusivos', 'Cargo VIP no Discord'], 'ponte-por-do-sol'],
      ];
      return cabeca('Loja VIP', 'Apoie o <em>servidor</em>', 'Os pacotes VIP vão abrir em breve, com pagamento por PIX. Todo apoio mantém o servidor online e sem lag.') +
        `<div class="grade g4">${vips.map(([t, d, l, f]) => `<div class="painel vip"><div class="kit-foto" style="background-image:url(/img/mapa/${f}.webp)"></div><span class="em-breve">EM BREVE</span><h3 style="margin-top:8px">${t}</h3>
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
      return cabeca('Sobre o servidor', 'Sobrevivência <em>de verdade</em>', 'Um servidor brasileiro de Unturned feito pra quem gosta de sobrevivência de verdade: lootear, negociar, caçar e lutar.') + `
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
      return cabeca('Regras oficiais', 'Regras do <em>servidor</em>', 'Ao entrar no servidor ou no Discord você concorda com as regras abaixo. Fora da safe zone, PvP, raid e emboscada fazem parte do jogo.') +
        `<div class="grade g2">${bloco('🎮 Jogo', jogo)}${bloco('💬 Discord', disc)}</div>`;
    },

    async perfil () {
      if (!eu || !eu.logado) {
        return cabeca('Seu perfil', 'Seu <em>perfil</em>', 'Entre com a sua conta Steam pra ver suas estatísticas, patente e o progresso dos Rank Kits.') +
          `<div style="text-align:center"><a class="botao" href="/entrar">Entrar com Steam</a>
           <p class="fraco" style="margin-top:14px;font-size:13px">O login é feito no site oficial da Steam. Não vemos sua senha.</p></div>`;
      }
      if (!eu.ficha) return cabeca('Seu perfil', 'Seu <em>perfil</em>', 'Você ainda não jogou no servidor. Entre uma vez e suas estatísticas aparecem aqui.') +
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

  // ---------- efeitos (os mesmos do site do SnapLag) ----------
  const reduzir = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const olho = 'IntersectionObserver' in window ? new IntersectionObserver(l => l.forEach(x => {
    if (x.isIntersecting) { x.target.classList.add('visto'); olho.unobserve(x.target); }
  }), { rootMargin: '0px 0px -8% 0px' }) : null;

  let girando = [];
  function girarSlides() {
    girando.forEach(clearInterval); girando = [];
    if (reduzir) return;
    document.querySelectorAll('#app .slides').forEach((el, n) => {
      const l = el.children; if (l.length < 2) return;
      let i = 0;
      girando.push(setInterval(() => { l[i].classList.remove('ativo'); i = (i + 1) % l.length; l[i].classList.add('ativo'); }, 6500 + n * 900));
    });
  }

  // vídeo de fundo da capa: teaser oficial do California 2 no YouTube, sem som e em loop.
  // Até começar a tocar, ficam as fotos trocando por baixo.
  const VIDEO = 'z0D3TqPYQhA', INICIO = 3;
  function videoHeroi() {
    const caixa = document.getElementById('heroi-video');
    if (!caixa || reduzir || innerWidth < 700) return;
    const criar = () => new YT.Player('yt-heroi', {
      videoId: VIDEO, host: 'https://www.youtube-nocookie.com',
      playerVars: { autoplay: 1, mute: 1, controls: 0, disablekb: 1, fs: 0, modestbranding: 1, playsinline: 1, rel: 0, iv_load_policy: 3, start: INICIO },
      events: {
        onReady: e => { e.target.mute(); e.target.playVideo(); },
        onStateChange: e => {
          if (e.data === 1) caixa.classList.add('tocando');
          if (e.data === 0) { e.target.seekTo(INICIO); e.target.playVideo(); }
        },
      },
    });
    if (window.YT && YT.Player) return criar();
    window.onYouTubeIframeAPIReady = () => { if (document.getElementById('yt-heroi')) criar(); };
    if (!document.getElementById('yt-api')) {
      const sc = document.createElement('script'); sc.id = 'yt-api'; sc.src = 'https://www.youtube.com/iframe_api'; document.head.appendChild(sc);
    }
  }

  function efeitos() {
    girarSlides();
    videoHeroi();
    document.querySelectorAll('#app .botao:not(.fantasma)').forEach(b => b.classList.add('brilho-btn'));
    document.querySelectorAll('#app .secao .painel, #app .grade > .painel, #app .podio > .painel, #app .perguntas details').forEach((el, i) => {
      if (el.classList.contains('visto')) return;
      el.classList.add('revela');
      el.style.setProperty('--atraso', (i % 6) * 70 + 'ms');
      if (olho && !reduzir) olho.observe(el); else el.classList.add('visto');
    });
    document.querySelectorAll('#app [data-conta]').forEach(el => contar(el, Number(el.dataset.conta)));
  }

  function contar(el, ate) {
    if (reduzir || !ate) { el.textContent = num(ate); return; }
    const t0 = performance.now();
    const passo = t => { const k = Math.min(1, (t - t0) / 1200), e = 1 - Math.pow(1 - k, 3); el.textContent = num(Math.round(ate * e)); if (k < 1) requestAnimationFrame(passo); };
    requestAnimationFrame(passo);
  }

  // céu: estrelas piscando, estrelas cadentes e a luz que segue o mouse
  (function ceu() {
    const cv = document.getElementById('estrelas'), ctx = cv.getContext('2d');
    let w = 0, h = 0, estrelas = [], cadentes = [], proxima = 0, rodando = false;
    function medir() {
      const dpr = Math.min(window.devicePixelRatio || 1, 2);
      w = innerWidth; h = innerHeight; cv.width = w * dpr; cv.height = h * dpr; ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const n = Math.min(520, Math.round(w * h / 4200));
      estrelas = Array.from({ length: n }, () => ({ x: Math.random() * w, y: Math.random() * h, r: Math.random() * 1.1 + 0.25,
        a: Math.random() * 0.55 + 0.25, f: Math.random() * 1.6 + 0.4, p: Math.random() * 6.3, q: Math.random() < 0.14 }));
      if (reduzir) quadro(0);
    }
    function quadro(t) {
      ctx.clearRect(0, 0, w, h);
      for (const s of estrelas) {
        const a = s.a * (0.5 + 0.5 * Math.sin(t / 1000 * s.f + s.p));
        ctx.fillStyle = s.q ? 'rgba(255,160,90,' + a + ')' : 'rgba(255,255,255,' + a + ')';
        if (s.r > 1) { ctx.beginPath(); ctx.arc(s.x, s.y, s.r, 0, 6.283); ctx.fill(); } else ctx.fillRect(s.x, s.y, s.r * 1.6, s.r * 1.6);
      }
      if (!reduzir) {
        if (t > proxima) {
          const ang = 0.35 + Math.random() * 0.25, vel = 9 + Math.random() * 6;
          cadentes.push({ x: w * (0.3 + Math.random() * 0.75), y: Math.random() * h * 0.45, vx: -Math.cos(ang) * vel, vy: Math.sin(ang) * vel, vida: 0, fim: 46 + Math.random() * 30 });
          proxima = t + 2600 + Math.random() * 4800;
        }
        cadentes = cadentes.filter(c => c.vida < c.fim);
        for (const c of cadentes) {
          c.x += c.vx; c.y += c.vy; c.vida++;
          const k = Math.sin(Math.PI * c.vida / c.fim), cx = c.x - c.vx * 9, cy = c.y - c.vy * 9;
          const g = ctx.createLinearGradient(c.x, c.y, cx, cy);
          g.addColorStop(0, 'rgba(255,240,220,' + 0.95 * k + ')'); g.addColorStop(0.3, 'rgba(255,140,60,' + 0.55 * k + ')'); g.addColorStop(1, 'rgba(255,90,0,0)');
          ctx.strokeStyle = g; ctx.lineWidth = 1.8; ctx.lineCap = 'round';
          ctx.beginPath(); ctx.moveTo(c.x, c.y); ctx.lineTo(cx, cy); ctx.stroke();
          ctx.fillStyle = 'rgba(255,245,230,' + k + ')'; ctx.beginPath(); ctx.arc(c.x, c.y, 1.6, 0, 6.283); ctx.fill();
        }
      }
      if (rodando) requestAnimationFrame(quadro);
    }
    function ligar() { if (!rodando && !reduzir && !document.hidden) { rodando = true; requestAnimationFrame(quadro); } }
    document.addEventListener('visibilitychange', () => { if (document.hidden) rodando = false; else ligar(); });
    addEventListener('resize', () => { clearTimeout(medir.t); medir.t = setTimeout(medir, 150); });
    medir(); ligar();

    const luz = document.getElementById('luz-mouse');
    let alvo = null, px = 0, py = 0, pendente = false;
    addEventListener('pointermove', e => {
      if (e.pointerType !== 'mouse') return;
      px = e.clientX; py = e.clientY; alvo = e.target.closest ? e.target.closest('.painel') : null;
      if (!pendente) { pendente = true; requestAnimationFrame(mover); }
    }, { passive: true });
    function mover() {
      pendente = false;
      luz.style.transform = 'translate(' + (px - 300) + 'px,' + (py - 300) + 'px)';
      luz.classList.add('on');
      if (alvo) { const r = alvo.getBoundingClientRect(); alvo.style.setProperty('--mx', (px - r.left) + 'px'); alvo.style.setProperty('--my', (py - r.top) + 'px'); }
    }
    document.addEventListener('pointerleave', () => luz.classList.remove('on'));
  })();

  function marcarMenu() {
    const ativo = document.querySelector('#menu a.ativo'), barra = document.getElementById('menu-luz');
    if (!barra) return;
    if (ativo && ativo.offsetWidth) { barra.style.width = ativo.offsetWidth + 'px'; barra.style.transform = 'translateX(' + ativo.offsetLeft + 'px)'; barra.classList.add('on'); }
    else barra.classList.remove('on');
  }
  addEventListener('resize', marcarMenu);

  // ---------- roteador ----------
  async function abrir(caminho, empilhar) {
    const partes = caminho.replace(/^\/+|\/+$/g, '').split('/');
    let nome = partes[0], arg = partes[1];
    if (empilhar) history.pushState({}, '', caminho);
    document.querySelectorAll('.menu a').forEach(a => a.classList.toggle('ativo', a.getAttribute('href') === '/' + nome));
    marcarMenu();
    document.getElementById('menu').classList.remove('aberto');
    const f = paginas[nome];
    if (!f) { app.innerHTML = cabeca('404', 'Página <em>não encontrada</em>', 'Volte pro início.'); return; }
    app.innerHTML = '<p class="carregando">Carregando...</p>';
    try { app.innerHTML = await f(arg); } catch (e) { app.innerHTML = cabeca('Erro', '<em>Ops</em>', 'Não consegui carregar agora. Tente de novo em alguns segundos.'); }
    window.scrollTo(0, 0);
    document.body.classList.toggle('pagina-inicio', nome === '');
    efeitos();
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
