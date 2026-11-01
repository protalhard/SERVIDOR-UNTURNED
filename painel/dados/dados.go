// Package dados lê o que o plugin SobrevivenciaCore grava no servidor:
// status.json (quem está online), estatisticas.tsv (abates, XP...) e a
// configuração (patentes e kits). Usado pelo site e pelo bot do Discord.
package dados

import (
	"bufio"
	"encoding/json"
	"encoding/xml"
	"math"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"time"
)

// PastaPadrao é onde o plugin grava os arquivos na VPS.
const PastaPadrao = "/home/unturned/server/Servers/dayz/Rocket/Plugins/SobrevivenciaCore"

type JogadorOnline struct {
	ID      string `json:"id"`
	Nome    string `json:"nome"`
	Patente string `json:"patente"`
	Cor     string `json:"cor"`
	Nivel   int    `json:"nivel"`
	Abates  int    `json:"abates"`
}

type Status struct {
	Online     bool            `json:"online"`
	Nome       string          `json:"nome"`
	Mapa       string          `json:"mapa"`
	Max        int             `json:"max"`
	Atualizado int64           `json:"atualizado"`
	Jogadores  []JogadorOnline `json:"jogadores"`
}

type Ficha struct {
	ID         string  `json:"id"`
	Nome       string  `json:"nome"`
	Abates     int     `json:"abates"`
	Mortes     int     `json:"mortes"`
	Cabeca     int     `json:"cabeca"`
	Zumbis     int     `json:"zumbis"`
	Megas      int     `json:"megas"`
	Segundos   int64   `json:"segundos"`
	Xp         int64   `json:"xp"`
	XpGasto    int64   `json:"xpGasto"`
	Nivel      int     `json:"nivel"`
	Kd         float64 `json:"kd"`
	PctCabeca  int     `json:"pctCabeca"`
	Patente    string  `json:"patente"`
	CorPatente string  `json:"corPatente"`
	Posicao    int     `json:"posicao"`
}

type Patente struct {
	Nome     string `xml:"Nome,attr" json:"nome"`
	XpMinimo int64  `xml:"XpMinimo,attr" json:"xpMinimo"`
	Cor      string `xml:"Cor,attr" json:"cor"`
}

type ItemKit struct {
	ID         int `xml:"Id,attr" json:"id"`
	Quantidade int `xml:"Quantidade,attr" json:"quantidade"`
}

type Kit struct {
	Nome      string    `xml:"Nome,attr" json:"nome"`
	Titulo    string    `xml:"Titulo,attr" json:"titulo"`
	Descricao string    `xml:"Descricao,attr" json:"descricao"`
	Espera    int       `xml:"Espera,attr" json:"espera"`
	Abates    int       `xml:"Abates,attr" json:"abates"`
	Permissao string    `xml:"Permissao,attr" json:"permissao"`
	BonusXp   int       `xml:"BonusXp,attr" json:"bonusXp"`
	Itens     []ItemKit `xml:"Itens>ItemKit" json:"itens"`
}

type PacoteXp struct {
	Nome      string `xml:"Nome,attr" json:"nome"`
	Descricao string `xml:"Descricao,attr" json:"descricao"`
	Preco     int64  `xml:"Preco,attr" json:"preco"`
}

type Config struct {
	Patentes []Patente  `xml:"Sistemas>Patentes>Patente"`
	Kits     []Kit      `xml:"Sistemas>Kits>Kit"`
	LojaXp   []PacoteXp `xml:"Sistemas>LojaXp>PacoteXp"`
	Discord  string     `xml:"Sistemas>LinkDiscord"`
	Donos    string     `xml:"Sistemas>Donos"`
	// raid (Raid.cs)
	RaidHorarioLigado   string `xml:"Sistemas>RaidHorarioLigado"`
	RaidInicioHora      int    `xml:"Sistemas>RaidInicioHora"`
	RaidFimHora         int    `xml:"Sistemas>RaidFimHora"`
	RaidDias            string `xml:"Sistemas>RaidDias"`
	RaidProtecaoOffline string `xml:"Sistemas>RaidProtecaoOffline"`
	RaidMinutosOffline  int    `xml:"Sistemas>RaidMinutosOffline"`
	RaidDiasRenovar     int    `xml:"Sistemas>RaidDiasRenovarClaim"`
}

type Fonte struct{ Pasta string }

func (f Fonte) LerStatus() Status {
	var s Status
	b, err := os.ReadFile(filepath.Join(f.Pasta, "status.json"))
	if err != nil || json.Unmarshal(b, &s) != nil {
		return Status{}
	}
	// o plugin grava a cada 15 s; mais de 90 s sem atualizar = servidor fora
	if time.Since(time.Unix(s.Atualizado, 0)) > 90*time.Second {
		s.Online = false
		s.Jogadores = nil
	}
	return s
}

