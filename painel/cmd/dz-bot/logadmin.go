package main

import (
	"io"
	"os"
	"path/filepath"
	"strings"

	"github.com/bwmarrin/discordgo"
)

// enviarLogsAdmin manda pro canal #logs as linhas novas do admin_log.txt que o plugin grava
// (kick, ban, itens, teleporte, reinício...). Na primeira vez começa do fim do arquivo.
func enviarLogsAdmin(s *discordgo.Session) {
	arq := filepath.Join(fonte.Pasta, "admin_log.txt")
	info, err := os.Stat(arq)
	if err != nil {
		return
	}
	mu.Lock()
	defer mu.Unlock()
	if estado.Canais["logs"] == "" {
		return
	}
	if estado.LogAdminPos == nil || *estado.LogAdminPos > info.Size() {
		p := info.Size()
		estado.LogAdminPos = &p
		salvarEstado()
		return
	}
	pos := *estado.LogAdminPos
	if pos == info.Size() {
		return
	}
	f, err := os.Open(arq)
	if err != nil {
		return
	}
	defer f.Close()
	if _, err := f.Seek(pos, io.SeekStart); err != nil {
		return
	}
	b := make([]byte, info.Size()-pos)
	n, _ := io.ReadFull(f, b)
	b = b[:n]
	fim := strings.LastIndexByte(string(b), '\n') // só até a última linha completa
	if fim < 0 {
		return
	}
	texto := strings.TrimSpace(string(b[:fim]))
	novo := pos + int64(fim) + 1
	estado.LogAdminPos = &novo
	salvarEstado()
	for len(texto) > 0 {
		parte := texto
		if len(parte) > 1800 {
			corte := strings.LastIndexByte(parte[:1800], '\n')
			if corte <= 0 {
				corte = 1800
			}
			parte = parte[:corte]
		}
		texto = strings.TrimSpace(texto[len(parte):])
		postar(s, "logs", &discordgo.MessageSend{
			Content: "🛡️ **Admin**\n```\n" + strings.ReplaceAll(parte, "```", "'''") + "\n```",
		})
	}
}
