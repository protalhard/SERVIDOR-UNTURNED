package main

// Painel admin no site (/admin): banidos com motivo, log e foto, suspeitos do DZ GUARD, reports e logs.
// Só entra quem é dono (config Donos) ou está no staff.tsv do plugin. Desbanir/banir/ignorar precisa da
// permissão "Ban": o site escreve o pedido em admin_comandos.txt e o plugin executa (e confere de novo).

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io/fs"
	"net/http"
	"os"
	"path/filepath"
	"regexp"
	"slices"
	"strconv"
	"strings"
	"sync"
	"time"
)

type acessoAdmin struct {
	Steam string `json:"steam"`
	Nome  string `json:"nome"`
	Staff bool   `json:"staff"`
	Dono  bool   `json:"dono"`
	Ban   bool   `json:"ban"`
	Mundo bool   `json:"mundo"` // anúncios e mensagens (aba Mensagens)
}

// permissoesAdmin: as mesmas do plugin (Plugin.PermissoesAdmin).
var permissoesAdmin = []string{"Kick", "Ban", "Tp", "Trazer", "Curar", "Matar", "Congelar", "Spy", "Itens", "Visao", "Deus", "Mundo", "Airdrop", "Reiniciar", "Precos", "Kits", "Skills"}

func acessoDe(id string) acessoAdmin {
	a := acessoAdmin{Steam: id}
	if id == "" {
		return a
	}
	for _, d := range strings.FieldsFunc(fonte.LerConfig().Donos, func(r rune) bool { return r == ',' || r == ' ' || r == ';' }) {
		if d == id {
			a.Dono, a.Staff, a.Ban, a.Mundo = true, true, true, true
		}
	}
	for _, c := range lerTSV("staff.tsv", 0) {
		if len(c) >= 3 && c[0] == id {
			a.Staff = true
			a.Nome = c[1]
			for _, p := range strings.Split(c[2], ",") {
				if p == "Ban" {
					a.Ban = true
				}
				if p == "Mundo" {
					a.Mundo = true
				}
			}
		}
	}
	if f := acharFicha(id); f != nil && a.Nome == "" {
		a.Nome = f.Nome
	}
	return a
}

// lerTSV lê um arquivo da pasta do plugin; max > 0 guarda só as últimas max linhas.
func lerTSV(nome string, max int) [][]string {
	f, err := os.Open(filepath.Join(fonte.Pasta, nome))
	if err != nil {
		return nil
	}
	defer f.Close()
	var r [][]string
	sc := bufio.NewScanner(f)
	sc.Buffer(make([]byte, 64*1024), 1024*1024)
	for sc.Scan() {
		if l := strings.TrimRight(sc.Text(), "\r"); l != "" {
			r = append(r, strings.Split(l, "\t"))
			if max > 0 && len(r) > max*2 {
				r = r[len(r)-max:]
			}
		}
	}
	if max > 0 && len(r) > max {
		r = r[len(r)-max:]
	}
	return r
}

func campo(c []string, i int) string {
	if i < len(c) {
		return c[i]
	}
	return ""
}

func num(s string) int64 { n, _ := strconv.ParseInt(s, 10, 64); return n }

// exigirStaff devolve o acesso ou responde 401/403.
func exigirStaff(w http.ResponseWriter, r *http.Request) (acessoAdmin, bool) {
	a := acessoDe(lerSessao(r))
	if a.Steam == "" {
		http.Error(w, "entre com a Steam", http.StatusUnauthorized)
		return a, false
	}
	if !a.Staff {
		http.Error(w, "so staff", http.StatusForbidden)
		return a, false
	}
	return a, true
}

