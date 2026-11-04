package main

// Novidades: posta em #updates cada bloco novo do novidades.md (embutido no binário).
// Como o bot reinicia a cada atualização do servidor, um bloco novo no arquivo sai sozinho.

import (
	_ "embed"
	"log"
	"strings"

	"github.com/bwmarrin/discordgo"
)

//go:embed novidades.md
var textoNovidades string

type novidade struct{ titulo, corpo string }

func lerNovidades() []novidade {
	var lista []novidade
	sem := textoNovidades
	for strings.Contains(sem, "<!--") {
		a := strings.Index(sem, "<!--")
		b := strings.Index(sem[a:], "-->")
		if b < 0 {
			break
		}
		sem = sem[:a] + sem[a+b+3:]
	}
	for _, bloco := range strings.Split("\n"+sem, "\n## ")[1:] {
		titulo, corpo, _ := strings.Cut(bloco, "\n")
		titulo, corpo = strings.TrimSpace(titulo), strings.TrimSpace(corpo)
		if titulo != "" && corpo != "" {
			lista = append(lista, novidade{titulo, corpo})
		}
	}
	return lista
}

func postarNovidades(s *discordgo.Session) {
	mu.Lock()
	ch := estado.Canais["updates"]
	mu.Unlock()
	if ch == "" {
		return
	}
	for _, n := range lerNovidades() {
		mu.Lock()
		feito := estado.Novidades[n.titulo]
		mu.Unlock()
		if feito {
			continue
		}
		if _, err := s.ChannelMessageSendComplex(ch, &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
			Title: "🛠 " + n.titulo, Description: n.corpo, Color: corLaranja, Footer: &discordgo.MessageEmbedFooter{Text: rodape},
		}}}); err != nil {
			log.Println("novidades:", err)
			return
		}
		mu.Lock()
		if estado.Novidades == nil {
			estado.Novidades = map[string]bool{}
		}
		estado.Novidades[n.titulo] = true
		salvarEstado()
		mu.Unlock()
		log.Println("novidade postada:", n.titulo)
	}
}
