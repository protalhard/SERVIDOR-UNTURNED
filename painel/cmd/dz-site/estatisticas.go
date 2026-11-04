package main

import (
	"net/http"
	"sort"
	"strconv"
	"time"
)

// Estatísticas do painel (aba Estatísticas): online_hist.tsv (foto do online a cada 5 min, do plugin)
// e jogadores_log.tsv (entrou/saiu). Tudo no horário de Brasília.

type diaStat struct {
	Dia      string `json:"dia"`
	Pico     int    `json:"pico"`
	Media    float64 `json:"media"`
	Jogadores int   `json:"jogadores"`
	Novatos  int    `json:"novatos"`
}

type tempoStat struct {
	Steam   string  `json:"steam"`
	Nome    string  `json:"nome"`
	Horas   float64 `json:"horas"`
	Sessoes int     `json:"sessoes"`
}

func estatisticas() map[string]any {
	br := time.FixedZone("BRT", -3*3600)
	agora := time.Now().In(br)
	desde30 := agora.AddDate(0, 0, -30)
	desde7 := agora.AddDate(0, 0, -7)
	diaDe := func(t time.Time) string { return t.In(br).Format("2006-01-02") }

	// ---- online por hora e por dia ----
	var somaHora, nHora [24]float64
	var picoHora [24]int
	dias := map[string]*diaStat{}
	somaDia := map[string]float64{}
	nDia := map[string]float64{}
	online, ultimaFoto := 0, ""
	for _, c := range lerTSV("online_hist.tsv", 0) {
		if len(c) < 2 {
			continue
		}
		t, err := time.Parse(time.RFC3339Nano, c[0])
		n, err2 := strconv.Atoi(c[1])
		if err != nil || err2 != nil {
			continue
		}
		t = t.In(br)
		online, ultimaFoto = n, t.Format("02/01 15:04")
		if t.Before(desde30) {
			continue
		}
		d := diaDe(t)
		if dias[d] == nil {
			dias[d] = &diaStat{Dia: d}
		}
		if n > dias[d].Pico {
			dias[d].Pico = n
		}
		somaDia[d] += float64(n)
		nDia[d]++
		if !t.Before(desde7) {
			h := t.Hour()
			somaHora[h] += float64(n)
			nHora[h]++
			if n > picoHora[h] {
				picoHora[h] = n
			}
		}
	}

	// ---- sessões, novatos e retenção ----
	type evento struct {
		t           time.Time
		steam, nome string
		tipo        string
	}
	var evs []evento
	for _, c := range lerTSV("jogadores_log.tsv", 0) {
		if len(c) < 4 || (c[3] != "entrou" && c[3] != "saiu" && c[3] != "chutado") {
			continue
		}
		t, err := time.Parse(time.RFC3339Nano, c[0])
		if err != nil {
			continue
		}
		evs = append(evs, evento{t.In(br), c[1], c[2], c[3]})
	}
	primeiro := map[string]time.Time{}
	diasDe := map[string]map[string]bool{}
	nomes := map[string]string{}
	aberta := map[string]time.Time{}
	tempo := map[string]*tempoStat{}
	jogaramDia := map[string]map[string]bool{}
	fechar := func(st string, fim time.Time) {
		ini, ok := aberta[st]
		if !ok {
			return
		}
		delete(aberta, st)
		if fim.Sub(ini) > 8*time.Hour { // reinício sem "saiu": não conta horas fantasmas
			fim = ini.Add(8 * time.Hour)
		}
		if fim.Before(desde7) {
			return
		}
		if ini.Before(desde7) {
			ini = desde7
		}
		ts := tempo[st]
		if ts == nil {
			ts = &tempoStat{Steam: st}
			tempo[st] = ts
		}
		ts.Horas += fim.Sub(ini).Hours()
		ts.Sessoes++
	}
	for _, e := range evs {
		if e.nome != "" {
			nomes[e.steam] = e.nome
		}
		if e.tipo == "entrou" {
			fechar(e.steam, e.t)
			aberta[e.steam] = e.t
			if _, ok := primeiro[e.steam]; !ok {
				primeiro[e.steam] = e.t
			}
			d := diaDe(e.t)
			if diasDe[e.steam] == nil {
				diasDe[e.steam] = map[string]bool{}
			}
			diasDe[e.steam][d] = true
			if jogaramDia[d] == nil {
				jogaramDia[d] = map[string]bool{}
			}
			jogaramDia[d][e.steam] = true
		} else {
			fechar(e.steam, e.t)
		}
	}
	for st := range aberta {
		fechar(st, agora)
	}
	novatos, voltaram := 0, 0
	for st, t := range primeiro {
		if t.Before(desde30) {
			continue
		}
		d := diaDe(t)
		if dias[d] == nil {
			dias[d] = &diaStat{Dia: d}
		}
		dias[d].Novatos++
		if agora.Sub(t) < 24*time.Hour { // ainda não deu tempo de voltar
			continue
		}
		novatos++
		if len(diasDe[st]) > 1 {
			voltaram++
		}
	}
	for d, m := range jogaramDia {
		if dias[d] != nil {
			dias[d].Jogadores = len(m)
		} else if t, err := time.ParseInLocation("2006-01-02", d, br); err == nil && !t.Before(desde30.Truncate(24*time.Hour)) {
			dias[d] = &diaStat{Dia: d, Jogadores: len(m)}
		}
	}
	listaDias := []diaStat{}
	for d, s := range dias {
		if nDia[d] > 0 {
			s.Media = float64(int(somaDia[d]/nDia[d]*10)) / 10
		}
		listaDias = append(listaDias, *s)
	}
	sort.Slice(listaDias, func(i, j int) bool { return listaDias[i].Dia < listaDias[j].Dia })

	horas := make([]map[string]any, 24)
	for h := 0; h < 24; h++ {
		m := 0.0
		if nHora[h] > 0 {
			m = float64(int(somaHora[h]/nHora[h]*10)) / 10
		}
		horas[h] = map[string]any{"hora": h, "media": m, "pico": picoHora[h]}
	}
	top := []tempoStat{}
	for st, t := range tempo {
		t.Nome = nomes[st]
		t.Horas = float64(int(t.Horas*10)) / 10
		top = append(top, *t)
	}
	sort.Slice(top, func(i, j int) bool { return top[i].Horas > top[j].Horas })
	if len(top) > 30 {
		top = top[:30]
	}
	pico30, unicos30 := 0, map[string]bool{}
	for _, d := range listaDias {
		if d.Pico > pico30 {
			pico30 = d.Pico
		}
	}
	for d, m := range jogaramDia {
		if t, err := time.ParseInLocation("2006-01-02", d, br); err == nil && !t.Before(desde30) {
			for st := range m {
				unicos30[st] = true
			}
		}
	}
	ret := 0.0
	if novatos > 0 {
		ret = float64(int(float64(voltaram)*1000/float64(novatos))) / 10
	}
	return map[string]any{
		"online": online, "ultimaFoto": ultimaFoto, "temHistorico": ultimaFoto != "",
		"pico30": pico30, "unicos30": len(unicos30), "novatos30": novatos, "voltaram30": voltaram, "retencao": ret,
		"porHora": horas, "porDia": listaDias, "tempo7": top,
	}
}

func rotasEstatisticas(mux *http.ServeMux) {
	mux.HandleFunc("/api/admin/estatisticas", func(w http.ResponseWriter, r *http.Request) {
		if _, ok := exigirStaff(w, r); !ok {
			return
		}
		jsonOK(w, estatisticas())
	})
}
