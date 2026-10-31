#!/usr/bin/env bash
# Baixa/atualiza o Unturned Dedicated Server e o RocketModFix. Rode como usuario "unturned".
set -euo pipefail
HOME_DIR="/home/unturned"
JOGO_DIR="$HOME_DIR/server"
STEAMCMD_DIR="$HOME_DIR/steamcmd"
ROCKET_URL="https://github.com/RocketModFix/RocketModFix/releases/download/4.23.1/Rocket.Unturned.Module.zip"

# Primeiro deixa o SteamCMD se atualizar sozinho (evita o erro "Missing configuration")
"$STEAMCMD_DIR/steamcmd.sh" +login anonymous +quit || true

OK=0
for tentativa in 1 2 3 4; do
  echo "Baixando Unturned (tentativa $tentativa)..."
  if "$STEAMCMD_DIR/steamcmd.sh" +force_install_dir "$JOGO_DIR" +login anonymous \
       +app_info_update 1 +app_update 1110390 validate +quit \
     && [ -x "$JOGO_DIR/ServerHelper.sh" ]; then
    OK=1; break
  fi
  sleep 5
done
[ "$OK" = 1 ] || { echo "Falha ao baixar o Unturned pelo SteamCMD."; exit 1; }

# Biblioteca da Steam que o servidor precisa
mkdir -p "$HOME_DIR/.steam/sdk64"
ln -sf "$STEAMCMD_DIR/linux64/steamclient.so" "$HOME_DIR/.steam/sdk64/steamclient.so"

# RocketModFix (versao mantida do RocketMod)
TMP=$(mktemp -d)
curl -fsSL -o "$TMP/rocket.zip" "$ROCKET_URL"
unzip -o -q "$TMP/rocket.zip" -d "$TMP/r"
rm -rf "$JOGO_DIR/Modules/Rocket.Unturned"
mkdir -p "$JOGO_DIR/Modules"
cp -r "$TMP/r/Rocket.Unturned" "$JOGO_DIR/Modules/"
rm -rf "$TMP"
echo "Unturned + RocketModFix atualizados."
