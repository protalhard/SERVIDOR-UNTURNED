#!/usr/bin/env python3
"""Atualização automática do Unturned (pedido do dono). Roda como root a cada 10 min (dz-update-jogo.timer).

Compara o build instalado (steamapps/appmanifest_1110390.acf) com o build público da Steam.
Saiu versão nova: avisa no chat do jogo (5, 3, 1 min, 30 e 10 s; sem ninguém online, vai direto),
avisa no Discord, salva o mundo e roda scripts/atualizar-jogo.sh (para o jogo, SteamCMD, liga de novo).
Se a Steam falhar, tenta de novo no máximo uma vez por hora pro mesmo build.
"""
import fcntl
import json
import os
import pwd
import re
import subprocess
import sys
import time
import urllib.request
from datetime import datetime

APP = "1110390"
REPO = "/home/unturned/repo"
JOGO = "/home/unturned/server"
MANIFESTO = os.path.join(JOGO, "steamapps", f"appmanifest_{APP}.acf")
STEAMCMD = "/home/unturned/steamcmd/steamcmd.sh"
PLUGIN = os.path.join(JOGO, "Servers/dayz/Rocket/Plugins/SobrevivenciaCore")
PAINEL = "/home/unturned/dz-painel"
ESTADO_BOT = "/home/unturned/dz-bot/estado.json"
ESTADO = "/var/lib/unturned-sync/update-jogo.json"
# segundos antes do reinício em que avisa no chat
AVISOS = [300, 180, 60, 30, 10]

try:
    U = pwd.getpwnam("unturned")
except KeyError:
    U = None


def log(*a):
    print(datetime.now().strftime("%F %T"), *a, flush=True)


def dono_unturned(caminho):
    if U:
        try:
            os.chown(caminho, U.pw_uid, U.pw_gid)
        except OSError:
            pass


def build_instalado():
    try:
        m = re.search(r'"buildid"\s+"(\d+)"', open(MANIFESTO, encoding="utf-8", errors="ignore").read())
        return m.group(1) if m else None
    except OSError:
        return None


def build_steam():
    # 1) API pública do steamcmd.net (rápida)
    try:
        req = urllib.request.Request(f"https://api.steamcmd.net/v1/info/{APP}", headers={"User-Agent": "dz-update-jogo"})
        d = json.load(urllib.request.urlopen(req, timeout=30))
        b = d["data"][APP]["depots"]["branches"]["public"]["buildid"]
        if str(b).isdigit():
            return str(b)
    except Exception as e:
        log("steamcmd.net falhou:", e)
    # 2) o próprio SteamCMD
    try:
        cmd = [STEAMCMD, "+login", "anonymous", "+app_info_update", "1", "+app_info_print", APP, "+quit"]
        if U and os.geteuid() == 0:
            cmd = ["sudo", "-u", "unturned"] + cmd
        out = subprocess.run(cmd, capture_output=True, text=True, timeout=300).stdout
        i = out.find('"branches"')
        m = re.search(r'"public"\s*\{[^}]*?"buildid"\s+"(\d+)"', out[i:] if i >= 0 else out, re.S)
        if m:
            return m.group(1)
    except Exception as e:
        log("steamcmd falhou:", e)
    return None


def jogo_ligado():
    return subprocess.run(["systemctl", "is-active", "unturned"], capture_output=True, text=True).stdout.strip() == "active"


def jogadores_online():
    try:
        st = json.load(open(os.path.join(PLUGIN, "status.json"), encoding="utf-8"))
        if time.time() - os.path.getmtime(os.path.join(PLUGIN, "status.json")) > 120:
            return 0
        j = st.get("jogadores", [])
        return len(j) if isinstance(j, list) else int(j or 0)
    except (OSError, ValueError, TypeError):
        return 0


def anunciar(texto):
    arq = os.path.join(PLUGIN, "admin_comandos.txt")
    try:
        with open(arq, "a", encoding="utf-8") as f:
            f.write(f"anuncio\t0\t0\t0\t{texto}\n")
        dono_unturned(arq)
    except OSError as e:
        log("anuncio:", e)


