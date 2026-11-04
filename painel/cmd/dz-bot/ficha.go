package main

// Ticket ligado ao jogo: a staff vê onde o jogador está, o que carrega, dinheiro e os últimos registros
// (baús, carros, entradas). O plugin grava fichas_jogo.json a cada 20 s. A ficha vai pro #staff quando o
// ticket abre e o botão "Ficha do jogo" no ticket mostra a atual só pra quem é staff.

import (
	"bufio"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/bwmarrin/discordgo"

	"github.com/protalhard/servidor-unturned/painel/dados"
)

type fichaJogo struct {
	Id, Nome, Lugar, Safe, Carro, Faccao string
	Online, Morto                        bool
	Visto                                time.Time
	X, Y, Z, Vida, Fome, Sede            int
	Banco, Bolso                         int64
	Itens                                []string
}

func lerFichaJogo(steam string) *fichaJogo {
	b, err := os.ReadFile(filepath.Join(fonte.Pasta, "fichas_jogo.json"))
	if err != nil {
		return nil
	}
	var m map[string]*fichaJogo
	if json.Unmarshal(b, &m) != nil {
		return nil
	}
	return m[steam]
}

// ultimasLinhas: as últimas n linhas do TSV em que a coluna col (ou col2) é o steam.
func ultimasLinhas(arq, steam string, n int, cols ...int) [][]string {
	f, err := os.Open(filepath.Join(fonte.Pasta, arq))
	if err != nil {
		return nil
	}
	defer f.Close()
	var l [][]string
	sc := bufio.NewScanner(f)
	sc.Buffer(make([]byte, 64*1024), 1024*1024)
	for sc.Scan() {
		c := strings.Split(sc.Text(), "\t")
		for _, k := range cols {
			if k < len(c) && c[k] == steam {
				l = append(l, c)
				if len(l) > n*4 {
					l = l[len(l)-n:]
				}
				break
			}
		}
	}
	if len(l) > n {
		l = l[len(l)-n:]
	}
	return l
}

func horaBR(s string) string {
	if t, err := time.Parse(time.RFC3339Nano, s); err == nil {
		return t.In(fusoBR).Format("02/01 15:04")
	}
	if len(s) > 16 {
		return s[:16]
	}
	return s
}

func cortar(s string, n int) string {
	if r := []rune(s); len(r) > n {
		return string(r[:n-1]) + "…"
	}
	return s
}

func dinheiroBR(v int64) string {
	s := fmt.Sprint(v)
	var b strings.Builder
	for i, r := range s {
		if i > 0 && (len(s)-i)%3 == 0 {
			b.WriteByte('.')
		}
		b.WriteRune(r)
	}
	return "$" + b.String()
}

