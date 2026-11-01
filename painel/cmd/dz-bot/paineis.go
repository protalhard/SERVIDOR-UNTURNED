package main

// Painéis fixos com botões (vincular Steam, stats, alerta de raid, ticket com motivo,
// regras de raid, loja VIP), boas-vindas no #bem-vindo, banimentos públicos e
// alerta de raid na DM de quem vinculou a Steam.

import (
	"fmt"
	"log"
	"regexp"
	"strings"
	"time"

	"github.com/bwmarrin/discordgo"
	"github.com/protalhard/servidor-unturned/painel/dados"
)

const siteURL = "http://54.94.93.87"

var chaveVinculo []byte

// garantirPainel edita a mensagem do bot com esse título no canal (ou posta se não existir).
func garantirPainel(s *discordgo.Session, canal, titulo string, embeds []*discordgo.MessageEmbed, comps []discordgo.MessageComponent) {
	mu.Lock()
	ch := estado.Canais[canal]
	mu.Unlock()
	if ch == "" || s.State == nil || s.State.User == nil {
		return
	}
	msgs, err := s.ChannelMessages(ch, 50, "", "", "")
	if err != nil {
		log.Printf("painel %s: %v", canal, err)
		return
	}
	if comps == nil {
		comps = []discordgo.MessageComponent{}
	}
	for _, m := range msgs {
		if m.Author == nil || m.Author.ID != s.State.User.ID || len(m.Embeds) == 0 || m.Embeds[0].Title != titulo {
			continue
		}
		if _, err := s.ChannelMessageEditComplex(&discordgo.MessageEdit{ID: m.ID, Channel: ch, Embeds: &embeds, Components: &comps}); err != nil {
			log.Printf("painel %s: %v", canal, err)
		}
		return
	}
	if _, err := s.ChannelMessageSendComplex(ch, &discordgo.MessageSend{Embeds: embeds, Components: comps}); err != nil {
		log.Printf("painel %s: %v", canal, err)
	}
}

func botao(rotulo, id, emoji string, estilo discordgo.ButtonStyle) discordgo.Button {
	return discordgo.Button{Label: rotulo, CustomID: id, Style: estilo, Emoji: &discordgo.ComponentEmoji{Name: emoji}}
}

func linha(b ...discordgo.MessageComponent) discordgo.ActionsRow {
	return discordgo.ActionsRow{Components: b}
}

