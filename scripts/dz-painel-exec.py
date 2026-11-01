#!/usr/bin/env python3
"""Executor do painel admin (roda como root a cada minuto pelo dz-painel.timer).

Lê /home/unturned/dz-painel/acao.json (gravado pelo site, só o dono pede):
  - avisa no chat do jogo antes (60, 30, 15, 10, 5, 2 e 1 min);
  - na hora: reinicia o servidor, ou faz o wipe:
    backup -> desliga o jogo -> apaga só o que foi marcado -> liga de novo.
Anota tudo em historico.jsonl e avisa no Discord (canal #wipe) pelo bot.
"""
import fcntl
import glob
import json
import os
import pwd
import shutil
import subprocess
import sys
import tarfile
import time
from datetime import datetime, timedelta, timezone

PAINEL = "/home/unturned/dz-painel"
ACAO = os.path.join(PAINEL, "acao.json")
SERVIDOR = "/home/unturned/server/Servers/dayz"
PLUGIN = os.path.join(SERVIDOR, "Rocket/Plugins/SobrevivenciaCore")
BACKUPS = "/home/unturned/backups"
ESTADO_BOT = "/home/unturned/dz-bot/estado.json"
BR = timezone(timedelta(hours=-3))
AVISOS = {"wipe": [60, 30, 15, 10, 5, 2, 1], "reiniciar": [10, 5, 1]}
NOMES = {
    "bases": "Bases e construcoes", "veiculos": "Veiculos", "inventario": "Inventario e roupas",
    "skills": "Skills", "personagem": "Personagem inteiro", "banco": "Banco",
    "ranking": "Ranking", "cofres": "Cofres", "kits": "Espera dos kits", "safes": "Construcoes das safes",
}

try:
    U = pwd.getpwnam("unturned")
except KeyError:
    U = None


def dono_unturned(caminho):
    if U:
        try:
            os.chown(caminho, U.pw_uid, U.pw_gid)
        except OSError:
            pass


def gravar_json(arq, dados):
    os.makedirs(os.path.dirname(arq), exist_ok=True)
    tmp = arq + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(dados, f, ensure_ascii=False, indent=2)
    os.chmod(tmp, 0o640)
    dono_unturned(tmp)
    os.replace(tmp, arq)


def anunciar(texto, por="0"):
    """Anúncio no chat do jogo (o plugin lê admin_comandos.txt)."""
    arq = os.path.join(PLUGIN, "admin_comandos.txt")
    texto = texto.replace("\t", " ").replace("\n", " ")
    try:
        with open(arq, "a", encoding="utf-8") as f:
            f.write(f"anuncio\t0\t{por}\t0\t{texto}\n")
        dono_unturned(arq)
    except OSError as e:
        print("anuncio:", e)


def avisar_discord(chave_canal, titulo, texto, cor="#FF5A00"):
    try:
        with open(ESTADO_BOT, encoding="utf-8") as f:
            canal = json.load(f).get("canais", {}).get(chave_canal, "")
    except (OSError, ValueError):
        canal = ""
    if not canal:
        return
    pid = datetime.now().strftime("%Y%m%d-%H%M%S") + "-exec"
    gravar_json(os.path.join(PAINEL, "fila", pid + ".json"), {
        "id": pid, "tipo": "aviso", "canal": canal, "titulo": titulo, "texto": texto,
        "cor": cor, "autor": "executor", "criado": int(time.time())})


def historico(registro):
    arq = os.path.join(PAINEL, "historico.jsonl")
    with open(arq, "a", encoding="utf-8") as f:
        f.write(json.dumps(registro, ensure_ascii=False) + "\n")
    dono_unturned(arq)


def systemctl(*args):
    return subprocess.run(["systemctl", *args], capture_output=True, text=True)


def parar_jogo():
    systemctl("stop", "unturned")
    for _ in range(90):
        if systemctl("is-active", "unturned").stdout.strip() != "active":
            return True
        time.sleep(1)
    return False


