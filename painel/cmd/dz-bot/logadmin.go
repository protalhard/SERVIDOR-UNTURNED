package main

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"time"

	"github.com/bwmarrin/discordgo"
)

// enviarLogsAdmin manda pro canal #logs as linhas novas do admin_log.txt que o plugin grava
// (kick, ban, itens, teleporte, reinício...). Bans também vão pro #banimentos (público).
func enviarLogsAdmin(s *discordgo.Session) {
	linhas := novasLinhas("admin_log.txt", "logs", &estado.LogAdminPos)
	postarLinhas(s, "logs", "🛡️ **Admin**", linhas)
	for _, l := range linhas {
		postarBan(s, l)
		postarDesban(s, l)
	}
}

var (
	reFotoGuard = regexp.MustCompile(`foto=(spy/[0-9]+_[0-9]{8}_[0-9]{6}_[a-z]+\.jpg)`)
	reDesban    = regexp.MustCompile(`^.*\(\d+\): DESBAN (.*) \((\d+)\)$`)
)

// enviarLogsGuard: alertas do DZ GUARD (anti-cheat) e reports vão pro #logs (staff).
// As fotos da tela (report, ban automático) vão anexadas.
func enviarLogsGuard(s *discordgo.Session) {
	linhas := novasLinhas("guard_log.txt", "logs", &estado.LogGuardPos)
	var texto []string
	for _, l := range linhas {
		m := reFotoGuard.FindStringSubmatch(l)
		if m == nil {
			texto = append(texto, l)
			continue
		}
		f, err := os.Open(filepath.Join(fonte.Pasta, m[1]))
		if err != nil {
			texto = append(texto, l)
			continue
		}
		mu.Lock()
		postar(s, "logs", &discordgo.MessageSend{
			Content: "📸 **DZ GUARD** " + strings.TrimSpace(strings.Replace(l, m[0], "", 1)),
			Files:   []*discordgo.File{{Name: filepath.Base(m[1]), ContentType: "image/jpeg", Reader: f}},
		})
		mu.Unlock()
		f.Close()
	}
	if len(texto) > 0 {
		postarLinhas(s, "logs", "🛡️ **DZ GUARD**", texto)
	}
}

func postarDesban(s *discordgo.Session, l string) {
	m := reDesban.FindStringSubmatch(l)
	if m == nil {
		return
	}
	mu.Lock()
	defer mu.Unlock()
	postar(s, "banimentos", &discordgo.MessageSend{Embeds: []*discordgo.MessageEmbed{{
		Title: "✅ Jogador desbanido", Color: 0x3DDC84,
		Description: fmt.Sprintf("**%s** foi desbanido pela staff.\n\n[Perfil Steam](https://steamcommunity.com/profiles/%s)", m[1], m[2]),
		Timestamp:   time.Now().Format(time.RFC3339), Footer: &discordgo.MessageEmbedFooter{Text: rodape},
	}}})
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

var reUpkeep = regexp.MustCompile(`UPKEEP (AVISO|APAGOU): base de (.*) \((\d+)\) com (\d+) pecas, (\d+) dias sem ninguem(?:, apaga em (-?\d+) dias)?`)

// enviarLogsUpkeep: bases abandonadas vão pro #logs e pra DM do dono (e da facção).
func enviarLogsUpkeep(s *discordgo.Session) {
	linhas := novasLinhas("upkeep_log.txt", "logs", &estado.LogUpkeepPos)
	postarLinhas(s, "logs", "🏚️ **Upkeep**", linhas)
	for _, l := range linhas {
		m := reUpkeep.FindStringSubmatch(l)
		if m == nil {
			continue
		}
		alvos := map[string]bool{m[3]: true}
		if f := reRaidFac.FindStringSubmatch(l); f != nil {
			if fc := fonte.FaccaoPorID(f[1]); fc != nil {
				for _, mb := range fc.Membros {
					alvos[mb.ID] = true
				}
			}
		}
		var e *discordgo.MessageEmbed
		if m[1] == "AVISO" {
			txt := fmt.Sprintf("Ninguém da sua base entra no servidor há **%s dias**.\nCom 15 dias ela perde a proteção offline", m[5])
			if m[6] != "" && m[6] != "-1" {
				txt += fmt.Sprintf(" e em **%s dias** o servidor apaga a base", m[6])
			}
			txt += ".\n\nBasta você ou alguém do grupo entrar no servidor pra zerar a contagem.\n`" + ipServidor + "`"
			e = &discordgo.MessageEmbed{Title: "🏚️ Sua base está abandonada", Description: txt, Color: corAmbar}
		} else {
			e = &discordgo.MessageEmbed{Title: "🏚️ Sua base foi apagada", Color: corVermelho,
				Description: fmt.Sprintf("Sua base (%s peças) foi apagada depois de %s dias sem ninguém do grupo entrar no servidor.", m[4], m[5])}
		}
		e.Timestamp = time.Now().Format(time.RFC3339)
		e.Footer = &discordgo.MessageEmbedFooter{Text: rodape}
		for _, v := range fonte.LerVinculos() {
			if alvos[v.Steam] {
				dm(s, v.Discord, e)
			}
		}
	}
}
