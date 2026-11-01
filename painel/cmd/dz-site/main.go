// dz-site: site do DAY Z BRASIL OFICIAL (status ao vivo, ranking, kits, regras,
// loja e perfil com login pela Steam). Lê os arquivos do plugin SobrevivenciaCore.
package main

import (
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"embed"
	"encoding/hex"
	"encoding/json"
	"flag"
	"io"
	"io/fs"
	"log"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"

	"github.com/protalhard/servidor-unturned/painel/dados"
)

//go:embed web
var webFS embed.FS

var (
	chaveVinculo []byte
	fonte        dados.Fonte
	segredo      []byte
	ip           string
)

func main() {
	addr := flag.String("addr", ":80", "endereço")
	pasta := flag.String("dados", dados.PastaPadrao, "pasta do plugin SobrevivenciaCore")
	arqSegredo := flag.String("segredo", "/home/unturned/dz-site/segredo", "arquivo com a chave dos cookies")
	flag.StringVar(&ip, "ip", "54.94.93.87:27015", "IP:porta do servidor do jogo")
	pastaIcones := flag.String("icones", pastaIconesPadrao(), "pasta com os PNGs dos itens (<id>.png)")
	arqChave := flag.String("chave-vinculo", dados.ArqChavePadrao, "chave dividida com o bot pra vincular Discord e Steam")
	flag.Parse()
	chaveVinculo = dados.Chave(*arqChave)
	fonte = dados.Fonte{Pasta: *pasta}
	segredo = carregarSegredo(*arqSegredo)

	web, _ := fs.Sub(webFS, "web")
	arquivos := http.FileServer(http.FS(web))

	mux := http.NewServeMux()
	// fotos dos itens pra janela da loja no jogo: /icones/<id>.png (sem a foto, manda a padrão)
	mux.HandleFunc("/icones/", func(w http.ResponseWriter, r *http.Request) {
		nome := filepath.Base(r.URL.Path)
		if !strings.HasSuffix(nome, ".png") || strings.Contains(nome, "..") {
			http.NotFound(w, r)
			return
		}
		arq := filepath.Join(*pastaIcones, nome)
		if _, err := os.Stat(arq); err != nil {
			arq = filepath.Join(*pastaIcones, "sem_foto.png")
		}
		w.Header().Set("Cache-Control", "public, max-age=86400")
		w.Header().Set("Content-Type", "image/png")
		http.ServeFile(w, r, arq)
	})
	mux.HandleFunc("/api/status", apiStatus)
	mux.HandleFunc("/api/ranking", apiRanking)
	mux.HandleFunc("/api/kits", apiKits)
	mux.HandleFunc("/api/jogador/", apiJogador)
	mux.HandleFunc("/api/eu", apiEu)
	mux.HandleFunc("/entrar", entrar)
	mux.HandleFunc("/entrar/retorno", retorno)
	mux.HandleFunc("/sair", sair)
	mux.HandleFunc("/vincular", vincular)
	mux.HandleFunc("/vincular/retorno", vincularRetorno)
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		p := strings.Trim(r.URL.Path, "/")
		switch p {
		case "", "servidores", "ranking", "kits", "regras", "loja", "sobre", "perfil":
			w.Header().Set("Cache-Control", "no-cache")
			b, _ := fs.ReadFile(web, "index.html")
			w.Header().Set("Content-Type", "text/html; charset=utf-8")
			_, _ = w.Write(b)
			return
		}
		if strings.HasPrefix(p, "jogador/") {
			b, _ := fs.ReadFile(web, "index.html")
			w.Header().Set("Content-Type", "text/html; charset=utf-8")
			_, _ = w.Write(b)
			return
		}
		w.Header().Set("Cache-Control", "public, max-age=3600")
		arquivos.ServeHTTP(w, r)
	})

	srv := &http.Server{Addr: *addr, Handler: cabecalhos(mux), ReadTimeout: 10 * time.Second, WriteTimeout: 20 * time.Second}
	log.Println("dz-site em", *addr)
	log.Fatal(srv.ListenAndServe())
}

func cabecalhos(h http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("X-Content-Type-Options", "nosniff")
		w.Header().Set("Referrer-Policy", "same-origin")
		h.ServeHTTP(w, r)
	})
}

func carregarSegredo(arq string) []byte {
	if b, err := os.ReadFile(arq); err == nil && len(b) >= 32 {
		return b
	}
	b := make([]byte, 32)
	_, _ = rand.Read(b)
	_ = os.MkdirAll(filepath.Dir(arq), 0o700)
	_ = os.WriteFile(arq, b, 0o600)
	return b
}

