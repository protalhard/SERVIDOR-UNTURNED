#!/usr/bin/env bash
# Puxa o GitHub e, se tiver commit novo, roda scripts/aplicar.sh (como root).
set -euo pipefail
REPO=/home/unturned/repo
LOG=/var/log/unturned-sync.log
antes=$(sudo -u unturned git -C "$REPO" rev-parse HEAD)
sudo -u unturned git -C "$REPO" fetch -q origin main
depois=$(sudo -u unturned git -C "$REPO" rev-parse origin/main)
if [ "$antes" = "$depois" ]; then
  # sem commit novo: só roda o aplicar.sh se tiver reinício pendente esperando o servidor esvaziar
  [ -f /var/lib/unturned-sync/reiniciar-pendente ] && bash "$REPO/scripts/aplicar.sh" >> "$LOG" 2>&1
  exit 0
fi
sudo -u unturned git -C "$REPO" reset -q --hard origin/main
{
  echo "== $(date '+%F %T') $antes -> $depois"
  [ -f "$REPO/scripts/aplicar.sh" ] && bash "$REPO/scripts/aplicar.sh"
} >> "$LOG" 2>&1