func garantirPaineis(s *discordgo.Session) {
	defer func() {
		if r := recover(); r != nil {
			log.Println("painéis:", r)
		}
	}()
	time.Sleep(5 * time.Second)
	mu.Lock()
	cv := estado.Canais["vincular"]
	mu.Unlock()

	garantirPainel(s, "vincular", "🔗 Vincule sua Steam", []*discordgo.MessageEmbed{{
		Title: "🔗 Vincule sua Steam", Color: corLaranja,
		Description: "**Ganhe na hora:**\n" +
			"🎁 **Kit Discord** no jogo: `/kit discord`\n" +
			"🚨 **Alerta de raid** na sua DM quando mexerem na sua base\n" +
			"📊 **Suas estatísticas** aqui no Discord\n\n" +
			"É só clicar no botão e entrar com a Steam (login no site oficial da Steam, a gente não vê sua senha).",
		Image: &discordgo.MessageEmbedImage{URL: capaURL}, Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}, []discordgo.MessageComponent{linha(
		botao("Vincular Steam", "vincular", "🔗", discordgo.PrimaryButton),
		botao("Revincular", "vincular_r", "🔄", discordgo.SecondaryButton),
	)})
	garantirPainel(s, "stats", "📊 Suas estatísticas", []*discordgo.MessageEmbed{{
		Title: "📊 Suas estatísticas", Color: corLaranja,
		Description: "**PvP e PvE num clique** (só você vê a resposta)\n" +
			"⚔️ **PvP**: abates, mortes, K/D, tiros na cabeça\n" +
			"🧟 **PvE**: zumbis, megas, tempo jogado\n" +
			"🏆 **Ranking**: o top 10 e a sua posição\n\n" +
			fmt.Sprintf("Precisa da Steam vinculada em <#%s>.", cv),
		Image: &discordgo.MessageEmbedImage{URL: capaURL}, Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}, []discordgo.MessageComponent{linha(
		botao("Estatísticas PvP", "stats_pvp", "⚔️", discordgo.PrimaryButton),
		botao("Estatísticas PvE", "stats_pve", "🧟", discordgo.SuccessButton),
		botao("Ranking", "stats_rank", "🏆", discordgo.SecondaryButton),
		botao("Vincular Steam", "vincular_2", "🔗", discordgo.SecondaryButton),
	)})
	garantirPainel(s, "raid", "🚨 Alerta de raid", []*discordgo.MessageEmbed{{
		Title: "🚨 Alerta de raid", Color: corVermelho,
		Description: "**Mexeram na sua base? Você fica sabendo.**\n" +
			"• O bot te avisa na **DM** na hora, com quem está atacando\n" +
			"• Liga e desliga quando quiser\n\n" +
			fmt.Sprintf("Precisa: Steam vinculada em <#%s> e DM aberta pra este servidor.", cv),
		Image: &discordgo.MessageEmbedImage{URL: capaURL}, Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}, []discordgo.MessageComponent{linha(
		botao("Ativar/Desativar alertas", "alerta_raid", "🚨", discordgo.DangerButton),
		botao("Vincular Steam", "vincular_3", "🔗", discordgo.PrimaryButton),
	)})
	garantirPainel(s, "ticket", "🎫 Suporte", []*discordgo.MessageEmbed{{
		Title: "🎫 Suporte", Color: corLaranja,
		Description: "**Fale com a staff**\n" +
			"• Escolha o **motivo** na lista abaixo\n" +
			"• Seja direto e mande **provas** (print ou vídeo)\n" +
			"• Pedido de unban de outra pessoa = ticket ignorado\n\n" +
			"Abre um canal privado só entre você e a staff.",
		Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}, []discordgo.MessageComponent{linha(discordgo.SelectMenu{
		CustomID: "ticket_motivo", Placeholder: "Selecione o motivo do ticket",
		Options: []discordgo.SelectMenuOption{
			{Label: "Denúncia de jogador", Value: "denuncia", Emoji: &discordgo.ComponentEmoji{Name: "🚩"}, Description: "Hack, abuso de bug, quebra de regra"},
			{Label: "Bug ou problema no servidor", Value: "bug", Emoji: &discordgo.ComponentEmoji{Name: "🐞"}},
			{Label: "Perdi itens / reembolso", Value: "itens", Emoji: &discordgo.ComponentEmoji{Name: "🎒"}},
			{Label: "VIP e pagamentos", Value: "vip", Emoji: &discordgo.ComponentEmoji{Name: "💎"}},
			{Label: "Pedido de unban", Value: "unban", Emoji: &discordgo.ComponentEmoji{Name: "🔓"}, Description: "Só o seu próprio ban"},
			{Label: "Outro assunto", Value: "outro", Emoji: &discordgo.ComponentEmoji{Name: "💬"}},
		},
	})})
	garantirPainel(s, "regrasraid", "🏠 Raid e proteção de bases", []*discordgo.MessageEmbed{embedRegrasRaid()}, nil)
	garantirPainel(s, "lojavip", "💎 Loja VIP", []*discordgo.MessageEmbed{{
		Title: "💎 Loja VIP", Color: corAmbar, URL: siteURL + "/loja",
		Description: "Apoie o servidor e sobreviva com estilo:\n" +
			"🎒 **Kit Iniciante** · R$ 6,40\n" +
			"🔫 **Bandido** (+5 kits) · R$ 19,40\n" +
			"🪖 **Mercenário** (+8 kits) · R$ 30,90\n" +
			"🎖️ **Comandante** (+11 kits) · R$ 45,30\n" +
			"💣 **Kit Especial** (Zona Quente, Base ou Raid) · R$ 21,60\n" +
			"🧰 **Cofres** Mochila 8×7 · Bunker 12×9 · Fortaleza 15×14 · a partir de R$ 13,60\n" +
			"👑 **DZ Supremo** (tudo, 35 dias) · R$ 77,70  |  **Vitalício** · R$ 255,90\n\n" +
			"Todos por 30 dias, com tag no chat e cargo VIP aqui no Discord.\n**O pagamento abre em breve.**",
		Image: &discordgo.MessageEmbedImage{URL: capaURL}, Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}, []discordgo.MessageComponent{linha(
		discordgo.Button{Label: "Ver a Loja VIP", Style: discordgo.LinkButton, URL: siteURL + "/loja", Emoji: &discordgo.ComponentEmoji{Name: "💎"}},
	)})
	log.Println("painéis conferidos")
}

func embedRegrasRaid() *discordgo.MessageEmbed {
	cfg := fonte.LerConfig()
	ini, fim := cfg.RaidInicioHora, cfg.RaidFimHora
	if ini == 0 && fim == 0 {
		ini, fim = 18, 23
	}
	dias := cfg.RaidDias
	if dias == "" || dias == "todos" {
		dias = "todos os dias"
	}
	horario := fmt.Sprintf("⏰ **Raid liberado das %02dh às %02dh** (horário de Brasília), %s.\nFora desse horário, explosivo e dano em base não funcionam.", ini, fim, dias)
	if strings.EqualFold(cfg.RaidHorarioLigado, "false") {
		horario = "⏰ **Raid liberado a qualquer hora.**"
	}
	min := cfg.RaidMinutosOffline
	if min == 0 {
		min = 10
	}
	ren := cfg.RaidDiasRenovar
	if ren == 0 {
		ren = 3
	}
	desc := horario + "\n\n" +
		fmt.Sprintf("🛡️ **Proteção offline:** base com **Claim Flag** fica protegida quando todos do grupo estão offline há mais de %d minutos.\n\n", min) +
		fmt.Sprintf("🚩 **Renove a Claim Flag:** passe perto dela pelo menos 1 vez a cada %d dias. Base abandonada perde a proteção.\n\n", ren) +
		"⚠️ **Sem Claim Flag = sem proteção.**\n\n" +
		"🔔 Vincule a Steam e ative o alerta pra saber na hora quando mexerem na sua base.\n" +
		"No jogo: `/raid` mostra se o raid está aberto agora."
	return &discordgo.MessageEmbed{Title: "🏠 Raid e proteção de bases", Description: desc, Color: corLaranja,
		Footer: &discordgo.MessageEmbedFooter{Text: rodape}}
}

// ---------------- vínculo Steam ----------------

func linkVincular(discord string) string {
	e := time.Now().Add(15 * time.Minute).Unix()
	return fmt.Sprintf("%s/vincular?d=%s&e=%d&s=%s", siteURL, discord, e, dados.AssinarVinculo(chaveVinculo, discord, e))
}

func usuario(i *discordgo.InteractionCreate) *discordgo.User {
	if i.Member != nil && i.Member.User != nil {
		return i.Member.User
	}
	return i.User
}

func responderComps(s *discordgo.Session, i *discordgo.InteractionCreate, e *discordgo.MessageEmbed, comps []discordgo.MessageComponent) {
	_ = s.InteractionRespond(i.Interaction, &discordgo.InteractionResponse{
		Type: discordgo.InteractionResponseChannelMessageWithSource,
		Data: &discordgo.InteractionResponseData{Embeds: []*discordgo.MessageEmbed{e}, Components: comps, Flags: discordgo.MessageFlagsEphemeral},
	})
}

func responderVincular(s *discordgo.Session, i *discordgo.InteractionCreate) {
	u := usuario(i)
	if u == nil {
		return
	}
	txt := "Clique no botão e faça login na **Steam** 👇\nO login é na página oficial da Steam. O link vale 15 minutos."
	if st := fonte.SteamDoDiscord(u.ID); st != "" {
		txt = fmt.Sprintf("Sua conta já está vinculada à Steam `%s`.\nPra trocar, clique no botão e entre com a outra Steam.", st)
	}
	responderComps(s, i, &discordgo.MessageEmbed{Title: "🔗 Conectar Steam", Description: txt, Color: corLaranja},
		[]discordgo.MessageComponent{linha(discordgo.Button{Label: "Conectar Steam", Style: discordgo.LinkButton, URL: linkVincular(u.ID)})})
}

func precisaVincular(s *discordgo.Session, i *discordgo.InteractionCreate) {
	mu.Lock()
	cv := estado.Canais["vincular"]
	mu.Unlock()
	responderComps(s, i, &discordgo.MessageEmbed{Title: "🔗 Vincule sua Steam primeiro", Color: corVermelho,
		Description: fmt.Sprintf("Clique no botão abaixo e entre com a Steam. Depois volte aqui. (Ou use o canal <#%s>.)", cv)},
		[]discordgo.MessageComponent{linha(discordgo.Button{Label: "Conectar Steam", Style: discordgo.LinkButton, URL: linkVincular(usuario(i).ID)})})
}

// conferirVinculos dá o cargo e manda DM pra quem acabou de vincular.
func conferirVinculos(s *discordgo.Session) {
	l := fonte.LerVinculos()
	mu.Lock()
	if estado.Vinculados == nil {
		estado.Vinculados = map[string]string{}
	}
	guild, cargo := estado.Guild, estado.Cargos["vinculado"]
	var novos []dados.Vinculo
	for _, v := range l {
		if estado.Vinculados[v.Discord] != v.Steam {
			estado.Vinculados[v.Discord] = v.Steam
			novos = append(novos, v)
		}
	}
	if len(novos) > 0 {
		salvarEstado()
	}
	mu.Unlock()
	for _, v := range novos {
		if guild != "" && cargo != "" {
			_ = s.GuildMemberRoleAdd(guild, v.Discord, cargo)
		}
		dm(s, v.Discord, &discordgo.MessageEmbed{Title: "✅ Steam vinculada!", Color: corVerde,
			Description: "Sua conta do Discord agora está ligada ao jogo.\n\n" +
				"🎁 Entre no servidor e digite `/kit discord`\n" +
				"🚨 O alerta de raid já está **ligado**: te aviso aqui quando mexerem na sua base.\n" +
				"📊 Veja suas estatísticas no canal de stats.",
			Footer: &discordgo.MessageEmbedFooter{Text: rodape}})
	}
}

func dm(s *discordgo.Session, usuario string, e *discordgo.MessageEmbed) {
	ch, err := s.UserChannelCreate(usuario)
	if err != nil {
		return
	}
	_, _ = s.ChannelMessageSendEmbed(ch.ID, e)
}

// ---------------- alerta de raid ----------------

func alternarAlerta(s *discordgo.Session, i *discordgo.InteractionCreate) {
	u := usuario(i)
	if fonte.SteamDoDiscord(u.ID) == "" {
		precisaVincular(s, i)
		return
	}
	mu.Lock()
	if estado.AlertaDesligado == nil {
		estado.AlertaDesligado = map[string]bool{}
	}
	desligado := !estado.AlertaDesligado[u.ID]
	estado.AlertaDesligado[u.ID] = desligado
	salvarEstado()
	mu.Unlock()
	e := &discordgo.MessageEmbed{Title: "🚨 Alerta de raid", Color: corVerde, Description: "**Ligado ✅**\nVocê vai receber aviso na DM quando mexerem na sua base."}
	if desligado {
		e = &discordgo.MessageEmbed{Title: "🚨 Alerta de raid", Color: corVermelho, Description: "**Desligado ❌**\nVocê não vai mais receber esses avisos na DM."}
	}
	responder(s, i, e, true)
}

// linha do raid_log: "dd/MM HH:mm RAID: Atacante (steam) atacando base de Dono (steam) em X Z"
var reRaid = regexp.MustCompile(`RAID: (.*) \((\d+)\) atacando base de (.*) \((\d+)\)`)

func avisarRaidDM(s *discordgo.Session, l string) {
	m := reRaid.FindStringSubmatch(l)
	if m == nil {
		return
	}
	atacante, dono := m[1], m[4]
	for _, v := range fonte.LerVinculos() {
		if v.Steam != dono {
			continue
		}
		mu.Lock()
		off := estado.AlertaDesligado[v.Discord]
		ultimo := ultimoAlerta[v.Discord]
		mu.Unlock()
		if off || time.Since(ultimo) < 2*time.Minute { // no máximo 1 aviso a cada 2 minutos
			return
		}
		mu.Lock()
		ultimoAlerta[v.Discord] = time.Now()
		mu.Unlock()
		dm(s, v.Discord, &discordgo.MessageEmbed{Title: "🚨 SUA BASE ESTÁ SENDO ATACADA!", Color: corVermelho,
			Description: fmt.Sprintf("**%s** está atacando a sua base agora.\nEntre no servidor pra defender!\n\n`%s`", atacante, ipServidor),
			Timestamp:   time.Now().Format(time.RFC3339), Footer: &discordgo.MessageEmbedFooter{Text: rodape}})
		return
	}
}

var ultimoAlerta = map[string]time.Time{}

// ---------------- stats ----------------

func fichaDoDiscord(id string) *dados.Ficha {
	st := fonte.SteamDoDiscord(id)
	if st == "" {
		return nil
	}
	cfg := fonte.LerConfig()
	for _, f := range fonte.LerFichas(cfg.Patentes) {
		if f.ID == st {
			f := f
			return &f
		}
	}
	return &dados.Ficha{ID: st}
}

func responderStats(s *discordgo.Session, i *discordgo.InteractionCreate, tipo string) {
	u := usuario(i)
	if tipo == "rank" {
		e := embedTop("abates")
		if f := fichaDoDiscord(u.ID); f != nil && f.Nome != "" {
			e.Description += fmt.Sprintf("\n**Você:** #%d • %d abates", f.Posicao, f.Abates)
		}
		responder(s, i, e, true)
		return
	}
	f := fichaDoDiscord(u.ID)
	if f == nil {
		precisaVincular(s, i)
		return
	}
	if f.Nome == "" {
		responder(s, i, &discordgo.MessageEmbed{Color: corAmbar, Description: "Sua Steam está vinculada, mas você ainda não jogou no servidor. Entre uma vez e volte aqui!"}, true)
		return
	}
	titulo := fmt.Sprintf("[%s] %s — Lv.%d", f.Patente, f.Nome, f.Nivel)
	var campos []*discordgo.MessageEmbedField
	if tipo == "pvp" {
		titulo = "⚔️ PvP • " + titulo
		campos = []*discordgo.MessageEmbedField{
			{Name: "Abates", Value: fmt.Sprint(f.Abates), Inline: true},
			{Name: "Mortes", Value: fmt.Sprint(f.Mortes), Inline: true},
			{Name: "K/D", Value: fmt.Sprintf("%.2f", f.Kd), Inline: true},
			{Name: "Na cabeça", Value: fmt.Sprintf("%d (%d%%)", f.Cabeca, f.PctCabeca), Inline: true},
			{Name: "Ranking", Value: fmt.Sprintf("#%d", f.Posicao), Inline: true},
			{Name: "Tempo jogado", Value: dados.Tempo(f.Segundos), Inline: true},
		}
	} else {
		titulo = "🧟 PvE • " + titulo
		campos = []*discordgo.MessageEmbedField{
			{Name: "Zumbis", Value: fmt.Sprint(f.Zumbis), Inline: true},
			{Name: "Megas", Value: fmt.Sprint(f.Megas), Inline: true},
			{Name: "XP", Value: fmt.Sprint(f.Xp), Inline: true},
			{Name: "Tempo jogado", Value: dados.Tempo(f.Segundos), Inline: true},
		}
	}
	responder(s, i, &discordgo.MessageEmbed{Title: titulo, Color: corLaranja, Fields: campos, URL: siteURL + "/jogador/" + f.ID,
		Footer: &discordgo.MessageEmbedFooter{Text: "DAY Z BRASIL • California 2 • Steam " + f.ID}}, true)
}

// ---------------- banimentos públicos ----------------

// linha do admin_log: "dd/MM HH:mm:ss Admin (id): BAN Nome (steam) permanente: motivo"
var reBan = regexp.MustCompile(`: BAN (.*) \((\d+)\) ([^:]+): (.*)$`)

func postarBan(s *discordgo.Session, l string) {
	m := reBan.FindStringSubmatch(l)
	if m == nil {
		return
	}
	nome, steam, dur, motivo := m[1], m[2], m[3], m[4]
	mu.Lock()
	defer mu.Unlock()
	postar(s, "banimentos", &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
		Title: "🔨 Jogador banido", Color: corVermelho,
		Description: fmt.Sprintf("**%s** foi banido do servidor.\n**Duração:** %s\n**Motivo:** %s\n\n[Perfil Steam](https://steamcommunity.com/profiles/%s)", nome, dur, motivo, steam),
		Timestamp:   time.Now().Format(time.RFC3339), Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}})
}

// ---------------- botões ----------------

// tratarComponente cuida dos botões novos. Devolve false se não for um deles.
func tratarComponente(s *discordgo.Session, i *discordgo.InteractionCreate) bool {
	d := i.MessageComponentData()
	switch {
	case d.CustomID == "vincular" || strings.HasPrefix(d.CustomID, "vincular_"):
		responderVincular(s, i)
	case d.CustomID == "alerta_raid":
		alternarAlerta(s, i)
	case d.CustomID == "stats_pvp":
		responderStats(s, i, "pvp")
	case d.CustomID == "stats_pve":
		responderStats(s, i, "pve")
	case d.CustomID == "stats_rank":
		responderStats(s, i, "rank")
	case d.CustomID == "ticket_motivo":
		motivo := "outro"
		if len(d.Values) > 0 {
			motivo = d.Values[0]
		}
		abrirTicket(s, i, motivo)
	default:
		return false
	}
	return true
}
