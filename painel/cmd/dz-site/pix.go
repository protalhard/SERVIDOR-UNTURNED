package main

// VIP por Pix (Mercado Pago, API de Orders). O dono cola o Access Token no painel admin (fica num arquivo 0600 no servidor,
// nunca volta pro navegador). O jogador logado com a Steam escolhe o pacote, o site cria o Pix no Mercado Pago
// e mostra o QR Code. Quando o Mercado Pago confirma (aviso no webhook ou a conferência a cada minuto), o site
// confere o pagamento direto na API e manda "vip" pro jogo pelo admin_comandos.txt. Cada pagamento entra uma vez só.

import (
	"bytes"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"math"
	"net/http"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/protalhard/servidor-unturned/painel/dados"
)

type pacotePix struct {
	Nome     string
	Centavos int64
	Escolhas []string
}

// preços iguais aos da página da Loja VIP (app.js)
var pacotesPix = map[string]pacotePix{
	"iniciante":         {"Kit Iniciante", 640, []string{"andarilho", "batedor", "saqueador"}},
	"bandido":           {"Bandido", 1940, nil},
	"mercenario":        {"Mercenário", 3090, nil},
	"comandante":        {"Comandante", 4530, nil},
	"especial":          {"Kit Especial", 2160, []string{"zonaquente", "base", "raid"}},
	"cofre-mochila":     {"Cofre Mochila", 1360, nil},
	"cofre-bunker":      {"Cofre Bunker", 2290, nil},
	"cofre-fortaleza":   {"Cofre Fortaleza", 4290, nil},
	"supremo":           {"DZ Supremo", 7770, nil},
	"supremo-vitalicio": {"DZ Supremo Vitalício", 25590, nil},
}

type pedidoPix struct {
	Id       string    `json:"id"` // id do pagamento no Mercado Pago
	Steam    string    `json:"steam"`
	Pacote   string    `json:"pacote"`
	Escolha  string    `json:"escolha"`
	Centavos int64     `json:"centavos"`
	Status   string    `json:"status"`
	Entregue bool      `json:"entregue"`
	Criado   time.Time `json:"criado"`
	Expira   time.Time `json:"expira"`
	Qr       string    `json:"qr"`
	QrImg    string    `json:"qrImg"`
}

var (
	muPix      sync.Mutex
	pastaPix   = "/home/unturned/dz-site"
	dominioPix string
	clientePix = &http.Client{Timeout: 20 * time.Second}
	reIdPix    = regexp.MustCompile(`^[A-Za-z0-9]{1,40}$`)
)

func arqTokenPix() string   { return filepath.Join(pastaPix, "mercadopago_token") }
func arqPedidosPix() string { return filepath.Join(pastaPix, "pix_pedidos.json") }

func tokenPix() string {
	b, err := os.ReadFile(arqTokenPix())
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(b))
}

func lerPedidosPix() []*pedidoPix {
	var l []*pedidoPix
	_ = dados.LerJSON(arqPedidosPix(), &l)
	return l
}

func gravarPedidosPix(l []*pedidoPix) {
	if len(l) > 5000 {
		l = l[len(l)-5000:]
	}
	if err := dados.GravarJSON(arqPedidosPix(), l); err != nil {
		log.Println("pix: gravar pedidos:", err)
	}
}

// chamarMP chama a API do Mercado Pago com o token do dono.
func chamarMP(metodo, caminho string, corpo any, idem string) (map[string]any, int, error) {
	tok := tokenPix()
	if tok == "" {
		return nil, 0, fmt.Errorf("sem token")
	}
	var rd io.Reader
	if corpo != nil {
		b, _ := json.Marshal(corpo)
		rd = bytes.NewReader(b)
	}
	req, _ := http.NewRequest(metodo, "https://api.mercadopago.com"+caminho, rd)
	req.Header.Set("Authorization", "Bearer "+tok)
	req.Header.Set("Content-Type", "application/json")
	if idem != "" {
		req.Header.Set("X-Idempotency-Key", idem)
	}
	resp, err := clientePix.Do(req)
	if err != nil {
		return nil, 0, err
	}
	defer resp.Body.Close()
	var m map[string]any
	_ = json.NewDecoder(io.LimitReader(resp.Body, 1<<20)).Decode(&m)
	return m, resp.StatusCode, nil
}

