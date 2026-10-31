#!/usr/bin/env bash
# Faz backup dos saves, banco e configs do servidor em /home/unturned/backups
set -euo pipefail
DEST="/home/unturned/backups"
mkdir -p "$DEST"
tar czf "$DEST/dayz-$(date +%F-%H%M).tar.gz" -C /home/unturned/server/Servers dayz
ls -1t "$DEST"/*.tar.gz | tail -n +15 | xargs -r rm -f
echo "Backup salvo em $DEST"
