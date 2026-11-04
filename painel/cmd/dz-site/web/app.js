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

  // foto própria de cada recurso (img/recursos/rec-*.webp); sem ela, fica a do mapa por baixo
  const fotoRecurso = {"Safe Zone": "safe", "ATM e dinheiro físico": "atm", "Mercado Negro": "negro", "Caça e pesca": "caca", "Airdrops": "airdrop", "Rank Kits": "rankkits", "Patentes e XP": "patentes", "Home e TPA": "home", "Vault": "vault"};

  // ---------- páginas ----------
  // pacotes VIP do DAY Z BRASIL (nomes, kits e preços nossos)
  const P = [
    { id: 'iniciante', aba: 'kits', nome: 'Kit Iniciante', selo: 'ENTRADA', cor: '#c9cdd3', foto: 'vila', preco: 6.40, dias: 30,
      desc: 'Escolha 1 dos 3 kits de iniciante.', itens: ['Escolha 1: Andarilho, Batedor ou Saqueador', 'Espera de 30 min', 'Tag do kit no chat', 'Cargo VIP no Discord'],
      kits: ['andarilho', 'batedor', 'saqueador'] },
    { id: 'bandido', aba: 'kits', nome: 'Bandido', selo: 'NÍVEL 1', cor: '#ffb020', foto: 'mercado', preco: 19.40, dias: 30,
      desc: 'Libera +5 kits.', itens: ['Kits Bandido 1 e 2 (30 min)', 'Inclui os 3 kits de iniciante', 'Tag [BANDIDO] no chat', 'Cargo VIP no Discord'],
      kits: ['bandido1', 'bandido2'] },
    { id: 'mercenario', aba: 'kits', nome: 'Mercenário', selo: 'NÍVEL 2', cor: '#ff5a00', foto: 'bunker', preco: 30.90, dias: 30,
      desc: 'Libera +8 kits.', itens: ['Kits Mercenário 1 e 2 (30 min)', 'Kit Base (12 h)', 'Inclui todos os kits do Bandido', 'Tag [MERCENÁRIO] no chat', 'Cargo VIP no Discord'],
      kits: ['mercenario1', 'mercenario2', 'base'] },
    { id: 'comandante', aba: 'kits', nome: 'Comandante', selo: 'NÍVEL 3', cor: '#ff3d00', foto: 'torre-noite', preco: 45.30, dias: 30,
      desc: 'Libera +11 kits.', itens: ['Kits Comandante 1 e 2 (30 min)', 'Kit Zona Quente (1 h)', 'Inclui todos os kits do Mercenário', 'Tag [COMANDANTE] no chat', 'Cargo VIP no Discord'],
      kits: ['comandante1', 'comandante2', 'zonaquente'] },
    { id: 'especial', aba: 'especiais', nome: 'Kit Especial', selo: 'ESPECIAL', cor: '#ffb020', foto: 'deserto', preco: 21.60, dias: 30,
      desc: 'Escolha 1 dos 3 kits especiais.', itens: ['Escolha 1: Zona Quente, Base ou Raid', 'Esperas diferentes (1 h, 12 h, 24 h)', 'Tag do kit no chat', 'Cargo VIP no Discord'],
      kits: ['zonaquente', 'base', 'raid'] },
    { id: 'cofre-mochila', slots: [8, 7], aba: 'cofres', nome: 'Cofre Mochila', selo: '8×7', cor: '#ffd08a', foto: 'neblina', preco: 13.60, dias: 30,
      desc: 'Seu /vault vira 8×7.', itens: ['Cofre 8×7 (56 espaços)', 'Tag [COFRE] no chat', 'Cargo VIP no Discord'] },
    { id: 'cofre-bunker', slots: [12, 9], aba: 'cofres', nome: 'Cofre Bunker', selo: '12×9', cor: '#ff8a3d', foto: 'colinas', preco: 22.90, dias: 30,
      desc: 'Libera o Cofre Mochila e o Bunker.', itens: ['Cofre 8×7 e 12×9 (108 espaços)', 'Tag [BUNKER] no chat', 'Cargo VIP no Discord'] },
    { id: 'cofre-fortaleza', slots: [15, 14], aba: 'cofres', nome: 'Cofre Fortaleza', selo: '15×14', cor: '#ffb020', foto: 'farol', preco: 42.90, dias: 30,
      desc: 'Os 3 cofres, o maior do servidor.', itens: ['Cofres 8×7, 12×9 e 15×14 (210 espaços)', 'Tag [FORTALEZA] no chat', 'Cargo VIP no Discord'] },
  ];
  const separado = P.reduce((t, p) => t + p.preco, 0);
  const S = [
    { id: 'supremo', nome: 'DZ Supremo', selo: 'TUDO INCLUSO', preco: 77.70, per: '35 dias', foto: 'ponte-por-do-sol',
      desc: 'Tudo que o servidor tem, por 30 dias + 5 de bônus.', itens: ['Libera +12 kits', 'Libera os 3 cofres', '30 dias + 5 dias de bônus', `Economize ${Math.round(100 - 100 * 77.70 / separado)}% comprando tudo junto`] },
    { id: 'supremo-vitalicio', nome: 'DZ Supremo Vitalício', selo: 'PRA SEMPRE', preco: 255.90, per: 'vitalício', foto: 'palmeiras',
      desc: 'Paga uma vez, é seu pra sempre.', itens: ['Libera +12 kits', 'Libera os 3 cofres', 'Vitalício', 'Tag [SUPREMO] dourada no chat'] },
  ];

  // Eventos automáticos e missões (o que o plugin faz; mudar aqui se mudar a config do jogo)
  const EVENTOS = [
    { id: 'airdrop', nome: 'Airdrop', quando: 'A cada 30 min', resumo: 'Caixa de armas ou equipamento cai longe da safe, com zumbis em volta.',
      itens: ['Cai num ponto de airdrop do mapa, entre 300 m e 1,5 km da safe mais perto, sempre com alguém online.',
        '70% das vezes é de <b>armas</b>: 2 armas boas com pentes, mochila, equipamento bom, MRE, remédio e chance de visão noturna.',
        '30% das vezes é de <b>equipamento</b>: mochila, colete, capacete, ferramenta, remédios e chance de uma arma.',
        '8 zumbis nascem em volta quando a caixa cai. Quem chegar primeiro leva.'] },
    { id: 'clima', nome: 'Evento de clima', quando: 'A cada 1h30', resumo: 'Tempestade, chuva ou neblina. Cai o BAÚ DO EVENTO com o melhor loot.',
      itens: ['O chat avisa 3 minutos antes: <b>"EVENTO: TEMPESTADE CHEGANDO"</b>.',
        'O clima é sorteado (tempestade, chuva, neblina, neve...) e dura 12 minutos.',
        'Quando começa, cai o <b>BAÚ DO EVENTO</b> longe da safe, com 14 zumbis em volta.',
        'É o melhor loot dos eventos: 2 armas do mercado negro, mochila, equipamento top e chance de visão noturna e acessório.',
        'O baú some em 10 minutos.'] },
    { id: 'comboio', nome: 'Comboio emboscado', quando: 'A cada 2h30', resumo: 'Caminhão militar quebrado na estrada. Arrombe as caixas antes de explodir.',
      itens: ['Precisa de pelo menos 2 jogadores online. O chat avisa a região.',
        'Um caminhão militar aparece quebrado numa estrada, longe das safes, com 2 caixas trancadas de loot militar.',
        'Pra abrir, fique <b>45 segundos</b> do lado das caixas, arrombando.',
        '15 zumbis em volta, e eles continuam nascendo enquanto tem gente perto.',
        'Em 15 minutos o caminhão <b>explode</b> e as caixas somem.'] },
    { id: 'missoes', nome: 'Missões diárias', quando: 'Todo dia', resumo: 'Fácil, média e difícil. Dinheiro e XP pra quem cumprir.',
      itens: ['Todo dia à meia-noite saem 3 missões iguais pra todo mundo: uma <b>fácil</b>, uma <b>média</b> e uma <b>difícil</b>.',
        'Exemplos: matar zumbis, caçar animais, pescar, matar mega zumbi, matar jogadores.',
        'Prêmio de $600 a $3.500 no banco, mais XP.',
        'Veja no NPC de missões da safe (aperte F) ou com <b class="mono">/missoes</b>.',
        'O progresso conta sozinho. Pra receber, vá na safe e aperte RECEBER (ou <b class="mono">/missoes receber</b>).'] },
  ];
  const cartaoEvento = e => `<a class="painel evento-cartao" href="/eventos#${e.id}" data-link><img src="/img/eventos/${e.id}.jpg" alt="${e.nome}" loading="lazy"><div><span class="evento-quando">${e.quando}</span><h3>${e.nome}</h3><p>${e.resumo}</p></div></a>`;

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

      <section class="secao"><h2>Conheça o <span>servidor</span></h2>
        <a class="arte-larga" href="/img/divulgacao/07-banner.webp" target="_blank" rel="noopener"><img src="/img/divulgacao/07-banner.webp" alt="DAY Z Brasil: sobreviva, lucre, domine" loading="lazy"></a>
        <div class="artes">${[['02-safe-zone', 'Safe zone completa', 'Lojas, caçador, ATM e garagem, sem PvP.'], ['03-economia', 'Economia de verdade', 'Cace, venda e guarde no banco.'], ['04-garagem-heli', 'Carro e helicóptero', 'Guarde na garagem e chame quando quiser.'], ['05-fora-da-safe', 'Fora da safe é guerra', 'Mercado negro, airdrops e raids.']]
          .map(([f, t, d]) => `<a class="arte" href="/img/divulgacao/${f}.webp" target="_blank" rel="noopener"><img src="/img/divulgacao/${f}.webp" alt="${t}" loading="lazy"><span><b>${t}</b>${d}</span></a>`).join('')}</div>
      </section>

      <section class="grade g4 numeros">
        <div class="painel"><div class="rotulo">Status</div><div class="numero ${s.online ? 'laranja' : ''}">${s.online ? 'ONLINE' : 'OFFLINE'}</div></div>
        <div class="painel"><div class="rotulo">Jogadores</div><div class="numero">${st.jogadores}/${s.max || '–'}</div></div>
        <div class="painel"><div class="rotulo">Mapa</div><div class="numero" style="font-size:26px">${esc(nomeMapa(s.mapa) || '–')}</div></div>
        <div class="painel"><div class="rotulo">Sobreviventes</div><div class="numero" data-conta="${Number(rk.total) || 0}">0</div></div>
      </section>

      <section class="secao"><h2>O que tem no <span>servidor</span></h2>
        <div class="grade g3">${recursos.map(([f, i, t, d]) => `<div class="painel recurso-foto" style="--foto:url(/img/recursos/rec-${fotoRecurso[t]}.webp), url(/img/mapa/${f}.webp)">
          <div class="ic">${i}</div><h3>${t}</h3><p>${d}</p></div>`).join('')}</div>
      </section>

      <section class="secao"><h2>Eventos e <span>missões</span></h2>
        <p class="fraco" style="margin:-8px 0 16px">Coisas acontecendo o tempo todo no mapa. <a href="/eventos" data-link>Veja como funciona cada um →</a></p>
        <div class="grade g4">${EVENTOS.map(cartaoEvento).join('')}</div>
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
      const torneio = async () => {
        let t; try { t = await api('/api/torneio'); } catch { return; }
        const el = document.getElementById('torneio'); if (!el) return;
        const dt = s => new Date(s).toLocaleString('pt-BR', { weekday: 'short', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
        const lista = (l, un) => l.length ? `<ol class="torneio-lista">${l.map(x => `<li><span>${esc(x.nome)}</span><b>${num(x.pts)} ${un}</b></li>`).join('')}</ol>` : `<p class="fraco">Ninguém pontuou ainda.</p>`;
        const estado = !t.tem ? 'Começa na sexta às 18h.' : t.aberto ? `Valendo até ${dt(t.fim)}.` : new Date(t.inicio) > new Date() ? `Começa ${dt(t.inicio)}.` : 'Acabou. O próximo começa na sexta às 18h.';
        const premios = (t.premios || []).map((p, i) => `${i + 1}º $${num(+p)}`).join(' · ');
        el.innerHTML = `<div class="grade g3">
          <div class="painel"><h3>🧟 Torneio: zumbis</h3><p class="fraco">${estado} Mega zumbi vale 5.</p>${lista(t.zumbis || [], 'pts')}</div>
          <div class="painel"><h3>🎣 Torneio: caça e pesca</h3><p class="fraco">Bicho ou peixe vale 1. Prêmio por categoria: ${premios || '-'}.</p>${lista(t.caca || [], 'pts')}</div>
          <div class="painel"><h3>⚔️ Arena 1x1</h3><p class="fraco">Desafie com /arena desafiar nome valor, dentro da safe.</p>${(t.arena || []).length ? `<ol class="torneio-lista">${t.arena.map(x => `<li><span>${esc(x.nome)}</span><b>${x.vitorias}V ${x.derrotas}D</b></li>`).join('')}</ol>` : '<p class="fraco">Ninguém lutou ainda.</p>'}</div></div>`;
      };
      setTimeout(() => {
        torneio();
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
        <div id="tabela"><p class="carregando">Carregando...</p></div>
        <div id="torneio" style="margin-top:28px"></div>`;
    },

    async leilao () {
      const desenhar = async () => {
        let d; try { d = await api('/api/leilao'); } catch { return; }
        const alvo = document.getElementById('lotes'); if (!alvo) return;
        const ativos = (d.lotes || []).filter(x => !x.encerrado).sort((a, b) => new Date(a.fim) - new Date(b.fim));
        const fim = (d.lotes || []).filter(x => x.encerrado);
        const resta = f => { const m = Math.max(0, (new Date(f) - Date.now()) / 60000); return m >= 60 ? `${Math.floor(m / 60)}h ${Math.round(m % 60)}min` : `${Math.ceil(m)} min`; };
        const resp = (d.respostas || []).length ? `<div class="painel" style="margin-bottom:16px"><b>Resposta do jogo:</b> ${d.respostas.map(esc).join('<br>')}</div>` : '';
        alvo.innerHTML = resp + (ativos.length ? `<div class="grade g3">${ativos.map(x => `<div class="painel lote">
            <div class="lote-topo"><img src="/icones/${x.item}.png?f=0" alt="" loading="lazy" onerror="this.style.visibility='hidden'"><div><h3>${esc(x.nome)}${x.qtd > 1 ? ` <small>x${x.qtd}</small>` : ''}</h3><p class="fraco">#${x.id} · de ${esc(x.vendedor)} · acaba em ${resta(x.fim)}</p></div></div>
            <p class="lote-preco">${x.lance ? `$${num(x.lance)} <small>${x.lances} lance(s) · ${x.souLider ? '<b class="laranja">você está ganhando</b>' : 'líder: ' + esc(x.lider)}</small>` : `<small>sem lance · mínimo</small> $${num(x.proximo)}`}</p>
            ${d.logado ? `<form class="lote-lance" data-id="${x.id}"><input type="number" min="${x.proximo}" step="1" value="${x.proximo}" aria-label="Valor do lance"><button class="botao">Dar lance</button></form>` : `<a class="botao fantasma" href="/entrar">Entre com a Steam para dar lance</a>`}
          </div>`).join('')}</div>` : `<p class="fraco">Nenhum item no leilão agora. No jogo, segure o item na mão dentro da safe e use <b>/leilao vender preço</b>.</p>`)
          + (fim.length ? `<h2 style="margin-top:32px">Acabaram há pouco</h2><div class="tabela-envolve"><table><thead><tr><th>#</th><th>Item</th><th>Vendido para</th><th>Valor</th></tr></thead><tbody>${fim.map(x => `<tr><td>${x.id}</td><td>${esc(x.nome)}</td><td>${x.lider ? esc(x.lider) : '<span class="fraco">sem lance</span>'}</td><td>${x.lance ? '$' + num(x.lance) : '-'}</td></tr>`).join('')}</tbody></table></div>` : '');
        alvo.querySelectorAll('.lote-lance').forEach(f => f.onsubmit = async e => {
          e.preventDefault();
          const valor = Number(f.querySelector('input').value), id = Number(f.dataset.id);
          const b = f.querySelector('button'); b.disabled = true; b.textContent = 'Enviando...';
          try {
            const r = await fetch('/api/leilao/lance', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-DZ': '1' }, body: JSON.stringify({ id, valor }) });
            if (!r.ok) throw new Error(await r.text());
            b.textContent = 'Lance enviado!'; setTimeout(desenhar, 3500);
          } catch (err) { b.disabled = false; b.textContent = 'Dar lance'; alert('Não deu: ' + err.message); }
        });
      };
      setTimeout(desenhar);
      return cabeca('Mercado entre jogadores', 'Leilão <em>DAY Z</em>', 'Lances com o dinheiro do seu banco no jogo. O lance fica preso até alguém cobrir ou acabar. Quem ganha retira o item na safe com /leilao retirar. Taxa de 5% pro vendedor.') + `<div id="lotes"><p class="carregando">Carregando...</p></div>`;
    },

    async faccoes () {
      const d = await api('/api/faccoes');
      const l = d.faccoes || [];
      const modo = ['Aberta', 'Por pedido', 'Fechada'];
      const corModo = ['var(--ok, #3DDC84)', 'var(--c2, #FFB020)', '#FF6060'];
      const lista = l.length ? `<div class="tabela-envolve"><table>
          <thead><tr><th>#</th><th>Facção</th><th>Kills</th><th>Zumbis</th><th>Nível</th><th>Membros</th><th>Recrutamento</th></tr></thead>
          <tbody>${l.map((f, i) => `<tr><td class="ord">${i + 1}</td>
            <td><details><summary style="cursor:pointer"><b style="color:${esc(f.cor)}">[${esc(f.tag)}]</b> ${esc(f.nome)}</summary>
              ${f.descricao ? `<p class="fraco" style="margin:6px 0">${esc(f.descricao)}</p>` : ''}
              <div class="fraco" style="font-size:13px">${(f.membros || []).map(m => `<a href="/jogador/${m.id}" data-link>${esc(m.nome)}</a> <small>(${esc(m.nomeCargo)})</small>`).join(' · ')}</div></details></td>
            <td>${num(f.abates)}</td><td>${num(f.zumbis)}</td><td>${f.nivel}</td><td>${(f.membros || []).length}</td>
            <td style="color:${corModo[f.modo] || ''}">${modo[f.modo] || ''}</td></tr>`).join('')}</tbody></table></div>`
        : `<p class="fraco carregando">Nenhuma facção ainda. Crie a sua no jogo: /menu > Facções.</p>`;
      return cabeca('Facções', 'As facções mais <em>fortes</em>', 'Ranking pela soma dos abates dos membros. Pra criar ou entrar numa facção, abra o /menu no jogo e vá em Facções.') + lista;
    },

    async kits () {
      const d = await api('/api/kits');
      const rank = d.kits.filter(k => k.abates > 0), livres = d.kits.filter(k => !k.abates && !k.permissao);
      const meus = eu && eu.ficha ? eu.ficha.abates : null;
      const fotosKit = ['deserto', 'lago', 'vila', 'mercado', 'colinas', 'bunker', 'torre-noite', 'farol', 'ponte', 'floresta', 'palmeiras', 'lanchonete'];
      let nFoto = 0;
      const cartao = k => `<div class="painel kit ${meus !== null && meus >= k.abates ? 'destaque' : ''}">
          <div class="kit-foto" style="background-image:url(/img/mapa/${fotosKit[nFoto++ % fotosKit.length]}.webp)">${(k.itens || []).length ? `<img class="kit-arma" src="/icones/${k.itens[0].id}.png?f=0" alt="" loading="lazy" onerror="this.remove()">` : ''}<span class="kit-nome">${esc((k.titulo || k.nome).toUpperCase())}</span></div>
          <div class="topo-kit"><span class="req">${k.abates ? `☠ ${k.abates} kills` : 'Livre'}</span><span class="cmd">/kit ${esc(k.nome)}</span></div>
          <h3>${esc(k.titulo || k.nome)}</h3><p class="fraco">${esc(k.descricao)}</p>
          ${(k.itens || []).length ? `<div class="vip-itens kit-itens">${k.itens.map(i => `<span class="vip-item" title="ID ${i.id}"><img src="/icones/${i.id}.png?f=0" alt="" loading="lazy" onerror="this.parentNode.remove()">${i.quantidade > 1 ? `<small>x${i.quantidade}</small>` : ''}</span>`).join('')}</div>` : ''}
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
      const d = await api('/api/kits').catch(() => ({ kits: [] }));
      const kits = d.kits || [];
      const real = v => 'R$ ' + v.toFixed(2).replace('.', ',');
      const conteudo = nomes => nomes.map(n => kits.find(k => k.nome === n)).filter(Boolean).map(k => `
        <div class="vip-kit"><div class="vip-kit-topo"><b>${esc(k.titulo || k.nome)}</b><span class="mono fraco">/kit ${esc(k.nome)} · ${espera(k.espera)}</span></div>
          <p class="fraco">${esc(k.descricao)}</p>
          <div class="vip-itens">${(k.itens || []).map(i => `<span class="vip-item" title="ID ${i.id}"><img src="/icones/${i.id}.png?f=0" alt="" loading="lazy" onerror="this.parentNode.remove()">${i.quantidade > 1 ? `<small>x${i.quantidade}</small>` : ''}</span>`).join('')}</div></div>`).join('');
      const todosKits = P.flatMap(p => p.kits || []);
      const idsDe = nomes => [...new Set(nomes.map(n => kits.find(k => k.nome === n)).filter(Boolean).flatMap(k => (k.itens || []).map(i => i.id)))];
      // foto: render do pacote (img/vip/<id>.webp) quando existir; enquanto isso, as fotos dos itens dos kits
      const fotoVip = (p, grande) => {
        const ids = idsDe(p.kits || (p.id.startsWith('supremo') ? todosKits : [])).slice(0, 8);
        let fundo;
        if (p.slots) fundo = `<div class="vip-slots" style="grid-template-columns:repeat(${p.slots[0]},1fr);grid-template-rows:repeat(${p.slots[1]},1fr)">${'<i></i>'.repeat(p.slots[0] * p.slots[1])}</div>`;
        else fundo = `<div class="vip-grade">${ids.map(id => `<span><img src="/icones/${id}.png?f=0" alt="" loading="lazy" onerror="this.parentNode.remove()"></span>`).join('')}</div>`;
        return `<div class="vip-foto itens${grande ? ' grande' : ''}">${fundo}<img class="vip-render" src="/img/vip/vip-${p.id}.webp" alt="" onerror="this.remove()"></div>`;
      };
      let pix = { ativo: false };
      try { pix = await fetch('/api/pix/status', { credentials: 'same-origin' }).then(r => r.json()); } catch { }
      const botaoDe = p => pix.ativo ? `<a class="botao vip-comprar" href="/comprar?pacote=${esc(p.id)}" data-link><span class="vip-formas"><svg class="ic" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3l4 4-4 4-4-4zM12 13l4 4-4 4-4-4zM3 12l4-4 4 4-4 4zM13 12l4-4 4 4-4 4z"/></svg><svg class="ic" viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18M7 15h4"/></svg></span>COMPRAR</a>`
        : `<button class="botao vip-comprar" disabled title="O pagamento abre em breve">🔒 EM BREVE</button>`;
      const cartao = p => `<div class="painel vip-card" data-aba="${p.aba}" style="--vc:${p.cor}">
          ${fotoVip(p)}
          <span class="vip-selo">[${esc(p.selo)}]</span>
          <h3>${esc(p.nome)}</h3><p class="fraco">${esc(p.desc)}</p>
          <div class="vip-preco">${real(p.preco)}<small> / ${p.dias} dias</small></div>
          <ul class="vip-lista">${p.itens.map(x => `<li>${esc(x)}</li>`).join('')}</ul>
          ${p.kits ? `<details class="vip-detalhes"><summary>DETALHES</summary>${conteudo(p.kits)}</details>` : ''}
          ${botaoDe(p)}
        </div>`;
      const supremo = p => `<div class="painel vip-supremo" data-aba="supremo">
          ${fotoVip(p, true)}
          <div class="vip-meio"><span class="vip-selo">[${esc(p.selo)}]</span><h3>${esc(p.nome)}</h3><p class="fraco">${esc(p.desc)}</p>
            <ul class="vip-lista duas">${p.itens.map(x => `<li>${esc(x)}</li>`).join('')}</ul></div>
          <div class="vip-dir">${p.id === 'supremo' ? `<div class="fraco mono" style="font-size:11px">COMPRANDO TUDO SEPARADO</div><s class="fraco">${real(separado)}</s>` : ''}
            <div class="vip-preco grande">${real(p.preco)}<small> / ${p.per}</small></div>${botaoDe(p)}</div>
        </div>`;
      setTimeout(() => {
        document.querySelectorAll('.vip-abas button').forEach(b => b.onclick = () => {
          document.querySelectorAll('.vip-abas button').forEach(x => x.classList.toggle('ativo', x === b));
          document.querySelectorAll('[data-aba]').forEach(c => { c.style.display = b.dataset.f === 'todos' || c.dataset.aba === b.dataset.f ? '' : 'none'; });
        });
      });
      return cabeca('Loja VIP', 'Apoie & <em>sobreviva</em>', 'Kits VIP, kits especiais, cofres maiores e o DZ Supremo. Pague com Pix ou cartão e o VIP é ativado sozinho no jogo, na hora.') +
        `<div class="vip-abas">${[['todos', 'Todos'], ['kits', 'Kits VIP'], ['especiais', 'Kits especiais'], ['cofres', 'Cofres'], ['supremo', 'Supremo']]
          .map(([f, t], i) => `<button data-f="${f}"${i ? '' : ' class="ativo"'}>${t}</button>`).join('')}</div>
         ${S.map(supremo).join('')}
         <div class="grade g4" style="margin-top:20px">${P.map(cartao).join('')}</div>
         <p class="fraco" style="text-align:center;margin-top:24px">As fotos dos itens aparecem conforme forem adicionadas. Dúvidas? Abra um ticket no nosso Discord.</p>`;
    },

    async comprar () {
      let pix = { ativo: false, chave: '' };
      try { pix = await api('/api/pix/status'); } catch { }
      const q = new URLSearchParams(location.search);
      const todos = [...S, ...P];
      const ini = todos.find(x => x.id === q.get('pacote')) || S[0];
      setTimeout(() => ligarCompra(pix, ini.id, q.get('pg')));
      const icone = n => `<svg class="ic" viewBox="0 0 24 24" aria-hidden="true">${({
        cadeado: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
        pix: '<path d="M12 3l4 4-4 4-4-4zM12 13l4 4-4 4-4-4zM3 12l4-4 4 4-4 4zM13 12l4-4 4 4-4 4z"/>',
        cartao: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18M7 15h4"/>',
        voltar: '<path d="M9 14l-5-5 5-5"/><path d="M4 9h11a5 5 0 0 1 0 10h-3"/>',
        check: '<path d="M5 12l5 5L20 7"/>', copiar: '<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15V5a2 2 0 0 1 2-2h10"/>',
        steam: '<circle cx="12" cy="12" r="9"/><circle cx="15" cy="9" r="2.5"/><path d="M4 14l5 2"/>',
      })[n]}</svg>`;
      return `<div class="compra">
        <a class="voltar-planos" href="/loja" data-link>${icone('voltar')}Voltar à Loja VIP</a>
        <div class="seguro"><span class="seguro-icone">${icone('cadeado')}</span><b>Checkout seguro</b><span>Os dados do cartão vão direto para o Mercado Pago, criptografados. O DAY Z BRASIL não vê nem guarda o número do seu cartão.</span></div>
        <div class="compra-grade">
          <div class="compra-esq" id="c-passos">
            <section class="caixa-passo">
              <h2 class="titulo-passo"><span class="num">1</span>Sua conta</h2>
              <p class="sub-passo">O VIP é ativado na conta Steam que estiver logada. O e-mail é para o recibo do pagamento.</p>
              ${eu && eu.logado ? `<div class="conta-ok"><span class="conta-avatar">${icone('check')}</span><div><b>${esc(eu.ficha ? eu.ficha.nome : 'Steam conectada')}</b><span>Steam ${esc(eu.id)}</span></div><a class="botao fantasma pequeno" href="/sair">Trocar</a></div>`
              : `<div class="linha-botoes" style="justify-content:flex-start;margin-top:16px"><a class="botao" id="c-entrar" href="/entrar?volta=${encodeURIComponent('/comprar?pacote=' + ini.id)}">${icone('steam')}Entrar com a Steam</a></div>`}
              <label class="campo" style="margin-top:12px"><span class="rot">E-mail</span><input id="c-email" type="email" inputmode="email" autocomplete="email" maxlength="120" placeholder="seuemail@exemplo.com"></label>
            </section>
            <section class="caixa-passo">
              <h2 class="titulo-passo"><span class="num">2</span>Pacote</h2>
              <div class="opcoes-plano" id="c-pacotes">${todos.map(p => `<button type="button" class="opcao plano-opcao" data-p="${p.id}">${p.id.startsWith('supremo') ? `<em class="po-tag">${p.id === 'supremo' ? 'MELHOR PREÇO' : 'PRA SEMPRE'}</em>` : ''}<span><b>${esc(p.nome)}</b><small>${esc(p.per || p.dias + ' dias')}</small></span><span class="po-preco"><small>R$</small>${p.preco.toFixed(2).replace('.', ',')}</span></button>`).join('')}</div>
              <div id="c-escolha" class="c-escolha" hidden></div>
            </section>
            <section class="caixa-passo">
              <h2 class="titulo-passo"><span class="num">3</span>Pagamento</h2>
              <div class="opcoes-pagamento">
                <button type="button" class="opcao marcada" data-m="pix">${icone('pix')}<span><b>Pix</b><small>Aprovação imediata</small></span></button>
                <button type="button" class="opcao" data-m="cartao">${icone('cartao')}<span><b>Cartão de crédito ou débito</b><small>Visa, Master, Elo, Amex, Hiper</small></span></button>
              </div>
              <div class="cartao-area" id="c-cartao" hidden>
                <div class="cartao-carregando" id="c-cartao-msg"><span class="girando"></span>Carregando o formulário seguro do Mercado Pago…</div>
                <div id="cartao-brick"></div>
                <p class="selo-seguro">${icone('cadeado')}Formulário do Mercado Pago, com criptografia. Os dados do cartão não passam pelo nosso servidor.</p>
              </div>
            </section>
            <p class="aceite">Pagamento único, sem renovação automática. Comprando de novo o mesmo pacote, os dias somam.</p>
          </div>
          <div class="compra-esq" id="c-pix" hidden>
            <section class="caixa-passo">
              <h2 class="titulo-passo"><span class="num">${icone('pix')}</span>Pague com Pix</h2>
              <p class="sub-passo">Abra o app do seu banco e escaneie o código, ou use o Pix copia e cola.</p>
              <div class="pix-corpo">
                <div class="qr"><img id="c-qr" alt="QR code do Pix"></div>
                <div class="pix-lado">
                  <span class="rotulo">Pix copia e cola</span>
                  <div class="copia"><input id="c-copia" readonly><button type="button" class="botao pequeno" id="c-copiar">${icone('copiar')}<span>Copiar</span></button></div>
                  <div class="esperando"><i></i>Aguardando o pagamento…</div>
                  <p class="fraco" id="c-expira"></p>
                  <p class="fraco">Assim que o Pix cair, o VIP entra na sua conta sozinho, no jogo e no Discord. Pode deixar esta página aberta.</p>
                </div>
              </div>
              <button type="button" class="botao fantasma pequeno" id="c-pix-voltar">${icone('voltar')}Trocar o pacote ou a forma de pagamento</button>
            </section>
          </div>
          <div class="compra-esq" id="c-pago" hidden>
            <section class="caixa-passo centro-passo">
              <span class="grande-icone ok">${icone('check')}</span>
              <h2>Pagamento aprovado!</h2>
              <p class="sub-passo" id="c-pago-txt">Seu VIP já está ativo.</p>
              <p class="fraco">Se você está no servidor, aparece no chat agora. Se não, ele já vai estar lá quando você entrar.</p>
              <div class="linha-botoes"><a class="botao" href="steam://connect/54.94.93.87:27015">⚡ Jogar agora</a><a class="botao fantasma" href="/perfil" data-link>Meu perfil</a></div>
            </section>
          </div>
          <div class="compra-esq" id="c-cancelado" hidden>
            <section class="caixa-passo centro-passo">
              <span class="grande-icone ruim">${icone('cartao')}</span>
              <h2>Esse pagamento não foi concluído</h2>
              <p class="sub-passo">Ele expirou ou foi cancelado, e nada foi cobrado. Você pode tentar de novo.</p>
              <div class="linha-botoes"><button type="button" class="botao" id="c-de-novo">Tentar de novo</button></div>
            </section>
          </div>
          <aside class="resumo-pedido" id="c-resumo">
            <span class="rotulo laranja">— Resumo do pedido —</span>
            <h2 id="r-nome"></h2>
            <p class="fraco" id="r-sub"></p>
            <div class="r-mes"><small>R$</small><b id="r-int"></b><span id="r-dec"></span><em id="r-per"></em></div>
            <div class="r-linhas" id="r-linhas"></div>
            <div class="r-total"><span>Total</span><b id="r-total"></b></div>
            ${pix.ativo ? '' : '<p class="aviso">O pagamento pelo site abre em breve.</p>'}
            <p class="erro" id="c-erro" hidden></p>
            <button type="button" class="botao pagar" id="c-pagar"${pix.ativo ? '' : ' disabled'}>${icone('cadeado')}<span id="c-pagar-txt">Pagar</span></button>
            <p class="r-nota">Ativação automática no jogo e cargo VIP no Discord (com a Steam vinculada).</p>
            <div class="r-recursos"><b>Incluso no pacote</b><ul id="r-itens"></ul></div>
          </aside>
        </div>
      </div>`;
    },

    async eventos () {
      setTimeout(() => { const h = location.hash.slice(1); if (h && document.getElementById('ev-' + h)) document.getElementById('ev-' + h).scrollIntoView({ behavior: 'smooth' }); }, 50);
      return cabeca('Eventos', 'Eventos e <em>missões</em>', 'Airdrop, clima, comboio e missões diárias: o que acontece no mapa e como pegar o loot.') +
        EVENTOS.map(e => `<section class="secao evento-detalhe" id="ev-${e.id}"><img src="/img/eventos/${e.id}.jpg" alt="${e.nome}" loading="lazy">
          <div><span class="evento-quando">${e.quando}</span><h2>${e.nome}</h2><ul>${e.itens.map(i => `<li>${i}</li>`).join('')}</ul></div></section>`).join('') +
        `<p class="fraco" style="text-align:center;margin-top:20px">A staff também pode começar um evento ou comboio a qualquer hora. Fique de olho no chat do jogo e no Discord.</p>`;
    },

    async sobre () {
      const cmds = [
        ['/menu', 'Mostra tudo que o servidor tem'], ['/kits · /kit nome', 'Seus kits e o que falta pra cada um'],
        ['/safe', 'Vai pra uma safe: dentro de uma safe, ou logo depois de entrar ou morrer (fora de combate)'], ['/home', 'Vai pra sua cama (10 s parado)'],
        ['/tpa nome · /tpaceitar', 'Teleporte até um amigo'], ['/saldo · /depositar · /sacar', 'Dinheiro no ATM'],
        ['/lojas · /loja · /comprar · /vender', 'Lojas de armas, comida, geral, caçador e mercado negro'],
        ['/marcar negro · /marcar cacador', 'Marca no mapa (aperte M) o mercado negro ou o caçador'],
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
        </div>
        <div class="grade g3" style="margin-top:20px">
          <div class="painel"><h3 style="margin-bottom:12px">🕶️ Mercado negro</h3>
            <p>Fica <b>fora da safe</b>, então lá o <b>PvP é liberado</b>: vá armado e de olho em quem chega.</p>
            <p style="margin-top:10px">É onde estão as <b>melhores armas</b> do servidor (snipers, rifles fortes e metralhadoras), equipamento tático, explosivos de raid e a venda de <b>helicóptero</b>.</p>
            <p style="margin-top:10px" class="fraco">Pra achar: digite <span class="mono">/marcar negro</span> e aperte M.</p></div>
          <div class="painel"><h3 style="margin-bottom:12px">📦 Airdrops</h3>
            <p>A cada <b>30 minutos</b> cai um airdrop num ponto aleatório do mapa, sempre fora das safes. O aviso sai no chat: olhe pro céu.</p>
            <p style="margin-top:10px">O que vem é sorteado: <b>70% de chance de armas boas</b> (rifles e metralhadoras, com pentes; as raras saem menos) e o resto é <b>equipamento</b> (mochila, colete, capacete, ferramentas e remédio).</p>
            <p style="margin-top:10px" class="fraco">Quem chegar primeiro leva. Espere briga.</p></div>
          <div class="painel"><h3 style="margin-bottom:12px">💰 Economia</h3>
            <p>O dinheiro são as notas do jogo. Guarde no <b>ATM</b>: o que está no bolso cai quando você morre.</p>
            <p style="margin-top:10px">As lojas compram de volta qualquer item por <b>60% do preço</b> de compra (uma arma de $1.000 vende por $600).</p>
            <p style="margin-top:10px"><b>Caça dá dinheiro:</b> mate o bicho, corte o corpo com uma faca e venda carne e couro no <b>caçador</b>, dentro da safe. Carne crua $60, assada $80, couro $50. Um grizzly rende mais de $600.</p></div>
        </div>
        <div class="painel destaque creditos" style="margin-top:20px"><h3 style="margin-bottom:8px">🙏 Créditos e agradecimento</h3>
          <p>O DAY Z BRASIL se inspirou no servidor <b>Zumbas Brasil</b>. Reconhecemos o ótimo trabalho que eles fazem pela comunidade brasileira de Unturned e agradecemos pela inspiração na construção do nosso servidor. Valeu, Zumbas!</p></div>`;
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
      const v = new URLSearchParams(location.search).get('vinculo');
      const aviso = v === 'ok' ? `<div class="painel destaque" style="text-align:center;margin-bottom:18px;border-color:#5be37d"><b style="color:#5be37d">✅ Steam vinculada ao Discord!</b><br><span class="fraco">No jogo, pegue seu kit com <b class="mono">/kit discord</b>. Os alertas de raid chegam na sua DM.</span></div>`
        : v ? `<div class="painel" style="text-align:center;margin-bottom:18px"><b style="color:#ff4d4d">O link de vínculo expirou ou deu erro.</b><br><span class="fraco">Volte no canal #vincular do Discord e clique de novo.</span></div>` : '';
      return aviso + await paginaPerfil();
    },
  };
  async function paginaPerfil () {
    {
      if (!eu || !eu.logado) {
        return cabeca('Seu perfil', 'Seu <em>perfil</em>', 'Entre com a sua conta Steam pra ver suas estatísticas, patente e o progresso dos Rank Kits.') +
          `<div style="text-align:center"><a class="botao" href="/entrar">Entrar com Steam</a>
           <p class="fraco" style="margin-top:14px;font-size:13px">O login é feito no site oficial da Steam. Não vemos sua senha.</p></div>`;
      }
      if (!eu.ficha) return cabeca('Seu perfil', 'Seu <em>perfil</em>', 'Você ainda não jogou no servidor. Entre uma vez e suas estatísticas aparecem aqui.') +
        `<div style="text-align:center"><a class="botao" href="/servidores" data-link>Ir pro servidor</a> <a class="botao fantasma" href="/sair">Sair</a></div>`;
      return desenharPerfil(eu.ficha, eu.kits, true);
    }
  }
  Object.assign(paginas, {

    async jogador (id) {
      const [f, k] = await Promise.all([api('/api/jogador/' + encodeURIComponent(id)).catch(() => null), api('/api/kits')]);
      if (!f) return cabeca('Jogador', 'Não encontrado', 'Esse jogador ainda não tem estatísticas.');
      return desenharPerfil(f, k.kits, false);
    },
  });

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
  // menu cheio (tela estreita ou conta logada larga): vira o botão ☰
  function ajustarMenu() {
    const b = document.body, env = document.querySelector('.topo .envolve'), m = document.getElementById('menu');
    if (!env || !m) return;
    const cheio = () => {
      const mr = m.getBoundingClientRect(), mk = document.querySelector('.marca').getBoundingClientRect(), d = document.querySelector('.topo-dir').getBoundingClientRect();
      return env.scrollWidth > env.clientWidth + 1 || mr.left < mk.right + 8 || mr.right > d.left - 8;
    };
    b.classList.remove('menu-compacto', 'menu-apertado');
    if (getComputedStyle(m).display !== 'none') {
      if (cheio()) b.classList.add('menu-apertado'); // primeiro esconde o botão do Discord e encurta o nome
      if (cheio()) b.classList.add('menu-compacto');
    }
    marcarMenu();
  }
  window.ajustarMenu = ajustarMenu;
  addEventListener('resize', ajustarMenu);

  // ---------- checkout da Loja VIP (Pix e cartão, tudo no nosso site) ----------
  const ESCOLHAS = {
    iniciante: [['andarilho', 'Andarilho'], ['batedor', 'Batedor'], ['saqueador', 'Saqueador']],
    especial: [['zonaquente', 'Zona Quente'], ['base', 'Base'], ['raid', 'Raid']],
  };
  let mpPronto = null;
  function script(src, attrs) {
    return new Promise((ok, falha) => {
      const s = document.createElement('script'); s.src = src; s.async = true;
      Object.keys(attrs || {}).forEach(k => s.setAttribute(k, attrs[k]));
      s.onload = ok; s.onerror = () => falha(new Error('não carregou'));
      document.head.appendChild(s);
    });
  }
  function carregarMP(chave) {
    if (!mpPronto) {
      script('https://www.mercadopago.com/v2/security.js', { view: 'checkout' }).catch(() => { });
      mpPronto = Promise.race([script('https://sdk.mercadopago.com/js/v2'), new Promise((_, f) => setTimeout(() => f(new Error('demorou')), 15000))])
        .then(() => { if (!window.MercadoPago) throw new Error('sem MercadoPago'); return new window.MercadoPago(chave, { locale: 'pt-BR' }); });
      mpPronto.catch(() => { mpPronto = null; });
    }
    return mpPronto;
  }
  function ligarCompra(pix, pacoteIni, pgIni) {
    const $ = id => document.getElementById(id);
    if (!$('c-pagar')) return;
    const todos = [...S, ...P];
    let pacote = pacoteIni, escolha = '', metodo = 'pix', brick = null, brickChave = '', pag = null, timer = 0, relogio = 0;
    const atual = () => todos.find(x => x.id === pacote);
    const real = v => 'R$ ' + v.toFixed(2).replace('.', ',');
    try { const e = localStorage.getItem('dz-email'); if (e && $('c-email')) $('c-email').value = e; } catch { }
    const email = () => ($('c-email') ? $('c-email').value.trim() : '');
    const erro = t => { $('c-erro').textContent = t || ''; $('c-erro').hidden = !t; };

    function resumo() {
      const p = atual();
      $('r-nome').textContent = p.nome;
      $('r-sub').textContent = p.desc;
      const [i, d] = p.preco.toFixed(2).split('.');
      $('r-int').textContent = i; $('r-dec').textContent = ',' + d; $('r-per').textContent = '/ ' + (p.per || p.dias + ' dias');
      const op = ESCOLHAS[p.id], nomeEsc = op && escolha ? op.find(x => x[0] === escolha)[1] : '';
      $('r-linhas').innerHTML = `<div><span>${esc(p.nome)}${nomeEsc ? ' · ' + esc(nomeEsc) : ''}</span><b>${real(p.preco)}</b></div>` +
        (p.id === 'supremo' ? `<div class="r-economia"><span>Economize ${Math.round(100 - 100 * p.preco / separado)}%</span><s>${real(separado)}</s></div>` : '') +
        `<div><span>Forma de pagamento</span><b>${metodo === 'pix' ? 'Pix' : 'Cartão'}</b></div>`;
      $('r-total').textContent = real(p.preco);
      $('c-pagar-txt').textContent = (eu && eu.logado ? 'Pagar ' : 'Entrar e pagar ') + real(p.preco);
      $('r-itens').innerHTML = p.itens.map(x => `<li><svg class="ic" viewBox="0 0 24 24"><path d="M5 12l5 5L20 7"/></svg><span>${esc(x)}</span></li>`).join('');
      document.querySelectorAll('#c-pacotes .opcao').forEach(b => b.classList.toggle('marcada', b.dataset.p === pacote));
      const ce = $('c-escolha');
      if (op) {
        ce.hidden = false;
        ce.innerHTML = `<span class="rotulo">Escolha o kit</span><div class="c-escolhas">${op.map(([v, t]) => `<button type="button" class="opcao${v === escolha ? ' marcada' : ''}" data-e="${v}"><b>${t}</b></button>`).join('')}</div>`;
        ce.querySelectorAll('[data-e]').forEach(b => b.onclick = () => { escolha = b.dataset.e; resumo(); });
      } else { ce.hidden = true; ce.innerHTML = ''; }
    }
    document.querySelectorAll('#c-pacotes .opcao').forEach(b => b.onclick = () => {
      pacote = b.dataset.p; escolha = ''; erro('');
      history.replaceState(null, '', '/comprar?pacote=' + pacote);
      if ($('c-entrar')) $('c-entrar').href = '/entrar?volta=' + encodeURIComponent('/comprar?pacote=' + pacote);
      resumo(); if (metodo === 'cartao') montarCartao();
    });
    document.querySelectorAll('[data-m]').forEach(b => b.onclick = () => {
      metodo = b.dataset.m; erro('');
      document.querySelectorAll('[data-m]').forEach(x => x.classList.toggle('marcada', x === b));
      $('c-cartao').hidden = metodo !== 'cartao';
      resumo();
      if (metodo === 'cartao') montarCartao(); else desmontar();
    });

    function desmontar() { if (brick) { try { brick.unmount(); } catch { } } brick = null; brickChave = ''; if ($('cartao-brick')) $('cartao-brick').innerHTML = ''; }
    function montarCartao(forcar) {
      const p = atual(), chave = p.id + '|' + email();
      if (!forcar && brick && brickChave === chave) return;
      desmontar(); brickChave = chave;
      const msg = $('c-cartao-msg');
      if (!pix.ativo) { msg.hidden = false; msg.textContent = 'O pagamento pelo site abre em breve.'; return; }
      msg.hidden = false; msg.innerHTML = '<span class="girando"></span>Carregando o formulário seguro do Mercado Pago…';
      carregarMP(pix.chave).then(mp => {
        if (brickChave !== chave || metodo !== 'cartao') return null;
        return mp.bricks().create('cardPayment', 'cartao-brick', {
          initialization: { amount: p.preco, payer: { email: email() || undefined } },
          customization: {
            visual: { hidePaymentButton: true, hideFormTitle: true, style: { theme: 'dark', customVariables: {
              baseColor: '#FF5A00', baseColorFirstVariant: '#FF7A2E', baseColorSecondVariant: '#FFB020', formBackgroundColor: '#121316',
              inputBackgroundColor: '#0A0B0D', textPrimaryColor: '#F5F6F7', textSecondaryColor: '#8A9099', outlinePrimaryColor: '#33363C',
              borderRadiusSmall: '8px', borderRadiusMedium: '12px', borderRadiusLarge: '16px', errorColor: '#FF6B6B' } } },
            paymentMethods: { maxInstallments: 12 }
          },
          callbacks: {
            onReady: () => { msg.hidden = true; },
            onSubmit: () => Promise.resolve(),
            onError: e => { if (e && e.type === 'critical') { msg.hidden = false; msg.textContent = 'Não consegui abrir o formulário do cartão. Use o Pix ou recarregue a página.'; } }
          }
        });
      }).then(ctrl => {
        if (!ctrl) return;
        if (brickChave !== chave) { try { ctrl.unmount(); } catch { } return; }
        brick = ctrl;
      }).catch(() => { msg.hidden = false; msg.textContent = 'Não consegui abrir o formulário do cartão. Use o Pix ou recarregue a página.'; });
    }

    function vista(id) {
      ['c-passos', 'c-pix', 'c-pago', 'c-cancelado'].forEach(v => { $(v).hidden = v !== id; });
      $('c-resumo').classList.toggle('travado', id !== 'c-passos');
      if (id !== 'c-pix') { clearInterval(timer); clearInterval(relogio); timer = relogio = 0; }
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
    function mostrar() {
      if (!pag) return;
      if (pag.estado === 'aprovado') {
        $('c-pago-txt').textContent = `Seu VIP ${(todos.find(x => x.id === pag.pacote) || {}).nome || ''} já está ativo na sua conta.`;
        vista('c-pago'); return;
      }
      if (pag.estado === 'cancelado') { vista('c-cancelado'); return; }
      if (pag.metodo !== 'pix') { vista('c-passos'); return; }
      $('c-qr').src = pag.qr64 ? 'data:image/png;base64,' + pag.qr64 : '';
      $('c-copia').value = pag.qr || '';
      vista('c-pix');
      const conta = () => {
        const s = Math.max(0, Math.round((new Date(pag.expira) - Date.now()) / 1000));
        $('c-expira').textContent = s > 0 ? 'O código vale por mais ' + Math.floor(s / 60) + ':' + ('0' + s % 60).slice(-2) : 'O código expirou. Volte e gere outro.';
      };
      conta(); clearInterval(relogio); relogio = setInterval(conta, 1000);
      clearInterval(timer);
      timer = setInterval(async () => {
        if (!document.body.contains($('c-pix') || document.createElement('i'))) { clearInterval(timer); clearInterval(relogio); return; }
        try { const r = await fetch('/api/vip/pagar?id=' + encodeURIComponent(pag.id), { credentials: 'same-origin' }); if (r.ok) { pag = await r.json(); if (pag.estado !== 'pendente') mostrar(); } } catch { }
      }, 5000);
    }
    $('c-copiar').onclick = () => { navigator.clipboard.writeText($('c-copia').value); $('c-copiar').querySelector('span').textContent = 'Copiado!'; };
    $('c-pix-voltar').onclick = () => { pag = null; history.replaceState(null, '', '/comprar?pacote=' + pacote); vista('c-passos'); };
    $('c-de-novo').onclick = $('c-pix-voltar').onclick;

    $('c-pagar').onclick = async () => {
      erro('');
      const p = atual(), em = email();
      if (!eu || !eu.logado) {
        try { if (em) localStorage.setItem('dz-email', em); } catch { }
        location.href = '/entrar?volta=' + encodeURIComponent('/comprar?pacote=' + p.id); return;
      }
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em)) { erro('Digite o seu e-mail no passo 1 (vai o recibo).'); $('c-email') && $('c-email').focus(); return; }
      if (ESCOLHAS[p.id] && !escolha) { erro('Escolha o kit do pacote no passo 2.'); return; }
      try { localStorage.setItem('dz-email', em); } catch { }
      const corpo = { pacote: p.id, escolha, email: em, metodo };
      if (metodo === 'cartao') {
        if (!brick) { erro('O formulário do cartão ainda está carregando. Espere um instante.'); return; }
        let d = null;
        try { d = await brick.getFormData(); } catch { d = null; }
        if (!d || !d.token) { erro('Confira os dados do cartão: número, validade, código de segurança, nome e CPF.'); return; }
        const pr = d.payer || {}, doc = pr.identification || {};
        corpo.cartao = { token: d.token, payment_method_id: d.payment_method_id || '', issuer_id: d.issuer_id ? String(d.issuer_id) : '',
          installments: Number(d.installments) || 1, tipoDocumento: doc.type || '', documento: doc.number || '', aparelho: window.MP_DEVICE_SESSION_ID || '' };
      }
      const b = $('c-pagar'); b.disabled = true; $('c-pagar-txt').textContent = metodo === 'cartao' ? 'Pagando…' : 'Gerando o Pix…';
      try {
        const r = await fetch('/api/vip/pagar', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-DZ': '1' }, body: JSON.stringify(corpo) });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.erro || 'Não deu certo agora. Tente de novo.');
        pag = j;
        history.replaceState(null, '', '/comprar?pacote=' + pacote + '&pg=' + encodeURIComponent(j.id));
        mostrar();
      } catch (e) {
        erro(e.message);
        if (metodo === 'cartao') montarCartao(true); // o token do cartão só vale uma vez
      } finally { b.disabled = false; resumo(); }
    };

    if ($('c-email')) $('c-email').addEventListener('change', () => { if (metodo === 'cartao') montarCartao(); });
    resumo();
    if (pgIni && eu && eu.logado) {
      fetch('/api/vip/pagar?id=' + encodeURIComponent(pgIni), { credentials: 'same-origin' }).then(r => r.ok ? r.json() : null).then(j => { if (j) { pag = j; mostrar(); } }).catch(() => { });
    }
  }

  // ---------- roteador ----------
  async function abrir(caminho, empilhar) {
    const partes = caminho.split(/[?#]/)[0].replace(/^\/+|\/+$/g, '').split('/');
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
  window.addEventListener('popstate', () => abrir(location.pathname + location.search, false));
  document.getElementById('hamburguer').onclick = () => document.getElementById('menu').classList.toggle('aberto');

  (async () => {
    try { eu = await api('/api/eu'); } catch (e) { eu = null; }
    const conta = document.getElementById('botao-conta');
    if (eu && eu.logado) { conta.textContent = eu.ficha ? eu.ficha.nome : 'Meu perfil'; conta.href = '/perfil'; conta.setAttribute('data-link', ''); }
    try {
      const st = await api('/api/status');
      if (st.discord) { const d = document.getElementById('link-discord'); d.href = st.discord; d.hidden = false; }
    } catch (e) {}
    ajustarMenu();
    if (document.fonts) document.fonts.ready.then(ajustarMenu);
    abrir(location.pathname + location.search, false);
  })();

})();