func registrarAdmin(mux *http.ServeMux, web fs.FS) {
	mux.HandleFunc("/admin", func(w http.ResponseWriter, r *http.Request) {
		b, _ := fs.ReadFile(web, "admin.html")
		w.Header().Set("Cache-Control", "no-cache")
		w.Header().Set("Content-Type", "text/html; charset=utf-8")
		w.Header().Set("X-Frame-Options", "DENY")
		_, _ = w.Write(b)
	})
	mux.HandleFunc("/admin/entrar", func(w http.ResponseWriter, r *http.Request) { redirSteam(w, r, "/admin/retorno") })
	mux.HandleFunc("/admin/retorno", func(w http.ResponseWriter, r *http.Request) {
		id := steamDoRetorno(r, "/admin/retorno")
		if id == "" {
			http.Redirect(w, r, "/admin?erro=1", http.StatusFound)
			return
		}
		logar(w, r, id)
		http.Redirect(w, r, "/admin", http.StatusFound)
	})
	mux.HandleFunc("/api/admin/eu", func(w http.ResponseWriter, r *http.Request) {
		jsonOK(w, acessoDe(lerSessao(r)))
	})
	mux.HandleFunc("/api/admin/banidos", func(w http.ResponseWriter, r *http.Request) {
		if _, ok := exigirStaff(w, r); !ok {
			return
		}
		type banido struct {
			Steam, Nome, Juiz, JuizNome, Motivo, Evidencia, Foto string
			Banido, Duracao, Resta                               int64
		}
		l := []banido{}
		for _, c := range lerTSV("banidos.tsv", 0) {
			l = append(l, banido{Steam: campo(c, 0), Nome: campo(c, 1), Juiz: campo(c, 2), JuizNome: campo(c, 3), Motivo: campo(c, 4),
				Banido: num(campo(c, 5)), Duracao: num(campo(c, 6)), Resta: num(campo(c, 7)), Evidencia: campo(c, 8), Foto: campo(c, 9)})
		}
		jsonOK(w, l)
	})
	mux.HandleFunc("/api/admin/suspeitos", func(w http.ResponseWriter, r *http.Request) {
		if _, ok := exigirStaff(w, r); !ok {
			return
		}
		type suspeito struct {
			Steam, Nome string
			Pontos      int64
			Ultimo      int64
			Motivos     []string
		}
		l := []suspeito{}
		for _, c := range lerTSV("suspeitos.tsv", 0) {
			var m []string
			if x := campo(c, 4); x != "" {
				m = strings.Split(x, " | ")
			}
			l = append(l, suspeito{Steam: campo(c, 0), Nome: campo(c, 1), Pontos: num(campo(c, 2)), Ultimo: num(campo(c, 3)), Motivos: nn(m)})
		}
		jsonOK(w, l)
	})
	mux.HandleFunc("/api/admin/reports", func(w http.ResponseWriter, r *http.Request) {
		if _, ok := exigirStaff(w, r); !ok {
			return
		}
		type report struct {
			Quando                                         int64
			Steam, Nome, QuemSteam, QuemNome, Motivo, Foto string
			Historico                                      []string
		}
		l := []report{}
		linhas := lerTSV("reports.tsv", 150)
		for i := len(linhas) - 1; i >= 0; i-- {
			c := linhas[i]
			var h []string
			if x := campo(c, 7); x != "" {
				h = strings.Split(x, " | ")
			}
			l = append(l, report{Quando: num(campo(c, 0)), Steam: campo(c, 1), Nome: campo(c, 2), QuemSteam: campo(c, 3), QuemNome: campo(c, 4),
				Motivo: campo(c, 5), Foto: campo(c, 6), Historico: nn(h)})
		}
		jsonOK(w, l)
	})
	mux.HandleFunc("/api/admin/log", func(w http.ResponseWriter, r *http.Request) {
		if _, ok := exigirStaff(w, r); !ok {
			return
		}
		arq := "admin_log.txt"
		switch r.URL.Query().Get("q") {
		case "guard":
			arq = "guard_log.txt"
		case "raid":
			arq = "raid_log.txt"
		}
		l := []string{}
		linhas := lerTSV(arq, 400)
		for i := len(linhas) - 1; i >= 0; i-- {
			l = append(l, strings.Join(linhas[i], " "))
		}
		jsonOK(w, l)
	})
	reFoto := regexp.MustCompile(`^[0-9]+_[0-9]{8}_[0-9]{6}_[a-z]+\.jpg$`)
	mux.HandleFunc("/api/admin/foto/", func(w http.ResponseWriter, r *http.Request) {
		if _, ok := exigirStaff(w, r); !ok {
			return
		}
		nome := strings.TrimPrefix(r.URL.Path, "/api/admin/foto/")
		if !reFoto.MatchString(nome) {
			http.NotFound(w, r)
			return
		}
		w.Header().Set("Content-Type", "image/jpeg")
		w.Header().Set("Cache-Control", "private, max-age=3600")
		http.ServeFile(w, r, filepath.Join(fonte.Pasta, "spy", nome))
	})
	// staff: só o dono vê e mexe (o plugin confere de novo)
	mux.HandleFunc("/api/admin/staff", func(w http.ResponseWriter, r *http.Request) {
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		if !a.Dono {
			http.Error(w, "so o dono", http.StatusForbidden)
			return
		}
		type membro struct {
			Steam, Nome string
			Perms       []string
		}
		l := []membro{}
		for _, c := range lerTSV("staff.tsv", 0) {
			if len(c) < 3 {
				continue
			}
			var ps []string
			for _, p := range strings.Split(c[2], ",") {
				if p != "" {
					ps = append(ps, p)
				}
			}
			l = append(l, membro{Steam: c[0], Nome: c[1], Perms: nn(ps)})
		}
		jsonOK(w, map[string]any{"staff": l, "permissoes": permissoesAdmin})
	})
	var muCmd sync.Mutex
	reSteam := regexp.MustCompile(`^7656[0-9]{13}$`)
	mux.HandleFunc("/api/admin/acao", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			http.Error(w, "metodo", http.StatusMethodNotAllowed)
			return
		}
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		var q struct {
			Acao, Steam, Motivo string
			Horas               float64
		}
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4096)).Decode(&q); err != nil {
			http.Error(w, "pedido invalido", http.StatusBadRequest)
			return
		}
		switch q.Acao {
		case "desbanir", "banir", "ignorar":
			if !a.Ban {
				http.Error(w, "voce nao tem a permissao Ban", http.StatusForbidden)
				return
			}
		case "perm", "staff_remover":
			if !a.Dono {
				http.Error(w, "so o dono mexe na staff", http.StatusForbidden)
				return
			}
			if q.Acao == "perm" && !slices.Contains(permissoesAdmin, q.Motivo) {
				http.Error(w, "permissao invalida", http.StatusBadRequest)
				return
			}
		default:
			http.Error(w, "acao invalida", http.StatusBadRequest)
			return
		}
		if !reSteam.MatchString(q.Steam) {
			http.Error(w, "Steam ID invalido (17 numeros, comeca com 7656)", http.StatusBadRequest)
			return
		}
		limpa := func(s string) string {
			return strings.NewReplacer("\t", " ", "\n", " ", "\r", " ").Replace(strings.TrimSpace(s))
		}
		motivo := limpa(q.Motivo)
		if len(motivo) > 200 {
			motivo = motivo[:200]
		}
		linha := fmt.Sprintf("%s\t%s\t%s\t%g\t%s\n", q.Acao, q.Steam, a.Steam, q.Horas, motivo)
		muCmd.Lock()
		defer muCmd.Unlock()
		f, err := os.OpenFile(filepath.Join(fonte.Pasta, "admin_comandos.txt"), os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o640)
		if err != nil {
			http.Error(w, "nao consegui falar com o servidor", http.StatusInternalServerError)
			return
		}
		defer f.Close()
		if _, err := f.WriteString(linha); err != nil {
			http.Error(w, "nao consegui falar com o servidor", http.StatusInternalServerError)
			return
		}
		jsonOK(w, map[string]any{"ok": true, "quando": time.Now().Unix()})
	})
}

// separarAdmin: com o domínio do painel configurado, o painel só abre nele (a raiz já leva pro /admin)
// e some do site público e do IP (404).
func separarAdmin(dominioAdmin string, h http.Handler) http.Handler {
	dominioAdmin = strings.ToLower(strings.TrimSpace(dominioAdmin))
	if dominioAdmin == "" {
		return h
	}
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		host := strings.ToLower(r.Host)
		if i := strings.LastIndexByte(host, ':'); i > 0 && !strings.Contains(host[i:], "]") {
			host = host[:i]
		}
		ehPainel := strings.HasPrefix(r.URL.Path, "/admin") || strings.HasPrefix(r.URL.Path, "/api/admin")
		if host == dominioAdmin {
			if r.URL.Path == "/" {
				http.Redirect(w, r, "/admin", http.StatusFound)
				return
			}
			w.Header().Set("X-Robots-Tag", "noindex, nofollow")
		} else if ehPainel {
			http.NotFound(w, r)
			return
		}
		h.ServeHTTP(w, r)
	})
}
