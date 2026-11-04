package main

import (
	"bytes"
	"image"
	"image/color"
	"image/draw"
	"image/jpeg"
	"net/http"
	"os"
	"path/filepath"
	"regexp"
	"strconv"
	"sync"
)

// Minimapa do jogo: o plugin manda pro jogador a URL /mini/<x>_<y>.jpg, com x e y = pixel do mapa
// (Chart do California2, 1 px = 1 m) onde o jogador está. O site recorta 256x256 em volta e devolve.

const ladoMini = 256

var (
	reMini     = regexp.MustCompile(`^(\d{1,5})_(\d{1,5})\.jpg$`)
	muMapa     sync.Mutex
	mapaChart  image.Image
	mapaErro   error
	cacheMini  sync.Map // nome -> []byte
	qtdCache   int
	muQtdCache sync.Mutex
	fundoMini  = color.RGBA{18, 22, 20, 255}
)

func carregarChart(arq string) (image.Image, error) {
	muMapa.Lock()
	defer muMapa.Unlock()
	if mapaChart != nil || mapaErro != nil {
		return mapaChart, mapaErro
	}
	f, err := os.Open(arq)
	if err != nil {
		mapaErro = err
		return nil, err
	}
	defer f.Close()
	mapaChart, mapaErro = jpeg.Decode(f)
	return mapaChart, mapaErro
}

func rotasMinimapa(mux *http.ServeMux, pastaIcones string) {
	arq := filepath.Join(pastaIcones, "..", "mapa", "ca2-chart.jpg")
	mux.HandleFunc("/mini/", func(w http.ResponseWriter, r *http.Request) {
		nome := filepath.Base(r.URL.Path)
		m := reMini.FindStringSubmatch(nome)
		if m == nil {
			http.NotFound(w, r)
			return
		}
		if b, ok := cacheMini.Load(nome); ok {
			mandarMini(w, b.([]byte))
			return
		}
		img, err := carregarChart(arq)
		if err != nil {
			http.Error(w, "mapa indisponivel", http.StatusServiceUnavailable)
			return
		}
		x, _ := strconv.Atoi(m[1])
		y, _ := strconv.Atoi(m[2])
		b := img.Bounds()
		if x > b.Dx()+ladoMini || y > b.Dy()+ladoMini {
			http.NotFound(w, r)
			return
		}
		out := image.NewRGBA(image.Rect(0, 0, ladoMini, ladoMini))
		draw.Draw(out, out.Bounds(), &image.Uniform{fundoMini}, image.Point{}, draw.Src)
		origem := image.Pt(x-ladoMini/2, y-ladoMini/2).Add(b.Min)
		draw.Draw(out, out.Bounds(), img, origem, draw.Src)
		var buf bytes.Buffer
		if err := jpeg.Encode(&buf, out, &jpeg.Options{Quality: 82}); err != nil {
			http.Error(w, "erro", http.StatusInternalServerError)
			return
		}
		muQtdCache.Lock()
		if qtdCache < 3000 { // ~20 KB cada: no máximo uns 60 MB na memória
			qtdCache++
			cacheMini.Store(nome, buf.Bytes())
		}
		muQtdCache.Unlock()
		mandarMini(w, buf.Bytes())
	})
}

func mandarMini(w http.ResponseWriter, b []byte) {
	w.Header().Set("Content-Type", "image/jpeg")
	w.Header().Set("Cache-Control", "public, max-age=604800")
	_, _ = w.Write(b)
}
