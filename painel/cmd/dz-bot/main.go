// dz-bot: bot do Discord do DAY Z BRASIL OFICIAL.
// Na primeira vez monta o servidor inteiro (cargos, canais, regras, painéis)
// e depois mantém atualizados o status do servidor e o ranking.
package main

import (
	_ "embed"
	"encoding/base64"
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"os"
	"os/signal"
	"path/filepath"
	"strings"
	"sync"
	"syscall"
	"time"

	"github.com/bwmarrin/discordgo"
	"github.com/protalhard/servidor-unturned/painel/dados"
)

//go:embed icone.png
var iconePNG []byte

type Estado struct {
	Guild      string            `json:"guild"`
	Montado    bool              `json:"montado"`
	Canais     map[string]string `json:"canais"`
	Cargos     map[string]string `json:"cargos"`
	MsgStatus  string            `json:"msgStatus"`
	MsgTop     string            `json:"msgTop"`
	AvatarFeito bool             `json:"avatarFeito"`
}

var (
	fonte     dados.Fonte
	arqEstado string
	estado    = &Estado{Canais: map[string]string{}, Cargos: map[string]string{}}
	mu        sync.Mutex
)

func main() {
	tokenArq := flag.String("token", "/home/unturned/discord-token", "arquivo com o token do bot")
	pasta := flag.String("dados", dados.PastaPadrao, "pasta do plugin SobrevivenciaCore")
	flag.StringVar(&arqEstado, "estado", "/home/unturned/dz-bot/estado.json", "arquivo de estado do bot")
	flag.Parse()
	fonte = dados.Fonte{Pasta: *pasta}

	tk, err := os.ReadFile(*tokenArq)
	if err != nil {
		log.Fatalf("sem token em %s: %v", *tokenArq, err)
	}
	carregarEstado()

	dg, err := discordgo.New("Bot " + strings.TrimSpace(string(tk)))
	if err != nil {
		log.Fatal(err)
	}
	dg.Identify.Intents = discordgo.IntentsGuilds | discordgo.IntentsGuildMembers
	dg.AddHandler(aoFicarPronto)
	dg.AddHandler(aoEntrarMembro)
	dg.AddHandler(aoInteragir)
	dg.AddHandler(func(s *discordgo.Session, g *discordgo.GuildCreate) { go prepararGuild(s, g.Guild) })

	if err := dg.Open(); err != nil {
		log.Fatal(err)
	}
	defer dg.Close()
	log.Println("dz-bot ligado")

	go laco(dg, 60*time.Second, atualizarStatus)
	go laco(dg, 10*time.Minute, atualizarTop)
	go laco(dg, 10*time.Minute, atualizarContador)

	sc := make(chan os.Signal, 1)
	signal.Notify(sc, syscall.SIGINT, syscall.SIGTERM)
	<-sc
}

func laco(s *discordgo.Session, cada time.Duration, f func(*discordgo.Session)) {
	time.Sleep(15 * time.Second)
	for {
		func() {
			defer func() {
				if r := recover(); r != nil {
					log.Println("erro:", r)
				}
			}()
			f(s)
		}()
		time.Sleep(cada)
	}
}

func carregarEstado() {
	b, err := os.ReadFile(arqEstado)
	if err == nil {
		_ = json.Unmarshal(b, estado)
	}
	if estado.Canais == nil {
		estado.Canais = map[string]string{}
	}
	if estado.Cargos == nil {
		estado.Cargos = map[string]string{}
	}
}

func salvarEstado() {
	_ = os.MkdirAll(filepath.Dir(arqEstado), 0o755)
	b, _ := json.MarshalIndent(estado, "", "  ")
	_ = os.WriteFile(arqEstado+".tmp", b, 0o600)
	_ = os.Rename(arqEstado+".tmp", arqEstado)
}

