#!/usr/bin/env bash
# Roda na VPS (como root) toda vez que o sincronizador acha um commit novo no GitHub.
# Aplica só o que é seguro: plugins compilados e pedido de reinício.
# Não mexe em Commands.dat, Config.txt, mundo nem token.
set -euo pipefail

REPO=/home/unturned/repo
INST=/home/unturned/server/Servers/dayz
ESTADO=/var/lib/unturned-sync
mkdir -p "$ESTADO"
reiniciar=0

# 1) Plugins (.dll) que estão em server-files/Rocket/Plugins
for dll in "$REPO"/server-files/Rocket/Plugins/*.dll; do
  [ -f "$dll" ] || continue
  destino="$INST/Rocket/Plugins/$(basename "$dll")"
  if ! cmp -s "$dll" "$destino"; then
    install -o unturned -g unturned -m 644 "$dll" "$destino"
    echo "plugin atualizado: $(basename "$dll")"
    reiniciar=1
  fi
done

# 2) Pedido de reinício: mudar o número em server-files/REINICIAR força um restart
if [ -f "$REPO/server-files/REINICIAR" ]; then
  pedido=$(cat "$REPO/server-files/REINICIAR")
  feito=$(cat "$ESTADO/reiniciar" 2>/dev/null || true)
  if [ "$pedido" != "$feito" ]; then
    echo "$pedido" > "$ESTADO/reiniciar"
    reiniciar=1
  fi
fi

if [ "$reiniciar" = 1 ]; then
  echo "reiniciando o servidor"
  systemctl restart unturned
fi
