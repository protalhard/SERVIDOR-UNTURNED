package dados

import (
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

// Vínculo Discord <-> Steam. O bot gera um link assinado (/vincular?d=..&e=..&s=..),
// o site confere a assinatura, faz o login na Steam e grava vinculos_discord.tsv
// na pasta do plugin (discord, steam, quando). O plugin libera o kit "discord"
// e o bot manda o alerta de raid na DM.

// ArqChavePadrao é a chave que o bot e o site dividem pra assinar o link.
const ArqChavePadrao = "/home/unturned/dz-vinculo.key"

// Chave lê a chave (ou cria uma nova se não existir).
func Chave(arq string) []byte {
	if b, err := os.ReadFile(arq); err == nil && len(b) >= 32 {
		return b
	}
	b := make([]byte, 32)
	_, _ = rand.Read(b)
	_ = os.MkdirAll(filepath.Dir(arq), 0o755)
	_ = os.WriteFile(arq, b, 0o640)
	return b
}

func AssinarVinculo(chave []byte, discord string, expira int64) string {
	m := hmac.New(sha256.New, chave)
	fmt.Fprintf(m, "vinculo:%s:%d", discord, expira)
	return hex.EncodeToString(m.Sum(nil))
}

// ConferirVinculo diz se o link é válido e não expirou.
func ConferirVinculo(chave []byte, discord, expira, assinatura string) bool {
	if _, err := strconv.ParseUint(discord, 10, 64); err != nil || len(discord) < 15 || len(discord) > 21 {
		return false
	}
	e, err := strconv.ParseInt(expira, 10, 64)
	if err != nil || time.Now().Unix() > e {
		return false
	}
	return hmac.Equal([]byte(AssinarVinculo(chave, discord, e)), []byte(assinatura))
}

type Vinculo struct {
	Discord, Steam string
	Quando         int64
}

func (f Fonte) arqVinculos() string { return filepath.Join(f.Pasta, "vinculos_discord.tsv") }

func (f Fonte) LerVinculos() []Vinculo {
	b, err := os.ReadFile(f.arqVinculos())
	if err != nil {
		return nil
	}
	var l []Vinculo
	for _, linha := range strings.Split(string(b), "\n") {
		c := strings.Split(strings.TrimSpace(linha), "\t")
		if len(c) < 2 || c[0] == "" || c[1] == "" {
			continue
		}
		v := Vinculo{Discord: c[0], Steam: c[1]}
		if len(c) > 2 {
			v.Quando, _ = strconv.ParseInt(c[2], 10, 64)
		}
		l = append(l, v)
	}
	return l
}

// GravarVinculo liga o Discord à Steam. Cada Discord tem uma Steam e cada Steam um Discord.
func (f Fonte) GravarVinculo(discord, steam string) error {
	var b strings.Builder
	for _, v := range f.LerVinculos() {
		if v.Discord == discord || v.Steam == steam {
			continue
		}
		fmt.Fprintf(&b, "%s\t%s\t%d\n", v.Discord, v.Steam, v.Quando)
	}
	fmt.Fprintf(&b, "%s\t%s\t%d\n", discord, steam, time.Now().Unix())
	tmp := f.arqVinculos() + ".tmp"
	if err := os.WriteFile(tmp, []byte(b.String()), 0o644); err != nil {
		return err
	}
	return os.Rename(tmp, f.arqVinculos())
}

// SteamDoDiscord devolve a Steam vinculada ("" se não tiver).
func (f Fonte) SteamDoDiscord(discord string) string {
	for _, v := range f.LerVinculos() {
		if v.Discord == discord {
			return v.Steam
		}
	}
	return ""
}