func aoFicarPronto(s *discordgo.Session, r *discordgo.Ready) {
	log.Printf("conectado como %s em %d servidor(es)", r.User.Username, len(r.Guilds))
	_ = s.UpdateGameStatus(0, "DAY Z BRASIL OFICIAL")
	mu.Lock()
	feito := estado.AvatarFeito
	mu.Unlock()
	if !feito {
		avatar := "data:image/png;base64," + base64.StdEncoding.EncodeToString(iconePNG)
		if _, err := s.UserUpdate("DZ Brasil", avatar, ""); err != nil {
			log.Println("avatar:", err)
		} else {
			mu.Lock()
			estado.AvatarFeito = true
			salvarEstado()
			mu.Unlock()
		}
	}
	registrarComandos(s, r.User.ID, r.Guilds)
}

// ---------------- Montagem do servidor ----------------

const (
	verCanal  = discordgo.PermissionViewChannel
	enviar    = discordgo.PermissionSendMessages
	historico = discordgo.PermissionReadMessageHistory
	reagir    = discordgo.PermissionAddReactions
	conectar  = discordgo.PermissionVoiceConnect
)

type canalDef struct {
	chave, nome, topico string
	tipo                discordgo.ChannelType
	soLeitura, oculto   bool
}

type categoriaDef struct {
	chave, nome string
	canais      []canalDef
}

var estrutura = []categoriaDef{
	{"cat_info", "📌 INFORMAÇÕES", []canalDef{
		{"bemvindo", "👋│bem-vindo", "Comece por aqui.", discordgo.ChannelTypeGuildText, true, false},
		{"regras", "📜│regras", "Regras do jogo e do Discord.", discordgo.ChannelTypeGuildText, true, false},
		{"comojogar", "🎮│como-jogar", "Como entrar e os comandos do servidor.", discordgo.ChannelTypeGuildText, true, false},
		{"anuncios", "📢│anúncios", "Novidades do servidor.", discordgo.ChannelTypeGuildText, true, false},
		{"wipe", "🔄│wipe", "Datas de wipe e troca de mapa.", discordgo.ChannelTypeGuildText, true, false},
		{"updates", "🛠│updates", "Mudanças e correções.", discordgo.ChannelTypeGuildText, true, false},
	}},
	{"cat_servidor", "🎮 SERVIDOR", []canalDef{
		{"status", "🟢│status-servidor", "Atualizado a cada minuto.", discordgo.ChannelTypeGuildText, true, false},
		{"top", "🏆│top-jogadores", "Ranking atualizado a cada 10 minutos.", discordgo.ChannelTypeGuildText, true, false},
		{"raid", "🚨│raid-alert", "Alertas de raid (em breve).", discordgo.ChannelTypeGuildText, true, false},
		{"banimentos", "🔨│banimentos", "Jogadores banidos.", discordgo.ChannelTypeGuildText, true, false},
	}},
	{"cat_comunidade", "💬 COMUNIDADE", []canalDef{
		{"geral", "💬│chat-geral", "Papo livre. Respeito sempre.", discordgo.ChannelTypeGuildText, false, false},
		{"grupo", "🤝│buscar-grupo", "Procure time pra jogar.", discordgo.ChannelTypeGuildText, false, false},
		{"midia", "📸│mídia", "Prints e clipes.", discordgo.ChannelTypeGuildText, false, false},
		{"sugestoes", "💡│sugestões", "Ideias pro servidor.", discordgo.ChannelTypeGuildText, false, false},
	}},
	{"cat_suporte", "🎫 SUPORTE", []canalDef{
		{"ticket", "🎫│abrir-ticket", "Fale com a staff.", discordgo.ChannelTypeGuildText, true, false},
	}},
	{"cat_voz", "🔊 VOZ", []canalDef{
		{"voz_geral", "🔊 Geral", "", discordgo.ChannelTypeGuildVoice, false, false},
		{"voz_squad1", "🎯 Squad 1", "", discordgo.ChannelTypeGuildVoice, false, false},
		{"voz_squad2", "🎯 Squad 2", "", discordgo.ChannelTypeGuildVoice, false, false},
	}},
	{"cat_staff", "🛡 STAFF", []canalDef{
		{"staff", "🛡│staff", "Só staff.", discordgo.ChannelTypeGuildText, false, true},
		{"logs", "📋│logs", "Registro do bot.", discordgo.ChannelTypeGuildText, false, true},
	}},
}

