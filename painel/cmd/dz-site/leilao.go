package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"
)

// Leilão: o plugin grava leilao.json; o lance do site vai pro leilao_comandos.txt (só logado com a Steam)
// e o plugin responde no leilao_respostas.tsv.

var muLeilao sync.Mutex

func rotasLeilao(mux *http.ServeMux) {
	mux.HandleFunc("/api/leilao", func(w http.ResponseWriter, r *http.Request) {
		var st struct {
			Lotes []struct {
				Id           int
				VendedorNome string
				Item         struct {
					Id, Qtd int
					Nome    string
				}
				Minimo, Lance int64
				Lider         uint64
				LiderNome     string
				Fim           time.Time
				Encerrado     bool
				Lances        int
			}
		}
		if b, err := os.ReadFile(filepath.Join(fonte.Pasta, "leilao.json")); err == nil {
			_ = json.Unmarshal(b, &st)
		}
		eu := lerSessao(r)
		l := []map[string]any{}
		for _, x := range st.Lotes {
			minimo := x.Minimo
			if x.Lance > 0 {
				inc := x.Lance * 5 / 100
				if inc < 1 {
					inc = 1
				}
				minimo = x.Lance + inc
			}
			l = append(l, map[string]any{"id": x.Id, "vendedor": x.VendedorNome, "item": x.Item.Id, "qtd": x.Item.Qtd, "nome": x.Item.Nome,
				"lance": x.Lance, "proximo": minimo, "lider": x.LiderNome, "souLider": eu != "" && fmt.Sprint(x.Lider) == eu,
				"fim": x.Fim, "encerrado": x.Encerrado, "lances": x.Lances})
		}
		jsonOK(w, map[string]any{"lotes": l, "logado": eu != "", "respostas": respostasLeilao(eu)})
	})
	mux.HandleFunc("/api/leilao/lance", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			http.Error(w, "metodo", http.StatusMethodNotAllowed)
			return
		}
		eu := lerSessao(r)
		if eu == "" {
			http.Error(w, "entre com a Steam para dar lance", http.StatusUnauthorized)
			return
		}
		var q struct {
			Id    int
			Valor int64
		}
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 1024)).Decode(&q); err != nil || q.Id <= 0 || q.Valor <= 0 || q.Valor > 1_000_000_000 {
			http.Error(w, "lance invalido", http.StatusBadRequest)
			return
		}
		muLeilao.Lock()
		defer muLeilao.Unlock()
		f, err := os.OpenFile(filepath.Join(fonte.Pasta, "leilao_comandos.txt"), os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o640)
		if err != nil {
			http.Error(w, "nao consegui falar com o jogo", http.StatusInternalServerError)
			return
		}
		defer f.Close()
		if _, err := fmt.Fprintf(f, "lance\t%d\t%s\t%d\n", q.Id, eu, q.Valor); err != nil {
			http.Error(w, "nao consegui falar com o jogo", http.StatusInternalServerError)
			return
		}
		jsonOK(w, map[string]any{"ok": true})
	})
}

// respostasLeilao: as últimas respostas do jogo pros lances dessa pessoa (últimos 10 min).
func respostasLeilao(steam string) []string {
	l := []string{}
	if steam == "" {
		return l
	}
	f, err := os.Open(filepath.Join(fonte.Pasta, "leilao_respostas.tsv"))
	if err != nil {
		return l
	}
	defer f.Close()
	sc := bufio.NewScanner(f)
	for sc.Scan() {
		c := strings.Split(sc.Text(), "\t")
		if len(c) < 4 || c[1] != steam {
			continue
		}
		if t, err := time.Parse(time.RFC3339Nano, c[0]); err == nil && time.Since(t) < 10*time.Minute {
			l = append(l, c[3])
		}
	}
	if len(l) > 3 {
		l = l[len(l)-3:]
	}
	return l
}
