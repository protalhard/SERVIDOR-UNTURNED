#!/usr/bin/env bash
# Puxa o GitHub e, se tiver commit novo, roda scripts/aplicar.sh (como root).
set -euo pipefail
REPO=/home/unturned/repo
LOG=/var/log/unturned-sync.log
antes=$(sudo -u unturned git -C "$REPO" rev-parse HEAD)
sudo -u unturned git -C "$REPO" fetch -q origin main
depois=$(sudo -u unturned git -C "$REPO" rev-parse origin/main)
[ "$antes" = "$depois" ] && exit 0
sudo -u unturned git -C "$REPO" reset -q --hard origin/main
{
  echo "== $(date '+%F %T') $antes -> $depois"
  [ -f "$REPO/scripts/aplicar.sh" ] && bash "$REPO/scripts/aplicar.sh"
} >> "$LOG" 2>&1