func prepararGuild(s *discordgo.Session, g *discordgo.Guild) {
	mu.Lock()
	defer mu.Unlock()
	if estado.Guild != "" && estado.Guild != g.ID {
		log.Printf("ignorando servidor %s (o bot já cuida de %s)", g.Name, estado.Guild)
		return
	}
	estado.Guild = g.ID
	if estado.Montado {
		return
	}
	log.Println("montando o servidor", g.Name)
	if err := montar(s, g); err != nil {
		log.Println("montagem:", err)
		salvarEstado()
		return
	}
	estado.Montado = true
	salvarEstado()
	log.Println("servidor montado")
}

func montar(s *discordgo.Session, g *discordgo.Guild) error {
	icone := "data:image/png;base64," + base64.StdEncoding.EncodeToString(iconePNG)
	if _, err := s.GuildEdit(g.ID, &discordgo.GuildParams{Name: nomeServidor, Icon: icone}); err != nil {
		log.Println("editar servidor:", err)
	}

	// cargos
	cargos := []struct {
		chave, nome string
		cor         int
		perms       int64
	}{
		{"staff", "🛡 Staff", corLaranja, discordgo.PermissionManageMessages | discordgo.PermissionKickMembers |
			discordgo.PermissionBanMembers | discordgo.PermissionModerateMembers | discordgo.PermissionManageChannels},
		{"vip", "⭐ VIP", corAmbar, 0},
		{"sobrevivente", "Sobrevivente", 0xC9CDD3, 0},
	}
	for _, c := range cargos {
		if estado.Cargos[c.chave] != "" {
			continue
		}
		cor, perms, hoist := c.cor, c.perms, true
		r, err := s.GuildRoleCreate(g.ID, &discordgo.RoleParams{Name: c.nome, Color: &cor, Permissions: &perms, Hoist: &hoist})
		if err != nil {
			return fmt.Errorf("cargo %s: %w", c.nome, err)
		}
		estado.Cargos[c.chave] = r.ID
	}

	// servidor novo: apaga os canais padrão do Discord
	if chs, err := s.GuildChannels(g.ID); err == nil && len(chs) <= 6 {
		for _, ch := range chs {
			if !ehNosso(ch.ID) {
				_, _ = s.ChannelDelete(ch.ID)
			}
		}
	}

	everyone := g.ID
	staff := estado.Cargos["staff"]
	for _, cat := range estrutura {
		if estado.Canais[cat.chave] == "" {
			var ow []*discordgo.PermissionOverwrite
			if cat.chave == "cat_staff" {
				ow = ocultoPraTodos(everyone, staff)
			}
			ch, err := s.GuildChannelCreateComplex(g.ID, discordgo.GuildChannelCreateData{
				Name: cat.nome, Type: discordgo.ChannelTypeGuildCategory, PermissionOverwrites: ow})
			if err != nil {
				return fmt.Errorf("categoria %s: %w", cat.nome, err)
			}
			estado.Canais[cat.chave] = ch.ID
		}
		for _, c := range cat.canais {
			if estado.Canais[c.chave] != "" {
				continue
			}
			var ow []*discordgo.PermissionOverwrite
			switch {
			case c.oculto:
				ow = ocultoPraTodos(everyone, staff)
			case c.soLeitura:
				ow = []*discordgo.PermissionOverwrite{
					{ID: everyone, Type: discordgo.PermissionOverwriteTypeRole, Allow: verCanal | historico | reagir, Deny: enviar},
					{ID: staff, Type: discordgo.PermissionOverwriteTypeRole, Allow: enviar},
				}
			}
			ch, err := s.GuildChannelCreateComplex(g.ID, discordgo.GuildChannelCreateData{
				Name: c.nome, Type: c.tipo, Topic: c.topico, ParentID: estado.Canais[cat.chave], PermissionOverwrites: ow})
			if err != nil {
				return fmt.Errorf("canal %s: %w", c.nome, err)
			}
			estado.Canais[c.chave] = ch.ID
		}
	}

	// contador de jogadores (canal de voz trancado no topo)
	if estado.Canais["contador"] == "" {
		ch, err := s.GuildChannelCreateComplex(g.ID, discordgo.GuildChannelCreateData{
			Name: "🔴 Servidor offline", Type: discordgo.ChannelTypeGuildVoice, Position: 0,
			PermissionOverwrites: []*discordgo.PermissionOverwrite{
				{ID: everyone, Type: discordgo.PermissionOverwriteTypeRole, Allow: verCanal, Deny: conectar},
			}})
		if err == nil {
			estado.Canais["contador"] = ch.ID
		}
	}
	salvarEstado()

	postar(s, "bemvindo", &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
		Title:       "Bem-vindo ao DAY Z BRASIL OFICIAL",
		Description: fmt.Sprintf(textoBemVindo, estado.Canais["regras"], estado.Canais["comojogar"], estado.Canais["status"], estado.Canais["ticket"]),
		Color:       corLaranja, Image: &discordgo.MessageEmbedImage{URL: capaURL},
		Thumbnail: &discordgo.MessageEmbedThumbnail{URL: iconeURL}, Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}})
	postar(s, "regras", &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{
		{Title: "🎮 Regras do jogo", Description: textoRegrasJogo, Color: corLaranja},
		{Title: "💬 Regras do Discord", Description: textoRegrasDiscord, Color: corAmbar, Footer: &discordgo.MessageEmbedFooter{Text: rodape}},
	}})
	postar(s, "comojogar", &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
		Title: "Como jogar", Description: textoComoJogar, Color: corLaranja,
		Thumbnail: &discordgo.MessageEmbedThumbnail{URL: iconeURL}, Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}})
	postar(s, "ticket", &discordgo.MessageSend{
		Embeds: []*discordgo.MessageEmbed{{Title: "🎫 Suporte", Description: textoTicket, Color: corLaranja,
			Footer: &discordgo.MessageEmbedFooter{Text: rodape}}},
		Components: []discordgo.MessageComponent{discordgo.ActionsRow{Components: []discordgo.MessageComponent{
			discordgo.Button{Label: "Abrir ticket", Style: discordgo.PrimaryButton, CustomID: "abrir_ticket", Emoji: &discordgo.ComponentEmoji{Name: "🎫"}},
		}}},
	})
	postar(s, "raid", &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
		Title: "🚨 Alerta de raid", Color: corVermelho,
		Description: "Em breve: vincule sua Steam e receba aviso na DM quando mexerem na sua base.",
	}}})
	return nil
}

