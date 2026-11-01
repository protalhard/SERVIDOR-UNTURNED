"""Exporta os 9 modelos do gerar2.py pra montar_modelos.tsv (o /montar do plugin le esse arquivo).
Linhas:
  modelo  cerca|parede  x1 y1 x2 y2
  modelo  zona  raio
  modelo  <peca>  x y fx fy [extra]      (fx,fy = pra onde a peca fica virada)
x = direita de quem entra, y = pra dentro; a entrada fica em y negativo."""
import math, sys
src = open(sys.argv[1] if len(sys.argv) > 1 else "gerar_modelos.py").read()
ns = {}
exec(src[:src.index("# ======================= DESENHO")].replace('os.makedirs(OUT, exist_ok=True)', ''), ns)
modelos = ns["modelos"]

NPC = {"armas": 51240, "mercado": 51241, "roupas": 51242, "equip": 51244, "oficina": 51245, "vault": 51246,
       "armas_n": 51247, "cacador": 51248, "banco": 51249, "tatico": 51250, "explosivos": 51251, "hangar": 51252}
# o que vai exposto nos mostruarios (o plugin pega itens dessa loja/categoria)
EXPOE = {"armas": "armas", "armas_n": "negro:Snipers,Rifles fortes,Metralhadoras", "mercado": "comida",
         "equip": "equipamentos:Mochilas,Coletes", "tatico": "negro:Elite e torres", "explosivos": "negro:Explosivos"}

def pecas_banca(tema, cx, cy, fx, fy, w, dd):
    sx, sy = fy, -fx
    def L(u, v): return (cx + sx * u + fx * (dd / 2 - v), cy + sy * u + fy * (dd / 2 - v))
    out = []
    def p(kind, pt, dx, dy, extra=""): out.append((kind, pt[0], pt[1], dx, dy, extra))
    for a, b in [((-w / 2, 0), (-w / 2, dd)), ((-w / 2, dd), (w / 2, dd)), ((w / 2, dd), (w / 2, 0))]:
        out.append(("parede",) + L(*a) + L(*b))
    def fundo(n, v=None):
        v = dd - .6 if v is None else v
        return [L(-w / 2 + 1 + (w - 2) * (i + .5) / n, v) for i in range(n)]
    def lados(n, recuo=.6):
        r = []
        for i in range(n):
            v = 1.2 + (dd - 2.4) * (i + .5) / n
            r.append((L(-w / 2 + recuo, v), (sx, sy)))      # parede da esquerda, virada pra dentro
            r.append((L(w / 2 - recuo, v), (-sx, -sy)))
        return r
    ex = EXPOE.get(tema, "")
    if tema in ("armas", "armas_n"):
        for q in fundo(5): p("rack", q, fx, fy, ex)
        for q, d in lados(2): p("rack", q, *d, extra=ex)
    elif tema == "mercado":
        for q in fundo(4): p("prateleira", q, fx, fy, "comida")
        for q, d in lados(2): p("prateleira", q, *d, extra="remedios")
    elif tema in ("roupas", "tatico"):
        for q in fundo(4, dd - 1.1): p("manequim", q, fx, fy, "roupas" if tema == "roupas" else "negro:Elite e torres")
        for q, d in lados(2): p("prateleira", q, *d, extra="roupas" if tema == "roupas" else ex)
    elif tema == "equip":
        for q in fundo(4): p("prateleira", q, fx, fy, ex)
        for q, d in lados(2): p("mesa", q, *d)
    elif tema == "vault":
        for q in fundo(4): p("cofre", q, fx, fy)
    elif tema == "banco":
        for q in fundo(3): p("cofre", q, fx, fy)
    elif tema == "explosivos":
        for q in fundo(5): p("barril", q, fx, fy)
        for q, d in lados(2): p("caixa", q, *d)
    elif tema == "oficina":
        for q in fundo(4): p("caixa", q, fx, fy)
        for q, d in lados(2): p("mesa", q, *d)
    elif tema == "hangar":
        for q, d in lados(2): p("barril", q, *d)
    elif tema == "cacador":
        for q in fundo(3): p("mesa", q, fx, fy)
        for q, d in lados(1): p("mesa", q, *d)
    if tema not in ("hangar", "oficina"):
        for u in (-1.2, 0, 1.2): p("balcao", L(u, dd * .38), fx, fy)
        p("npc", L(0, dd * .58), fx, fy, NPC[tema])
    else:
        p("npc", L(-w / 2 + 1.6, 1.4), fx, fy, NPC[tema])
    p("luz", L(w / 2 - .8, .5), fx, fy)
    return out

MAPA = {"luz": "luz", "caixa": "caixa_grande", "torre": "torre", "fogueira": "fogueira", "barraca": "barraca",
        "varal": "mesa", "banco_praca": "banco_praca", "bandeira": "bandeira", "heliponto": "heliponto",
        "veiculo": "veiculo", "mesa": "mesa"}

with open(sys.argv[2] if len(sys.argv) > 2 else "montar_modelos.tsv", "w") as fo:
    fo.write("# gerado por exportar.py - " + __doc__.splitlines()[0] + "\n")
    for m in modelos:
        c = m.cod
        if m.zona: fo.write(f"{c}\tzona\t{m.zona}\n")
        for a, b in m.cercas: fo.write(f"{c}\tcerca\t{a[0]:.2f}\t{a[1]:.2f}\t{b[0]:.2f}\t{b[1]:.2f}\n")
        for (tema, nome, x, y, fx, fy, w, dd) in m.bancas:
            for pc in pecas_banca(tema, x, y, fx, fy, w, dd):
                if pc[0] == "parede": fo.write(f"{c}\tparede\t{pc[1]:.2f}\t{pc[2]:.2f}\t{pc[3]:.2f}\t{pc[4]:.2f}\n")
                else: fo.write(f"{c}\t{pc[0]}\t{pc[1]:.2f}\t{pc[2]:.2f}\t{pc[3]:.3f}\t{pc[4]:.3f}\t{pc[5]}\n")
        for t, x, y, nome in m.itens:
            # virado pro centro do modelo
            dx, dy = -x, -y
            l = math.hypot(dx, dy) or 1
            fo.write(f"{c}\t{MAPA[t]}\t{x:.2f}\t{y:.2f}\t{dx / l:.3f}\t{dy / l:.3f}\t\n")
print("ok")
