#!/usr/bin/env bash
# Roda na VPS (como root) toda vez que o sincronizador acha um commit novo no GitHub.
# Aplica só o que é seguro: plugins compilados e pedido de reinício.
# Não mexe em Commands.dat, Config.txt, mundo nem token.
set -euo pipefail

REPO=/home/unturned/repo
INST=/home/unturned/server/Servers/dayz
ESTADO=/var/lib/unturned-sync
mkdir -p "$ESTADO"
reiniciar=0

# 1) Plugins (.dll) que estão em server-files/Rocket/Plugins
for dll in "$REPO"/server-files/Rocket/Plugins/*.dll; do
  [ -f "$dll" ] || continue
  destino="$INST/Rocket/Plugins/$(basename "$dll")"
  if ! cmp -s "$dll" "$destino"; then
    install -o unturned -g unturned -m 644 "$dll" "$destino"
    echo "plugin atualizado: $(basename "$dll")"
    reiniciar=1
  fi
done

# 2) Permissões dos jogadores: garante que o grupo default tem tudo de server-files/permissoes-jogador.txt
PERM="$INST/Rocket/Permissions.config.xml"
if [ -f "$PERM" ] && [ -f "$REPO/server-files/permissoes-jogador.txt" ]; then
  if python3 - "$PERM" "$REPO/server-files/permissoes-jogador.txt" <<'PY'
import re, sys
arq, lista = sys.argv[1], sys.argv[2]
s = open(arq, encoding="utf-8").read()
quer = [l.strip() for l in open(lista) if l.strip() and not l.startswith("#")]
m = re.search(r"<Id>default</Id>.*?<Permissions>(.*?)</Permissions>", s, re.S)
if not m:
    sys.exit(1)
tem = set(re.findall(r">([^<]+)</Permission>", m.group(1)))
falta = [q for q in quer if q not in tem]
if not falta:
    sys.exit(1)
novo = m.group(1).rstrip() + "\n" + "".join('        <Permission Cooldown="0">%s</Permission>\n' % f for f in falta) + "      "
s = s[:m.start(1)] + novo + s[m.end(1):]
open(arq, "w", encoding="utf-8").write(s)
print("permissoes adicionadas: " + ", ".join(falta))
PY
  then
    chown unturned:unturned "$PERM"
    reiniciar=1
  fi
fi

# 3) Pedido de reinício: mudar o número em server-files/REINICIAR força um restart
if [ -f "$REPO/server-files/REINICIAR" ]; then
  pedido=$(cat "$REPO/server-files/REINICIAR")
  feito=$(cat "$ESTADO/reiniciar" 2>/dev/null || true)
  if [ "$pedido" != "$feito" ]; then
    echo "$pedido" > "$ESTADO/reiniciar"
    reiniciar=1
  fi
fi

# 4) Bot do Discord (não reinicia o jogo)
if [ -f "$REPO/painel/bin/dz-bot" ]; then
  bot=0
  if ! cmp -s "$REPO/painel/bin/dz-bot" /usr/local/bin/dz-bot; then
    install -m 755 "$REPO/painel/bin/dz-bot" /usr/local/bin/dz-bot; bot=1
  fi
  if ! cmp -s "$REPO/scripts/dz-bot.service" /etc/systemd/system/dz-bot.service; then
    cp "$REPO/scripts/dz-bot.service" /etc/systemd/system/dz-bot.service
    systemctl daemon-reload; systemctl enable dz-bot; bot=1
  fi
  if [ "$bot" = 1 ]; then echo "bot do discord atualizado"; systemctl restart dz-bot; fi
fi

# 5) Site (porta 80, não reinicia o jogo)
if [ -f "$REPO/painel/bin/dz-site" ]; then
  site=0
  if ! cmp -s "$REPO/painel/bin/dz-site" /usr/local/bin/dz-site; then
    install -m 755 "$REPO/painel/bin/dz-site" /usr/local/bin/dz-site; site=1
  fi
  if ! cmp -s "$REPO/scripts/dz-site.service" /etc/systemd/system/dz-site.service; then
    cp "$REPO/scripts/dz-site.service" /etc/systemd/system/dz-site.service
    systemctl daemon-reload; systemctl enable dz-site; site=1
    command -v ufw >/dev/null && { ufw allow 80/tcp; ufw allow 443/tcp; } >/dev/null 2>&1 || true
  fi
  if [ "$site" = 1 ]; then echo "site atualizado"; systemctl restart dz-site; fi
  # músicas da DZ RÁDIO (só baixa as que faltam; roda em segundo plano)
  [ -x "$REPO/scripts/baixar-musicas.sh" ] && (REPO="$REPO" nohup flock -n /tmp/dz-musicas.lock "$REPO/scripts/baixar-musicas.sh" >/var/log/dz-musicas.log 2>&1 &)
fi

# 6) O próprio sincronizador também se atualiza
if ! cmp -s "$REPO/scripts/unturned-sync.sh" /usr/local/bin/unturned-sync.sh; then
  install -m 755 "$REPO/scripts/unturned-sync.sh" /usr/local/bin/unturned-sync.sh
  echo "sincronizador atualizado"
fi

# 6b) Backup diário do mundo às 5h de Brasília (não reinicia o jogo)
if ! cmp -s "$REPO/scripts/backup.sh" /usr/local/bin/dz-backup.sh; then
  install -m 755 "$REPO/scripts/backup.sh" /usr/local/bin/dz-backup.sh
  echo "backup atualizado"
fi
bk=0
for u in dz-backup.service dz-backup.timer; do
  if ! cmp -s "$REPO/scripts/$u" "/etc/systemd/system/$u"; then
    cp "$REPO/scripts/$u" "/etc/systemd/system/$u"; bk=1
  fi
done
if [ "$bk" = 1 ]; then systemctl daemon-reload; systemctl enable --now dz-backup.timer; echo "backup diario ligado"; fi

# 6c) Painel admin: pasta compartilhada (site, bot e executor) e o executor de wipe/reinício
install -d -o unturned -g unturned -m 750 /home/unturned/dz-painel
if ! cmp -s "$REPO/scripts/dz-painel-exec.py" /usr/local/bin/dz-painel-exec.py; then
  install -m 755 "$REPO/scripts/dz-painel-exec.py" /usr/local/bin/dz-painel-exec.py
  echo "executor do painel atualizado"
fi
pn=0
for u in dz-painel.service dz-painel.timer; do
  if ! cmp -s "$REPO/scripts/$u" "/etc/systemd/system/$u"; then
    cp "$REPO/scripts/$u" "/etc/systemd/system/$u"; pn=1
  fi
done
if [ "$pn" = 1 ]; then systemctl daemon-reload; systemctl enable --now dz-painel.timer; echo "executor do painel ligado"; fi

# 7) Reinício do jogo: NUNCA automático (pedido do dono). Os arquivos novos já ficam no lugar e entram
#    no reinício diário da madrugada (ReinicioDiario do plugin) ou quando o dono mandar reiniciar.
#    O arquivo "reiniciar-pendente" só avisa que tem novidade esperando; some quando o jogo reinicia.
PENDENTE="$ESTADO/reiniciar-pendente"
[ "$reiniciar" = 1 ] && [ ! -f "$PENDENTE" ] && touch "$PENDENTE"
if [ -f "$PENDENTE" ]; then
  inicio=$(systemctl show unturned -p ActiveEnterTimestamp --value 2>/dev/null)
  ini_s=$(date -d "$inicio" +%s 2>/dev/null || echo 0)
  if [ "$ini_s" -gt "$(stat -c %Y "$PENDENTE")" ]; then
    rm -f "$PENDENTE"
  elif [ "$reiniciar" = 1 ]; then
    echo "jogo atualizado: entra no proximo reinicio da madrugada"
  fi
fi