func ehNosso(id string) bool {
	for _, v := range estado.Canais {
		if v == id {
			return true
		}
	}
	return false
}

func ocultoPraTodos(everyone, staff string) []*discordgo.PermissionOverwrite {
	return []*discordgo.PermissionOverwrite{
		{ID: everyone, Type: discordgo.PermissionOverwriteTypeRole, Deny: verCanal},
		{ID: staff, Type: discordgo.PermissionOverwriteTypeRole, Allow: verCanal | enviar | historico},
	}
}

func postar(s *discordgo.Session, canal string, m *discordgo.MessageSend) {
	if id := estado.Canais[canal]; id != "" {
		if _, err := s.ChannelMessageSendComplex(id, m); err != nil {
			log.Printf("postar em %s: %v", canal, err)
		}
	}
}

// ---------------- Membros ----------------

func aoEntrarMembro(s *discordgo.Session, m *discordgo.GuildMemberAdd) {
	mu.Lock()
	cargo, geral, regras := estado.Cargos["sobrevivente"], estado.Canais["geral"], estado.Canais["regras"]
	mu.Unlock()
	if cargo != "" {
		_ = s.GuildMemberRoleAdd(m.GuildID, m.User.ID, cargo)
	}
	if geral != "" {
		_, _ = s.ChannelMessageSendComplex(geral, &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
			Description: fmt.Sprintf("👋 <@%s> chegou no **DAY Z BRASIL OFICIAL**! Leia as <#%s> e bom jogo.", m.User.ID, regras),
			Color:       corLaranja,
		}}})
	}
	if dm, err := s.UserChannelCreate(m.User.ID); err == nil {
		_, _ = s.ChannelMessageSendComplex(dm.ID, &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
			Title:       "Bem-vindo ao DAY Z BRASIL OFICIAL",
			Description: "Pra entrar: Unturned → Servidores → Internet → procure **DAY Z BRASIL**.\nNo jogo, digite `/menu` pra ver tudo.",
			Color:       corLaranja, Thumbnail: &discordgo.MessageEmbedThumbnail{URL: iconeURL},
			Footer: &discordgo.MessageEmbedFooter{Text: rodape},
		}}})
	}
}

