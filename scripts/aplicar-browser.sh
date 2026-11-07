#!/usr/bin/env bash
# Copia os textos de server-files/browser.txt (Icon, Thumbnail, Desc_*) pro bloco Browser do Config.txt do servidor.
# Só grava se mudou. O jogo lê o Config.txt quando liga, então vale a partir do próximo reinício.
REPO=/home/unturned/repo
CFG=/home/unturned/server/Servers/dayz/Config.txt
[ -f "$REPO/server-files/browser.txt" ] && [ -f "$CFG" ] || exit 0
python3 - "$REPO/server-files/browser.txt" "$CFG" <<'PY'
import re, sys
src, cfg = sys.argv[1], sys.argv[2]
quero = {}
for l in open(src, encoding="utf-8"):
    l = l.rstrip("\n")
    m = re.match(r"(Icon|Thumbnail|Desc_Server_List|Desc_Hint|Desc_Full):\s*(.*)$", l)
    if m:
        quero[m.group(1)] = m.group(2).strip()
txt = open(cfg, encoding="utf-8-sig").read()
linhas = txt.split("\n")
prof, dentro, ant, fim, achou = 0, False, "", -1, set()
for i, l in enumerate(linhas):
    t = l.strip()
    if not t or t.startswith("//"):
        continue
    if t in ("{", "["):
        if prof == 0 and ant == "Browser":
            dentro = True
        prof += 1
    elif t in ("}", "]"):
        prof -= 1
        if prof == 0 and dentro:
            fim = i
            break
    elif prof == 1 and dentro:
        k = t.split(" ", 1)[0]
        if k in quero:
            linhas[i] = "\t" + k + " " + quero[k]
            achou.add(k)
    ant = t
if fim < 0:
    print("bloco Browser nao achado no Config.txt"); sys.exit(0)
falta = [k for k in quero if k not in achou]
linhas[fim:fim] = ["\t" + k + " " + quero[k] for k in falta]
novo = "\n".join(linhas)
if novo != txt:
    open(cfg, "w", encoding="utf-8").write(novo)
    print("textos da lista de servidores atualizados no Config.txt")
PY
chown unturned:unturned "$CFG" 2>/dev/null || true
