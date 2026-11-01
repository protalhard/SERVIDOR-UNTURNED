package main

// Desempenho da máquina pro dashboard do painel (CPU, memória, disco, rede e o processo do jogo).
// Lê /proc a cada 2 s em segundo plano; o painel pede /api/admin/maquina e recebe a última leitura.

import (
	"net/http"
	"os"
	"strconv"
	"strings"
	"sync"
	"syscall"
	"time"
)

type leituraMaquina struct {
	Quando      int64     `json:"quando"`
	CPU         float64   `json:"cpu"` // % da máquina toda
	Nucleos     int       `json:"nucleos"`
	ModeloCPU   string    `json:"modeloCpu"`
	Carga       [3]string `json:"carga"`
	RAMTotal    uint64    `json:"ramTotal"`
	RAMUsada    uint64    `json:"ramUsada"`
	DiscoTotal  uint64    `json:"discoTotal"`
	DiscoUsado  uint64    `json:"discoUsado"`
	RedeDesce   float64   `json:"redeDesce"` // bytes/s
	RedeSobe    float64   `json:"redeSobe"`
	Ligada      int64     `json:"ligada"`  // segundos desde que a máquina ligou
	JogoCPU     float64   `json:"jogoCpu"` // % de um núcleo
	JogoRAM     uint64    `json:"jogoRam"`
	JogoLigado  int64     `json:"jogoLigado"` // segundos
	JogoRodando bool      `json:"jogoRodando"`
	HistCPU     []float64 `json:"histCpu"`
	HistRAM     []float64 `json:"histRam"`
	HistDesce   []float64 `json:"histDesce"`
	HistSobe    []float64 `json:"histSobe"`
}

var (
	muMaquina   sync.Mutex
	ultMaquina  leituraMaquina
	ultPedido   time.Time
	maquinaRoda bool
)

const tamHist = 90 // 3 minutos de histórico (a cada 2 s)

func lerLinhas(arq string) []string {
	b, err := os.ReadFile(arq)
	if err != nil {
		return nil
	}
	return strings.Split(string(b), "\n")
}

func cpuTotal() (ocupado, total uint64) {
	for _, l := range lerLinhas("/proc/stat") {
		if !strings.HasPrefix(l, "cpu ") {
			continue
		}
		c := strings.Fields(l)[1:]
		for i, v := range c {
			n, _ := strconv.ParseUint(v, 10, 64)
			if i < 8 { // user nice system idle iowait irq softirq steal
				total += n
				if i != 3 && i != 4 {
					ocupado += n
				}
			}
		}
		return
	}
	return
}

func memoria() (total, usada uint64) {
	var disp uint64
	for _, l := range lerLinhas("/proc/meminfo") {
		c := strings.Fields(l)
		if len(c) < 2 {
			continue
		}
		n, _ := strconv.ParseUint(c[1], 10, 64)
		switch c[0] {
		case "MemTotal:":
			total = n * 1024
		case "MemAvailable:":
			disp = n * 1024
		}
	}
	if total > disp {
		usada = total - disp
	}
	return
}

func rede() (rx, tx uint64) {
	for _, l := range lerLinhas("/proc/net/dev") {
		i := strings.IndexByte(l, ':')
		if i < 0 {
			continue
		}
		nome := strings.TrimSpace(l[:i])
		if nome == "lo" || strings.HasPrefix(nome, "docker") || strings.HasPrefix(nome, "veth") {
			continue
		}
		c := strings.Fields(l[i+1:])
		if len(c) < 9 {
			continue
		}
		r, _ := strconv.ParseUint(c[0], 10, 64)
		t, _ := strconv.ParseUint(c[8], 10, 64)
		rx += r
		tx += t
	}
	return
}

func modeloCPU() (string, int) {
	modelo, n := "", 0
	for _, l := range lerLinhas("/proc/cpuinfo") {
		if strings.HasPrefix(l, "processor") {
			n++
		}
		if modelo == "" && strings.HasPrefix(l, "model name") {
			if i := strings.IndexByte(l, ':'); i > 0 {
				modelo = strings.TrimSpace(l[i+1:])
			}
		}
	}
	return modelo, n
}

func segundosLigada() float64 {
	if l := lerLinhas("/proc/uptime"); len(l) > 0 {
		if c := strings.Fields(l[0]); len(c) > 0 {
			v, _ := strconv.ParseFloat(c[0], 64)
			return v
		}
	}
	return 0
}

