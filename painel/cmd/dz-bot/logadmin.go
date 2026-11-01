package main

import (
	"io"
	"os"
	"path/filepath"
	"strings"

	"github.com/bwmarrin/discordgo"
)

// enviarLogsAdmin manda pro canal #logs as linhas novas do admin_log.txt que o plugin grava
// (kick, ban, itens, teleporte, reinício...). Bans também vão pro #banimentos (público).
func enviarLogsAdmin(s *discordgo.Session) {
	linhas := novasLinhas("admin_log.txt", "logs", &estado.LogAdminPos)
	postarLinhas(s, "logs", "🛡️ **Admin**", linhas)
	for _, l := range linhas {
		postarBan(s, l)
	}
}

// enviarLogsRaid: as tentativas de raid vão pro #logs (staff) e pra DM do dono da base.
// (Antes iam pro #raid-alert público, mas mostrava a posição da base pra todo mundo.)
func enviarLogsRaid(s *discordgo.Session) {
	linhas := novasLinhas("raid_log.txt", "logs", &estado.LogRaidPos)
	postarLinhas(s, "logs", "💥 **Raid**", linhas)
	for _, l := range linhas {
		avisarRaidDM(s, l)
	}
}

// novasLinhas lê o que foi acrescentado no arquivo desde a última vez (na primeira vez começa do fim).
func novasLinhas(nome, canal string, posp **int64) []string {
	arq := filepath.Join(fonte.Pasta, nome)
	info, err := os.Stat(arq)
	if err != nil {
		return nil
	}
	mu.Lock()
	defer mu.Unlock()
	if estado.Canais[canal] == "" {
		return nil
	}
	if *posp == nil || **posp > info.Size() {
		p := info.Size()
		*posp = &p
		salvarEstado()
		return nil
	}
	pos := **posp
	if pos == info.Size() {
		return nil
	}
	f, err := os.Open(arq)
	if err != nil {
		return nil
	}
	defer f.Close()
	if _, err := f.Seek(pos, io.SeekStart); err != nil {
		return nil
	}
	b := make([]byte, info.Size()-pos)
	n, _ := io.ReadFull(f, b)
	b = b[:n]
	fim := strings.LastIndexByte(string(b), '\n') // só até a última linha completa
	if fim < 0 {
		return nil
	}
	novo := pos + int64(fim) + 1
	*posp = &novo
	salvarEstado()
	var l []string
	for _, x := range strings.Split(string(b[:fim]), "\n") {
		if x = strings.TrimSpace(x); x != "" {
			l = append(l, x)
		}
	}
	return l
}

func postarLinhas(s *discordgo.Session, canal, titulo string, linhas []string) {
	texto := strings.Join(linhas, "\n")
	mu.Lock()
	defer mu.Unlock()
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
