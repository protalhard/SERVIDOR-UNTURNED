package main

// Ligação com o painel admin do site: o bot executa os pedidos da pasta fila/
// (mensagem, aviso ou enquete num canal) e grava discord.json a cada minuto
// com canais, membros, tickets, salas de voz e o resultado das enquetes.

import (
	"fmt"
	"log"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"time"

	"github.com/bwmarrin/discordgo"
	"github.com/protalhard/servidor-unturned/painel/dados"
)

var pastaPainel = dados.PastaPainel

func linkMensagem(guild, canal, msg string) string {
	return fmt.Sprintf("https://discord.com/channels/%s/%s/%s", guild, canal, msg)
}

func corHex(s string, padrao int) int {
	s = strings.TrimPrefix(strings.TrimSpace(s), "#")
	if n, err := strconv.ParseInt(s, 16, 32); err == nil && len(s) == 6 {
		return int(n)
	}
	return padrao
}

// processarFilaPainel executa os pedidos que o site deixou em fila/.
func processarFilaPainel(s *discordgo.Session) {
	fila := filepath.Join(pastaPainel, "fila")
	ents, err := os.ReadDir(fila)
	if err != nil {
		_ = os.MkdirAll(fila, 0o750)
		return
	}
	var nomes []string
	for _, e := range ents {
		if !e.IsDir() && strings.HasSuffix(e.Name(), ".json") {
			nomes = append(nomes, e.Name())
		}
	}
	sort.Strings(nomes)
	for _, n := range nomes {
		arq := filepath.Join(fila, n)
		var p dados.PedidoBot
		if err := dados.LerJSON(arq, &p); err != nil {
			_ = os.Remove(arq)
			continue
		}
		_ = os.Remove(arq)
		res := executarPedido(s, p)
		res.ID, res.Quando = p.ID, time.Now().Unix()
		if res.Ok {
			log.Printf("painel: %s em %s por %s", p.Tipo, p.Canal, p.Autor)
		} else {
			log.Printf("painel: %s falhou: %s", p.Tipo, res.Erro)
		}
		_ = dados.GravarJSON(filepath.Join(pastaPainel, "feitos", p.ID+".json"), res)
	}
}

func executarPedido(s *discordgo.Session, p dados.PedidoBot) dados.ResultadoBot {
	mu.Lock()
	guild := estado.Guild
	mu.Unlock()
	ch, err := s.State.Channel(p.Canal)
	if err != nil {
		ch, err = s.Channel(p.Canal)
	}
	if err != nil || ch.GuildID != guild {
		return dados.ResultadoBot{Erro: "canal não encontrado no Discord"}
	}
	m := &discordgo.MessageSend{AllowedMentions: &discordgo.MessageAllowedMentions{}}
	switch p.Mencao {
	case "everyone":
		m.Content = "@everyone"
		m.AllowedMentions.Parse = []discordgo.AllowedMentionType{discordgo.AllowedMentionTypeEveryone}
	case "here":
		m.Content = "@here"
		m.AllowedMentions.Parse = []discordgo.AllowedMentionType{discordgo.AllowedMentionTypeEveryone}
	}
	cor := corHex(p.Cor, corLaranja)
	switch p.Tipo {
	case "mensagem":
		if m.Content != "" {
			m.Content += "\n"
		}
		m.Content += p.Texto
	case "aviso":
		e := &discordgo.MessageEmbed{Title: p.Titulo, Description: p.Texto, Color: cor,
			Footer: &discordgo.MessageEmbedFooter{Text: rodape}, Timestamp: time.Now().Format(time.RFC3339)}
		if strings.HasPrefix(p.Imagem, "https://") {
			e.Image = &discordgo.MessageEmbedImage{URL: p.Imagem}
		}
		m.Embeds = []*discordgo.MessageEmbed{e}
	case "enquete":
		if len(p.Opcoes) < 2 {
			return dados.ResultadoBot{Erro: "a enquete precisa de pelo menos 2 opções"}
		}
		horas := p.Horas
		if horas < 1 {
			horas = 24
		}
		if horas > 768 {
			horas = 768
		}
		poll := &discordgo.Poll{Question: discordgo.PollMedia{Text: p.Titulo}, AllowMultiselect: p.Multi, Duration: horas}
		for _, o := range p.Opcoes {
			poll.Answers = append(poll.Answers, discordgo.PollAnswer{Media: &discordgo.PollMedia{Text: o}})
		}
		m.Poll = poll
		if p.Texto != "" {
			if m.Content != "" {
				m.Content += "\n"
			}
			m.Content += p.Texto
		}
	default:
		return dados.ResultadoBot{Erro: "tipo de mensagem inválido"}
	}
	msg, err := s.ChannelMessageSendComplex(p.Canal, m)
	if err != nil {
		return dados.ResultadoBot{Erro: "o Discord recusou: " + err.Error()}
	}
	if p.Tipo == "enquete" {
		mu.Lock()
		estado.Enquetes = append(estado.Enquetes, dados.EnqueteDiscord{Canal: p.Canal, CanalNome: ch.Name, Msg: msg.ID, Pergunta: p.Titulo,
			Rotulo: p.Rotulo, Criada: time.Now().Unix(), Termina: time.Now().Add(time.Duration(p.Horas) * time.Hour).Unix(),
			Link: linkMensagem(guild, p.Canal, msg.ID)})
		if len(estado.Enquetes) > 15 {
			estado.Enquetes = estado.Enquetes[len(estado.Enquetes)-15:]
		}
		salvarEstado()
		mu.Unlock()
		go func() { time.Sleep(3 * time.Second); exportarDiscord(s) }()
	}
	return dados.ResultadoBot{Ok: true, Link: linkMensagem(guild, p.Canal, msg.ID)}
}