// processoJogo acha o Unturned_Headless e devolve pid, ticks de CPU, RSS e início (em ticks desde o boot).
func processoJogo(pidAntes int) (pid int, ticks, rss, inicio uint64) {
	ler := func(p int) bool {
		b, err := os.ReadFile("/proc/" + strconv.Itoa(p) + "/stat")
		if err != nil {
			return false
		}
		s := string(b)
		i := strings.LastIndexByte(s, ')')
		j := strings.IndexByte(s, '(')
		if i < 0 || j < 0 || !strings.HasPrefix(s[j+1:i], "Unturned") {
			return false
		}
		c := strings.Fields(s[i+2:])
		if len(c) < 22 {
			return false
		}
		u, _ := strconv.ParseUint(c[11], 10, 64)
		st, _ := strconv.ParseUint(c[12], 10, 64)
		ini, _ := strconv.ParseUint(c[19], 10, 64)
		pg, _ := strconv.ParseUint(c[21], 10, 64)
		pid, ticks, inicio, rss = p, u+st, ini, pg*uint64(os.Getpagesize())
		return true
	}
	if pidAntes > 0 && ler(pidAntes) {
		return
	}
	ents, _ := os.ReadDir("/proc")
	for _, e := range ents {
		if p, err := strconv.Atoi(e.Name()); err == nil && ler(p) {
			return
		}
	}
	return 0, 0, 0, 0
}

func empurrar(l []float64, v float64) []float64 {
	l = append(l, v)
	if len(l) > tamHist {
		l = l[len(l)-tamHist:]
	}
	return l
}

// lacoMaquina lê a máquina a cada 2 s enquanto alguém estiver com o painel aberto (e mais 1 min).
func lacoMaquina() {
	modelo, nucleos := modeloCPU()
	const hz = 100.0 // USER_HZ do Linux
	var (
		ocAnt, totAnt, rxAnt, txAnt, jogoAnt uint64
		pid                                  int
		antes                                time.Time
		hc, hr, hd, hs                       []float64
	)
	for {
		muMaquina.Lock()
		parado := time.Since(ultPedido) > time.Minute
		if parado {
			maquinaRoda = false
			muMaquina.Unlock()
			return
		}
		muMaquina.Unlock()

		agora := time.Now()
		oc, tot := cpuTotal()
		rx, tx := rede()
		mt, mu := memoria()
		var fs syscall.Statfs_t
		var dt, du uint64
		if syscall.Statfs("/", &fs) == nil {
			dt = fs.Blocks * uint64(fs.Bsize)
			du = dt - fs.Bavail*uint64(fs.Bsize)
		}
		p, ticks, rss, ini := processoJogo(pid)
		ligada := segundosLigada()
		l := leituraMaquina{Quando: agora.Unix(), Nucleos: nucleos, ModeloCPU: modelo, RAMTotal: mt, RAMUsada: mu,
			DiscoTotal: dt, DiscoUsado: du, Ligada: int64(ligada), JogoRodando: p > 0, JogoRAM: rss}
		if c := lerLinhas("/proc/loadavg"); len(c) > 0 {
			if f := strings.Fields(c[0]); len(f) >= 3 {
				l.Carga = [3]string{f[0], f[1], f[2]}
			}
		}
		if p > 0 {
			l.JogoLigado = int64(ligada - float64(ini)/hz)
		}
		if !antes.IsZero() {
			dtSeg := agora.Sub(antes).Seconds()
			if tot > totAnt {
				l.CPU = 100 * float64(oc-ocAnt) / float64(tot-totAnt)
			}
			if rx >= rxAnt && tx >= txAnt {
				l.RedeDesce, l.RedeSobe = float64(rx-rxAnt)/dtSeg, float64(tx-txAnt)/dtSeg
			}
			if p > 0 && p == pid && ticks >= jogoAnt {
				l.JogoCPU = 100 * float64(ticks-jogoAnt) / hz / dtSeg
			}
			hc, hd, hs = empurrar(hc, l.CPU), empurrar(hd, l.RedeDesce), empurrar(hs, l.RedeSobe)
			if mt > 0 {
				hr = empurrar(hr, 100*float64(mu)/float64(mt))
			}
		}
		l.HistCPU, l.HistRAM, l.HistDesce, l.HistSobe = append([]float64(nil), hc...), append([]float64(nil), hr...),
			append([]float64(nil), hd...), append([]float64(nil), hs...)
		ocAnt, totAnt, rxAnt, txAnt, jogoAnt, pid, antes = oc, tot, rx, tx, ticks, p, agora
		muMaquina.Lock()
		ultMaquina = l
		muMaquina.Unlock()
		time.Sleep(2 * time.Second)
	}
}

func apiMaquina(w http.ResponseWriter, r *http.Request) {
	if _, ok := exigirStaff(w, r); !ok {
		return
	}
	muMaquina.Lock()
	ultPedido = time.Now()
	if !maquinaRoda {
		maquinaRoda = true
		go lacoMaquina()
	}
	l := ultMaquina
	muMaquina.Unlock()
	jsonOK(w, l)
}
