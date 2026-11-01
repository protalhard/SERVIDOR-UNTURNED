package main

import (
	"crypto/tls"
	"log"
	"net"
	"net/http"
	"strings"
	"time"

	"golang.org/x/crypto/acme/autocert"
)

// servirHTTPS: o domínio (ex.: dayz.snaplag.com.br) abre com cadeado, com certificado grátis do Let's Encrypt
// que se renova sozinho. Pelo IP continua em http (o jogo e links antigos usam o IP).
// Quem entra pelo domínio em http é mandado pro https.
func servirHTTPS(addrHTTP, dominios, pastaCert string, h http.Handler) {
	var nomes []string
	for _, d := range strings.Split(dominios, ",") {
		if d = strings.ToLower(strings.TrimSpace(d)); d != "" {
			nomes = append(nomes, d)
		}
	}
	m := &autocert.Manager{
		Prompt:     autocert.AcceptTOS,
		HostPolicy: autocert.HostWhitelist(nomes...),
		Cache:      autocert.DirCache(pastaCert),
	}
	ehDominio := func(host string) bool {
		if hh, _, err := net.SplitHostPort(host); err == nil {
			host = hh
		}
		host = strings.ToLower(host)
		for _, n := range nomes {
			if host == n {
				return true
			}
		}
		return false
	}
	http80 := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if ehDominio(r.Host) {
			http.Redirect(w, r, "https://"+strings.ToLower(r.Host)+r.URL.RequestURI(), http.StatusMovedPermanently)
			return
		}
		h.ServeHTTP(w, r)
	})
	go func() {
		srv := &http.Server{Addr: addrHTTP, Handler: m.HTTPHandler(http80), ReadTimeout: 10 * time.Second, WriteTimeout: 20 * time.Second}
		log.Println("dz-site http em", addrHTTP)
		log.Fatal(srv.ListenAndServe())
	}()
	seguro := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Strict-Transport-Security", "max-age=86400")
		h.ServeHTTP(w, r)
	})
	srv := &http.Server{
		Addr: ":443", Handler: seguro, ReadTimeout: 10 * time.Second, WriteTimeout: 20 * time.Second,
		TLSConfig: &tls.Config{GetCertificate: m.GetCertificate, MinVersion: tls.VersionTLS12, NextProtos: []string{"h2", "http/1.1", "acme-tls/1"}},
	}
	log.Println("dz-site https em :443 para", strings.Join(nomes, ", "))
	log.Fatal(srv.ListenAndServeTLS("", ""))
}
