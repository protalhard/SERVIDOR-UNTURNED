package main

// Painel admin completo: dashboard, mensagens (jogo e Discord), enquetes, wipe e reinício.
// O site não fala direto com o Discord nem desliga o jogo: ele deixa pedidos em arquivos
// (ver dados/painel.go). O bot executa os do Discord, o plugin os do jogo e o executor
// (dz-painel-exec, root) os de wipe e reinício.

import (
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/protalhard/servidor-unturned/painel/dados"
)

var (
	muComandos   sync.Mutex
	pastaBackup  = "/home/unturned/backups"
	arqEstadoBot = "/home/unturned/dz-bot/estado.json"
	fusoBR       = time.FixedZone("BRT", -3*3600)
)

// comandoJogo põe uma linha no admin_comandos.txt (o plugin lê a cada segundo).
func comandoJogo(acao, alvo, quem string, horas float64, texto string) error {
	limpa := strings.NewReplacer("\t", " ", "\n", " ", "\r", " ").Replace(strings.TrimSpace(texto))
	if len([]rune(limpa)) > 240 {
		limpa = string([]rune(limpa)[:240])
	}
	muComandos.Lock()
	defer muComandos.Unlock()
	f, err := os.OpenFile(filepath.Join(fonte.Pasta, "admin_comandos.txt"), os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o640)
	if err != nil {
		return err
	}
	defer f.Close()
	_, err = fmt.Fprintf(f, "%s\t%s\t%s\t%g\t%s\n", acao, alvo, quem, horas, limpa)
	return err
}

func lerDiscord() (dados.ExportDiscord, bool) {
	var ex dados.ExportDiscord
	if err := dados.LerJSON(filepath.Join(dados.PastaPainel, "discord.json"), &ex); err != nil {
		return ex, false
	}
	return ex, time.Since(time.Unix(ex.Atualizado, 0)) < 3*time.Minute
}

// canaisDoBot: chaves do bot (wipe, anuncios...) -> id do canal.
func canaisDoBot() map[string]string {
	var e struct {
		Canais map[string]string `json:"canais"`
	}
	_ = dados.LerJSON(arqEstadoBot, &e)
	if e.Canais == nil {
		e.Canais = map[string]string{}
	}
	return e.Canais
}

func lerAcao() *dados.AcaoServidor {
	var a dados.AcaoServidor
	if dados.LerJSON(filepath.Join(dados.PastaPainel, "acao.json"), &a) != nil || a.Tipo == "" {
		return nil
	}
	return &a
}

func lerHistorico(max int) []map[string]any {
	b, _ := os.ReadFile(filepath.Join(dados.PastaPainel, "historico.jsonl"))
	l := []map[string]any{}
	linhas := strings.Split(strings.TrimSpace(string(b)), "\n")
	for i := len(linhas) - 1; i >= 0 && len(l) < max; i-- {
		var m map[string]any
		if json.Unmarshal([]byte(linhas[i]), &m) == nil {
			l = append(l, m)
		}
	}
	return l
}

type infoBackup struct {
	Nome    string `json:"nome"`
	Quando  int64  `json:"quando"`
	Tamanho int64  `json:"tamanho"`
}

func lerBackups() []infoBackup {
	ents, _ := os.ReadDir(pastaBackup)
	l := []infoBackup{}
	for _, e := range ents {
		n := e.Name()
		if e.IsDir() || !strings.HasSuffix(n, ".tar.gz") {
			continue
		}
		if i, err := e.Info(); err == nil {
			l = append(l, infoBackup{n, i.ModTime().Unix(), i.Size()})
		}
	}
	sort.Slice(l, func(i, j int) bool { return l[i].Quando > l[j].Quando })
	return l
}

