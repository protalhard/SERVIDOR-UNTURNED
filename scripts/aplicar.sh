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
    command -v ufw >/dev/null && ufw allow 80/tcp >/dev/null 2>&1 || true
  fi
  if [ "$site" = 1 ]; then echo "site atualizado"; systemctl restart dz-site; fi
fi

if [ "$reiniciar" = 1 ]; then
  echo "reiniciando o servidor"
  systemctl restart unturned
fi
