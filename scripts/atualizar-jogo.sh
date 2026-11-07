#!/usr/bin/env bash
# Atualiza o Unturned pelo SteamCMD e religa o servidor. Roda como root, numa unidade própria do systemd
# (o aplicar.sh chama com systemd-run, pra não morrer junto com o sincronizador).
set -uo pipefail
REPO=/home/unturned/repo
INST=/home/unturned/server/Servers/dayz
exec >> /var/log/dz-atualizar-jogo.log 2>&1
# um de cada vez (pedido manual e atualização automática)
exec 9>/run/dz-atualizar-jogo.lock
flock -n 9 || { echo "== $(date '+%F %T') ja tem uma atualizacao rodando"; exit 0; }
bash "$REPO/scripts/aplicar-browser.sh" || true
echo "== $(date '+%F %T') atualizando o jogo"
systemctl stop unturned || true
sudo -u unturned bash "$REPO/scripts/update.sh" || echo "SteamCMD falhou; religando com a versao que tem"
for d in "$REPO"/server-files/Rocket/Plugins/*.dll; do
  install -o unturned -g unturned -m 644 "$d" "$INST/Rocket/Plugins/"
done
systemctl start unturned
echo "== $(date '+%F %T') jogo religado"