func idAleatorio() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b)
}

// conferirPix olha o pagamento no Mercado Pago e entrega o VIP se foi aprovado com o valor certo.
func conferirPix(id string) {
	if !reIdPix.MatchString(id) {
		return
	}
	m, code, err := chamarMP("GET", "/v1/orders/"+id, nil, "")
	if err != nil || code != 200 {
		return
	}
	status, _ := m["status"].(string)
	detalhe, _ := m["status_detail"].(string)
	valorTxt, _ := m["total_amount"].(string)
	valor, _ := strconv.ParseFloat(valorTxt, 64)
	ref, _ := m["external_reference"].(string)
	pago := status == "processed" && detalhe == "accredited"
	muPix.Lock()
	defer muPix.Unlock()
	l := lerPedidosPix()
	for _, p := range l {
		if p.Id != id {
			continue
		}
		p.Status = status
		if pago && !p.Entregue {
			// confere que é o mesmo pedido e o mesmo valor (nada vem do aviso, tudo da API)
			if ref != p.Steam+"|"+p.Pacote+"|"+p.Escolha || int64(math.Round(valor*100)) < p.Centavos {
				log.Printf("pix: pagamento %s aprovado mas não confere (ref %q valor %.2f)", id, ref, valor)
				break
			}
			if err := comandoJogo("vip", p.Steam, "0", 0, p.Pacote+"|"+p.Escolha+"|mp"+p.Id); err != nil {
				log.Println("pix: mandar vip pro jogo:", err)
				break
			}
			p.Entregue = true
			log.Printf("pix: VIP %s (%s) entregue pra %s, pagamento %s", p.Pacote, p.Escolha, p.Steam, p.Id)
		}
		break
	}
	gravarPedidosPix(l)
}

