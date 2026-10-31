#!/usr/bin/env bash
# Instalador do servidor Unturned estilo DayZ (Ubuntu 22.04/24.04).
# Uso (como root):
#   curl -fsSL https://raw.githubusercontent.com/protalhard/SERVIDOR-UNTURNED/main/install.sh | bash
# Variáveis opcionais: OWNER_STEAMID, GSLT, SERVER_NAME, MAP, MAX_PLAYERS
set -euo pipefail

REPO_URL="https://github.com/protalhard/SERVIDOR-UNTURNED.git"
ROCKET_URL="https://github.com/RocketModFix/RocketModFix/releases/download/4.23.1/Rocket.Unturned.Module.zip"
AIRDROP_URL="https://github.com/RestoreMonarchyPlugins/AirdropManager3/releases/download/v3.1.3/AirdropManager3-v3.1.3.zip"

USUARIO="unturned"
HOME_DIR="/home/$USUARIO"
JOGO_DIR="$HOME_DIR/server"
STEAMCMD_DIR="$HOME_DIR/steamcmd"
REPO_DIR="$HOME_DIR/repo"
SERVER_ID="dayz"
INST_DIR="$JOGO_DIR/Servers/$SERVER_ID"

OWNER_STEAMID="${OWNER_STEAMID:-76561198168015168}"
SERVER_NAME="${SERVER_NAME:-DayZ Brasil | ATM | Lojas | Safe Zone}"
MAP="${MAP:-Russia}"
MAX_PLAYERS="${MAX_PLAYERS:-24}"

verde() { echo -e "\e[32m==> $*\e[0m"; }
amarelo() { echo -e "\e[33m!! $*\e[0m"; }

[ "$(id -u)" -eq 0 ] || { echo "Rode como root (sudo -i)."; exit 1; }

if [ -z "${GSLT:-}" ]; then
  echo
  echo "Cole o token GSLT (steamcommunity.com/dev/managegameservers, App ID 304930)."
  echo "Deixe vazio pra pular (o servidor so aparece na lista LAN/por IP sem ele)."
  read -r -p "GSLT: " GSLT </dev/tty || GSLT=""
fi

verde "Instalando pacotes"
export DEBIAN_FRONTEND=noninteractive
dpkg --add-architecture i386
echo "(se o Ubuntu estiver se atualizando sozinho, espera ele terminar...)"
APT="apt-get -o DPkg::Lock::Timeout=900"
$APT update -y
$APT install -y curl wget unzip tar git ufw ca-certificates screen \
  lib32gcc-s1 lib32stdc++6 libc6-i386 libstdc++6 libgdiplus

verde "Configurando memoria swap (a VPS tem pouca RAM)"
if ! swapon --show | grep -q /swapfile; then
  fallocate -l 4G /swapfile || dd if=/dev/zero of=/swapfile bs=1M count=4096
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  grep -q '/swapfile' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
  sysctl -w vm.swappiness=20 >/dev/null
  echo 'vm.swappiness=20' > /etc/sysctl.d/99-unturned.conf
fi

verde "Criando usuario $USUARIO"
id "$USUARIO" >/dev/null 2>&1 || useradd -m -s /bin/bash "$USUARIO"

verde "Baixando o repositorio"
if [ -d "$REPO_DIR/.git" ]; then
  sudo -u "$USUARIO" git -C "$REPO_DIR" pull --ff-only
else
  sudo -u "$USUARIO" git clone "$REPO_URL" "$REPO_DIR"
fi

verde "Instalando SteamCMD"
sudo -u "$USUARIO" mkdir -p "$STEAMCMD_DIR"
if [ ! -x "$STEAMCMD_DIR/steamcmd.sh" ]; then
  sudo -u "$USUARIO" bash -c "cd '$STEAMCMD_DIR' && curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar xz"
fi

