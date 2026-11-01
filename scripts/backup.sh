#!/usr/bin/env bash
# Backup do mundo, jogadores, banco e configs do servidor em /home/unturned/backups.
# Pula a pasta Workshop (mods), que o servidor baixa de novo sozinho.
# Guarda só os 2 mais novos (só os dayz-*.tar.gz; outros arquivos da pasta ficam), pra não encher o disco.
set -euo pipefail
DEST="/home/unturned/backups"
mkdir -p "$DEST"
tar czf "$DEST/dayz-$(date +%F-%H%M).tar.gz" --exclude=dayz/Workshop -C /home/unturned/server/Servers dayz
ls -1t "$DEST"/dayz-*.tar.gz | tail -n +3 | xargs -r rm -f
echo "Backup salvo em $DEST"