// exportarDiscord grava discord.json pro painel.
func exportarDiscord(s *discordgo.Session) {
	mu.Lock()
	guild := estado.Guild
	conv := estado.Convite
	tickets := map[string]string{}
	for k, v := range estado.Tickets {
		tickets[k] = v
	}
	salas := map[string]string{}
	for k, v := range estado.Salas {
		salas[k] = v
	}
	enquetes := append([]dados.EnqueteDiscord(nil), estado.Enquetes...)
	mu.Unlock()
	if guild == "" {
		return
	}
	ex := dados.ExportDiscord{Atualizado: time.Now().Unix(), Convite: conv,
		Canais: []dados.CanalDiscord{}, Tickets: []dados.TicketDiscord{}, Salas: []dados.SalaDiscord{}, Enquetes: []dados.EnqueteDiscord{}}
	if g, err := s.GuildWithCounts(guild); err == nil {
		ex.Servidor, ex.Membros, ex.Online = g.Name, g.ApproximateMemberCount, g.ApproximatePresenceCount
	}
	chs, err := s.GuildChannels(guild)
	if err != nil {
		log.Println("painel canais:", err)
		return
	}
	cats := map[string]string{}
	for _, c := range chs {
		if c.Type == discordgo.ChannelTypeGuildCategory {
			cats[c.ID] = c.Name
		}
	}
	sort.SliceStable(chs, func(i, j int) bool {
		pi, pj := chs[i].Position, chs[j].Position
		if a, b := chs[i].ParentID, chs[j].ParentID; a != b {
			return posCat(chs, a) < posCat(chs, b)
		}
		return pi < pj
	})
	for _, c := range chs {
		tipo := ""
		switch c.Type {
		case discordgo.ChannelTypeGuildText:
			tipo = "texto"
		case discordgo.ChannelTypeGuildNews:
			tipo = "anuncio"
		case discordgo.ChannelTypeGuildVoice:
			tipo = "voz"
		default:
			continue
		}
		if _, ehTicket := tickets[c.ID]; ehTicket {
			ex.Tickets = append(ex.Tickets, dados.TicketDiscord{Canal: c.ID, Nome: c.Name, Dono: tickets[c.ID], Link: linkCanal(guild, c.ID)})
			continue
		}
		if _, ehSala := salas[c.ID]; ehSala {
			ex.Salas = append(ex.Salas, dados.SalaDiscord{Canal: c.ID, Nome: c.Name, Pessoas: pessoasNaSala(s, guild, c.ID)})
			continue
		}
		ex.Canais = append(ex.Canais, dados.CanalDiscord{ID: c.ID, Nome: c.Name, Categoria: cats[c.ParentID], Tipo: tipo})
	}
	// enquetes: as 6 mais novas com os votos atuais
	if len(enquetes) > 6 {
		enquetes = enquetes[len(enquetes)-6:]
	}
	for k := len(enquetes) - 1; k >= 0; k-- {
		e := enquetes[k]
		m, err := s.ChannelMessage(e.Canal, e.Msg)
		if err != nil || m.Poll == nil {
			continue
		}
		votos := map[int]int{}
		if m.Poll.Results != nil {
			e.Encerrada = m.Poll.Results.Finalized
			for _, c := range m.Poll.Results.AnswerCounts {
				votos[c.ID] = c.Count
			}
		}
		if m.Poll.Expiry != nil {
			e.Termina = m.Poll.Expiry.Unix()
			if time.Now().After(*m.Poll.Expiry) {
				e.Encerrada = true
			}
		}
		e.Respostas, e.Total = nil, 0
		for _, a := range m.Poll.Answers {
			t := ""
			if a.Media != nil {
				t = a.Media.Text
			}
			e.Respostas = append(e.Respostas, dados.RespostaEnquete{Texto: t, Votos: votos[a.AnswerID]})
			e.Total += votos[a.AnswerID]
		}
		ex.Enquetes = append(ex.Enquetes, e)
	}
	if err := dados.GravarJSON(filepath.Join(pastaPainel, "discord.json"), ex); err != nil {
		log.Println("painel discord.json:", err)
	}
}

func posCat(chs []*discordgo.Channel, id string) int {
	if id == "" {
		return -1
	}
	for _, c := range chs {
		if c.ID == id {
			return c.Position
		}
	}
	return 9999
}