def apagar(caminho, lista):
    if os.path.isdir(caminho) and not os.path.islink(caminho):
        shutil.rmtree(caminho)
        lista.append(caminho)
    elif os.path.lexists(caminho):
        os.remove(caminho)
        lista.append(caminho)


def com_til(arquivos):
    for a in arquivos:
        yield a
        yield a + "~"


def fazer_wipe(itens):
    niveis = [d for d in glob.glob(os.path.join(SERVIDOR, "Level", "*")) if os.path.isdir(d)]
    nomes_niveis = [os.path.basename(d) for d in niveis]
    jogadores = glob.glob(os.path.join(SERVIDOR, "Players", "*"))
    apagados = []
    for nivel in niveis:
        if itens.get("safes"):  # apaga TUDO que foi construído, inclusive as safes
            for a in com_til([os.path.join(nivel, "Barricades.dat"), os.path.join(nivel, "Structures.dat")]):
                apagar(a, apagados)
        if itens.get("veiculos"):
            for a in com_til([os.path.join(nivel, "Vehicles.dat")]):
                apagar(a, apagados)
    for jog in jogadores:
        for n in nomes_niveis:
            pasta = os.path.join(jog, n)
            if itens.get("personagem"):
                apagar(pasta, apagados)
                continue
            p = os.path.join(pasta, "Player")
            if itens.get("inventario"):
                for a in com_til([os.path.join(p, "Inventory.dat"), os.path.join(p, "Clothing.dat")]):
                    apagar(a, apagados)
            if itens.get("skills"):
                for a in com_til([os.path.join(p, "Skills.dat")]):
                    apagar(a, apagados)
    if itens.get("safes"):
        apagar(os.path.join(PLUGIN, "montagens.tsv"), apagados)
    if itens.get("banco"):
        apagar(os.path.join(PLUGIN, "banco.txt"), apagados)
    if itens.get("ranking"):
        apagar(os.path.join(PLUGIN, "estatisticas.tsv"), apagados)
    if itens.get("cofres"):
        for a in glob.glob(os.path.join(PLUGIN, "vaults", "*")):
            apagar(a, apagados)
    if itens.get("kits"):
        for a in ("kits_uso.tsv", "kit_recebido.txt"):
            apagar(os.path.join(PLUGIN, a), apagados)
    return apagados


def jogo_ligado():
    return systemctl("is-active", "unturned").stdout.strip() == "active"


def pedir_plugin(acao, por, espera):
    """Manda um comando pro plugin (admin_comandos.txt) e espera a resposta <acao>_feito.txt."""
    feito = os.path.join(PLUGIN, acao + "_feito.txt")
    try:
        os.remove(feito)
    except OSError:
        pass
    arq = os.path.join(PLUGIN, "admin_comandos.txt")
    with open(arq, "a", encoding="utf-8") as f:
        f.write(f"{acao}\t0\t{por}\t0\t\n")
    dono_unturned(arq)
    for _ in range(espera):
        time.sleep(1)
        if os.path.exists(feito):
            try:
                with open(feito, encoding="utf-8") as f:
                    return f.read().strip() or "ok"
            finally:
                try:
                    os.remove(feito)
                except OSError:
                    pass
    return None


def backup(nome):
    os.makedirs(BACKUPS, exist_ok=True)
    arq = os.path.join(BACKUPS, nome)
    with tarfile.open(arq, "w:gz") as t:
        t.add(SERVIDOR, arcname="dayz", filter=lambda ti: None if "/Workshop" in ti.name or ti.name.endswith("/Workshop") else ti)
    dono_unturned(arq)
    # guarda só os 2 backups de wipe mais novos (não enche o disco)
    for velho in sorted(glob.glob(os.path.join(BACKUPS, "wipe-*.tar.gz")), key=os.path.getmtime, reverse=True)[2:]:
        try:
            os.remove(velho)
        except OSError:
            pass
    return arq