func jsonOK(w http.ResponseWriter, v any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Header().Set("Cache-Control", "no-cache")
	_ = json.NewEncoder(w).Encode(v)
}

// ---------------- API ----------------

func apiStatus(w http.ResponseWriter, r *http.Request) {
	st := fonte.LerStatus()
	cfg := fonte.LerConfig()
	jsonOK(w, map[string]any{"status": st, "ip": ip, "jogadores": len(st.Jogadores), "discord": cfg.Discord})
}

func apiRanking(w http.ResponseWriter, r *http.Request) {
	cfg := fonte.LerConfig()
	por := r.URL.Query().Get("por")
	busca := strings.ToLower(strings.TrimSpace(r.URL.Query().Get("busca")))
	todos := fonte.LerFichas(cfg.Patentes)
	l := dados.Ordenar(todos, por)
	saida := []dados.Ficha{}
	for _, f := range l {
		if busca != "" && !strings.Contains(strings.ToLower(f.Nome), busca) && !strings.Contains(f.ID, busca) {
			continue
		}
		saida = append(saida, f)
		if len(saida) == 100 {
			break
		}
	}
	jsonOK(w, map[string]any{"jogadores": saida, "total": len(todos)})
}

func apiKits(w http.ResponseWriter, r *http.Request) {
	cfg := fonte.LerConfig()
	jsonOK(w, map[string]any{"kits": nn(cfg.Kits), "lojaXp": nn(cfg.LojaXp), "patentes": nn(cfg.Patentes)})
}

// nn devolve lista vazia em vez de null no JSON.
func nn[T any](l []T) []T {
	if l == nil {
		return []T{}
	}
	return l
}

func acharFicha(id string) *dados.Ficha {
	cfg := fonte.LerConfig()
	for _, f := range fonte.LerFichas(cfg.Patentes) {
		if f.ID == id {
			f := f
			return &f
		}
	}
	return nil
}

func apiJogador(w http.ResponseWriter, r *http.Request) {
	id := strings.TrimPrefix(r.URL.Path, "/api/jogador/")
	if f := acharFicha(id); f != nil {
		jsonOK(w, f)
		return
	}
	http.Error(w, "nao encontrado", http.StatusNotFound)
}

func apiEu(w http.ResponseWriter, r *http.Request) {
	id := lerSessao(r)
	if id == "" {
		jsonOK(w, map[string]any{"logado": false})
		return
	}
	jsonOK(w, map[string]any{"logado": true, "id": id, "ficha": acharFicha(id), "kits": nn(fonte.LerConfig().Kits)})
}

// ---------------- Login com a Steam (OpenID 2.0) ----------------

const steamOpenID = "https://steamcommunity.com/openid/login"

func base(r *http.Request) string {
	esquema := "http"
	if r.TLS != nil || r.Header.Get("X-Forwarded-Proto") == "https" {
		esquema = "https"
	}
	return esquema + "://" + r.Host
}

func entrar(w http.ResponseWriter, r *http.Request) { redirSteam(w, r, "/entrar/retorno") }

// steamDoRetorno confere a resposta da Steam (OpenID) e devolve o SteamID64 ("" se inválida).
func steamDoRetorno(r *http.Request, caminho string) string {
	q := r.URL.Query()
	if q.Get("openid.mode") != "id_res" || !strings.HasPrefix(q.Get("openid.return_to"), base(r)+caminho) {
		return ""
	}
	v := url.Values{}
	for k, vs := range q {
		if strings.HasPrefix(k, "openid.") {
			v[k] = vs
		}
	}
	v.Set("openid.mode", "check_authentication")
	cli := &http.Client{Timeout: 10 * time.Second}
	resp, err := cli.PostForm(steamOpenID, v)
	if err != nil {
		return ""
	}
	defer resp.Body.Close()
	corpo, _ := io.ReadAll(io.LimitReader(resp.Body, 4096))
	if !strings.Contains(string(corpo), "is_valid:true") {
		return ""
	}
	const prefixo = "https://steamcommunity.com/openid/id/"
	claimed := q.Get("openid.claimed_id")
	id := strings.TrimPrefix(claimed, prefixo)
	if !strings.HasPrefix(claimed, prefixo) || len(id) != 17 {
		return ""
	}
	if _, err := strconv.ParseUint(id, 10, 64); err != nil {
		return ""
	}
	return id
}