func rotasPix(mux *http.ServeMux) {
	// confere os Pix pendentes das últimas 2 horas a cada minuto (se o aviso do Mercado Pago se perder)
	go func() {
		for range time.Tick(time.Minute) {
			muPix.Lock()
			var ids []string
			for _, p := range lerPedidosPix() {
				if !p.Entregue && (p.Status == "action_required" || p.Status == "processing" || p.Status == "processed" || p.Status == "created") && time.Since(p.Criado) < 2*time.Hour {
					ids = append(ids, p.Id)
				}
			}
			muPix.Unlock()
			for _, id := range ids {
				conferirPix(id)
			}
		}
	}()

	mux.HandleFunc("/api/pix/status", func(w http.ResponseWriter, r *http.Request) {
		jsonOK(w, map[string]any{"ativo": tokenPix() != "", "logado": lerSessao(r) != ""})
	})

	mux.HandleFunc("/api/pix/comprar", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			http.Error(w, "metodo", http.StatusMethodNotAllowed)
			return
		}
		eu := lerSessao(r)
		if eu == "" {
			http.Error(w, "entre com a Steam pra comprar", http.StatusUnauthorized)
			return
		}
		if tokenPix() == "" {
			http.Error(w, "o pagamento ainda não abriu", http.StatusServiceUnavailable)
			return
		}
		var q struct{ Pacote, Escolha string }
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 1024)).Decode(&q); err != nil {
			http.Error(w, "pedido invalido", http.StatusBadRequest)
			return
		}
		pac, ok := pacotesPix[q.Pacote]
		if !ok {
			http.Error(w, "pacote invalido", http.StatusBadRequest)
			return
		}
		if len(pac.Escolhas) > 0 {
			valida := false
			for _, e := range pac.Escolhas {
				valida = valida || e == q.Escolha
			}
			if !valida {
				http.Error(w, "escolha o kit", http.StatusBadRequest)
				return
			}
		} else {
			q.Escolha = ""
		}
		muPix.Lock()
		l := lerPedidosPix()
		recentes := 0
		for _, p := range l {
			if p.Steam != eu || time.Since(p.Criado) > time.Hour {
				continue
			}
			recentes++
			// mesmo pacote ainda esperando pagamento: mostra o mesmo QR
			if p.Pacote == q.Pacote && p.Escolha == q.Escolha && p.Status == "action_required" && time.Now().Before(p.Expira.Add(-2*time.Minute)) {
				muPix.Unlock()
				jsonOK(w, p)
				return
			}
		}
		muPix.Unlock()
		if recentes >= 6 {
			http.Error(w, "muitos pedidos, espere um pouco", http.StatusTooManyRequests)
			return
		}
		expira := time.Now().Add(30 * time.Minute) // o Pix vale 1 dia; aqui só reaproveita o mesmo QR por meia hora
		dom := dominioPix
		if dom == "" {
			dom = "dayz.snaplag.com.br"
		}
		valor := fmt.Sprintf("%d.%02d", pac.Centavos/100, pac.Centavos%100)
		corpo := map[string]any{
			"type":               "online",
			"processing_mode":    "automatic",
			"total_amount":       valor,
			"external_reference": eu + "|" + q.Pacote + "|" + q.Escolha,
			"payer":              map[string]any{"email": "steam" + eu + "@" + dom},
			"transactions": map[string]any{"payments": []any{map[string]any{
				"amount":         valor,
				"payment_method": map[string]any{"id": "pix", "type": "bank_transfer"},
			}}},
		}
		m, code, err := chamarMP("POST", "/v1/orders", corpo, idAleatorio())
		if err != nil || code < 200 || code > 299 {
			log.Printf("pix: criar pagamento: %v %d %v", err, code, m["message"])
			http.Error(w, "não consegui gerar o Pix agora, tente de novo", http.StatusBadGateway)
			return
		}
		p := &pedidoPix{Steam: eu, Pacote: q.Pacote, Escolha: q.Escolha, Centavos: pac.Centavos, Criado: time.Now(), Expira: expira}
		p.Id, _ = m["id"].(string)
		p.Status, _ = m["status"].(string)
		if tr, ok := m["transactions"].(map[string]any); ok {
			if pg, ok := tr["payments"].([]any); ok && len(pg) > 0 {
				if pm, ok := pg[0].(map[string]any)["payment_method"].(map[string]any); ok {
					p.Qr, _ = pm["qr_code"].(string)
					p.QrImg, _ = pm["qr_code_base64"].(string)
				}
			}
		}
		if p.Id == "" || p.Qr == "" {
			http.Error(w, "o Mercado Pago não devolveu o Pix", http.StatusBadGateway)
			return
		}
		muPix.Lock()
		gravarPedidosPix(append(lerPedidosPix(), p))
		muPix.Unlock()
		jsonOK(w, p)
	})

	mux.HandleFunc("/api/pix/pedido", func(w http.ResponseWriter, r *http.Request) {
		eu := lerSessao(r)
		id := r.URL.Query().Get("id")
		if eu == "" || !reIdPix.MatchString(id) {
			http.Error(w, "pedido invalido", http.StatusBadRequest)
			return
		}
		muPix.Lock()
		var achou *pedidoPix
		for _, p := range lerPedidosPix() {
			if p.Id == id && p.Steam == eu {
				achou = p
			}
		}
		muPix.Unlock()
		if achou == nil {
			http.NotFound(w, r)
			return
		}
		if !achou.Entregue && r.URL.Query().Get("conferir") == "1" {
			conferirPix(id)
			muPix.Lock()
			for _, p := range lerPedidosPix() {
				if p.Id == id {
					achou = p
				}
			}
			muPix.Unlock()
		}
		jsonOK(w, map[string]any{"status": achou.Status, "entregue": achou.Entregue})
	})

	// aviso do Mercado Pago: só diz "o pagamento X mudou"; o resto vem da API
	mux.HandleFunc("/api/pix/webhook", func(w http.ResponseWriter, r *http.Request) {
		id := r.URL.Query().Get("data.id")
		if id == "" {
			id = r.URL.Query().Get("id")
		}
		if id == "" && r.Method == http.MethodPost {
			var c struct {
				Data struct {
					Id any `json:"id"`
				} `json:"data"`
			}
			_ = json.NewDecoder(http.MaxBytesReader(w, r.Body, 8192)).Decode(&c)
			id = fmt.Sprint(c.Data.Id)
		}
		if reIdPix.MatchString(id) {
			go conferirPix(id)
		}
		w.WriteHeader(http.StatusOK)
	})

	// painel admin (só o dono): colar o token e ver as vendas
	mux.HandleFunc("/api/admin/pix", func(w http.ResponseWriter, r *http.Request) {
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		if !a.Dono {
			http.Error(w, "so o dono", http.StatusForbidden)
			return
		}
		if r.Method == http.MethodPost {
			if r.Header.Get("X-DZ") != "1" {
				http.Error(w, "metodo", http.StatusMethodNotAllowed)
				return
			}
			var q struct{ Token string }
			if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4096)).Decode(&q); err != nil {
				http.Error(w, "invalido", http.StatusBadRequest)
				return
			}
			q.Token = strings.TrimSpace(q.Token)
			if q.Token == "" { // apagar
				_ = os.Remove(arqTokenPix())
				jsonOK(w, map[string]any{"ok": true})
				return
			}
			if !strings.HasPrefix(q.Token, "APP_USR-") || len(q.Token) > 200 {
				http.Error(w, "o Access Token de produção começa com APP_USR-", http.StatusBadRequest)
				return
			}
			antigo, _ := os.ReadFile(arqTokenPix())
			if err := os.WriteFile(arqTokenPix(), []byte(q.Token), 0o600); err != nil {
				http.Error(w, "não consegui salvar", http.StatusInternalServerError)
				return
			}
			m, code, err := chamarMP("GET", "/users/me", nil, "")
			if err != nil || code != 200 {
				if len(antigo) > 0 {
					_ = os.WriteFile(arqTokenPix(), antigo, 0o600)
				} else {
					_ = os.Remove(arqTokenPix())
				}
				http.Error(w, "o Mercado Pago recusou esse token", http.StatusBadRequest)
				return
			}
			log.Printf("pix: token do Mercado Pago trocado por %s", a.Steam)
			jsonOK(w, map[string]any{"ok": true, "conta": m["nickname"]})
			return
		}
		muPix.Lock()
		l := lerPedidosPix()
		muPix.Unlock()
		vendas := []map[string]any{}
		var total, mes int64
		inicioMes := time.Now().In(time.FixedZone("BRT", -3*3600))
		inicioMes = time.Date(inicioMes.Year(), inicioMes.Month(), 1, 0, 0, 0, 0, inicioMes.Location())
		for i := len(l) - 1; i >= 0; i-- {
			p := l[i]
			if p.Entregue {
				total += p.Centavos
				if p.Criado.After(inicioMes) {
					mes += p.Centavos
				}
			}
			if len(vendas) < 100 {
				vendas = append(vendas, map[string]any{"id": p.Id, "steam": p.Steam, "pacote": p.Pacote, "escolha": p.Escolha,
					"valor": p.Centavos, "status": p.Status, "entregue": p.Entregue, "criado": p.Criado})
			}
		}
		jsonOK(w, map[string]any{"configurado": tokenPix() != "", "vendas": vendas, "total": total, "mes": mes})
	})
}