def salvar_mundo():
    feito = os.path.join(PLUGIN, "salvar_feito.txt")
    try:
        os.remove(feito)
    except OSError:
        pass
    arq = os.path.join(PLUGIN, "admin_comandos.txt")
    with open(arq, "a", encoding="utf-8") as f:
        f.write("salvar\t0\t0\t0\t\n")
    dono_unturned(arq)
    for _ in range(60):
        time.sleep(1)
        if os.path.exists(feito):
            try:
                os.remove(feito)
            except OSError:
                pass
            return True
    return False


def avisar_discord(titulo, texto, cor="#FF5A00"):
    try:
        canais = json.load(open(ESTADO_BOT, encoding="utf-8")).get("canais", {})
    except (OSError, ValueError):
        return
    canal = canais.get("anuncios") or canais.get("status") or ""
    if not canal:
        return
    pid = datetime.now().strftime("%Y%m%d-%H%M%S") + "-update"
    arq = os.path.join(PAINEL, "fila", pid + ".json")
    os.makedirs(os.path.dirname(arq), exist_ok=True)
    with open(arq + ".tmp", "w", encoding="utf-8") as f:
        json.dump({"id": pid, "tipo": "aviso", "canal": canal, "titulo": titulo, "texto": texto,
                   "cor": cor, "autor": "executor", "criado": int(time.time())}, f, ensure_ascii=False)
    dono_unturned(arq + ".tmp")
    os.replace(arq + ".tmp", arq)


def ler_estado():
    try:
        return json.load(open(ESTADO, encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def gravar_estado(e):
    os.makedirs(os.path.dirname(ESTADO), exist_ok=True)
    with open(ESTADO + ".tmp", "w", encoding="utf-8") as f:
        json.dump(e, f)
    os.replace(ESTADO + ".tmp", ESTADO)


def main():
    trava = open("/run/dz-update-jogo.lock", "w")
    try:
        fcntl.flock(trava, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        return
    instalado, steam = build_instalado(), build_steam()
    if not instalado or not steam:
        log(f"nao deu pra comparar (instalado={instalado}, steam={steam})")
        return
    if instalado == steam:
        return
    est = ler_estado()
    if est.get("build") == steam and time.time() - est.get("tentou", 0) < 3600:
        return  # já tentou esse build faz menos de 1 hora
    gravar_estado({"build": steam, "tentou": int(time.time()), "de": instalado})
    log(f"versao nova do Unturned: build {instalado} -> {steam}")
    avisar_discord("🔄 ATUALIZAÇÃO DO UNTURNED",
                   "Saiu versão nova do Unturned. O servidor vai reiniciar pra atualizar e volta em poucos minutos.\n\n"
                   "**Atualize seu jogo na Steam** antes de entrar, senão o servidor não aparece na lista.")
    if jogo_ligado() and jogadores_online() > 0:
        fim = time.time() + AVISOS[0]
        for s in AVISOS:
            espera = fim - s - time.time()
            if espera > 0:
                time.sleep(espera)
            quando = f"{s // 60} MINUTO(S)" if s >= 60 else f"{s} SEGUNDOS"
            anunciar(f"ATUALIZACAO DO UNTURNED! O servidor reinicia em {quando} pra atualizar. "
                     "Guarde suas coisas e atualize o jogo na Steam. Volta em poucos minutos.")
        time.sleep(max(0, fim - time.time()))
    if jogo_ligado():
        log("mundo salvo" if salvar_mundo() else "plugin nao respondeu o salvar")
    r = subprocess.run(["/bin/bash", os.path.join(REPO, "scripts", "atualizar-jogo.sh")])
    novo = build_instalado()
    log(f"atualizar-jogo.sh saiu com {r.returncode}; build instalado agora {novo}")
    if novo == steam:
        avisar_discord("✅ SERVIDOR ATUALIZADO", "O servidor já está na versão nova do Unturned e voltando. Bom jogo!", "#5BE37D")


if __name__ == "__main__":
    sys.exit(main())