// embedFichaJogo monta a ficha do jogo de quem tem esse Discord ("" se não tem Steam vinculada).
func embedFichaJogo(discord, ticket string) *discordgo.MessageEmbed {
	steam := fonte.SteamDoDiscord(discord)
	e := &discordgo.MessageEmbed{Title: "📋 Ficha do jogo", Color: corLaranja}
	if ticket != "" {
		e.Description = fmt.Sprintf("Ticket <#%s> de <@%s>\n", ticket, discord)
	}
	if steam == "" {
		e.Description += "Esse jogador não vinculou a Steam, então não dá pra puxar a ficha do jogo."
		return e
	}
	e.Description += fmt.Sprintf("Steam: [%s](https://steamcommunity.com/profiles/%s)", steam, steam)
	f := lerFichaJogo(steam)
	campo := func(nome, valor string, inline bool) {
		if valor != "" {
			e.Fields = append(e.Fields, &discordgo.MessageEmbedField{Name: nome, Value: cortar(valor, 1000), Inline: inline})
		}
	}
	if f == nil {
		campo("Jogo", "Ainda não tem foto desse jogador (não entrou desde que a ficha começou a ser gravada).", false)
	} else {
		e.Title = "📋 Ficha do jogo: " + f.Nome
		status := "🟢 Online agora"
		if !f.Online {
			status = "⚫ Offline, visto em " + f.Visto.In(fusoBR).Format("02/01 15:04")
		}
		if f.Morto {
			status += " (morto)"
		}
		campo("Status", status, false)
		onde := fmt.Sprintf("%s (X %d, Z %d)", f.Lugar, f.X, f.Z)
		if f.Safe != "" {
			onde += "\nNa safe " + f.Safe
		}
		if f.Carro != "" {
			onde += "\nNo carro: " + f.Carro
		}
		campo("Onde está", onde, false)
		campo("Vida / fome / sede", fmt.Sprintf("%d / %d / %d", f.Vida, f.Fome, f.Sede), true)
		campo("Dinheiro", fmt.Sprintf("Banco %s\nBolso %s", dinheiroBR(f.Banco), dinheiroBR(f.Bolso)), true)
		if f.Faccao != "" {
			campo("Facção", f.Faccao, true)
		}
		itens := "Nada"
		if len(f.Itens) > 0 {
			itens = strings.Join(f.Itens, "\n")
		}
		campo("Carregando", itens, false)
	}
	cfg := fonte.LerConfig()
	for _, x := range fonte.LerFichas(cfg.Patentes) {
		if x.ID == steam {
			campo("Estatísticas", fmt.Sprintf("Abates %d, mortes %d, zumbis %d, tempo %s, posição #%d", x.Abates, x.Mortes, x.Zumbis, dados.Tempo(x.Segundos), x.Posicao), false)
			break
		}
	}
	// baús e carros: roubos_log.tsv = hora, tipo, quemId, quemNome, donoId, donoNome, oque, X, Z
	var rl []string
	for _, c := range ultimasLinhas("roubos_log.tsv", steam, 8, 2, 4) {
		if len(c) < 7 {
			continue
		}
		linha := fmt.Sprintf("`%s` %s mexeu em %s de %s", horaBR(c[0]), c[3], c[6], c[5])
		if c[2] != steam {
			linha = "⚠️ " + linha
		}
		rl = append(rl, cortar(linha, 120))
	}
	campo("Baús e carros (últimos)", strings.Join(rl, "\n"), false)
	// entradas e saídas: jogadores_log.tsv = data, steam, nome, evento, detalhe
	var jl []string
	for _, c := range ultimasLinhas("jogadores_log.tsv", steam, 6, 1) {
		if len(c) < 4 {
			continue
		}
		d := ""
		if len(c) > 4 && c[4] != "" {
			d = " " + c[4]
		}
		jl = append(jl, cortar(fmt.Sprintf("`%s` %s%s", horaBR(c[0]), c[3], d), 120))
	}
	campo("Entradas e saídas", strings.Join(jl, "\n"), false)
	e.Footer = &discordgo.MessageEmbedFooter{Text: "Só a staff vê esta ficha."}
	return e
}

func fichaNoTicket(s *discordgo.Session, i *discordgo.InteractionCreate) {
	if !ehStaff(i) {
		avisoPrivado(s, i, "Só a staff vê a ficha do jogo.", corAmbar)
		return
	}
	mu.Lock()
	dono := estado.Tickets[i.ChannelID]
	mu.Unlock()
	if dono == "" {
		avisoPrivado(s, i, "Não achei quem abriu este ticket.", corAmbar)
		return
	}
	responder(s, i, embedFichaJogo(dono, ""), true)
}

// sincronizarVipDiscord: quem tem VIP ativo (vips.json do plugin, comprado pelo Pix) ganha o cargo VIP;
// quando vence, o bot tira o cargo (só de quem ganhou por aqui).
func sincronizarVipDiscord(s *discordgo.Session) {
	b, err := os.ReadFile(filepath.Join(fonte.Pasta, "vips.json"))
	if err != nil {
		return
	}
	var st struct {
		Jogadores map[string][]struct{ Expira *time.Time }
	}
	if json.Unmarshal(b, &st) != nil {
		return
	}
	ativos := map[string]bool{}
	for steam, l := range st.Jogadores {
		for _, v := range l {
			if v.Expira == nil || v.Expira.After(time.Now()) {
				ativos[steam] = true
			}
		}
	}
	mu.Lock()
	guild, cargo := estado.Guild, estado.Cargos["vip"]
	if estado.VipDado == nil {
		estado.VipDado = map[string]bool{}
	}
	dado := map[string]bool{}
	for k, v := range estado.VipDado {
		dado[k] = v
	}
	mu.Unlock()
	if guild == "" || cargo == "" {
		return
	}
	mudou := false
	for _, v := range fonte.LerVinculos() {
		if ativos[v.Steam] && !dado[v.Discord] {
			if s.GuildMemberRoleAdd(guild, v.Discord, cargo) == nil {
				dado[v.Discord], mudou = true, true
				dm(s, v.Discord, &discordgo.MessageEmbed{Title: "⭐ VIP ativado!", Color: corAmbar,
					Description: "Obrigado por apoiar o DAY Z BRASIL! Seu VIP já está ativo no jogo e você ganhou o cargo VIP aqui no Discord.",
					Footer:      &discordgo.MessageEmbedFooter{Text: rodape}})
			}
		} else if !ativos[v.Steam] && dado[v.Discord] {
			if s.GuildMemberRoleRemove(guild, v.Discord, cargo) == nil {
				delete(dado, v.Discord)
				mudou = true
			}
		}
	}
	if mudou {
		mu.Lock()
		estado.VipDado = dado
		salvarEstado()
		mu.Unlock()
	}
}