// ---------------- Status, ranking e contador ----------------

func embedStatus() *discordgo.MessageEmbed {
	st := fonte.LerStatus()
	e := &discordgo.MessageEmbed{Title: "DAY Z BRASIL OFICIAL", Thumbnail: &discordgo.MessageEmbedThumbnail{URL: iconeURL},
		Footer: &discordgo.MessageEmbedFooter{Text: rodape}, Timestamp: time.Now().Format(time.RFC3339)}
	if !st.Online {
		e.Color = corVermelho
		e.Description = "🔴 **Offline** (reiniciando ou em manutenção)"
		return e
	}
	e.Color = corVerde
	e.Description = "🟢 **Online**"
	e.Fields = []*discordgo.MessageEmbedField{
		{Name: "Jogadores", Value: fmt.Sprintf("**%d/%d**", len(st.Jogadores), st.Max), Inline: true},
		{Name: "Mapa", Value: st.Mapa, Inline: true},
		{Name: "Conectar", Value: "`" + ipServidor + "`", Inline: true},
	}
	if len(st.Jogadores) > 0 {
		var b strings.Builder
		for i, j := range st.Jogadores {
			if i == 25 {
				fmt.Fprintf(&b, "… e mais %d", len(st.Jogadores)-25)
				break
			}
			fmt.Fprintf(&b, "`[%s]` %s • Lv.%d\n", j.Patente, j.Nome, j.Nivel)
		}
		e.Fields = append(e.Fields, &discordgo.MessageEmbedField{Name: "Online agora", Value: b.String()})
	}
	return e
}

func embedTop(por string) *discordgo.MessageEmbed {
	cfg := fonte.LerConfig()
	l := dados.Ordenar(fonte.LerFichas(cfg.Patentes), por)
	titulo := map[string]string{"abates": "abates", "kd": "K/D", "xp": "XP", "tempo": "tempo jogado", "zumbis": "zumbis"}[por]
	if titulo == "" {
		por, titulo = "abates", "abates"
	}
	var b strings.Builder
	medalha := []string{"🥇", "🥈", "🥉"}
	for i, f := range l {
		if i == 10 {
			break
		}
		pos := fmt.Sprintf("`%2d.`", i+1)
		if i < 3 {
			pos = medalha[i]
		}
		var v string
		switch por {
		case "kd":
			v = fmt.Sprintf("%.2f K/D", f.Kd)
		case "xp":
			v = fmt.Sprintf("%d XP", f.Xp)
		case "tempo":
			v = dados.Tempo(f.Segundos)
		case "zumbis":
			v = fmt.Sprintf("%d zumbis", f.Zumbis)
		default:
			v = fmt.Sprintf("%d abates • %.2f K/D", f.Abates, f.Kd)
		}
		fmt.Fprintf(&b, "%s **[%s] %s** Lv.%d — %s\n", pos, f.Patente, f.Nome, f.Nivel, v)
	}
	if b.Len() == 0 {
		b.WriteString("Ninguém no ranking ainda. Seja o primeiro!")
	}
	return &discordgo.MessageEmbed{Title: "🏆 Top 10 — " + titulo, Description: b.String(), Color: corLaranja,
		Footer: &discordgo.MessageEmbedFooter{Text: rodape}, Timestamp: time.Now().Format(time.RFC3339)}
}

// manterMensagem edita a mensagem salva; se não existir mais, posta outra.
func manterMensagem(s *discordgo.Session, canal string, msgID *string, e *discordgo.MessageEmbed) {
	mu.Lock()
	ch := estado.Canais[canal]
	id := *msgID
	mu.Unlock()
	if ch == "" {
		return
	}
	if id != "" {
		embeds := []*discordgo.MessageEmbed{e}
		if _, err := s.ChannelMessageEditComplex(&discordgo.MessageEdit{ID: id, Channel: ch, Embeds: &embeds}); err == nil {
			return
		}
	}
	m, err := s.ChannelMessageSendComplex(ch, &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{e}})
	if err != nil {
		log.Println("mensagem fixa:", err)
		return
	}
	mu.Lock()
	*msgID = m.ID
	salvarEstado()
	mu.Unlock()
}

