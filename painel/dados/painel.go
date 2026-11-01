package dados

// Ligação entre o painel admin (site), o bot do Discord e o executor do servidor.
// Tudo passa por arquivos em PastaPainel (os três rodam na mesma máquina):
//   fila/<id>.json     pedido do site pro bot (mensagem ou enquete no Discord)
//   feitos/<id>.json   resposta do bot (ok, erro, link da mensagem)
//   discord.json       o bot grava a cada minuto: canais, membros, tickets, salas e enquetes
//   acao.json          pedido do site pro executor (root): wipe ou reinício agendado
//   historico.jsonl    o executor anota cada wipe/reinício feito

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"os"
	"path/filepath"
	"time"
)

const PastaPainel = "/home/unturned/dz-painel"

// PedidoBot: o que o site pede pro bot fazer no Discord.
type PedidoBot struct {
	ID     string   `json:"id"`
	Tipo   string   `json:"tipo"` // "mensagem", "aviso" (embed) ou "enquete"
	Canal  string   `json:"canal"`
	Titulo string   `json:"titulo,omitempty"`
	Texto  string   `json:"texto,omitempty"`
	Cor    string   `json:"cor,omitempty"` // "#FF5A00"
	Imagem string   `json:"imagem,omitempty"`
	Mencao string   `json:"mencao,omitempty"` // "", "everyone", "here"
	Opcoes []string `json:"opcoes,omitempty"`
	Horas  int      `json:"horas,omitempty"`
	Multi  bool     `json:"multi,omitempty"`
	Autor  string   `json:"autor,omitempty"`
	Criado int64    `json:"criado"`
	Rotulo string   `json:"rotulo,omitempty"` // ex.: "wipe" pra enquete de wipe
}

type ResultadoBot struct {
	ID     string `json:"id"`
	Ok     bool   `json:"ok"`
	Erro   string `json:"erro,omitempty"`
	Link   string `json:"link,omitempty"`
	Quando int64  `json:"quando"`
}

type CanalDiscord struct {
	ID        string `json:"id"`
	Nome      string `json:"nome"`
	Categoria string `json:"categoria"`
	Tipo      string `json:"tipo"` // "texto", "voz", "anuncio"
}

type TicketDiscord struct {
	Canal string `json:"canal"`
	Nome  string `json:"nome"`
	Dono  string `json:"dono"`
	Link  string `json:"link"`
}

type SalaDiscord struct {
	Canal   string `json:"canal"`
	Nome    string `json:"nome"`
	Pessoas int    `json:"pessoas"`
}

type RespostaEnquete struct {
	Texto string `json:"texto"`
	Votos int    `json:"votos"`
}

type EnqueteDiscord struct {
	Canal     string            `json:"canal"`
	CanalNome string            `json:"canalNome"`
	Msg       string            `json:"msg"`
	Pergunta  string            `json:"pergunta"`
	Rotulo    string            `json:"rotulo,omitempty"`
	Criada    int64             `json:"criada"`
	Termina   int64             `json:"termina"`
	Encerrada bool              `json:"encerrada"`
	Respostas []RespostaEnquete `json:"respostas"`
	Total     int               `json:"total"`
	Link      string            `json:"link"`
}

type ExportDiscord struct {
	Atualizado int64            `json:"atualizado"`
	Servidor   string           `json:"servidor"`
	Membros    int              `json:"membros"`
	Online     int              `json:"online"`
	Convite    string           `json:"convite"`
	Canais     []CanalDiscord   `json:"canais"`
	Tickets    []TicketDiscord  `json:"tickets"`
	Salas      []SalaDiscord    `json:"salas"`
	Enquetes   []EnqueteDiscord `json:"enquetes"`
}

// AcaoServidor: wipe ou reinício agendado (só o dono pede; o executor roda como root).
type AcaoServidor struct {
	Tipo     string          `json:"tipo"` // "wipe" ou "reiniciar"
	Quando   int64           `json:"quando"`
	Itens    map[string]bool `json:"itens,omitempty"`
	Motivo   string          `json:"motivo,omitempty"`
	Por      string          `json:"por"`
	PorNome  string          `json:"porNome"`
	Criado   int64           `json:"criado"`
	Avisados []int           `json:"avisados,omitempty"` // minutos já anunciados (o executor preenche)
}

// NovoID gera um id curto e único pros pedidos.
func NovoID() string {
	b := make([]byte, 6)
	_, _ = rand.Read(b)
	return time.Now().Format("20060102-150405") + "-" + hex.EncodeToString(b)
}

// GravarJSON grava de forma atômica (arquivo temporário + rename).
func GravarJSON(arq string, v any) error {
	if err := os.MkdirAll(filepath.Dir(arq), 0o750); err != nil {
		return err
	}
	b, err := json.MarshalIndent(v, "", "  ")
	if err != nil {
		return err
	}
	tmp := arq + ".tmp"
	if err := os.WriteFile(tmp, b, 0o640); err != nil {
		return err
	}
	return os.Rename(tmp, arq)
}

func LerJSON(arq string, v any) error {
	b, err := os.ReadFile(arq)
	if err != nil {
		return err
	}
	return json.Unmarshal(b, v)
}
