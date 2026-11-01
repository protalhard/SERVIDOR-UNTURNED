package main

// Salas de voz particulares: quem entra em "➕ Criar sala" ganha uma sala própria
// e vira o dono. No chat da sala vem um painel pra trancar, liberar, expulsar e
// limitar vagas. Quem quer entrar numa sala trancada usa /pedir-sala e o dono
// aceita ou recusa. A sala some sozinha quando fica vazia.

import (
	"fmt"
	"log"
	"strconv"
	"strings"
	"time"

	"github.com/bwmarrin/discordgo"
)

const (
	falar      = discordgo.PermissionVoiceSpeak
	moverGente = discordgo.PermissionVoiceMoveMembers
	gerenciar  = discordgo.PermissionManageChannels
)

func aoMudarVoz(s *discordgo.Session, v *discordgo.VoiceStateUpdate) {
	if v.VoiceState == nil {
		return
	}
	mu.Lock()
	criar := estado.Canais["voz_criar"]
	mu.Unlock()
	if criar != "" && v.ChannelID == criar {
		go criarSala(s, v.GuildID, v.UserID, v.Member)
	}
	if v.BeforeUpdate != nil && v.BeforeUpdate.ChannelID != "" && v.BeforeUpdate.ChannelID != v.ChannelID {
		antes := v.BeforeUpdate.ChannelID
		go func() { time.Sleep(3 * time.Second); apagarSeVazia(s, v.GuildID, antes) }()
	}
}

func donoDaSala(canal string) string {
	mu.Lock()
	defer mu.Unlock()
	return estado.Salas[canal]
}

func salaDoDono(user string) string {
	mu.Lock()
	defer mu.Unlock()
	for c, d := range estado.Salas {
		if d == user {
			return c
		}
	}
	return ""
}

func nomeDe(m *discordgo.Member, u *discordgo.User) string {
	if m != nil && m.Nick != "" {
		return m.Nick
	}
	if m != nil && m.User != nil {
		u = m.User
	}
	if u == nil {
		return "jogador"
	}
	if u.GlobalName != "" {
		return u.GlobalName
	}
	return u.Username
}

func criarSala(s *discordgo.Session, guild, user string, m *discordgo.Member) {
	// já tem sala? volta pra ela
	if c := salaDoDono(user); c != "" {
		if _, err := s.Channel(c); err == nil {
			_ = s.GuildMemberMove(guild, user, &c)
			return
		}
		mu.Lock()
		delete(estado.Salas, c)
		salvarEstado()
		mu.Unlock()
	}
	mu.Lock()
	cat := estado.Canais["cat_voz"]
	mu.Unlock()
	nome := []rune("🎯 Sala do " + nomeDe(m, nil))
	if len(nome) > 90 {
		nome = nome[:90]
	}
	ch, err := s.GuildChannelCreateComplex(guild, discordgo.GuildChannelCreateData{
		Name: string(nome), Type: discordgo.ChannelTypeGuildVoice, ParentID: cat,
		PermissionOverwrites: []*discordgo.PermissionOverwrite{
			{ID: user, Type: discordgo.PermissionOverwriteTypeMember, Allow: verCanal | conectar | falar | moverGente | gerenciar},
		}})
	if err != nil {
		log.Println("criar sala:", err)
		return
	}
	mu.Lock()
	if estado.Salas == nil {
		estado.Salas = map[string]string{}
	}
	estado.Salas[ch.ID] = user
	salvarEstado()
	mu.Unlock()
	if err := s.GuildMemberMove(guild, user, &ch.ID); err != nil {
		log.Println("mover pra sala:", err)
	}
	_, err = s.ChannelMessageSendComplex(ch.ID, &discordgo.MessageSend{
		Content: "<@" + user + ">",
		Embeds: []*discordgo.MessageEmbed{{Title: "🎯 Sua sala", Color: corLaranja, Description: "Essa sala é sua. Ela some sozinha quando todo mundo sair.\n\n" +
			"🔒 **Trancar**: só entra quem você liberar.\n" +
			"➕ **Liberar**: escolha quem pode entrar.\n" +
			"👢 **Expulsar**: tira da sala e bloqueia.\n" +
			"👥 **Vagas**: limite de pessoas.\n\n" +
			"Quem quiser entrar com a sala trancada usa `/pedir-sala` e você aceita ou recusa aqui."}},
		Components: painelSala(),
	})
	if err != nil {
		log.Println("painel da sala:", err)
	}
}

