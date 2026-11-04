package main

// Loja VIP com pagamento pelo Mercado Pago, tudo dentro do nosso site (Checkout Transparente).
//
// Pix: cria uma order (API de Orders; se a conta recusar, um pagamento pela API antiga) e a página mostra
// o QR e o copia e cola. Cartão (crédito ou débito): o formulário é o Card Payment Brick do Mercado Pago,
// embutido na página com o nosso visual. O número do cartão nunca chega aqui, só o token de uso único.
//
// O Access Token fica em <pasta do site>/mercadopago_token (0600, nunca volta pro navegador). O site
// confere os pagamentos pendentes a cada 15 s (não precisa de webhook) e, quando cai, manda "vip" pro
// jogo pelo admin_comandos.txt. Cada pedido entra uma vez só.

import (
	"bytes"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log"
	"net/http"
	"net/url"
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

// preços iguais aos da página da Loja VIP (app.js); o valor cobrado é sempre este, nunca o da página
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

// chave pública de produção do Mercado Pago (é pública, vai pro navegador); o dono pode trocar no painel
const chavePublicaPadrao = "APP_USR-99939ce4-8899-4aaf-90ca-f1bec56745ea"

type pedidoPix struct {
	Id       string    `json:"id"` // nosso (dz_...), vai como external_reference
	Steam    string    `json:"steam"`
	Pacote   string    `json:"pacote"`
	Escolha  string    `json:"escolha"`
	Centavos int64     `json:"centavos"`
	Metodo   string    `json:"metodo"` // pix | cartao
	Email    string    `json:"email"`
	Ordem    string    `json:"ordem,omitempty"` // order do Pix
	MP       string    `json:"mp,omitempty"`    // pagamento (cartão ou Pix pela API antiga)
	Estado   string    `json:"estado"`          // pendente | aprovado | cancelado
	Status   string    `json:"status"`          // o que o Mercado Pago disse
	Entregue bool      `json:"entregue"`
	Criado   time.Time `json:"criado"`
	Expira   time.Time `json:"expira"`
	Qr       string    `json:"qr,omitempty"`
	QrImg    string    `json:"qrImg,omitempty"`

	consultei time.Time
}

var (
	muPix      sync.Mutex
	pastaPix   = "/home/unturned/dz-site"
	dominioPix string
	clientePix = &http.Client{Timeout: 20 * time.Second}
	reIdPix    = regexp.MustCompile(`^[A-Za-z0-9_]{1,40}$`)
	reEmail    = regexp.MustCompile(`^[^\s@]+@[^\s@]+\.[^\s@]+$`)
	pedidosPix []*pedidoPix
	carregou   bool
)

const prazoPix = 30 * time.Minute

func arqTokenPix() string   { return filepath.Join(pastaPix, "mercadopago_token") }
func arqChavePix() string   { return filepath.Join(pastaPix, "mercadopago_chave_publica") }
func arqPedidosPix() string { return filepath.Join(pastaPix, "pix_pedidos.json") }

func lerTexto(arq string) string {
	b, err := os.ReadFile(arq)
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(b))
}

func tokenPix() string { return lerTexto(arqTokenPix()) }
func chavePix() string {
	if c := lerTexto(arqChavePix()); c != "" {
		return c
	}
	return chavePublicaPadrao
}

// pedidos: quem chama segura muPix
func pedidos() []*pedidoPix {
	if !carregou {
		carregou = true
		_ = dados.LerJSON(arqPedidosPix(), &pedidosPix)
	}
	return pedidosPix
}

func gravarPedidos() {
	if len(pedidosPix) > 5000 {
		pedidosPix = pedidosPix[len(pedidosPix)-5000:]
	}
	if err := dados.GravarJSON(arqPedidosPix(), pedidosPix); err != nil {
		log.Println("vip: gravar pedidos:", err)
	}
}

func pedidoPorId(id string) *pedidoPix {
	for _, p := range pedidos() {
		if p.Id == id {
			return p
		}
	}
	return nil
}

type erroMP struct {
	status int
	corpo  string
}

func (e *erroMP) Error() string {
	return fmt.Sprintf("Mercado Pago respondeu %d: %s", e.status, e.corpo)
}

