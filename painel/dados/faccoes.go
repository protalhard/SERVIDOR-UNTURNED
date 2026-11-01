package dados

import (
	"encoding/xml"
	"os"
	"path/filepath"
	"sort"
)

// Facção como o plugin grava em faccoes.xml.
type MembroFaccao struct {
	ID    string `xml:"Id,attr" json:"id"`
	Nome  string `xml:"Nome,attr" json:"nome"`
	Cargo int    `xml:"Cargo,attr" json:"cargo"`
	Visto int64  `xml:"Visto,attr" json:"visto"`
	// preenchido na leitura com o nome do cargo da facção
	NomeCargo string `xml:"-" json:"nomeCargo"`
}

type Faccao struct {
	ID           string         `xml:"Id,attr" json:"id"`
	Nome         string         `xml:"Nome,attr" json:"nome"`
	Tag          string         `xml:"Tag,attr" json:"tag"`
	Cor          string         `xml:"Cor,attr" json:"cor"`
	Descricao    string         `xml:"Descricao,attr" json:"descricao"`
	Modo         int            `xml:"Modo,attr" json:"modo"`
	Nivel        int            `xml:"Nivel,attr" json:"nivel"`
	Cofre        int64          `xml:"Cofre,attr" json:"cofre"`
	Criada       int64          `xml:"Criada,attr" json:"criada"`
	UltimoOnline int64          `xml:"UltimoOnline,attr" json:"ultimoOnline"`
	Abates       int            `xml:"Abates,attr" json:"abates"`
	Zumbis       int            `xml:"Zumbis,attr" json:"zumbis"`
	Cargos       []string       `xml:"Cargos>string" json:"cargos"`
	Membros      []MembroFaccao `xml:"Membros>MembroFaccao" json:"membros"`
	Pedidos      int            `xml:"-" json:"pedidos"`
	PedidosXML   []struct{}     `xml:"Pedidos>PedidoFaccao" json:"-"`
}

var cargosPadrao = []string{"Lider", "Co-lider", "Tesoureiro", "Oficial", "Membro", "Recruta"}

// LerFaccoes devolve as facções ordenadas pelo ranking (abates, depois nível e zumbis).
func (f Fonte) LerFaccoes() []Faccao {
	b, err := os.ReadFile(filepath.Join(f.Pasta, "faccoes.xml"))
	if err != nil {
		return nil
	}
	var arq struct {
		Lista []Faccao `xml:"Lista>Faccao"`
	}
	if xml.Unmarshal(b, &arq) != nil {
		return nil
	}
	for i := range arq.Lista {
		fc := &arq.Lista[i]
		fc.Pedidos = len(fc.PedidosXML)
		for j := range fc.Membros {
			c := fc.Membros[j].Cargo
			if c >= 0 && c < len(fc.Cargos) {
				fc.Membros[j].NomeCargo = fc.Cargos[c]
			} else if c >= 0 && c < len(cargosPadrao) {
				fc.Membros[j].NomeCargo = cargosPadrao[c]
			}
		}
		sort.SliceStable(fc.Membros, func(a, b int) bool { return fc.Membros[a].Cargo < fc.Membros[b].Cargo })
	}
	sort.SliceStable(arq.Lista, func(a, b int) bool {
		x, y := arq.Lista[a], arq.Lista[b]
		if x.Abates != y.Abates {
			return x.Abates > y.Abates
		}
		if x.Nivel != y.Nivel {
			return x.Nivel > y.Nivel
		}
		return x.Zumbis > y.Zumbis
	})
	return arq.Lista
}

// FaccaoPorID acha a facção pelo id do grupo.
func (f Fonte) FaccaoPorID(id string) *Faccao {
	for _, fc := range f.LerFaccoes() {
		if fc.ID == id {
			fc := fc
			return &fc
		}
	}
	return nil
}
