package main

// Textos fixos que o bot posta quando monta o servidor.

const (
	corLaranja  = 0xFF5A00
	corAmbar    = 0xFFB020
	corVerde    = 0x5BE37D
	corVermelho = 0xFF4D4D
	corCinza    = 0x16171A

	nomeServidor = "DAY Z BRASIL OFICIAL"
	ipServidor   = "54.94.93.87:27015"
	site         = "https://snaplag.com.br/"
	iconeURL     = "https://raw.githubusercontent.com/protalhard/SERVIDOR-UNTURNED/main/branding/icon.png"
	capaURL      = "https://raw.githubusercontent.com/protalhard/SERVIDOR-UNTURNED/main/branding/capa.jpg"
	rodape       = "DAY Z BRASIL OFICIAL • powered by SnapLag"
)

const textoBemVindo = `Sobrevivência **PvP estilo DayZ** no Unturned, com dinheiro físico, ATM, lojas, mercado negro, caça e airdrops.

**Comece por aqui**
📜 Leia as regras em <#%s>
🎮 Aprenda a jogar em <#%s>
🟢 Veja se o servidor está online em <#%s>
🎫 Precisa de ajuda? Abra um ticket em <#%s>

Bom jogo, sobrevivente.`

const textoRegrasJogo = `**1.** Proibido hack, cheat, macro ou qualquer programa que dê vantagem. *Ban permanente.*
**2.** Proibido abusar de bug ou exploit. Achou um? Reporte em ticket. *Ban.*
**3.** Proibido construir dentro da safe zone ou bloquear lojas e ATMs. *Remoção + aviso.*
**4.** Proibido usar conta secundária pra fugir de punição. *Ban permanente.*
**5.** Racismo, ameaça real ou ofensa pesada no chat. *Mute ou ban.*
**6.** Entrar num grupo só pra roubar ou destruir a base por dentro. *Punição variada.*
**7.** Fora da safe zone vale tudo: PvP, raid e emboscada fazem parte do jogo.`

const textoRegrasDiscord = `**1.** Respeito acima de tudo. Sem discriminação, assédio ou exposição de dados de ninguém. *Ban permanente.*
**2.** Sem divulgar outros servidores, cheats ou links suspeitos. *Ban.*
**3.** Sem flood, spam ou correntes. *Mute.*
**4.** Problemas com a staff se resolvem em ticket, não no chat geral. *Aviso.*
**5.** Conteúdo adulto ou violento real é proibido. *Ban permanente.*

Ao entrar no servidor do jogo ou no Discord você concorda com estas regras.`

const textoComoJogar = `**Conectar:** Unturned → Jogar → Servidores → aba **Internet** → procure **DAY Z BRASIL**.
Ou: Conectar → IP ` + "`54.94.93.87`" + ` porta ` + "`27015`" + `.

**Dentro do jogo, digite no chat:**
` + "`/menu`" + ` tudo que o servidor tem
` + "`/kits`" + ` e ` + "`/kit nome`" + ` seus kits (abates liberam kits melhores)
` + "`/safe`" + ` volta pra safe zone (fique parado 15 s)
` + "`/saldo`" + `, ` + "`/depositar`" + `, ` + "`/sacar`" + ` dinheiro no ATM
` + "`/lojas`" + ` e ` + "`/loja`" + ` lojas de armas, comida, geral, caçador e mercado negro
` + "`/perfil`" + ` e ` + "`/top`" + ` suas estatísticas e o ranking
` + "`/vault`" + ` seu cofre pessoal
` + "`/tpa nome`" + ` teleporte até um amigo
` + "`/lojaxp`" + ` troque XP por equipamento

**Dica:** dinheiro no bolso cai quando você morre. Guarde no ATM.`

const textoTicket = `Precisa de ajuda, quer denunciar alguém ou achou um bug?
Clique no botão abaixo. Abre um canal privado só entre você e a staff.

Mande prints ou vídeo sempre que puder.`