func atualizarStatus(s *discordgo.Session) { manterMensagem(s, "status", &estado.MsgStatus, embedStatus()) }
func atualizarTop(s *discordgo.Session)    { manterMensagem(s, "top", &estado.MsgTop, embedTop("abates")) }

func atualizarContador(s *discordgo.Session) {
	mu.Lock()
	ch := estado.Canais["contador"]
	mu.Unlock()
	if ch == "" {
		return
	}
	st := fonte.LerStatus()
	nome := "🔴 Servidor offline"
	if st.Online {
		nome = fmt.Sprintf("🟢 Online: %d/%d", len(st.Jogadores), st.Max)
	}
	_, _ = s.ChannelEdit(ch, &discordgo.ChannelEdit{Name: nome})
}

// ---------------- Comandos e botões ----------------

func registrarComandos(s *discordgo.Session, appID string, guilds []*discordgo.Guild) {
	cmds := []*discordgo.ApplicationCommand{
		{Name: "status", Description: "Mostra se o servidor está online e quem está jogando"},
		{Name: "ip", Description: "Como conectar no servidor"},
		{Name: "top", Description: "Ranking dos jogadores", Options: []*discordgo.ApplicationCommandOption{{
			Type: discordgo.ApplicationCommandOptionString, Name: "por", Description: "Ordenar por",
			Choices: []*discordgo.ApplicationCommandOptionChoice{
				{Name: "Abates", Value: "abates"}, {Name: "K/D", Value: "kd"}, {Name: "XP", Value: "xp"},
				{Name: "Tempo jogado", Value: "tempo"}, {Name: "Zumbis", Value: "zumbis"},
			}}}},
		{Name: "perfil", Description: "Estatísticas de um jogador", Options: []*discordgo.ApplicationCommandOption{{
			Type: discordgo.ApplicationCommandOptionString, Name: "nome", Description: "Nome no jogo", Required: true}}},
	}
	for _, g := range guilds {
		if _, err := s.ApplicationCommandBulkOverwrite(appID, g.ID, cmds); err != nil {
			log.Println("comandos:", err)
		}
	}
}

func responder(s *discordgo.Session, i *discordgo.InteractionCreate, e *discordgo.MessageEmbed, privado bool) {
	var flags discordgo.MessageFlags
	if privado {
		flags = discordgo.MessageFlagsEphemeral
	}
	_ = s.InteractionRespond(i.Interaction, &discordgo.InteractionResponse{
		Type: discordgo.InteractionResponseChannelMessageWithSource,
		Data: &discordgo.InteractionResponseData{Embeds: []*discordgo.MessageEmbed{e}, Flags: flags},
	})
}

func aoInteragir(s *discordgo.Session, i *discordgo.InteractionCreate) {
	switch i.Type {
	case discordgo.InteractionApplicationCommand:
		d := i.ApplicationCommandData()
		switch d.Name {
		case "status":
			responder(s, i, embedStatus(), false)
		case "ip":
			responder(s, i, &discordgo.MessageEmbed{Title: "Como conectar", Color: corLaranja, Description: textoComoJogar}, true)
		case "top":
			por := "abates"
			if len(d.Options) > 0 {
				por = d.Options[0].StringValue()
			}
			responder(s, i, embedTop(por), false)
		case "perfil":
			responder(s, i, embedPerfil(d.Options[0].StringValue()), false)
		}
	case discordgo.InteractionMessageComponent:
		switch i.MessageComponentData().CustomID {
		case "abrir_ticket":
			abrirTicket(s, i)
		case "fechar_ticket":
			_ = s.InteractionRespond(i.Interaction, &discordgo.InteractionResponse{
				Type: discordgo.InteractionResponseChannelMessageWithSource,
				Data: &discordgo.InteractionResponseData{Content: "Fechando o ticket em 5 segundos..."}})
			canal := i.ChannelID
			go func() { time.Sleep(5 * time.Second); _, _ = s.ChannelDelete(canal) }()
		}
	}
}