func (f Fonte) LerConfig() Config {
	var c Config
	b, err := os.ReadFile(filepath.Join(f.Pasta, "SobrevivenciaCore.configuration.xml"))
	if err == nil {
		_ = xml.Unmarshal(b, &c)
	}
	if len(c.Patentes) == 0 {
		c.Patentes = []Patente{{"Recruta", 0, "#8A9099"}}
	}
	sort.Slice(c.Kits, func(i, j int) bool { return c.Kits[i].Abates < c.Kits[j].Abates })
	c.Discord = strings.TrimSpace(c.Discord)
	if c.Discord == "" {
		c.Discord = ConviteBot()
	}
	return c
}

// ArqConvite é onde o bot grava o convite permanente do Discord.
const ArqConvite = "/home/unturned/dz-bot/convite.txt"

// ConviteBot devolve o convite criado pelo bot (ou "").
func ConviteBot() string {
	b, err := os.ReadFile(ArqConvite)
	if err != nil {
		return ""
	}
	l := strings.TrimSpace(string(b))
	if !strings.HasPrefix(l, "https://discord.gg/") {
		return ""
	}
	return l
}

func NivelDe(xp int64) int { return 1 + int(math.Floor(math.Sqrt(math.Max(0, float64(xp))/100))) }

func PatenteDe(ps []Patente, xp int64) Patente {
	melhor := ps[0]
	for _, p := range ps {
		if xp >= p.XpMinimo && p.XpMinimo >= melhor.XpMinimo {
			melhor = p
		}
	}
	return melhor
}

// LerFichas devolve todas as fichas, já ordenadas por abates (posição preenchida).
func (f Fonte) LerFichas(ps []Patente) []Ficha {
	arq, err := os.Open(filepath.Join(f.Pasta, "estatisticas.tsv"))
	if err != nil {
		return nil
	}
	defer arq.Close()
	var lista []Ficha
	sc := bufio.NewScanner(arq)
	sc.Buffer(make([]byte, 64*1024), 1024*1024)
	for sc.Scan() {
		c := strings.Split(sc.Text(), "\t")
		if len(c) < 11 {
			continue
		}
		n := func(i int) int64 { v, _ := strconv.ParseInt(c[i], 10, 64); return v }
		fi := Ficha{
			ID: c[0], Nome: c[1], Abates: int(n(2)), Mortes: int(n(3)), Cabeca: int(n(4)),
			Zumbis: int(n(5)), Megas: int(n(6)), Segundos: n(7), Xp: n(8), XpGasto: n(9),
		}
		fi.Nivel = NivelDe(fi.Xp)
		if fi.Mortes == 0 {
			fi.Kd = float64(fi.Abates)
		} else {
			fi.Kd = math.Round(float64(fi.Abates)/float64(fi.Mortes)*100) / 100
		}
		if fi.Abates > 0 {
			fi.PctCabeca = int(math.Round(100 * float64(fi.Cabeca) / float64(fi.Abates)))
		}
		p := PatenteDe(ps, fi.Xp)
		fi.Patente, fi.CorPatente = p.Nome, p.Cor
		lista = append(lista, fi)
	}
	sort.SliceStable(lista, func(i, j int) bool {
		if lista[i].Abates != lista[j].Abates {
			return lista[i].Abates > lista[j].Abates
		}
		return lista[i].Xp > lista[j].Xp
	})
	for i := range lista {
		lista[i].Posicao = i + 1
	}
	return lista
}

// Ordenar devolve uma cópia ordenada por: abates, kd, xp, tempo, zumbis, cabeca.
func Ordenar(l []Ficha, por string) []Ficha {
	c := append([]Ficha(nil), l...)
	chave := func(f Ficha) float64 {
		switch por {
		case "kd":
			if f.Abates < 5 {
				return -1
			}
			return f.Kd
		case "xp":
			return float64(f.Xp)
		case "tempo":
			return float64(f.Segundos)
		case "zumbis":
			return float64(f.Zumbis)
		case "cabeca":
			return float64(f.Cabeca)
		default:
			return float64(f.Abates)
		}
	}
	sort.SliceStable(c, func(i, j int) bool { return chave(c[i]) > chave(c[j]) })
	return c
}

func Tempo(seg int64) string {
	h := seg / 3600
	m := seg % 3600 / 60
	if h > 0 {
		return strconv.FormatInt(h, 10) + "h " + strconv.FormatInt(m, 10) + "min"
	}
	return strconv.FormatInt(m, 10) + "min"
}