// chamarMP chama a API do Mercado Pago com o token do dono. Erro de recusa vem como *erroMP (com o corpo).
func chamarMP(metodo, caminho, idem string, extra map[string]string, corpo any, saida any) error {
	tok := tokenPix()
	if tok == "" {
		return errors.New("sem token")
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
	for k, v := range extra {
		req.Header.Set(k, v)
	}
	resp, err := clientePix.Do(req)
	if err != nil {
		return errors.New("sem resposta do Mercado Pago")
	}
	defer resp.Body.Close()
	b, _ := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
	if resp.StatusCode/100 != 2 {
		c := string(b)
		if len(c) > 600 {
			c = c[:600]
		}
		return &erroMP{resp.StatusCode, c}
	}
	if saida == nil {
		return nil
	}
	return json.Unmarshal(b, saida)
}

func idAleatorio() string {
	b := make([]byte, 12)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b)
}

func valorTexto(c int64) string { return fmt.Sprintf("%d.%02d", c/100, c%100) }

type ordemMP struct {
	ID         string `json:"id"`
	Status     string `json:"status"`
	Detalhe    string `json:"status_detail"`
	Total      string `json:"total_amount"`
	Ref        string `json:"external_reference"`
	Transacoes struct {
		Pagamentos []struct {
			Metodo struct {
				QR   string `json:"qr_code"`
				QR64 string `json:"qr_code_base64"`
			} `json:"payment_method"`
		} `json:"payments"`
	} `json:"transactions"`
}

// idMP aceita o id como número ou texto
type idMP string

func (i *idMP) UnmarshalJSON(b []byte) error {
	s := strings.Trim(string(b), `"`)
	if s == "null" {
		s = ""
	}
	*i = idMP(s)
	return nil
}

type pagamentoMP struct {
	ID      idMP    `json:"id"`
	Status  string  `json:"status"`
	Detalhe string  `json:"status_detail"`
	Valor   float64 `json:"transaction_amount"`
	Ref     string  `json:"external_reference"`
	Ponto   struct {
		Dados struct {
			QR   string `json:"qr_code"`
			QR64 string `json:"qr_code_base64"`
		} `json:"transaction_data"`
	} `json:"point_of_interaction"`
}

func descricaoVip(p *pedidoPix) string { return "DAY Z BRASIL - VIP " + pacotesPix[p.Pacote].Nome }

func criarPix(p *pedidoPix) error {
	valor := valorTexto(p.Centavos)
	var o ordemMP
	err := chamarMP("POST", "/v1/orders", p.Id, nil, map[string]any{
		"type":               "online",
		"total_amount":       valor,
		"external_reference": p.Id,
		"processing_mode":    "automatic",
		"transactions": map[string]any{"payments": []map[string]any{{
			"amount":          valor,
			"payment_method":  map[string]string{"id": "pix", "type": "bank_transfer"},
			"expiration_time": fmt.Sprintf("PT%dM", int(prazoPix.Minutes())),
		}}},
		"payer": map[string]any{"email": p.Email},
	}, &o)
	if err == nil && o.ID != "" && len(o.Transacoes.Pagamentos) > 0 && o.Transacoes.Pagamentos[0].Metodo.QR != "" {
		m := o.Transacoes.Pagamentos[0].Metodo
		p.Ordem, p.Qr, p.QrImg, p.Status = o.ID, m.QR, m.QR64, o.Status
		return nil
	}
	var e *erroMP
	if err != nil && !errors.As(err, &e) {
		return err
	}
	log.Printf("vip %s: API de Orders não deu (%v); tentando a de pagamentos", p.Id, err)
	var pg pagamentoMP
	err = chamarMP("POST", "/v1/payments", p.Id+"_p", nil, map[string]any{
		"transaction_amount": float64(p.Centavos) / 100,
		"description":        descricaoVip(p),
		"payment_method_id":  "pix",
		"external_reference": p.Id,
		"date_of_expiration": p.Expira.In(time.FixedZone("BRT", -3*3600)).Format("2006-01-02T15:04:05.000-07:00"),
		"payer":              map[string]any{"email": p.Email},
	}, &pg)
	if err != nil {
		return err
	}
	if pg.ID == "" || pg.Ponto.Dados.QR == "" {
		return errors.New("o Mercado Pago não mandou o Pix")
	}
	p.MP, p.Qr, p.QrImg, p.Status = string(pg.ID), pg.Ponto.Dados.QR, pg.Ponto.Dados.QR64, pg.Status
	return nil
}