func redirSteam(w http.ResponseWriter, r *http.Request, caminho string) {
	b := base(r)
	q := url.Values{
		"openid.ns":         {"http://specs.openid.net/auth/2.0"},
		"openid.mode":       {"checkid_setup"},
		"openid.return_to":  {b + caminho},
		"openid.realm":      {b + "/"},
		"openid.identity":   {"http://specs.openid.net/auth/2.0/identifier_select"},
		"openid.claimed_id": {"http://specs.openid.net/auth/2.0/identifier_select"},
	}
	http.Redirect(w, r, steamOpenID+"?"+q.Encode(), http.StatusFound)
}

func logar(w http.ResponseWriter, r *http.Request, id string) {
	expira := time.Now().Add(30 * 24 * time.Hour).Unix()
	valor := id + "." + strconv.FormatInt(expira, 10)
	http.SetCookie(w, &http.Cookie{Name: "dz", Value: valor + "." + assinar(valor), Path: "/", HttpOnly: true,
		SameSite: http.SameSiteLaxMode, Expires: time.Unix(expira, 0), Secure: r.TLS != nil})
}

func retorno(w http.ResponseWriter, r *http.Request) {
	id := steamDoRetorno(r, "/entrar/retorno")
	if id == "" {
		http.Redirect(w, r, "/perfil?erro=1", http.StatusFound)
		return
	}
	logar(w, r, id)
	http.Redirect(w, r, "/perfil", http.StatusFound)
}

// /vincular?d=<discord>&e=<expira>&s=<assinatura>: link que o bot manda no #vincular.
func vincular(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query()
	d, e, s := q.Get("d"), q.Get("e"), q.Get("s")
	if !dados.ConferirVinculo(chaveVinculo, d, e, s) {
		http.Redirect(w, r, "/perfil?vinculo=expirado", http.StatusFound)
		return
	}
	http.SetCookie(w, &http.Cookie{Name: "dzv", Value: d + "." + e + "." + s, Path: "/vincular", HttpOnly: true,
		SameSite: http.SameSiteLaxMode, MaxAge: 900, Secure: r.TLS != nil})
	redirSteam(w, r, "/vincular/retorno")
}

func vincularRetorno(w http.ResponseWriter, r *http.Request) {
	c, err := r.Cookie("dzv")
	if err != nil {
		http.Redirect(w, r, "/perfil?vinculo=expirado", http.StatusFound)
		return
	}
	p := strings.Split(c.Value, ".")
	if len(p) != 3 || !dados.ConferirVinculo(chaveVinculo, p[0], p[1], p[2]) {
		http.Redirect(w, r, "/perfil?vinculo=expirado", http.StatusFound)
		return
	}
	id := steamDoRetorno(r, "/vincular/retorno")
	if id == "" {
		http.Redirect(w, r, "/perfil?erro=1", http.StatusFound)
		return
	}
	if err := fonte.GravarVinculo(p[0], id); err != nil {
		log.Println("vinculo:", err)
		http.Redirect(w, r, "/perfil?vinculo=erro", http.StatusFound)
		return
	}
	http.SetCookie(w, &http.Cookie{Name: "dzv", Value: "", Path: "/vincular", MaxAge: -1})
	logar(w, r, id)
	http.Redirect(w, r, "/perfil?vinculo=ok", http.StatusFound)
}

func sair(w http.ResponseWriter, r *http.Request) {
	http.SetCookie(w, &http.Cookie{Name: "dz", Value: "", Path: "/", MaxAge: -1})
	http.Redirect(w, r, "/", http.StatusFound)
}

func assinar(s string) string {
	m := hmac.New(sha256.New, segredo)
	m.Write([]byte(s))
	return hex.EncodeToString(m.Sum(nil))
}

func lerSessao(r *http.Request) string {
	c, err := r.Cookie("dz")
	if err != nil {
		return ""
	}
	p := strings.Split(c.Value, ".")
	if len(p) != 3 || !hmac.Equal([]byte(assinar(p[0]+"."+p[1])), []byte(p[2])) {
		return ""
	}
	exp, _ := strconv.ParseInt(p[1], 10, 64)
	if time.Now().Unix() > exp {
		return ""
	}
	return p[0]
}

// pastaIconesPadrao: o repositório na AWS (/home/unturned/repo) ou <repo>/server-files/icones ao lado do binário.
func pastaIconesPadrao() string {
	const naAws = "/home/unturned/repo/server-files/icones"
	if _, err := os.Stat(naAws); err == nil {
		return naAws
	}
	if exe, err := os.Executable(); err == nil {
		return filepath.Join(filepath.Dir(exe), "..", "..", "server-files", "icones")
	}
	return "icones"
}