func painelSala() []discordgo.MessageComponent {
	um := 1
	vagas := []discordgo.SelectMenuOption{{Label: "Sem limite", Value: "0", Emoji: &discordgo.ComponentEmoji{Name: "♾️"}}}
	for _, n := range []int{2, 3, 4, 5, 6, 8, 10} {
		vagas = append(vagas, discordgo.SelectMenuOption{Label: fmt.Sprintf("%d vagas", n), Value: strconv.Itoa(n)})
	}
	return []discordgo.MessageComponent{
		linha(botao("Trancar", "sala_trancar", "🔒", discordgo.DangerButton), botao("Destrancar", "sala_abrir", "🔓", discordgo.SuccessButton)),
		linha(discordgo.SelectMenu{MenuType: discordgo.UserSelectMenu, CustomID: "sala_liberar", Placeholder: "➕ Liberar pessoas (entram mesmo trancada)", MinValues: &um, MaxValues: 10}),
		linha(discordgo.SelectMenu{MenuType: discordgo.UserSelectMenu, CustomID: "sala_expulsar", Placeholder: "👢 Expulsar e bloquear", MinValues: &um, MaxValues: 10}),
		linha(discordgo.SelectMenu{CustomID: "sala_vagas", Placeholder: "👥 Limite de vagas", Options: vagas}),
	}
}

// apagarSeVazia apaga a sala se não tiver ninguém nela.
func apagarSeVazia(s *discordgo.Session, guild, canal string) {
	if donoDaSala(canal) == "" {
		return
	}
	if t, err := discordgo.SnowflakeTimestamp(canal); err == nil && time.Since(t) < 20*time.Second {
		return // acabou de ser criada, o dono ainda está sendo movido
	}
	if pessoasNaSala(s, guild, canal) > 0 {
		return
	}
	if _, err := s.ChannelDelete(canal); err != nil {
		if _, e2 := s.Channel(canal); e2 == nil {
			log.Println("apagar sala:", err)
			return
		}
	}
	mu.Lock()
	delete(estado.Salas, canal)
	salvarEstado()
	mu.Unlock()
}

func pessoasNaSala(s *discordgo.Session, guild, canal string) int {
	g, err := s.State.Guild(guild)
	if err != nil {
		return 1 // sem informação: não apaga
	}
	n := 0
	for _, vs := range g.VoiceStates {
		if vs.ChannelID == canal {
			n++
		}
	}
	return n
}

func quemEstaNaSala(s *discordgo.Session, guild, canal string) []string {
	var l []string
	if g, err := s.State.Guild(guild); err == nil {
		for _, vs := range g.VoiceStates {
			if vs.ChannelID == canal {
				l = append(l, vs.UserID)
			}
		}
	}
	return l
}

// limparSalas roda de tempos em tempos: tira do estado salas apagadas e apaga as vazias.
func limparSalas(s *discordgo.Session) {
	mu.Lock()
	guild := estado.Guild
	var l []string
	for c := range estado.Salas {
		l = append(l, c)
	}
	mu.Unlock()
	for _, c := range l {
		if _, err := s.Channel(c); err != nil {
			if re, ok := err.(*discordgo.RESTError); ok && re.Response != nil && re.Response.StatusCode == 404 {
				mu.Lock()
				delete(estado.Salas, c)
				salvarEstado()
				mu.Unlock()
			}
			continue
		}
		apagarSeVazia(s, guild, c)
	}
}

