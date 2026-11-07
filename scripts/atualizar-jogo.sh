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
# pedido manual (aplicar.sh passa "avisar"): com gente online, avisa no chat 60, 30 e 10 s antes e salva o mundo.
# A atualização automática (dz-update-jogo.py) já avisa sozinha e não passa "avisar".
PLUG="$INST/Rocket/Plugins/SobrevivenciaCore"
anuncio() { printf 'anuncio\t0\t0\t0\t%s\n' "$1" >> "$PLUG/admin_comandos.txt"; chown unturned:unturned "$PLUG/admin_comandos.txt" 2>/dev/null || true; }
online=$(python3 -c "import json,os,time,sys;f=sys.argv[1];d=json.load(open(f));j=d.get('jogadores',[]);print((len(j) if isinstance(j,list) else int(j or 0)) if time.time()-os.path.getmtime(f)<120 else 0)" "$PLUG/status.json" 2>/dev/null || echo 0)
if [ "${1:-}" = "avisar" ] && [ "${online:-0}" -gt 0 ] && systemctl is-active -q unturned; then
  echo "== $(date '+%F %T') avisando $online jogador(es)"
  anuncio "O SERVIDOR VAI REINICIAR EM 1 MINUTO pra atualizar. Guarde suas coisas. Volta em poucos segundos."; sleep 30
  anuncio "O SERVIDOR REINICIA EM 30 SEGUNDOS. Guarde suas coisas."; sleep 20
  anuncio "O SERVIDOR REINICIA EM 10 SEGUNDOS."
  rm -f "$PLUG/salvar_feito.txt"; printf 'salvar\t0\t0\t0\t\n' >> "$PLUG/admin_comandos.txt"
  for i in $(seq 1 10); do sleep 1; [ -f "$PLUG/salvar_feito.txt" ] && break; done
  rm -f "$PLUG/salvar_feito.txt"
fi
echo "== $(date '+%F %T') atualizando o jogo"
systemctl stop unturned || true
sudo -u unturned bash "$REPO/scripts/update.sh" || echo "SteamCMD falhou; religando com a versao que tem"
for d in "$REPO"/server-files/Rocket/Plugins/*.dll; do
  install -o unturned -g unturned -m 644 "$d" "$INST/Rocket/Plugins/"
done
systemctl start unturned
echo "== $(date '+%F %T') jogo religado"
