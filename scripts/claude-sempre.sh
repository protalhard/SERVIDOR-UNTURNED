#!/usr/bin/env bash
# Deixa o Claude (remote-control) sempre ligado na VPS.
# Roda dentro de um "screen" (não morre quando fecha o console) e um vigia
# no cron religa a cada 2 minutos se ele cair, inclusive depois de reiniciar a VPS.
set -e
CLAUDE=$(command -v claude || echo /root/.local/bin/claude)
DIR=/opt/unturned
mkdir -p "$DIR"
cat > /usr/local/bin/claude-vigia.sh <<SH
#!/usr/bin/env bash
export HOME=/root PATH=/root/.local/bin:/usr/local/bin:/usr/bin:/bin
pgrep -f 'claude remote-control' >/dev/null && exit 0
cd $DIR && /usr/bin/screen -dmS claude $CLAUDE remote-control
SH
chmod 755 /usr/local/bin/claude-vigia.sh
( crontab -l 2>/dev/null | grep -v claude-vigia; echo '*/2 * * * * /usr/local/bin/claude-vigia.sh'; echo '@reboot sleep 30 && /usr/local/bin/claude-vigia.sh' ) | crontab -
/usr/local/bin/claude-vigia.sh
sleep 3
crontab -l 2>/dev/null | grep -q claude-vigia || { echo "ERRO: o cron do vigia nao foi gravado (instale o pacote cron)."; exit 1; }
pgrep -f 'claude remote-control' >/dev/null && echo "OK: Claude ligado e vigiado." || echo "ERRO: nao ligou. Rode: cd $DIR && claude remote-control"