verde "Baixando/atualizando o Unturned Dedicated Server (pode demorar)"
sudo -u "$USUARIO" bash "$REPO_DIR/scripts/update.sh"

verde "Configurando o servidor ($SERVER_ID)"
sudo -u "$USUARIO" mkdir -p "$INST_DIR/Server" "$INST_DIR/Rocket/Plugins" "$INST_DIR/Rocket/Libraries"

NAME_ESC=$(printf '%s' "$SERVER_NAME" | sed 's/[\\&|]/\\&/g')
sed -e "s|__NAME__|$NAME_ESC|" -e "s|__MAP__|$MAP|" -e "s|__MAXPLAYERS__|$MAX_PLAYERS|" \
    -e "s|__OWNER__|$OWNER_STEAMID|" -e "s|__GSLT__|${GSLT:-}|" \
    "$REPO_DIR/server-files/Commands.dat.template" > "$INST_DIR/Server/Commands.dat"
[ -n "${GSLT:-}" ] || sed -i '/^GSLT/d' "$INST_DIR/Server/Commands.dat"
chmod 600 "$INST_DIR/Server/Commands.dat"

[ -f "$INST_DIR/WorkshopDownloadConfig.json" ] || cp "$REPO_DIR/server-files/WorkshopDownloadConfig.json" "$INST_DIR/"

# Permissões do Rocket (não sobrescreve se já existir); dono vai pro grupo admin
if [ ! -f "$INST_DIR/Rocket/Permissions.config.xml" ]; then
  cp "$REPO_DIR/server-files/Rocket/Permissions.config.xml" "$INST_DIR/Rocket/Permissions.config.xml"
  python3 - "$INST_DIR/Rocket/Permissions.config.xml" "$OWNER_STEAMID" <<'PY' || true
import sys,re
p,sid=sys.argv[1],sys.argv[2]
s=open(p).read()
# coloca o dono como membro do grupo admin
s=re.sub(r'(<Id>admin</Id>.*?)<Members />', r'\1<Members><Member>%s</Member></Members>'%sid, s, count=1, flags=re.S)
open(p,'w').write(s)
PY
fi

verde "Instalando plugins"
cp "$REPO_DIR/server-files/Rocket/Plugins/"*.dll "$INST_DIR/Rocket/Plugins/"
TMP=$(mktemp -d)
curl -fsSL -o "$TMP/airdrop.zip" "$AIRDROP_URL"
unzip -o -q "$TMP/airdrop.zip" -d "$INST_DIR/Rocket/"
rm -rf "$TMP"

chown -R "$USUARIO:$USUARIO" "$HOME_DIR"

# Impede que as atualizacoes automaticas reiniciem o servidor no meio do jogo
mkdir -p /etc/needrestart/conf.d
echo '$nrconf{override_rc}{qr(^unturned\.service$)} = 0;' > /etc/needrestart/conf.d/unturned.conf

verde "Criando o servico (liga sozinho quando a VPS reinicia)"
cp "$REPO_DIR/scripts/unturned.service" /etc/systemd/system/unturned.service
systemctl daemon-reload
systemctl enable unturned

verde "Liberando portas no firewall"
ufw allow OpenSSH >/dev/null
ufw allow 27015:27017/udp >/dev/null
ufw allow 27015:27017/tcp >/dev/null
ufw --force enable >/dev/null

verde "Ligando o servidor"
systemctl restart unturned

echo
verde "PRONTO! Servidor instalado."
echo "  IP pra conectar: $(curl -fsS -4 https://api.ipify.org 2>/dev/null || hostname -I | awk '{print $1}'):27015"
echo "  Ver o log:       journalctl -u unturned -f"
echo "  Reiniciar:       systemctl restart unturned"
echo "  Atualizar:       bash $REPO_DIR/scripts/update-all.sh"
[ -n "${GSLT:-}" ] || amarelo "Sem GSLT: o servidor nao aparece na lista da internet. Rode o instalador de novo com o token."
