#!/usr/bin/env bash
# Atualiza tudo: repositorio, jogo, Rocket e nosso plugin. Rode como root.
set -euo pipefail
REPO_DIR="/home/unturned/repo"
INST_DIR="/home/unturned/server/Servers/dayz"
systemctl stop unturned || true
sudo -u unturned git -C "$REPO_DIR" pull --ff-only
sudo -u unturned bash "$REPO_DIR/scripts/update.sh"
sudo -u unturned cp "$REPO_DIR/server-files/Rocket/Plugins/"*.dll "$INST_DIR/Rocket/Plugins/"
systemctl start unturned
echo "Atualizado e religado. Log: journalctl -u unturned -f"