func ehStaff(i *discordgo.InteractionCreate) bool {
	if i.Member == nil {
		return false
	}
	if i.Member.Permissions&(discordgo.PermissionAdministrator|discordgo.PermissionManageChannels) != 0 {
		return true
	}
	mu.Lock()
	staff := estado.Cargos["staff"]
	mu.Unlock()
	for _, r := range i.Member.Roles {
		if r == staff {
			return true
		}
	}
	return false
}

func avisoPrivado(s *discordgo.Session, i *discordgo.InteractionCreate, texto string, cor int) {
	responder(s, i, &discordgo.MessageEmbed{Description: texto, Color: cor}, true)
}

func linkCanal(guild, canal string) string {
	return fmt.Sprintf("https://discord.com/channels/%s/%s", guild, canal)
}

func mencoes(ids []string) string {
	var b []string
	for _, id := range ids {
		b = append(b, "<@"+id+">")
	}
	return strings.Join(b, ", ")
}

// tratarSala cuida dos botões do painel da sala. Devolve false se não for dele.
func tratarSala(s *discordgo.Session, i *discordgo.InteractionCreate) bool {
	d := i.MessageComponentData()
	if !strings.HasPrefix(d.CustomID, "sala_") {
		return false
	}
	canal := i.ChannelID
	u := usuario(i)
	dono := donoDaSala(canal)
	if dono == "" {
		avisoPrivado(s, i, "Essa sala não existe mais.", corVermelho)
		return true
	}
	if u.ID != dono && !ehStaff(i) {
		avisoPrivado(s, i, "Só o dono da sala mexe aqui.", corVermelho)
		return true
	}
	everyone := i.GuildID
	switch {
	case d.CustomID == "sala_trancar":
		// quem já está dentro continua podendo voltar
		for _, id := range quemEstaNaSala(s, i.GuildID, canal) {
			_ = s.ChannelPermissionSet(canal, id, discordgo.PermissionOverwriteTypeMember, verCanal|conectar|falar, 0)
		}
		if err := s.ChannelPermissionSet(canal, everyone, discordgo.PermissionOverwriteTypeRole, verCanal, conectar); err != nil {
			avisoPrivado(s, i, "Não consegui trancar agora. Tente de novo.", corVermelho)
			return true
		}
		avisoPrivado(s, i, "🔒 Sala trancada. Só entra quem você liberar.", corVerde)
	case d.CustomID == "sala_abrir":
		_ = s.ChannelPermissionDelete(canal, everyone)
		avisoPrivado(s, i, "🔓 Sala aberta. Qualquer um pode entrar.", corVerde)
	case d.CustomID == "sala_liberar":
		for _, id := range d.Values {
			_ = s.ChannelPermissionSet(canal, id, discordgo.PermissionOverwriteTypeMember, verCanal|conectar|falar, 0)
			dm(s, id, &discordgo.MessageEmbed{Color: corVerde, Description: fmt.Sprintf("🎯 <@%s> liberou você na sala de voz dele. Entre por aqui: %s", dono, linkCanal(i.GuildID, canal))})
		}
		avisoPrivado(s, i, "➕ Liberado: "+mencoes(d.Values), corVerde)
	case d.CustomID == "sala_expulsar":
		var tirados []string
		for _, id := range d.Values {
			if id == dono {
				continue
			}
			_ = s.ChannelPermissionSet(canal, id, discordgo.PermissionOverwriteTypeMember, 0, conectar)
			for _, dentro := range quemEstaNaSala(s, i.GuildID, canal) {
				if dentro == id {
					_ = s.GuildMemberMove(i.GuildID, id, nil)
				}
			}
			tirados = append(tirados, id)
		}
		if len(tirados) == 0 {
			avisoPrivado(s, i, "Você não pode expulsar o dono da sala.", corVermelho)
			return true
		}
		avisoPrivado(s, i, "👢 Expulso e bloqueado: "+mencoes(tirados), corVerde)
	case d.CustomID == "sala_vagas":
		n := 0
		if len(d.Values) > 0 {
			n, _ = strconv.Atoi(d.Values[0])
		}
		ep := discordgo.EndpointChannel(canal)
		if _, err := s.RequestWithBucketID("PATCH", ep, map[string]int{"user_limit": n}, ep); err != nil {
			avisoPrivado(s, i, "Não consegui mudar as vagas agora.", corVermelho)
			return true
		}
		if n == 0 {
			avisoPrivado(s, i, "👥 Sala sem limite de vagas.", corVerde)
		} else {
			avisoPrivado(s, i, fmt.Sprintf("👥 Sala limitada a %d pessoas.", n), corVerde)
		}
	case strings.HasPrefix(d.CustomID, "sala_aceitar:"):
		quem := strings.TrimPrefix(d.CustomID, "sala_aceitar:")
		_ = s.ChannelPermissionSet(canal, quem, discordgo.PermissionOverwriteTypeMember, verCanal|conectar|falar, 0)
		dm(s, quem, &discordgo.MessageEmbed{Color: corVerde, Description: fmt.Sprintf("✅ <@%s> aceitou seu pedido. Entre por aqui: %s", dono, linkCanal(i.GuildID, canal))})
		atualizarPedido(s, i, fmt.Sprintf("✅ Pedido de <@%s> aceito por <@%s>.", quem, u.ID), corVerde)
	case strings.HasPrefix(d.CustomID, "sala_recusar:"):
		quem := strings.TrimPrefix(d.CustomID, "sala_recusar:")
		dm(s, quem, &discordgo.MessageEmbed{Color: corVermelho, Description: fmt.Sprintf("❌ <@%s> recusou seu pedido pra entrar na sala.", dono)})
		atualizarPedido(s, i, fmt.Sprintf("❌ Pedido de <@%s> recusado.", quem), corVermelho)
	default:
		return false
	}
	return true
}