func embedPerfil(nome string) *discordgo.MessageEmbed {
	cfg := fonte.LerConfig()
	nomeMin := strings.ToLower(nome)
	var achou *dados.Ficha
	l := fonte.LerFichas(cfg.Patentes)
	for idx := range l {
		if strings.ToLower(l[idx].Nome) == nomeMin {
			achou = &l[idx]
			break
		}
	}
	if achou == nil {
		for idx := range l {
			if strings.Contains(strings.ToLower(l[idx].Nome), nomeMin) {
				achou = &l[idx]
				break
			}
		}
	}
	if achou == nil {
		return &discordgo.MessageEmbed{Description: "Jogador não encontrado.", Color: corVermelho}
	}
	f := achou
	return &discordgo.MessageEmbed{
		Title: fmt.Sprintf("[%s] %s — Lv.%d", f.Patente, f.Nome, f.Nivel), Color: corLaranja,
		Fields: []*discordgo.MessageEmbedField{
			{Name: "Abates", Value: fmt.Sprint(f.Abates), Inline: true},
			{Name: "Mortes", Value: fmt.Sprint(f.Mortes), Inline: true},
			{Name: "K/D", Value: fmt.Sprintf("%.2f", f.Kd), Inline: true},
			{Name: "Na cabeça", Value: fmt.Sprintf("%d%%", f.PctCabeca), Inline: true},
			{Name: "Zumbis", Value: fmt.Sprint(f.Zumbis), Inline: true},
			{Name: "Tempo jogado", Value: dados.Tempo(f.Segundos), Inline: true},
			{Name: "XP", Value: fmt.Sprint(f.Xp), Inline: true},
			{Name: "Ranking", Value: fmt.Sprintf("#%d", f.Posicao), Inline: true},
		},
		Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}
}

func abrirTicket(s *discordgo.Session, i *discordgo.InteractionCreate) {
	if i.Member == nil {
		return
	}
	mu.Lock()
	cat, staff := estado.Canais["cat_suporte"], estado.Cargos["staff"]
	mu.Unlock()
	u := i.Member.User
	nome := "ticket-" + strings.ToLower(strings.Map(func(r rune) rune {
		if r >= 'a' && r <= 'z' || r >= 'A' && r <= 'Z' || r >= '0' && r <= '9' {
			return r
		}
		return -1
	}, u.Username))
	ch, err := s.GuildChannelCreateComplex(i.GuildID, discordgo.GuildChannelCreateData{
		Name: nome, Type: discordgo.ChannelTypeGuildText, ParentID: cat,
		PermissionOverwrites: []*discordgo.PermissionOverwrite{
			{ID: i.GuildID, Type: discordgo.PermissionOverwriteTypeRole, Deny: verCanal},
			{ID: u.ID, Type: discordgo.PermissionOverwriteTypeMember, Allow: verCanal | enviar | historico | discordgo.PermissionAttachFiles},
			{ID: staff, Type: discordgo.PermissionOverwriteTypeRole, Allow: verCanal | enviar | historico},
		}})
	if err != nil {
		responder(s, i, &discordgo.MessageEmbed{Description: "Não consegui abrir o ticket agora. Tente de novo.", Color: corVermelho}, true)
		return
	}
	_, _ = s.ChannelMessageSendComplex(ch.ID, &discordgo.MessageSend{
		Content: fmt.Sprintf("<@%s> <@&%s>", u.ID, staff),
		Embeds: []*discordgo.MessageEmbed{{Title: "🎫 Ticket aberto", Color: corLaranja,
			Description: "Conte o que aconteceu com o máximo de detalhes. Mande prints ou vídeo se tiver.\nA staff responde aqui."}},
		Components: []discordgo.MessageComponent{discordgo.ActionsRow{Components: []discordgo.MessageComponent{
			discordgo.Button{Label: "Fechar ticket", Style: discordgo.DangerButton, CustomID: "fechar_ticket", Emoji: &discordgo.ComponentEmoji{Name: "🔒"}},
		}}},
	})
	responder(s, i, &discordgo.MessageEmbed{Description: fmt.Sprintf("Ticket aberto: <#%s>", ch.ID), Color: corVerde}, true)
}