func raidAgora(c dados.Config) (aberto bool, texto string) {
	ini, fim := c.RaidInicioHora, c.RaidFimHora
	if ini == 0 && fim == 0 {
		ini, fim = 18, 23
	}
	dias := strings.ToLower(strings.TrimSpace(c.RaidDias))
	if dias == "" {
		dias = "todos"
	}
	if strings.EqualFold(strings.TrimSpace(c.RaidHorarioLigado), "false") {
		return true, "Raid liberado o dia todo"
	}
	quais := dias
	if strings.Contains(dias, "todos") {
		quais = "todo dia"
	}
	texto = fmt.Sprintf("%s, das %dh às %dh", quais, ini, fim)
	agora := time.Now().In(fusoBR)
	nomes := []string{"dom", "seg", "ter", "qua", "qui", "sex", "sab"}
	if !strings.Contains(dias, "todos") && !strings.Contains(dias, nomes[agora.Weekday()]) {
		return false, texto
	}
	m, a, b := agora.Hour()*60+agora.Minute(), ini*60, fim*60
	if a <= b {
		return m >= a && m < b, texto
	}
	return m >= a || m < b, texto
}

func contarLinhas(nome string) int { return len(lerTSV(nome, 0)) }

func registrarPainel(mux *http.ServeMux) {
	mux.HandleFunc("/api/admin/maquina", apiMaquina)
	mux.HandleFunc("/api/admin/painel", func(w http.ResponseWriter, r *http.Request) {
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		cfg := fonte.LerConfig()
		st := fonte.LerStatus()
		disc, botOn := lerDiscord()
		aberto, txtRaid := raidAgora(cfg)
		fichas := fonte.LerFichas(cfg.Patentes)
		bks := lerBackups()
		if len(bks) > 5 {
			bks = bks[:5]
		}
		canais := canaisDoBot()
		jsonOK(w, map[string]any{
			"eu":     a,
			"agora":  time.Now().Unix(),
			"ip":     ip,
			"status": st,
			"raid":   map[string]any{"aberto": aberto, "texto": txtRaid, "protecao": !strings.EqualFold(cfg.RaidProtecaoOffline, "false")},
			"contagem": map[string]int{
				"banidos": contarLinhas("banidos.tsv"), "suspeitos": contarLinhas("suspeitos.tsv"),
				"reports": contarLinhas("reports.tsv"), "jogadores": len(fichas), "staff": contarLinhas("staff.tsv"),
			},
			"discord":     disc,
			"botOnline":   botOn,
			"canaisBot":   map[string]string{"wipe": canais["wipe"], "anuncios": canais["anuncios"], "updates": canais["updates"], "geral": canais["geral"]},
			"backups":     bks,
			"acao":        lerAcao(),
			"historico":   lerHistorico(10),
			"votacaoJogo": lerVotacaoJogo(),
		})
	})

	mux.HandleFunc("/api/admin/jogadores", func(w http.ResponseWriter, r *http.Request) {
		if _, ok := exigirStaff(w, r); !ok {
			return
		}
		cfg := fonte.LerConfig()
		on := map[string]bool{}
		for _, j := range fonte.LerStatus().Jogadores {
			on[j.ID] = true
		}
		type jog struct {
			dados.Ficha
			Online bool `json:"online"`
		}
		l := []jog{}
		for _, f := range dados.Ordenar(fonte.LerFichas(cfg.Patentes), "xp") {
			l = append(l, jog{f, on[f.ID]})
		}
		jsonOK(w, l)
	})

	// mensagem / aviso / enquete pro jogo e/ou pro Discord
	mux.HandleFunc("/api/admin/mensagem", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			http.Error(w, "metodo", http.StatusMethodNotAllowed)
			return
		}
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		if !a.Mundo {
			http.Error(w, "voce nao tem a permissao de anuncio (Mundo)", http.StatusForbidden)
			return
		}
		var q struct {
			Jogo, Discord bool
			Canal, Tipo   string
			Titulo, Texto string
			Cor, Imagem   string
			Mencao        string
			Opcoes        []string
			Horas         int
			Multi         bool
			Rotulo        string
		}
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 16384)).Decode(&q); err != nil {
			http.Error(w, "pedido invalido", http.StatusBadRequest)
			return
		}
		q.Titulo, q.Texto = strings.TrimSpace(q.Titulo), strings.TrimSpace(q.Texto)
		if !q.Jogo && !q.Discord {
			http.Error(w, "escolha pelo menos um destino (jogo ou Discord)", http.StatusBadRequest)
			return
		}
		switch q.Tipo {
		case "mensagem", "aviso", "enquete":
		default:
			http.Error(w, "tipo invalido", http.StatusBadRequest)
			return
		}
		var opcoes []string
		for _, o := range q.Opcoes {
			if o = strings.TrimSpace(o); o != "" {
				if len([]rune(o)) > 55 {
					o = string([]rune(o)[:55])
				}
				opcoes = append(opcoes, o)
			}
		}
		if q.Tipo == "enquete" {
			if q.Titulo == "" || len(opcoes) < 2 || len(opcoes) > 10 {
				http.Error(w, "enquete precisa de pergunta e de 2 a 10 opcoes", http.StatusBadRequest)
				return
			}
			if len([]rune(q.Titulo)) > 300 {
				http.Error(w, "pergunta muito longa (max 300)", http.StatusBadRequest)
				return
			}
		} else if q.Texto == "" && q.Titulo == "" {
			http.Error(w, "escreva a mensagem", http.StatusBadRequest)
			return
		}
		if len([]rune(q.Texto)) > 3500 {
			http.Error(w, "texto muito longo (max 3500)", http.StatusBadRequest)
			return
		}
		res := map[string]any{}
		if q.Jogo && q.Tipo == "enquete" {
			// votação dentro do jogo: o plugin lê enquete_jogo.txt, anuncia e o pessoal vota com /votar N
			if err := criarVotacaoJogo(q.Titulo, opcoes, q.Horas); err != nil {
				res["jogo"] = map[string]any{"ok": false, "erro": "nao consegui criar a votacao no jogo"}
			} else {
				res["jogo"] = map[string]any{"ok": true, "online": fonte.LerStatus().Online, "votacao": true}
			}
		} else if q.Jogo {
			txt := q.Texto
			switch {
			case q.Titulo != "" && q.Texto != "":
				txt = q.Titulo + " - " + q.Texto
			case q.Titulo != "":
				txt = q.Titulo
			}
			if err := comandoJogo("anuncio", "0", a.Steam, 0, txt); err != nil {
				res["jogo"] = map[string]any{"ok": false, "erro": "nao consegui falar com o jogo"}
			} else {
				res["jogo"] = map[string]any{"ok": true, "online": fonte.LerStatus().Online}
			}
		}
		if q.Discord {
			if q.Canal == "" {
				res["discord"] = map[string]any{"ok": false, "erro": "escolha o canal do Discord"}
			} else {
				p := dados.PedidoBot{ID: dados.NovoID(), Tipo: q.Tipo, Canal: q.Canal, Titulo: q.Titulo, Texto: q.Texto, Cor: q.Cor,
					Imagem: strings.TrimSpace(q.Imagem), Mencao: q.Mencao, Opcoes: opcoes, Horas: q.Horas, Multi: q.Multi,
					Autor: a.Nome + " (" + a.Steam + ")", Criado: time.Now().Unix(), Rotulo: q.Rotulo}
				if p.Mencao != "everyone" && p.Mencao != "here" {
					p.Mencao = ""
				}
				res["discord"] = enviarAoBot(p)
			}
		}
		jsonOK(w, res)
	})

	// wipe / reinício / cancelar (só o dono)
	// limpar os itens do chão (precisa de Mundo ou ser dono); o plugin avisa no chat e limpa depois de N segundos
	mux.HandleFunc("/api/admin/limparitens", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			http.Error(w, "metodo", http.StatusMethodNotAllowed)
			return
		}
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		if !a.Mundo {
			http.Error(w, "voce nao tem a permissao Mundo", http.StatusForbidden)
			return
		}
		var q struct{ Segundos int }
		_ = json.NewDecoder(http.MaxBytesReader(w, r.Body, 1024)).Decode(&q)
		if q.Segundos < 0 || q.Segundos > 600 {
			q.Segundos = 30
		}
		if err := comandoJogo("limpar_itens", "0", a.Steam, 0, strconv.Itoa(q.Segundos)); err != nil {
			http.Error(w, "nao consegui falar com o jogo", http.StatusInternalServerError)
			return
		}
		jsonOK(w, map[string]any{"ok": true, "segundos": q.Segundos})
	})
	mux.HandleFunc("/api/admin/servidor", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			http.Error(w, "metodo", http.StatusMethodNotAllowed)
			return
		}
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		if !a.Dono {
			http.Error(w, "so o dono pode fazer wipe ou reiniciar", http.StatusForbidden)
			return
		}
		var q struct {
			Acao      string
			Quando    int64
			Itens     map[string]bool
			Motivo    string
			Confirmar string
			Anunciar  bool
			Canal     string
		}
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 8192)).Decode(&q); err != nil {
			http.Error(w, "pedido invalido", http.StatusBadRequest)
			return
		}
		arq := filepath.Join(dados.PastaPainel, "acao.json")
		agora := time.Now().Unix()
		if q.Acao == "cancelar" {
			at := lerAcao()
			if at == nil {
				http.Error(w, "nao tem nada agendado", http.StatusBadRequest)
				return
			}
			if err := os.Remove(arq); err != nil {
				http.Error(w, "nao consegui cancelar", http.StatusInternalServerError)
				return
			}
			nome := map[string]string{"wipe": "WIPE", "reiniciar": "REINICIO"}[at.Tipo]
			_ = comandoJogo("anuncio", "0", a.Steam, 0, nome+" CANCELADO pela staff.")
			jsonOK(w, map[string]any{"ok": true})
			return
		}
		if lerAcao() != nil {
			http.Error(w, "ja tem um wipe ou reinicio agendado: cancele antes", http.StatusConflict)
			return
		}
		if q.Quando < agora+60 {
			q.Quando = agora + 60
		}
		if q.Quando > agora+60*24*3600 {
			http.Error(w, "data muito longe (max 60 dias)", http.StatusBadRequest)
			return
		}
		ac := dados.AcaoServidor{Tipo: q.Acao, Quando: q.Quando, Motivo: strings.TrimSpace(q.Motivo), Por: a.Steam, PorNome: a.Nome, Criado: agora}
		switch q.Acao {
		case "reiniciar":
		case "wipe":
			if strings.ToUpper(strings.TrimSpace(q.Confirmar)) != "WIPE" {
				http.Error(w, "digite WIPE pra confirmar", http.StatusBadRequest)
				return
			}
			ac.Itens = map[string]bool{}
			for _, k := range itensWipe {
				if q.Itens[k] {
					ac.Itens[k] = true
				}
			}
			if len(ac.Itens) == 0 {
				http.Error(w, "marque o que vai ser apagado", http.StatusBadRequest)
				return
			}
			if ac.Quando < agora+5*60 {
				ac.Quando = agora + 5*60 // pelo menos 5 min pros avisos no jogo
			}
		default:
			http.Error(w, "acao invalida", http.StatusBadRequest)
			return
		}
		if err := dados.GravarJSON(arq, ac); err != nil {
			http.Error(w, "nao consegui agendar", http.StatusInternalServerError)
			return
		}
		quando := time.Unix(ac.Quando, 0).In(fusoBR).Format("02/01 às 15:04")
		res := map[string]any{"ok": true, "quando": ac.Quando}
		if q.Acao == "wipe" {
			_ = comandoJogo("anuncio", "0", a.Steam, 0, "WIPE marcado para "+strings.Replace(quando, "às", "as", 1)+" (horario de Brasilia).")
			if q.Anunciar && q.Canal != "" {
				var apaga []string
				for _, k := range itensWipe {
					if ac.Itens[k] {
						apaga = append(apaga, "• "+nomesWipe[k])
					}
				}
				txt := fmt.Sprintf("O wipe vai acontecer **%s** (horário de Brasília).\n\n**O que vai ser apagado:**\n%s", quando, strings.Join(apaga, "\n"))
				if ac.Motivo != "" {
					txt += "\n\n" + ac.Motivo
				}
				res["discord"] = enviarAoBot(dados.PedidoBot{ID: dados.NovoID(), Tipo: "aviso", Canal: q.Canal, Titulo: "🔄 WIPE MARCADO",
					Texto: txt, Cor: "#FF5A00", Mencao: "everyone", Autor: a.Nome + " (" + a.Steam + ")", Criado: agora})
			}
		} else {
			_ = comandoJogo("anuncio", "0", a.Steam, 0, "Servidor vai reiniciar "+strings.Replace(quando, "às", "as", 1)+".")
		}
		jsonOK(w, res)
	})
}