type dadosCartao struct {
	Token         string `json:"token"`
	MetodoMP      string `json:"payment_method_id"`
	Emissor       string `json:"issuer_id"`
	Parcelas      int    `json:"installments"`
	TipoDocumento string `json:"tipoDocumento"`
	Documento     string `json:"documento"`
	Aparelho      string `json:"aparelho"`
}

func recusaCartao(detalhe string) string {
	switch detalhe {
	case "cc_rejected_insufficient_amount":
		return "o cartão não tem limite para essa compra"
	case "cc_rejected_bad_filled_security_code":
		return "o código de segurança (CVV) está errado"
	case "cc_rejected_bad_filled_date":
		return "a validade do cartão está errada"
	case "cc_rejected_bad_filled_card_number", "cc_rejected_bad_filled_other":
		return "confira os dados do cartão e tente de novo"
	case "cc_rejected_call_for_authorize":
		return "o seu banco pediu para autorizar essa compra; fale com ele e tente de novo"
	case "cc_rejected_card_disabled":
		return "o cartão está bloqueado para compras online; fale com o seu banco"
	case "cc_rejected_duplicated_payment":
		return "esse pagamento já foi feito; confira antes de pagar de novo"
	case "cc_rejected_max_attempts":
		return "muitas tentativas com esse cartão; use outro cartão ou o Pix"
	case "cc_rejected_high_risk", "rejected_high_risk", "cc_rejected_blacklist":
		return "o pagamento foi recusado pela análise de segurança; tente outro cartão ou o Pix"
	}
	return "o cartão foi recusado; tente outro cartão ou o Pix"
}

func soDigitos(s string) string {
	var b strings.Builder
	for _, c := range s {
		if c >= '0' && c <= '9' {
			b.WriteRune(c)
		}
	}
	return b.String()
}

// cobrarCartao cobra na hora com o token do formulário seguro. Devolve a mensagem de recusa ("" se ok).
func cobrarCartao(p *pedidoPix, d dadosCartao) (string, error) {
	corpo := map[string]any{
		"transaction_amount":   float64(p.Centavos) / 100,
		"token":                d.Token,
		"description":          descricaoVip(p),
		"installments":         d.Parcelas,
		"payment_method_id":    d.MetodoMP,
		"external_reference":   p.Id,
		"statement_descriptor": "DAYZBRASIL",
		"binary_mode":          true,
		"payer": map[string]any{"email": p.Email,
			"identification": map[string]string{"type": d.TipoDocumento, "number": d.Documento}},
	}
	if d.Emissor != "" {
		corpo["issuer_id"] = d.Emissor
	}
	extra := map[string]string{}
	if d.Aparelho != "" && len(d.Aparelho) < 200 {
		extra["X-meli-session-id"] = d.Aparelho
	}
	var out pagamentoMP
	if err := chamarMP("POST", "/v1/payments", p.Id, extra, corpo, &out); err != nil {
		return "", err
	}
	p.MP, p.Status = string(out.ID), out.Status
	switch out.Status {
	case "approved":
		if int64(out.Valor*100+0.5) >= p.Centavos {
			p.Estado = "aprovado"
		}
	case "rejected", "cancelled":
		p.Estado = "cancelado"
		return recusaCartao(out.Detalhe), nil
	}
	return "", nil
}

// consultarMP pergunta ao Mercado Pago se o pedido caiu. Quem chama NÃO segura muPix.
func consultarMP(p pedidoPix) (estado, status string, err error) {
	if p.Ordem != "" {
		var o ordemMP
		if err := chamarMP("GET", "/v1/orders/"+url.PathEscape(p.Ordem), "", nil, nil, &o); err != nil {
			return "", "", err
		}
		switch o.Status {
		case "processed":
			v, err := strconv.ParseFloat(o.Total, 64)
			if err != nil || int64(v*100+0.5) < p.Centavos || (o.Ref != "" && o.Ref != p.Id) {
				log.Printf("vip %s: order %s com valor %q ou referência %q estranhos; não liberei", p.Id, o.ID, o.Total, o.Ref)
				return "pendente", o.Status, nil
			}
			return "aprovado", o.Status, nil
		case "canceled", "failed", "refunded", "charged_back":
			return "cancelado", o.Status, nil
		}
		return "pendente", o.Status, nil
	}
	if p.MP != "" {
		var m pagamentoMP
		if err := chamarMP("GET", "/v1/payments/"+url.PathEscape(p.MP), "", nil, nil, &m); err != nil {
			return "", "", err
		}
		switch m.Status {
		case "approved":
			if int64(m.Valor*100+0.5) < p.Centavos || (m.Ref != "" && m.Ref != p.Id) {
				return "pendente", m.Status, nil
			}
			return "aprovado", m.Status, nil
		case "cancelled", "rejected", "refunded", "charged_back":
			return "cancelado", m.Status, nil
		}
		return "pendente", m.Status, nil
	}
	return "pendente", "", nil
}