def main():
    os.makedirs(PAINEL, exist_ok=True)
    trava = open("/run/dz-painel-exec.lock", "w")
    try:
        fcntl.flock(trava, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        return
    try:
        with open(ACAO, encoding="utf-8") as f:
            ac = json.load(f)
    except (OSError, ValueError):
        return
    tipo = ac.get("tipo")
    if tipo not in AVISOS:
        return
    agora = int(time.time())
    falta = int(ac.get("quando", 0)) - agora
    por = str(ac.get("por", "0"))

    if falta > 0:
        avisados = set(ac.get("avisados") or [])
        devidos = [m for m in AVISOS[tipo] if falta <= m * 60 + 30]
        if devidos:
            m = min(devidos)
            if m not in avisados:
                minutos = max(1, round(falta / 60))
                if tipo == "wipe":
                    anunciar(f"WIPE em {minutos} minuto(s)! Guarde o que puder.", por)
                else:
                    anunciar(f"Servidor reinicia em {minutos} minuto(s).", por)
                avisados.update(x for x in AVISOS[tipo] if x >= m)
                ac["avisados"] = sorted(avisados)
                gravar_json(ACAO, ac)
        return

    # chegou a hora: tira o pedido antes (não roda duas vezes)
    os.remove(ACAO)
    reg = {"tipo": tipo, "agendado": ac.get("quando"), "inicio": agora, "por": ac.get("porNome") or por}
    if tipo == "reiniciar":
        anunciar("Servidor reiniciando agora.", por)
        time.sleep(5)
        r = systemctl("restart", "unturned")
        reg.update(ok=r.returncode == 0, erro=r.stderr.strip()[:300])
        historico(reg)
        return

    itens = {k: True for k, v in (ac.get("itens") or {}).items() if v}
    reg["itens"] = sorted(itens)
    anunciar("WIPE AGORA! O servidor vai desligar.", por)
    time.sleep(8)
    ligado = jogo_ligado()
    # 1) salva o mundo e faz o backup antes de mexer em qualquer coisa
    if ligado:
        reg["salvo"] = pedir_plugin("salvar", por, 60) or "sem resposta do plugin"
    try:
        nome = "wipe-" + datetime.now(BR).strftime("%Y-%m-%d-%H%M") + ".tar.gz"
        reg["backup"] = backup(nome)
    except Exception as e:  # sem backup não apaga nada
        reg.update(ok=False, erro=f"backup falhou: {e}; nada foi apagado")
        historico(reg)
        avisar_discord("logs", "Wipe NAO feito", reg["erro"], "#FF4D4D")
        return
    # 2) bases dos jogadores: o plugin apaga peça por peça e deixa as safes (dono, staff e /montar)
    if itens.get("bases") and not itens.get("safes"):
        if ligado:
            reg["bases"] = pedir_plugin("wipe_bases", por, 180) or "sem resposta: o plugin faz quando o servidor ligar"
        else:
            pedir_plugin("wipe_bases", por, 0)
            reg["bases"] = "servidor desligado: o plugin faz quando ligar"
    if not parar_jogo():
        reg.update(ok=False, erro="o jogo nao desligou; o resto do wipe foi cancelado")
        systemctl("start", "unturned")
        historico(reg)
        return
    try:
        apagados = fazer_wipe(itens)
        reg.update(ok=True, apagados=len(apagados))
    except Exception as e:
        reg.update(ok=False, erro=f"erro ao apagar: {e}")
    r = systemctl("start", "unturned")
    if r.returncode != 0:
        reg["erroLigar"] = r.stderr.strip()[:300]
    reg["fim"] = int(time.time())
    historico(reg)
    if reg.get("ok"):
        lista = "\n".join("• " + NOMES.get(k, k) for k in sorted(itens))
        avisar_discord("wipe", "✅ WIPE FEITO", "O servidor já está voltando. Bom jogo!\n\n**Foi zerado:**\n" + lista, "#5BE37D")
    else:
        avisar_discord("logs", "Wipe com erro", reg.get("erro", ""), "#FF4D4D")


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print("dz-painel-exec:", e, file=sys.stderr)
        sys.exit(1)