func atualizarPedido(s *discordgo.Session, i *discordgo.InteractionCreate, texto string, cor int) {
	_ = s.InteractionRespond(i.Interaction, &discordgo.InteractionResponse{
		Type: discordgo.InteractionResponseUpdateMessage,
		Data: &discordgo.InteractionResponseData{Content: "", Embeds: []*discordgo.MessageEmbed{{Description: texto, Color: cor}},
			Components: []discordgo.MessageComponent{}}})
}

// pedirSala: /pedir-sala dono:@fulano manda um pedido pro chat da sala do fulano.
func pedirSala(s *discordgo.Session, i *discordgo.InteractionCreate, dono *discordgo.User) {
	u := usuario(i)
	if dono == nil {
		return
	}
	canal := salaDoDono(dono.ID)
	if canal == "" {
		avisoPrivado(s, i, fmt.Sprintf("<@%s> não tem sala aberta agora.", dono.ID), corVermelho)
		return
	}
	if dono.ID == u.ID {
		avisoPrivado(s, i, "Essa sala já é sua.", corAmbar)
		return
	}
	_, err := s.ChannelMessageSendComplex(canal, &discordgo.MessageSend{
		Content: "<@" + dono.ID + ">",
		Embeds:  []*discordgo.MessageEmbed{{Description: fmt.Sprintf("🙋 <@%s> quer entrar na sua sala.", u.ID), Color: corAmbar}},
		Components: []discordgo.MessageComponent{linha(
			botao("Aceitar", "sala_aceitar:"+u.ID, "✅", discordgo.SuccessButton),
			botao("Recusar", "sala_recusar:"+u.ID, "❌", discordgo.DangerButton))},
	})
	if err != nil {
		avisoPrivado(s, i, "Não consegui mandar o pedido agora.", corVermelho)
		return
	}
	avisoPrivado(s, i, fmt.Sprintf("Pedido enviado pra <@%s>. Você recebe a resposta na DM.", dono.ID), corVerde)
}