// entregar manda o VIP pro jogo. Quem chama segura muPix.
func entregar(p *pedidoPix) {
	if p.Estado != "aprovado" || p.Entregue {
		return
	}
	if err := comandoJogo("vip", p.Steam, "0", 0, p.Pacote+"|"+p.Escolha+"|"+p.Id); err != nil {
		log.Println("vip: mandar pro jogo:", err)
		return
	}
	p.Entregue = true
	log.Printf("vip %s: %s (%s) entregue pra %s, %s R$ %s", p.Id, p.Pacote, p.Escolha, p.Steam, p.Metodo, valorTexto(p.Centavos))
}

// atualizar confere um pedido pendente no Mercado Pago (no máximo 1 vez a cada 3 s).
func atualizar(id string) {
	muPix.Lock()
	p := pedidoPorId(id)
	if p == nil || p.Estado != "pendente" || time.Since(p.consultei) < 3*time.Second {
		muPix.Unlock()
		return
	}
	p.consultei = time.Now()
	copia := *p
	muPix.Unlock()
	estado, status, err := consultarMP(copia)
	muPix.Lock()
	defer muPix.Unlock()
	if err != nil {
		log.Printf("vip %s: %v", id, err)
		return
	}
	if p.Estado != "pendente" {
		return
	}
	p.Status = status
	switch {
	case estado == "aprovado":
		p.Estado = "aprovado"
		entregar(p)
	case estado == "cancelado" || time.Now().After(p.Expira.Add(time.Hour)):
		p.Estado = "cancelado"
	}
	gravarPedidos()
}

func visaoPedido(p *pedidoPix) map[string]any {
	return map[string]any{"id": p.Id, "metodo": p.Metodo, "estado": p.Estado, "entregue": p.Entregue, "pacote": p.Pacote,
		"escolha": p.Escolha, "valor": p.Centavos, "qr": p.Qr, "qr64": p.QrImg, "expira": p.Expira, "email": mascararEmail(p.Email)}
}

func mascararEmail(e string) string {
	i := strings.Index(e, "@")
	if i < 2 {
		return e
	}
	return e[:2] + "***" + e[i:]
}

func erroJSON(w http.ResponseWriter, code int, msg string) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(map[string]string{"erro": msg})
}

