package main

// Tickets: 1 aberto por jogador, botão "Assumir" pra staff, só a staff fecha e,
// ao fechar, a conversa inteira vai como arquivo pro #logs.

import (
	"bytes"
	"fmt"
	"log"
	"strings"
	"time"

	"github.com/bwmarrin/discordgo"
)

var fusoBR = time.FixedZone("BRT", -3*3600)

// ticketDe devolve o canal do ticket aberto do jogador ("" se não tiver).
func ticketDe(s *discordgo.Session, user string) string {
	mu.Lock()
	var achou []string
	for c, d := range estado.Tickets {
		if d == user {
			achou = append(achou, c)
		}
	}
	mu.Unlock()
	for _, c := range achou {
		if _, err := s.Channel(c); err == nil {
			return c
		} else if re, ok := err.(*discordgo.RESTError); ok && re.Response != nil && re.Response.StatusCode == 404 {
			mu.Lock()
			delete(estado.Tickets, c)
			salvarEstado()
			mu.Unlock()
		}
	}
	return ""
}

func assumirTicket(s *discordgo.Session, i *discordgo.InteractionCreate) {
	if !ehStaff(i) {
		avisoPrivado(s, i, "Só a staff assume ticket.", corVermelho)
		return
	}
	responder(s, i, &discordgo.MessageEmbed{Description: fmt.Sprintf("🙋 <@%s> está atendendo este ticket.", usuario(i).ID), Color: corVerde}, false)
}

func fecharTicket(s *discordgo.Session, i *discordgo.InteractionCreate) {
	if !ehStaff(i) {
		avisoPrivado(s, i, "Só a staff fecha o ticket. Se já resolveu, avise aqui que a staff encerra.", corAmbar)
		return
	}
	canal := i.ChannelID
	quem := usuario(i).ID
	_ = s.InteractionRespond(i.Interaction, &discordgo.InteractionResponse{
		Type: discordgo.InteractionResponseChannelMessageWithSource,
		Data: &discordgo.InteractionResponseData{Content: "Fechando o ticket em 5 segundos. A conversa fica salva no #logs."}})
	go func() {
		defer func() {
			if r := recover(); r != nil {
				log.Println("fechar ticket:", r)
			}
		}()
		texto, nome := transcricao(s, canal)
		mu.Lock()
		dono := estado.Tickets[canal]
		msg := fmt.Sprintf("🔒 **Ticket fechado** `#%s` por <@%s>", nome, quem)
		if dono != "" {
			msg += fmt.Sprintf(" (aberto por <@%s>)", dono)
		}
		postar(s, "logs", &discordgo.MessageSend{Content: msg, AllowedMentions: &discordgo.MessageAllowedMentions{},
			Files: []*discordgo.File{{Name: "ticket-" + nome + ".txt", ContentType: "text/plain; charset=utf-8", Reader: bytes.NewReader([]byte(texto))}}})
		mu.Unlock()
		time.Sleep(5 * time.Second)
		if _, err := s.ChannelDelete(canal); err != nil {
			log.Println("apagar ticket:", err)
			return
		}
		mu.Lock()
		delete(estado.Tickets, canal)
		salvarEstado()
		mu.Unlock()
	}()
}

// transcricao junta todas as mensagens do canal (até 2000), da mais velha pra mais nova.
func transcricao(s *discordgo.Session, canal string) (string, string) {
	nome := canal
	if ch, err := s.Channel(canal); err == nil {
		nome = ch.Name
	}
	var todas []*discordgo.Message
	antes := ""
	for len(todas) < 2000 {
		l, err := s.ChannelMessages(canal, 100, antes, "", "")
		if err != nil || len(l) == 0 {
			break
		}
		todas = append(todas, l...)
		antes = l[len(l)-1].ID
		if len(l) < 100 {
			break
		}
	}
	var b strings.Builder
	fmt.Fprintf(&b, "Ticket #%s — salvo em %s\n\n", nome, time.Now().In(fusoBR).Format("02/01/2006 15:04"))
	for k := len(todas) - 1; k >= 0; k-- {
		m := todas[k]
		autor := "?"
		if m.Author != nil {
			autor = m.Author.Username
			if m.Author.GlobalName != "" {
				autor = m.Author.GlobalName + " (" + m.Author.Username + ")"
			}
		}
		fmt.Fprintf(&b, "[%s] %s: %s\n", m.Timestamp.In(fusoBR).Format("02/01 15:04"), autor, m.Content)
		for _, e := range m.Embeds {
			if e.Title != "" || e.Description != "" {
				fmt.Fprintf(&b, "    [embed] %s %s\n", e.Title, strings.ReplaceAll(e.Description, "\n", " "))
			}
		}
		for _, a := range m.Attachments {
			fmt.Fprintf(&b, "    [anexo] %s\n", a.URL)
		}
	}
	return b.String(), nome
}
