#!/bin/bash
# Baixa as músicas da DZ RÁDIO (lista em painel/cmd/dz-site/web/radio.json) pra pasta do site.
# Todas são Creative Commons (CC BY) do archive.org; o crédito aparece no player.
# Só baixa o que falta. Com ffmpeg, regrava em 128 kbps (mais leve pro site).
set -u
REPO=${REPO:-/home/unturned/repo}
DEST=${1:-/home/unturned/dz-site/musicas}
LISTA="$REPO/painel/cmd/dz-site/web/radio.json"
mkdir -p "$DEST"
python3 - "$LISTA" <<'PY' | while IFS=$'\t' read -r arq url; do
import json, sys, urllib.parse
for t in json.load(open(sys.argv[1])):
    base, nome = t["url"].rsplit("/", 1)
    print(t["arquivo"] + "\t" + base + "/" + urllib.parse.quote(nome))
PY
  [ -s "$DEST/$arq" ] && continue
  tmp="$DEST/.$arq.baixando"
  if curl -fsSL --retry 3 -m 300 -o "$tmp" "$url" </dev/null; then
    if command -v ffmpeg >/dev/null; then
      ffmpeg -nostdin -v error -y -i "$tmp" -map 0:a -map_metadata -1 -codec:a libmp3lame -b:a 128k -ac 2 "$tmp.mp3" && mv "$tmp.mp3" "$DEST/$arq" && rm -f "$tmp"
    fi
    [ -f "$tmp" ] && mv "$tmp" "$DEST/$arq"
    echo "baixou $arq"
  else
    rm -f "$tmp"; echo "falhou $arq"
  fi
done
chown -R unturned:unturned "$DEST" 2>/dev/null || true
