#!/usr/bin/env bash
# Baixa/atualiza o Unturned Dedicated Server e o RocketModFix. Rode como usuario "unturned".
set -euo pipefail
HOME_DIR="/home/unturned"
JOGO_DIR="$HOME_DIR/server"
STEAMCMD_DIR="$HOME_DIR/steamcmd"
ROCKET_URL="https://github.com/RocketModFix/RocketModFix/releases/download/4.23.1/Rocket.Unturned.Module.zip"

"$STEAMCMD_DIR/steamcmd.sh" +force_install_dir "$JOGO_DIR" +login anonymous +app_update 1110390 validate +quit

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
