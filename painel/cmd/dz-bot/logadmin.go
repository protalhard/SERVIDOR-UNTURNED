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
	enviarLog(s, "admin_log.txt", "logs", "🛡️ **Admin**", &estado.LogAdminPos)
}

// enviarLogsRaid manda pro canal #raid-alert as bases atacadas (raid_log.txt do plugin).
func enviarLogsRaid(s *discordgo.Session) {
	enviarLog(s, "raid_log.txt", "raid", "💥 **Raid em andamento**", &estado.LogRaidPos)
}

func enviarLog(s *discordgo.Session, nome, canal, titulo string, posp **int64) {
	arq := filepath.Join(fonte.Pasta, nome)
	info, err := os.Stat(arq)
	if err != nil {
		return
	}
	mu.Lock()
	defer mu.Unlock()
	if estado.Canais[canal] == "" {
		return
	}
	if *posp == nil || **posp > info.Size() {
		p := info.Size()
		*posp = &p
		salvarEstado()
		return
	}
	pos := **posp
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
	*posp = &novo
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
		postar(s, canal, &discordgo.MessageSend{
			Content: titulo + "\n```\n" + strings.ReplaceAll(parte, "```", "'''") + "\n```",
		})
	}
}