func rotasPix(mux *http.ServeMux) {
	// confere os pendentes a cada 15 s (não precisa de webhook)
	go func() {
		for range time.Tick(15 * time.Second) {
			muPix.Lock()
			var ids []string
			for _, p := range pedidos() {
				if p.Estado == "pendente" && len(ids) < 40 {
					ids = append(ids, p.Id)
				}
				if p.Estado == "aprovado" && !p.Entregue {
					entregar(p)
				}
			}
			muPix.Unlock()
			for _, id := range ids {
				atualizar(id)
			}
		}
	}()

	mux.HandleFunc("/api/pix/status", func(w http.ResponseWriter, r *http.Request) {
		jsonOK(w, map[string]any{"ativo": tokenPix() != "", "logado": lerSessao(r) != "", "chave": chavePix()})
	})

	// POST: cria o pedido (Pix) ou cobra o cartão. GET ?id=: situação do pedido.
	mux.HandleFunc("/api/vip/pagar", func(w http.ResponseWriter, r *http.Request) {
		eu := lerSessao(r)
		if eu == "" {
			erroJSON(w, http.StatusUnauthorized, "entre com a Steam pra comprar")
			return
		}
		if r.Method == http.MethodGet {
			id := r.URL.Query().Get("id")
			if !reIdPix.MatchString(id) {
				erroJSON(w, 400, "pedido invalido")
				return
			}
			atualizar(id)
			muPix.Lock()
			defer muPix.Unlock()
			p := pedidoPorId(id)
			if p == nil || p.Steam != eu {
				erroJSON(w, 404, "pedido não encontrado")
				return
			}
			jsonOK(w, visaoPedido(p))
			return
		}
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			erroJSON(w, http.StatusMethodNotAllowed, "metodo")
			return
		}
		if tokenPix() == "" {
			erroJSON(w, 503, "o pagamento ainda não abriu")
			return
		}
		var q struct {
			Pacote, Escolha, Email, Metodo string
			Cartao                         *dadosCartao
		}
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 8192)).Decode(&q); err != nil {
			erroJSON(w, 400, "pedido invalido")
			return
		}
		pac, ok := pacotesPix[q.Pacote]
		if !ok {
			erroJSON(w, 400, "escolha um pacote")
			return
		}
		if len(pac.Escolhas) > 0 {
			valida := false
			for _, e := range pac.Escolhas {
				valida = valida || e == q.Escolha
			}
			if !valida {
				erroJSON(w, 400, "escolha o kit do pacote")
				return
			}
		} else {
			q.Escolha = ""
		}
		q.Email = strings.ToLower(strings.TrimSpace(q.Email))
		if !reEmail.MatchString(q.Email) || len(q.Email) > 120 {
			erroJSON(w, 400, "digite um e-mail válido (vai o recibo)")
			return
		}
		if q.Metodo != "pix" && q.Metodo != "cartao" {
			erroJSON(w, 400, "escolha Pix ou cartão")
			return
		}
		muPix.Lock()
		recentes := 0
		for _, p := range pedidos() {
			if p.Steam != eu || time.Since(p.Criado) > time.Hour {
				continue
			}
			recentes++
			// mesmo Pix ainda valendo: mostra o mesmo QR
			if q.Metodo == "pix" && p.Metodo == "pix" && p.Pacote == q.Pacote && p.Escolha == q.Escolha && p.Email == q.Email &&
				p.Estado == "pendente" && time.Now().Before(p.Expira.Add(-3*time.Minute)) {
				v := visaoPedido(p)
				muPix.Unlock()
				jsonOK(w, v)
				return
			}
		}
		muPix.Unlock()
		if recentes >= 10 {
			erroJSON(w, 429, "muitos pedidos, espere um pouco")
			return
		}
		p := &pedidoPix{Id: "dz_" + idAleatorio(), Steam: eu, Pacote: q.Pacote, Escolha: q.Escolha, Centavos: pac.Centavos,
			Metodo: q.Metodo, Email: q.Email, Estado: "pendente", Criado: time.Now().UTC(), Expira: time.Now().Add(prazoPix).UTC()}
		recusa := ""
		var err error
		if q.Metodo == "pix" {
			err = criarPix(p)
		} else {
			d := q.Cartao
			if d == nil {
				erroJSON(w, 400, "preencha os dados do cartão")
				return
			}
			d.Documento = soDigitos(d.Documento)
			if d.TipoDocumento == "" {
				d.TipoDocumento = "CPF"
			}
			if len(d.Token) < 10 || len(d.Token) > 200 || d.MetodoMP == "" || len(d.MetodoMP) > 40 ||
				(d.TipoDocumento != "CPF" && d.TipoDocumento != "CNPJ") || (len(d.Documento) != 11 && len(d.Documento) != 14) {
				erroJSON(w, 400, "confira os dados do cartão e o CPF do titular")
				return
			}
			if d.Parcelas < 1 || d.Parcelas > 12 {
				d.Parcelas = 1
			}
			recusa, err = cobrarCartao(p, *d)
		}
		if err != nil {
			log.Printf("vip %s (%s, %s): %v", p.Id, q.Metodo, q.Pacote, err)
			var e *erroMP
			if errors.As(err, &e) && e.status/100 == 4 {
				if q.Metodo == "cartao" {
					erroJSON(w, 400, "confira os dados do cartão e tente de novo")
				} else {
					erroJSON(w, 400, "o Mercado Pago recusou o Pix; confira o e-mail e tente de novo")
				}
				return
			}
			erroJSON(w, 502, "não consegui falar com o Mercado Pago agora; tente de novo")
			return
		}
		muPix.Lock()
		pedidosPix = append(pedidos(), p)
		entregar(p)
		gravarPedidos()
		v := visaoPedido(p)
		muPix.Unlock()
		log.Printf("vip %s: novo %s %s R$ %s pra %s (%s)", p.Id, p.Metodo, p.Pacote, valorTexto(p.Centavos), p.Steam, p.Status)
		if recusa != "" {
			erroJSON(w, 402, recusa)
			return
		}
		jsonOK(w, v)
	})

	// aviso do Mercado Pago (opcional): só diz "mudou"; o resto vem da API
	mux.HandleFunc("/api/pix/webhook", func(w http.ResponseWriter, r *http.Request) {
		id := r.URL.Query().Get("data.id")
		if id == "" && r.Method == http.MethodPost {
			var c struct {
				Data struct {
					Id any `json:"id"`
				} `json:"data"`
			}
			_ = json.NewDecoder(http.MaxBytesReader(w, r.Body, 8192)).Decode(&c)
			id = fmt.Sprint(c.Data.Id)
		}
		if id != "" {
			muPix.Lock()
			var nosso string
			for _, p := range pedidos() {
				if p.Ordem == id || p.MP == id {
					nosso = p.Id
				}
			}
			muPix.Unlock()
			if nosso != "" {
				go atualizar(nosso)
			}
		}
		w.WriteHeader(http.StatusOK)
	})

	// painel admin (só o dono): token, chave pública e as vendas
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
			var q struct{ Token, Chave string }
			if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4096)).Decode(&q); err != nil {
				http.Error(w, "invalido", http.StatusBadRequest)
				return
			}
			if c := strings.TrimSpace(q.Chave); c != "" {
				if !strings.HasPrefix(c, "APP_USR-") || len(c) > 100 {
					http.Error(w, "a Public Key começa com APP_USR-", http.StatusBadRequest)
					return
				}
				_ = os.WriteFile(arqChavePix(), []byte(c), 0o600)
				jsonOK(w, map[string]any{"ok": true})
				return
			}
			q.Token = strings.TrimSpace(q.Token)
			if !strings.HasPrefix(q.Token, "APP_USR-") || len(q.Token) > 200 {
				http.Error(w, "o Access Token de produção começa com APP_USR-", http.StatusBadRequest)
				return
			}
			antigo, _ := os.ReadFile(arqTokenPix())
			if err := os.WriteFile(arqTokenPix(), []byte(q.Token), 0o600); err != nil {
				http.Error(w, "não consegui salvar", http.StatusInternalServerError)
				return
			}
			var me map[string]any
			if err := chamarMP("GET", "/users/me", "", nil, nil, &me); err != nil {
				if len(antigo) > 0 {
					_ = os.WriteFile(arqTokenPix(), antigo, 0o600)
				} else {
					_ = os.Remove(arqTokenPix())
				}
				http.Error(w, "o Mercado Pago recusou esse token", http.StatusBadRequest)
				return
			}
			log.Printf("vip: token do Mercado Pago trocado por %s", a.Steam)
			jsonOK(w, map[string]any{"ok": true, "conta": me["nickname"]})
			return
		}
		muPix.Lock()
		l := pedidos()
		vendas := []map[string]any{}
		var total, mes int64
		br := time.FixedZone("BRT", -3*3600)
		agora := time.Now().In(br)
		inicioMes := time.Date(agora.Year(), agora.Month(), 1, 0, 0, 0, 0, br)
		for i := len(l) - 1; i >= 0; i-- {
			p := l[i]
			if p.Estado == "aprovado" {
				total += p.Centavos
				if p.Criado.After(inicioMes) {
					mes += p.Centavos
				}
			}
			if len(vendas) < 100 {
				vendas = append(vendas, map[string]any{"id": p.Id, "steam": p.Steam, "pacote": p.Pacote, "escolha": p.Escolha, "metodo": p.Metodo,
					"valor": p.Centavos, "estado": p.Estado, "entregue": p.Entregue, "criado": p.Criado})
			}
		}
		muPix.Unlock()
		jsonOK(w, map[string]any{"configurado": tokenPix() != "", "chave": chavePix(), "vendas": vendas, "total": total, "mes": mes})
	})
}
