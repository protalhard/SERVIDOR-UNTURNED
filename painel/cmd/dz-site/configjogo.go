package main

// Aba "Servidor" do painel admin: o dono muda o loot e outras regras do jogo pelo site.
// Os valores ficam no Config.txt do servidor (Servers/dayz/Config.txt). Chave com valor no
// Config.txt vence a do mapa (Mode_Config_Overrides do California2); chave vazia usa a do mapa
// ou o padrão. O jogo só lê o arquivo quando liga, então vale no próximo reinício.

import (
	"encoding/json"
	"fmt"
	"log"
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

type campoJogo struct {
	Chave  string  `json:"chave"` // Bloco.Nome, igual ao Config.txt
	Grupo  string  `json:"grupo"`
	Nome   string  `json:"nome"`
	Ajuda  string  `json:"ajuda"`
	Tipo   string  `json:"tipo"` // num | int | bool
	Min    float64 `json:"min"`
	Max    float64 `json:"max"`
	Padrao string  `json:"padrao"` // o que vale com a chave vazia (mapa ou jogo), só pra mostrar
}

var camposJogo = []campoJogo{
	{"Items.Spawn_Chance", "Loot", "Quantidade de loot no mapa", "Fração dos pontos de loot que ficam com item (0 a 1). 1 = todos os pontos cheios.", "num", 0, 1, "0.35"},
	{"Items.Respawn_Time", "Loot", "Tempo pro loot renascer (segundos)", "Quanto menor, mais rápido o loot volta depois de pego.", "num", 5, 3600, "100"},
	{"Items.Despawn_Natural_Time", "Loot", "Loot que ninguém pega some em (segundos)", "", "num", 60, 86400, "900"},
	{"Items.Despawn_Dropped_Time", "Loot", "Item jogado no chão some em (segundos)", "", "num", 30, 86400, "600"},
	{"Items.Quality_Full_Chance", "Loot", "Chance do item nascer 100% (0 a 1)", "", "num", 0, 1, "0.1"},
	{"Items.Gun_Bullets_Full_Chance", "Loot", "Chance da arma nascer com munição cheia (0 a 1)", "", "num", 0, 1, "0.05"},
	{"Items.Magazine_Bullets_Full_Chance", "Loot", "Chance do pente nascer cheio (0 a 1)", "", "num", 0, 1, "0.2 (mapa)"},
	{"Items.Magazine_Bullets_Multiplier", "Loot", "Quanto de bala vem no pente que não nasce cheio (0 a 1)", "", "num", 0, 1, "1 (mapa)"},
	{"Items.Crate_Bullets_Full_Chance", "Loot", "Chance da caixa de munição nascer cheia (0 a 1)", "", "num", 0, 1, "0.2 (mapa)"},

	{"Zombies.Spawn_Chance", "Zumbis", "Quantidade de zumbis (0 a 1)", "Fração dos pontos de zumbi que ficam ocupados.", "num", 0, 1, "0.35"},
	{"Zombies.Loot_Chance", "Zumbis", "Chance do zumbi dropar loot (0 a 1)", "", "num", 0, 1, "0.55"},
	{"Zombies.Min_Drops", "Zumbis", "Itens que o zumbi dropa (mínimo)", "", "int", 0, 100, "1"},
	{"Zombies.Max_Drops", "Zumbis", "Itens que o zumbi dropa (máximo)", "", "int", 0, 100, "1"},
	{"Zombies.Min_Mega_Drops", "Zumbis", "Itens do zumbi mega (mínimo)", "", "int", 0, 200, "5"},
	{"Zombies.Max_Mega_Drops", "Zumbis", "Itens do zumbi mega (máximo)", "", "int", 0, 200, "5"},
	{"Zombies.Min_Boss_Drops", "Zumbis", "Itens do chefe (mínimo)", "", "int", 0, 200, "8"},
	{"Zombies.Max_Boss_Drops", "Zumbis", "Itens do chefe (máximo)", "", "int", 0, 200, "10"},
	{"Zombies.Respawn_Day_Time", "Zumbis", "Zumbi renasce de dia em (segundos)", "", "num", 1, 3600, "360"},
	{"Zombies.Respawn_Night_Time", "Zumbis", "Zumbi renasce de noite em (segundos)", "", "num", 1, 3600, "30"},
	{"Zombies.Damage_Multiplier", "Zumbis", "Dano do zumbi (1 = normal)", "", "num", 0, 10, "0.9 (mapa)"},
	{"Zombies.Armor_Multiplier", "Zumbis", "Resistência do zumbi (menor = morre mais fácil)", "", "num", 0, 10, "0.75 (mapa)"},
	{"Zombies.Sprinter_Chance", "Zumbis", "Chance de zumbi corredor (0 a 1)", "", "num", 0, 1, "0.15"},

	{"Animals.Respawn_Time", "Animais", "Animal renasce em (segundos)", "", "num", 1, 3600, "180"},
	{"Animals.Max_Instances_Medium", "Animais", "Máximo de animais (mapa médio)", "", "int", 0, 1000, "12"},
	{"Animals.Max_Instances_Large", "Animais", "Máximo de animais (mapa grande)", "", "int", 0, 1000, "16"},
	{"Animals.Max_Instances_Insane", "Animais", "Máximo de animais (mapa gigante, o California2)", "", "int", 0, 1000, "30 (mapa)"},
	{"Animals.Damage_Multiplier", "Animais", "Dano dos animais (1 = normal)", "", "num", 0, 10, "1"},

	{"Vehicles.Respawn_Time", "Veículos", "Carro renasce em (segundos)", "", "num", 10, 86400, "300"},
	{"Vehicles.Max_Instances_Insane", "Veículos", "Máximo de carros (mapa gigante, o California2)", "", "int", 0, 500, "64"},
	{"Vehicles.Min_Natural_Vehicles", "Veículos", "Mínimo de carros soltos no mapa", "", "int", 0, 500, "0"},
	{"Vehicles.Has_Battery_Chance", "Veículos", "Chance do carro nascer com bateria (0 a 1)", "", "num", 0, 1, "0.8"},
	{"Vehicles.Has_Tire_Chance", "Veículos", "Chance do carro nascer com pneu (0 a 1)", "", "num", 0, 1, "0.7"},
	{"Vehicles.Armor_Multiplier", "Veículos", "Resistência dos carros (menor = quebra mais fácil)", "", "num", 0, 10, "1"},

	{"Players.Experience_Multiplier", "Jogadores", "XP ganho (1 = normal, 2 = dobro)", "", "num", 0, 100, "1"},
	{"Players.Food_Use_Ticks", "Jogadores", "Fome (maior = demora mais pra ficar com fome)", "", "int", 1, 10000, "700 (mapa)"},
	{"Players.Water_Use_Ticks", "Jogadores", "Sede (maior = demora mais pra ficar com sede)", "", "int", 1, 10000, "700 (mapa)"},
	{"Players.Lose_Items_PvP", "Jogadores", "Fração dos itens que cai ao morrer pra jogador (0 a 1)", "", "num", 0, 1, "1"},
	{"Players.Lose_Items_PvE", "Jogadores", "Fração dos itens que cai ao morrer pra zumbi (0 a 1)", "", "num", 0, 1, "1"},
	{"Players.Skill_Cost_Multiplier", "Jogadores", "Preço das skills (1 = normal, 0.5 = metade)", "", "num", 0, 100, "1"},
	{"Players.Can_Stop_Bleeding", "Jogadores", "Sangramento para sozinho", "", "bool", 0, 0, "False (mapa)"},
	{"Players.Allow_Instakill_Headshots", "Jogadores", "Headshot mata na hora", "", "bool", 0, 0, "False"},

	{"Barricades.Decay_Time", "Bases", "Barricada abandonada some em (segundos)", "604800 = 7 dias. O upkeep do plugin continua valendo.", "int", 0, 100000000, "604800 (mapa)"},
	{"Structures.Decay_Time", "Bases", "Estrutura abandonada some em (segundos)", "", "int", 0, 100000000, "604800 (mapa)"},
}

// Chaves que o multiplicador de loot mexe (a partir da base gravada na 1ª vez).
var chavesMult = []string{"Items.Spawn_Chance", "Items.Respawn_Time", "Zombies.Loot_Chance", "Zombies.Min_Drops", "Zombies.Max_Drops",
	"Zombies.Min_Mega_Drops", "Zombies.Max_Mega_Drops", "Zombies.Min_Boss_Drops", "Zombies.Max_Boss_Drops"}

var baseLootX1 = map[string]string{"Items.Spawn_Chance": "0.2", "Items.Respawn_Time": "180", "Zombies.Loot_Chance": "0.5", "Zombies.Min_Drops": "1", "Zombies.Max_Drops": "2",
	"Zombies.Min_Mega_Drops": "5", "Zombies.Max_Mega_Drops": "8", "Zombies.Min_Boss_Drops": "8", "Zombies.Max_Boss_Drops": "12"}

type estadoConfigJogo struct {
	Base  map[string]string `json:"base"`
	Mult  float64           `json:"mult"`
	Salvo int64             `json:"salvo"`
	Por   string            `json:"por"`
}

var muConfigJogo sync.Mutex

func arqConfigTxt() string {
	return filepath.Clean(filepath.Join(fonte.Pasta, "..", "..", "..", "Config.txt"))
}
func arqEstadoConfigJogo() string { return filepath.Join(dados.PastaPainel, "config_jogo.json") }

// lerChavesConfig: valor de cada Bloco.Chave do Config.txt ("" = vazia, usa o mapa/padrão).
func lerChavesConfig(txt string) map[string]string {
	m := map[string]string{}
	bloco, prof := "", 0
	ant := ""
	for _, l := range strings.Split(strings.TrimPrefix(txt, "\uFEFF"), "\n") {
		t := strings.TrimSpace(l)
		switch {
		case t == "" || strings.HasPrefix(t, "//"):
			continue
		case t == "{" || t == "[":
			if prof == 0 {
				bloco = ant
			}
			prof++
		case t == "}" || t == "]":
			prof--
			if prof == 0 {
				bloco = ""
			}
		case prof == 1 && bloco != "":
			k, v, _ := strings.Cut(t, " ")
			m[bloco+"."+k] = strings.TrimSpace(v)
		}
		ant = t
	}
	return m
}

// gravarChaveConfig troca (ou cria) Bloco.Chave no Config.txt, mantendo o resto igual.
func gravarChaveConfig(txt, chave, valor string) (string, error) {
	bloco, nome, ok := strings.Cut(chave, ".")
	if !ok {
		return txt, fmt.Errorf("chave invalida %s", chave)
	}
	linhas := strings.Split(txt, "\n")
	prof, dentro, ant, fim := 0, false, "", -1
	nova := "\t" + nome
	if valor != "" {
		nova += " " + valor
	}
	for i, l := range linhas {
		t := strings.TrimSpace(strings.TrimPrefix(l, "\uFEFF"))
		switch {
		case t == "" || strings.HasPrefix(t, "//"):
			continue
		case t == "{" || t == "[":
			if prof == 0 && ant == bloco {
				dentro = true
			}
			prof++
		case t == "}" || t == "]":
			prof--
			if prof == 0 && dentro {
				fim = i
			}
		case prof == 1 && dentro:
			if k, _, _ := strings.Cut(t, " "); k == nome {
				linhas[i] = nova
				return strings.Join(linhas, "\n"), nil
			}
		}
		if fim >= 0 {
			break
		}
		ant = t
	}
	if fim < 0 {
		return txt, fmt.Errorf("bloco %s nao achado no Config.txt", bloco)
	}
	linhas = append(linhas[:fim], append([]string{nova}, linhas[fim:]...)...)
	return strings.Join(linhas, "\n"), nil
}

func validarValor(c campoJogo, v string) (string, error) {
	v = strings.TrimSpace(strings.ReplaceAll(v, ",", "."))
	if v == "" {
		return "", nil
	}
	switch c.Tipo {
	case "bool":
		switch strings.ToLower(v) {
		case "true", "sim", "1":
			return "True", nil
		case "false", "nao", "não", "0":
			return "False", nil
		}
		return "", fmt.Errorf("%s: use sim ou não", c.Nome)
	case "int":
		n, err := strconv.ParseUint(v, 10, 32)
		if err != nil || float64(n) < c.Min || float64(n) > c.Max {
			return "", fmt.Errorf("%s: número inteiro de %g a %g", c.Nome, c.Min, c.Max)
		}
		return strconv.FormatUint(n, 10), nil
	default:
		f, err := strconv.ParseFloat(v, 64)
		if err != nil || f < c.Min || f > c.Max {
			return "", fmt.Errorf("%s: número de %g a %g", c.Nome, c.Min, c.Max)
		}
		return strconv.FormatFloat(f, 'f', -1, 64), nil
	}
}

func lerEstadoConfigJogo() estadoConfigJogo {
	var e estadoConfigJogo
	if b, err := os.ReadFile(arqEstadoConfigJogo()); err == nil {
		_ = json.Unmarshal(b, &e)
	}
	if e.Mult <= 0 {
		e.Mult = 1
	}
	return e
}

// guardarBackupConfig copia o Config.txt antes de mexer (fica os 10 mais novos).
func guardarBackupConfig(txt []byte) error {
	pasta := filepath.Join(dados.PastaPainel, "config-backups")
	if err := os.MkdirAll(pasta, 0o750); err != nil {
		return err
	}
	if err := os.WriteFile(filepath.Join(pasta, "Config-"+time.Now().In(fusoBR).Format("2006-01-02-150405")+".txt"), txt, 0o640); err != nil {
		return err
	}
	l, _ := filepath.Glob(filepath.Join(pasta, "Config-*.txt"))
	sort.Strings(l)
	for len(l) > 10 {
		_ = os.Remove(l[0])
		l = l[1:]
	}
	return nil
}

func rotasConfigJogo(mux *http.ServeMux) {
	mux.HandleFunc("/api/admin/configjogo", func(w http.ResponseWriter, r *http.Request) {
		a, ok := exigirStaff(w, r)
		if !ok {
			return
		}
		if !a.Dono {
			http.Error(w, "so o dono", http.StatusForbidden)
			return
		}
		muConfigJogo.Lock()
		defer muConfigJogo.Unlock()
		bruto, err := os.ReadFile(arqConfigTxt())
		if err != nil {
			http.Error(w, "não achei o Config.txt do servidor", http.StatusInternalServerError)
			return
		}
		atual := lerChavesConfig(string(bruto))
		est := lerEstadoConfigJogo()
		if est.Base == nil {
			// x1 = como o servidor estava antes do loot 5x (Spawn_Chance 0.2, Respawn_Time 180)
			est.Base = baseLootX1
			if f, err := strconv.ParseFloat(atual["Items.Respawn_Time"], 64); err == nil && f > 0 {
				est.Mult = float64(int(180/f*10+0.5)) / 10
			}
			_ = dados.GravarJSON(arqEstadoConfigJogo(), est)
		}
		if r.Method == http.MethodGet {
			valores := map[string]string{}
			for _, c := range camposJogo {
				valores[c.Chave] = atual[c.Chave]
			}
			jsonOK(w, map[string]any{"campos": camposJogo, "valores": valores, "base": est.Base, "chavesMult": chavesMult, "mult": est.Mult, "salvo": est.Salvo, "por": est.Por})
			return
		}
		if r.Method != http.MethodPost || r.Header.Get("X-DZ") != "1" {
			http.Error(w, "metodo", http.StatusMethodNotAllowed)
			return
		}
		var q struct {
			Valores map[string]string
			Mult    float64
		}
		if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 16384)).Decode(&q); err != nil {
			http.Error(w, "pedido invalido", http.StatusBadRequest)
			return
		}
		txt := string(bruto)
		var mudou []string
		for _, c := range camposJogo {
			v, tem := q.Valores[c.Chave]
			if !tem {
				continue
			}
			v, err := validarValor(c, v)
			if err != nil {
				http.Error(w, err.Error(), http.StatusBadRequest)
				return
			}
			if v == atual[c.Chave] {
				continue
			}
			if txt, err = gravarChaveConfig(txt, c.Chave, v); err != nil {
				http.Error(w, err.Error(), http.StatusInternalServerError)
				return
			}
			mudou = append(mudou, fmt.Sprintf("%s: %q -> %q", c.Chave, atual[c.Chave], v))
		}
		if q.Mult >= 1 && q.Mult <= 100 {
			est.Mult = q.Mult
		}
		if len(mudou) > 0 {
			if err := guardarBackupConfig(bruto); err != nil {
				http.Error(w, "não consegui fazer o backup, nada foi mudado", http.StatusInternalServerError)
				return
			}
			tmp := arqConfigTxt() + ".dz.tmp"
			if err := os.WriteFile(tmp, []byte(txt), 0o644); err != nil {
				http.Error(w, "não consegui gravar o Config.txt", http.StatusInternalServerError)
				return
			}
			if err := os.Rename(tmp, arqConfigTxt()); err != nil {
				_ = os.Remove(tmp)
				http.Error(w, "não consegui gravar o Config.txt", http.StatusInternalServerError)
				return
			}
			est.Salvo, est.Por = time.Now().Unix(), a.Nome
			log.Printf("configjogo: %s mudou %s", a.Steam, strings.Join(mudou, "; "))
		}
		_ = dados.GravarJSON(arqEstadoConfigJogo(), est)
		jsonOK(w, map[string]any{"ok": true, "mudou": len(mudou)})
	})
}