var itensWipe = []string{"bases", "safes", "veiculos", "inventario", "skills", "personagem", "banco", "ranking", "cofres", "kits"}
var nomesWipe = map[string]string{
	"bases": "Bases dos jogadores (as safes ficam)", "veiculos": "Veículos no mapa", "inventario": "Inventário e roupas",
	"skills": "Skills", "personagem": "Personagem inteiro (vida, posição, inventário, skills)",
	"banco": "Dinheiro do banco", "ranking": "Ranking e estatísticas", "cofres": "Cofres (vaults)", "kits": "Espera dos kits",
	"safes": "Construções das safes (zera o mapa inteiro)",
}

// enviarAoBot põe o pedido na fila e espera a resposta do bot (até 10 s).
func enviarAoBot(p dados.PedidoBot) map[string]any {
	if _, on := lerDiscord(); !on {
		return map[string]any{"ok": false, "erro": "o bot do Discord está desligado"}
	}
	if err := dados.GravarJSON(filepath.Join(dados.PastaPainel, "fila", p.ID+".json"), p); err != nil {
		return map[string]any{"ok": false, "erro": "nao consegui falar com o bot"}
	}
	feito := filepath.Join(dados.PastaPainel, "feitos", p.ID+".json")
	for i := 0; i < 40; i++ {
		time.Sleep(250 * time.Millisecond)
		var res dados.ResultadoBot
		if dados.LerJSON(feito, &res) == nil {
			_ = os.Remove(feito)
			return map[string]any{"ok": res.Ok, "erro": res.Erro, "link": res.Link}
		}
	}
	return map[string]any{"ok": true, "pendente": true}
}

