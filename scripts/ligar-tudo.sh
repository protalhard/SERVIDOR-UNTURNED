#!/usr/bin/env bash
# Liga tudo de uma vez na VPS (rodar como root):
# sincronizador com o GitHub, bot do Discord, site na porta 80 e o Claude sempre ligado.
set -e
R=/home/unturned/repo
sudo -u unturned git -C "$R" pull -q --ff-only origin main
install -m 755 "$R/scripts/unturned-sync.sh" /usr/local/bin/unturned-sync.sh
cp "$R/scripts/unturned-sync.service" "$R/scripts/unturned-sync.timer" /etc/systemd/system/
mkdir -p /var/lib/unturned-sync
[ -f /var/lib/unturned-sync/reiniciar ] || cat "$R/server-files/REINICIAR" > /var/lib/unturned-sync/reiniciar
systemctl daemon-reload
systemctl enable --now unturned-sync.timer
command -v ufw >/dev/null && ufw allow 80/tcp >/dev/null || true
bash "$R/scripts/aplicar.sh"
bash "$R/scripts/claude-sempre.sh"
echo
for s in unturned unturned-sync.timer dz-bot dz-site; do printf '%-22s %s\n' "$s" "$(systemctl is-active $s)"; done
crontab -l | grep -q claude-vigia && echo "vigia do Claude: ok"
echo "PRONTO. Site: http://$(curl -fsS4 ifconfig.me 2>/dev/null || hostname -I | cut -d" " -f1)"