// ---------------- votação no jogo ----------------

func criarVotacaoJogo(pergunta string, opcoes []string, horas int) error {
	if horas < 1 {
		horas = 24
	}
	limpa := strings.NewReplacer("\t", " ", "\n", " ", "\r", " ", "|", "/")
	var ops []string
	for _, o := range opcoes {
		ops = append(ops, limpa.Replace(o))
	}
	id := dados.NovoID()
	linha := fmt.Sprintf("%s\t%d\t%s\t%s\n", id, time.Now().Add(time.Duration(horas)*time.Hour).Unix(), limpa.Replace(pergunta), strings.Join(ops, "|"))
	arq := filepath.Join(fonte.Pasta, "enquete_jogo.txt")
	if err := os.WriteFile(arq+".tmp", []byte(linha), 0o640); err != nil {
		return err
	}
	return os.Rename(arq+".tmp", arq)
}

func lerVotacaoJogo() map[string]any {
	b, err := os.ReadFile(filepath.Join(fonte.Pasta, "enquete_jogo.txt"))
	if err != nil {
		return nil
	}
	c := strings.Split(strings.TrimSpace(string(b)), "\t")
	if len(c) < 4 {
		return nil
	}
	termina, _ := strconv.ParseInt(c[1], 10, 64)
	ops := strings.Split(c[3], "|")
	votos := make([]int, len(ops))
	total := 0
	for _, l := range lerTSV("enquete_votos.tsv", 0) {
		if len(l) >= 3 && l[0] == c[0] {
			if n, err := strconv.Atoi(l[2]); err == nil && n >= 1 && n <= len(ops) {
				votos[n-1]++
				total++
			}
		}
	}
	resp := []map[string]any{}
	for i, o := range ops {
		resp = append(resp, map[string]any{"texto": o, "votos": votos[i]})
	}
	return map[string]any{"id": c[0], "pergunta": c[2], "termina": termina, "encerrada": time.Now().Unix() >= termina,
		"respostas": resp, "total": total}
}
