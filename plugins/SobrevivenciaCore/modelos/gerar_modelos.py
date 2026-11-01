"""Modelos de safe / mercado negro / cacador com cantinhos tematicos por NPC.
Metros. x = direita de quem entra, y = pra dentro (entrada em y negativo)."""
import math, random, os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else "/tmp/modelos"
os.makedirs(OUT, exist_ok=True)
LAR = (255, 106, 0)
ESC_NPC = 1.0; PRETO = (16, 16, 18); BRANCO = (240, 240, 240)
def f(sz, b=True): return ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans%s.ttf" % ("-Bold" if b else ""), sz)

def norm(x, y):
    l = math.hypot(x, y) or 1; return x / l, y / l

class M:
    def __init__(s, cod, titulo, desc, alcance, centro_y=0, cx=0):
        s.cod, s.titulo, s.desc, s.alcance, s.cy, s.cx = cod, titulo, desc, alcance, centro_y, cx
        s.cercas, s.itens, s.caminhos, s.areas, s.bancas, s.pistas = [], [], [], [], [], []
        s.zona = 0; s.entrada = (0, 0); s.mostrar_entrada = True
    def seg(s, a, b, ent=None):
        if ent: s.cercas += corta(a, b, *ent)
        else: s.cercas.append((a, b))
    def circulo(s, r, larg=7):
        n = max(16, int(2 * math.pi * r / 4))
        pts = [(r * math.sin(2 * math.pi * (i + .5) / n), r * math.cos(2 * math.pi * (i + .5) / n)) for i in range(n)]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            if my < 0 and abs(mx) < larg / 2 + 1.2: continue
            s.cercas.append((a, b))
        s.entrada = (0, -r)
    def ret(s, w, h, larg=7, y0=None, ex=0):
        y0 = -h / 2 if y0 is None else y0
        x1, x2, y1, y2 = -w / 2, w / 2, y0, y0 + h
        s.seg((x1, y1), (x1, y2)); s.seg((x1, y2), (x2, y2)); s.seg((x2, y2), (x2, y1))
        s.seg((x2, y1), (x1, y1), (ex, y1, larg)); s.entrada = (ex, y1)
    def add(s, t, x, y, nome=""): s.itens.append((t, x, y, nome))
    def banca(s, tema, nome, x, y, fx, fy, w=12, d=9):
        """cantinho: centro x,y, frente aberta virada pra (fx,fy)"""
        fx, fy = norm(fx, fy); s.bancas.append((tema, nome, x, y, fx, fy, w, d))

def corta(a, b, ex, ey, lg):
    ax, ay = a; bx, by = b; L = math.hypot(bx - ax, by - ay); ux, uy = (bx - ax) / L, (by - ay) / L
    t = (ex - ax) * ux + (ey - ay) * uy; out = []
    if t - lg / 2 > .3: out.append((a, (ax + ux * (t - lg / 2), ay + uy * (t - lg / 2))))
    if t + lg / 2 < L - .3: out.append(((ax + ux * (t + lg / 2), ay + uy * (t + lg / 2)), b))
    return out

def arco(m, temas, r, a0, a1, w=12, d=9):
    n = len(temas)
    for i, (tema, nome) in enumerate(temas):
        a = math.radians(a0 + (a1 - a0) * (i / (n - 1) if n > 1 else .5))
        x, y = r * math.sin(a), r * math.cos(a)
        m.banca(tema, nome, x, y, -x, -y, w, d)

modelos = []
SAFE8 = [("armas", "ARMAS"), ("roupas", "ROUPAS"), ("equip", "EQUIPAMENTOS"), ("construtor", "CONSTRUTOR"),
         ("comida", "COMIDA"), ("remedios", "REMEDIOS"), ("vault", "VAULT"), ("banco", "BANCO / ATM")]

def pista_direita(m, x, y0=-60, comp=120, nome="PISTA DE AVIAO"):
    m.pistas.append((x, y0 + comp / 2, 0, 1, comp, 20, nome))

# ======================= SAFE =======================
m = M("safe1", "Praca redonda", ["Cerca redonda de 110 m, 1 entrada", "8 cantinhos + oficina em volta da praca",
      "Hangar com heliponto dentro da safe", "Pista de aviao do lado de fora"], 86, 2, 18)
m.circulo(55, 8); m.zona = 60
arco(m, SAFE8, 40, -100, 100)
m.banca("oficina", "OFICINA", -27, -26, 1, 0, 16, 12)
m.banca("hangar", "", 28, -24, -1, 0, 20, 18)
m.add("heliponto", 9, -24)
m.add("bandeira", 0, 10); m.add("bandeira", -5.5, -56.5); m.add("bandeira", 5.5, -56.5)
for i in range(14):
    a = 2 * math.pi * (i + .5) / 14
    if math.cos(a) < -.9: continue
    m.add("luz", 51 * math.sin(a), 51 * math.cos(a))
for p in [(-8, 18), (8, 18), (-8, 2), (8, 2)]: m.add("luz", *p)
m.add("veiculo", -10, -66, "CARROS")
pista_direita(m, 74)
m.caminhos += [[(0, -64), (0, 10)], [(0, -26), (-18, -26)], [(55, -45), (64, -45)]]
m.areas.append(("praca", 0, 10, 14))
modelos.append(m)

m = M("safe2", "Rua do comercio", ["Area de 56 x 110 m, 1 entrada", "Rua reta com 4 cantinhos de cada lado",
      "Oficina e hangar com heliponto no fim da rua", "Pista de aviao do lado de fora"], 76, 0, 18)
m.ret(56, 110, 8); m.zona = 62
for i, (t, n) in enumerate(SAFE8[:4]): m.banca(t, n, -21, -42 + 16 * i, 1, 0)
for i, (t, n) in enumerate(SAFE8[4:]): m.banca(t, n, 21, -42 + 16 * i, -1, 0)
m.banca("oficina", "OFICINA", -14, 46, 0, -1, 22, 13)
m.banca("hangar", "", 15, 44, 0, -1, 20, 18)
m.add("heliponto", 15, 26)
for i in range(7): m.add("luz", -6, -48 + 14 * i); m.add("luz", 6, -48 + 14 * i)
m.add("bandeira", -5.5, -56.5); m.add("bandeira", 5.5, -56.5); m.add("bandeira", 0, 30)
m.add("veiculo", -10, -66, "CARROS")
pista_direita(m, 52)
m.caminhos += [[(0, -64), (0, 36)]]
modelos.append(m)

m = M("safe3", "Fortaleza", ["Muro quadrado de 100 x 100 m, 1 portao", "Cantinhos encostados nos muros",
      "Praca no meio, hangar e oficina no fundo", "Pista de aviao do lado de fora"], 80, -2, 16)
m.ret(100, 100, 9); m.zona = 72
for i, (t, n) in enumerate(SAFE8[:4]): m.banca(t, n, -43, -30 + 17 * i, 1, 0)
for i, (t, n) in enumerate(SAFE8[4:]): m.banca(t, n, 43, -30 + 17 * i, -1, 0)
m.banca("oficina", "OFICINA", -17, 41, 0, -1, 22, 14)
m.banca("hangar", "", 17, 40, 0, -1, 20, 18)
m.add("heliponto", 17, 22)
for cx in (-46, 46):
    for cy in (-46, 46): m.add("torre", cx, cy)
m.add("bandeira", 0, 0); m.add("bandeira", -6, -51.5); m.add("bandeira", 6, -51.5)
for p in [(-12, 0), (12, 0), (0, 12), (0, -12)]: m.add("luz", *p)
m.add("veiculo", -10, -60, "CARROS")
pista_direita(m, 72)
m.caminhos += [[(0, -58), (0, 30)], [(-36, 0), (36, 0)]]
m.areas.append(("praca", 0, 0, 13))
modelos.append(m)

# ======================= MERCADO NEGRO =======================
NEGRO = [("armas_n", "ARMAS PESADAS"), ("tatico", "TATICO"), ("raid", "RAID")]
m = M("negro1", "Bunker redondo", ["Cerca redonda de 80 m, so 1 entrada", "Armas pesadas, tatico e RAID",
      "Hangar com heliponto (helis raros)", "Pista de aviao do lado de fora"], 66, 0, 16)
m.circulo(40, 6)
arco(m, NEGRO, 27, -65, 65)
m.banca("hangar", "HANGAR", -21, -14, 1, 0, 20, 18)
m.add("heliponto", 6, -14)
for p in [(20, -4), (24, -16), (-2, 6), (14, -30)]: m.add("caixa", *p)
for p in [(-28, 24), (28, 24), (-16, -33), (16, -33), (0, 8), (33, -8)]: m.add("luz", *p)
m.add("bandeira", -4.5, -41.5); m.add("bandeira", 4.5, -41.5)
pista_direita(m, 56)
m.caminhos += [[(0, -50), (0, 16)], [(40, -40), (46, -40)]]
modelos.append(m)

m = M("negro2", "Corredor da morte", ["Quadrado de 80 x 80 m", "Entrada por corredor estreito em curva",
      "Hangar com heliponto no canto", "Pista de aviao do lado de fora"], 68, 0, 14)
m.seg((-40, -40), (-40, 40)); m.seg((-40, 40), (40, 40)); m.seg((40, 40), (40, -40)); m.seg((40, -40), (-40, -40), (-28, -40, 5))
m.entrada = (-28, -40)
m.seg((-25.5, -40), (-25.5, -20)); m.seg((-25.5, -20), (0, -20))
m.seg((-30.5, -40), (-30.5, -15)); m.seg((-30.5, -15), (0, -15))
for i, (t, n) in enumerate(NEGRO): m.banca(t, n, -22 + 18 * i, 33, 0, -1)
m.banca("hangar", "HANGAR", 29, -4, -1, 0, 20, 18)
m.add("heliponto", 9, -2)
for p in [(-24, 10), (-12, 14), (24, -28)]: m.add("caixa", *p)
for p in [(-28, -28), (-14, -17.5), (0, 14), (-35, 35), (35, 35), (-35, -35)]: m.add("luz", *p)
m.add("bandeira", -32.5, -42); m.add("bandeira", -23.5, -42)
pista_direita(m, 58)
m.caminhos += [[(-28, -52), (-28, -17.5), (2, -17.5), (2, 4), (-4, 26)]]
modelos.append(m)

m = M("negro3", "Galpao do contrabando", ["Retangulo de 80 x 60 m, 1 portao", "3 cantinhos no fundo do galpao",
      "Hangar no lado com heliponto", "Pista de aviao do lado de fora"], 68, -2, 14)
m.ret(80, 60, 7)
for i, (t, n) in enumerate(NEGRO): m.banca(t, n, -28 + 15 * i, 23, 0, -1, 12, 9)
m.banca("hangar", "HANGAR", 25, 18, 0, -1, 20, 18)
m.add("heliponto", 25, -2)
for x in (-34, -28):
    for y in (-18, -11, -4): m.add("caixa", x, y)
for p in [(-12, -12), (-14, 4)]: m.add("caixa", *p)
for p in [(-35, 25), (-8, 10), (8, -22), (-35, -25), (35, -25), (12, 6)]: m.add("luz", *p)
m.add("bandeira", -4.5, -31.5); m.add("bandeira", 4.5, -31.5)
pista_direita(m, 56)
m.caminhos += [[(0, -42), (0, 4), (-13, 16)]]
modelos.append(m)

# ======================= CACADOR =======================
m = M("cacador1", "Acampamento", ["Sem cerca, aberto no meio do mato", "Cantinho com couros, chifres e carne",
      "Fogueira, barracas e lampioes", "Hangar com heliponto do lado"], 34, 0, 8)
m.banca("cacador", "CACADOR", -6, 7, 0, -1, 11, 7)
m.add("fogueira", -6, -3); m.add("barraca", -17, 2); m.add("barraca", 5, 2)
m.add("varal", -13, -8); m.add("varal", 1, -8)
for p in [(-16, -12), (4, -12), (-14, 13), (2, 13)]: m.add("luz", *p)
m.add("bandeira", 1, 12)
m.banca("hangar", "", 22, 12, 0, -1, 20, 18)
m.add("heliponto", 22, -8)
m.areas.append(("clareira", -6, 0, 17)); m.entrada = (-6, -17)
m.caminhos += [[(-6, -24), (-6, -5)]]
modelos.append(m)

m = M("cacador2", "Cabana cercada", ["Cerca redonda de 40 m, 1 entrada", "Cantinho do cacador no fundo",
      "Fogueira no meio, varais de carne", "Hangar com heliponto do lado de fora"], 36, 0, 10)
m.circulo(20, 5)
m.banca("cacador", "CACADOR", 0, 10, 0, -1, 11, 7)
m.add("fogueira", 0, -3); m.add("varal", -10, -2); m.add("varal", 10, -2); m.add("barraca", -11, -10)
for p in [(-13, 9), (13, 9), (9, -13)]: m.add("luz", *p)
m.add("bandeira", -4, -21.5); m.add("bandeira", 4, -21.5)
m.banca("hangar", "", 34, 10, 0, -1, 20, 18)
m.add("heliponto", 34, -10)
m.caminhos += [[(0, -28), (0, 5)], [(4, -24), (34, -18)]]
modelos.append(m)

m = M("cacador3", "Posto de caca", ["Quadrado de 36 x 36 m, 1 portao", "Cantinho do cacador com balcao",
      "Torre de vigia com bandeira DZ", "Hangar com heliponto do lado de fora"], 36, 0, 10)
m.ret(36, 36, 5)
m.banca("cacador", "CACADOR", -3, 11, 0, -1, 11, 7)
m.add("torre", 14, 14); m.add("bandeira", 14, 14)
m.add("varal", -14, -4); m.add("varal", -14, 4); m.add("varal", 14, -4)
m.add("fogueira", 3, -5)
for p in [(-15, -15), (15, -15), (8, 8)]: m.add("luz", *p)
m.add("bandeira", -3.5, -19.5); m.add("bandeira", 3.5, -19.5)
m.banca("hangar", "", 34, 10, 0, -1, 20, 18)
m.add("heliponto", 34, -10)
m.caminhos += [[(0, -28), (-3, 6)], [(3, -24), (34, -18)]]
modelos.append(m)

# ======================= DESENHO =======================
TEMA = {  # piso, cor do NPC, letra, o que o NPC segura
    "armas": ((95, 70, 60), (220, 60, 50), "A"), "equip": ((90, 85, 65), (160, 120, 80), "E"),
    "roupas": ((80, 85, 110), (80, 140, 230), "R"), "mercado": ((90, 105, 80), (90, 190, 80), "M"),
    "vault": ((95, 95, 100), (230, 190, 40), "V"), "banco": ((70, 95, 105), (40, 200, 200), "$"),
    "oficina": ((88, 88, 92), (240, 200, 40), "O"), "armas_n": ((70, 50, 60), (200, 40, 40), "A"),
    "tatico": ((60, 65, 60), (110, 150, 90), "T"), "explosivos": ((80, 60, 45), (250, 150, 30), "X"),
    "hangar": ((85, 88, 95), (170, 170, 190), "H"), "cacador": ((100, 80, 55), (140, 170, 70), "C"),
    "comida": ((90, 105, 80), (90, 190, 80), "C"), "remedios": ((150, 155, 160), (240, 240, 240), "+"),
    "construtor": ((105, 95, 70), (250, 190, 40), "B"), "raid": ((80, 60, 45), (250, 150, 30), "R"),
}

def terreno(w, h, seed, tipo):
    random.seed(seed); base = {"safe": (74, 96, 58), "negro": (72, 72, 62), "cacador": (58, 88, 48)}[tipo]
    img = Image.new("RGB", (w, h), base); d = ImageDraw.Draw(img)
    for _ in range(3000):
        x, y = random.randint(0, w), random.randint(0, h); r = random.randint(4, 26); k = random.randint(-14, 14)
        d.ellipse([x - r, y - r, x + r, y + r], fill=tuple(max(0, min(255, c + k)) for c in base))
    return img.filter(ImageFilter.GaussianBlur(6))

def desenhar(m, tipo, num, PX=1000):
    esc = PX / (2 * m.alcance); TOP = 200
    W, H = PX, PX + TOP
    img = Image.new("RGB", (W, H), PRETO); img.paste(terreno(PX, PX, sum(map(ord, m.cod)), tipo), (0, TOP))
    d = ImageDraw.Draw(img, "RGBA")
    cy0 = TOP + PX / 2 + m.cy * esc
    def P(x, y): return (W / 2 + (x - m.cx) * esc, cy0 - y * esc)
    def circ(x, y, r, **k):
        px, py = P(x, y); r *= esc; d.ellipse([px - r, py - r, px + r, py + r], **k)
    def rq(x, y, hw, hh, **k):
        a, b = P(x - hw, y + hh), P(x + hw, y - hh); d.rectangle([a, b], **k)
    def poly(pts, **k): d.polygon([P(*p) for p in pts], **k)
    def linha(a, b, cor, larg): d.line([P(*a), P(*b)], fill=cor, width=max(2, int(larg * esc)))

    for kind, x, y, r in m.areas:
        circ(x, y, r, fill=(150, 140, 120, 200) if kind == "praca" else (110, 100, 70, 160))
    if m.zona:
        px, py = P(0, 0); rr = m.zona * esc
        for i in range(90): d.arc([px - rr, py - rr, px + rr, py + rr], i * 4, i * 4 + 2.4, fill=(80, 230, 120, 230), width=3)
    for cam in m.caminhos: d.line([P(*p) for p in cam], fill=(150, 125, 90, 255), width=int(4.5 * esc), joint="curve")

    # ---------- cantinhos ----------
    def banca(tema, nome, cx, cy, fx, fy, w, dd):
        sx, sy = fy, -fx  # lado (direita olhando pra fora da banca)... qualquer perpendicular
        def L(u, v):  # u ao longo da frente, v profundidade (0 frente, dd fundo)
            return (cx + sx * u + fx * (dd / 2 - v), cy + sy * u + fy * (dd / 2 - v))
        piso, cor, letra = TEMA[tema]
        poly([L(-w / 2, 0), L(w / 2, 0), L(w / 2, dd), L(-w / 2, dd)], fill=piso + (255,))
        # telhado leve (sombra) e paredes em 3 lados
        for a, b in [((-w / 2, 0), (-w / 2, dd)), ((-w / 2, dd), (w / 2, dd)), ((w / 2, dd), (w / 2, 0))]:
            linha(L(*a), L(*b), (35, 32, 30, 255), .55)
        def ao_longo(v, n, u0=None, u1=None):
            u0 = -w / 2 + 1 if u0 is None else u0; u1 = w / 2 - 1 if u1 is None else u1
            return [L(u0 + (u1 - u0) * (i + .5) / n, v) for i in range(n)]
        def arma(p, comp=1.6, cor_a=(30, 30, 30, 255)):
            a = (p[0] - sx * comp / 2, p[1] - sy * comp / 2); b = (p[0] + sx * comp / 2, p[1] + sy * comp / 2)
            linha(a, b, cor_a, .22); circ(*b, .22, fill=(90, 60, 35, 255))
        def lateral(u, n, fn):
            for i in range(n): fn(L(u, 1.2 + (dd - 2.4) * (i + .5) / n))
        if tema in ("armas", "armas_n"):
            for p in ao_longo(dd - .45, 5 if w < 12 else 7): arma(p)
            for u in (-w / 2 + .45, w / 2 - .45): lateral(u, 3, lambda p: arma(p, 1.3))
            for p in ao_longo(dd * .5, 2, -w / 3, w / 3): rq(*p, .5, .5, fill=(110, 90, 40, 255), outline=(60, 50, 20, 255))
            if tema == "armas_n":
                for p in ao_longo(dd - 1.3, 2, -w / 4, w / 4): arma(p, 2.2, (60, 70, 50, 255))
        elif tema == "comida":
            cores = [(220, 60, 50), (240, 200, 50), (90, 180, 70), (240, 140, 40), (250, 250, 250)]
            for j, v in enumerate((dd - .5, dd - 1.4)):
                for i, p in enumerate(ao_longo(v, 8)): circ(*p, .28, fill=cores[(i + j) % 5] + (255,))
            for u in (-w / 2 + .5, w / 2 - .5): lateral(u, 3, lambda p: rq(*p, .4, .4, fill=(200, 160, 90, 255), outline=(90, 60, 30, 255)))
        elif tema == "remedios":
            for p in ao_longo(dd - .6, 5):
                rq(*p, .45, .45, fill=(245, 245, 245, 255), outline=(200, 30, 30, 255), width=2)
                px_, py_ = P(*p); k = .25 * esc
                d.line([px_ - k, py_, px_ + k, py_], fill=(210, 30, 30, 255), width=3); d.line([px_, py_ - k, px_, py_ + k], fill=(210, 30, 30, 255), width=3)
            lateral(-w / 2 + .5, 3, lambda p: rq(*p, .35, .5, fill=(220, 220, 225, 255), outline=(120, 120, 130, 255)))
            lateral(w / 2 - .7, 2, lambda p: rq(*p, .5, 1.0, fill=(240, 240, 240, 255), outline=(150, 150, 150, 255)))
        elif tema == "construtor":
            for p in ao_longo(dd - .7, 3): rq(*p, 1.4, .35, fill=(170, 130, 80, 255), outline=(90, 60, 30, 255))
            for p in ao_longo(dd - 1.6, 3): rq(*p, 1.2, .3, fill=(150, 150, 155, 255), outline=(80, 80, 80, 255))
            lateral(-w / 2 + .7, 2, lambda p: rq(*p, .6, .6, fill=(125, 90, 50, 255), outline=(70, 50, 25, 255), width=2))
            lateral(w / 2 - .7, 2, lambda p: rq(*p, .6, .6, fill=(240, 190, 40, 255), outline=(60, 50, 20, 255), width=2))
        elif tema == "mercado":
            cores = [(220, 60, 50), (240, 200, 50), (90, 180, 70), (240, 140, 40), (250, 250, 250)]
            for j, v in enumerate((dd - .5, dd - 1.4)):
                for i, p in enumerate(ao_longo(v, 8)): circ(*p, .28, fill=cores[(i + j) % 5] + (255,))
            for u in (-w / 2 + .5,): lateral(u, 3, lambda p: circ(*p, .3, fill=(220, 60, 50, 255)))
            lateral(w / 2 - .6, 2, lambda p: (rq(*p, .45, .45, fill=(245, 245, 245, 255), outline=(200, 30, 30, 255), width=2)))
        elif tema in ("roupas", "tatico"):
            for p in ao_longo(dd - 1.1, 4):
                circ(*p, .55, fill=(200, 190, 170, 255) if tema == "roupas" else (80, 95, 70, 255), outline=(40, 40, 40, 255))
                circ(*p, .22, fill=(230, 210, 190, 255))
            lateral(-w / 2 + .5, 4, lambda p: rq(*p, .35, .2, fill=(70, 110, 200, 255) if tema == "roupas" else (60, 70, 55, 255)))
            lateral(w / 2 - .5, 4, lambda p: rq(*p, .35, .2, fill=(170, 60, 60, 255) if tema == "roupas" else (50, 50, 50, 255)))
        elif tema == "equip":
            for p in ao_longo(dd - .6, 5): d.rounded_rectangle([P(p[0] - .4, p[1] + .45), P(p[0] + .4, p[1] - .45)], 4, fill=(100, 110, 60, 255))
            lateral(-w / 2 + .5, 3, lambda p: rq(*p, .4, .3, fill=(150, 150, 150, 255)))
            lateral(w / 2 - .6, 3, lambda p: rq(*p, .45, .3, fill=(160, 140, 90, 255)))
        elif tema == "vault":
            for v in (dd - .6, dd - 1.5):
                for p in ao_longo(v, 6): rq(*p, .4, .4, fill=(120, 125, 135, 255), outline=(50, 50, 55, 255), width=2)
        elif tema == "banco":
            for p in ao_longo(dd - .6, 3): rq(*p, .55, .4, fill=(60, 70, 85, 255), outline=(40, 220, 140, 255), width=3)
        elif tema in ("explosivos", "raid"):
            for p in ao_longo(dd - .6, 6): circ(*p, .4, fill=(190, 40, 30, 255), outline=(40, 20, 20, 255))
            lateral(-w / 2 + .6, 3, lambda p: rq(*p, .5, .4, fill=(230, 190, 40, 255), outline=(20, 20, 20, 255), width=2))
            lateral(w / 2 - .6, 3, lambda p: rq(*p, .5, .4, fill=(110, 110, 60, 255), outline=(20, 20, 20, 255), width=2))
        elif tema == "oficina":
            # elevador com carro, pneus, bancada
            c = L(-w / 4, dd / 2 + .5); rq(*c, 2.2, 2.2, outline=(240, 200, 40, 255), width=4)
            px, py = P(*c); d.rounded_rectangle([px - 1.0 * esc, py - 2 * esc, px + 1.0 * esc, py + 2 * esc], 8, fill=(40, 90, 170, 255))
            c2 = L(w / 4, dd / 2 + .5); px, py = P(*c2); d.rounded_rectangle([px - 1.0 * esc, py - 2 * esc, px + 1.0 * esc, py + 2 * esc], 8, fill=(170, 40, 40, 255))
            for p in ao_longo(dd - .5, 6): circ(*p, .4, fill=(25, 25, 25, 255), outline=(80, 80, 80, 255))
            lateral(-w / 2 + .6, 3, lambda p: rq(*p, .4, .6, fill=(200, 40, 40, 255)))
        elif tema == "hangar":
            px, py = P(*L(0, dd / 2 + .5))
            d.ellipse([px - 1.1 * esc, py - 3 * esc, px + 1.1 * esc, py + 3 * esc], fill=(50, 70, 50, 255))
            d.line([px - 5 * esc, py - 5 * esc, px + 5 * esc, py + 5 * esc], fill=(30, 30, 30, 255), width=4)
            d.line([px - 5 * esc, py + 5 * esc, px + 5 * esc, py - 5 * esc], fill=(30, 30, 30, 255), width=4)
            lateral(-w / 2 + .7, 4, lambda p: circ(*p, .45, fill=(200, 40, 30, 255)))
            lateral(w / 2 - .7, 3, lambda p: rq(*p, .5, .5, fill=(120, 120, 120, 255)))
        elif tema == "cacador":
            for p in ao_longo(dd - .55, 4):
                px, py = P(*p); d.ellipse([px - .7 * esc, py - .5 * esc, px + .7 * esc, py + .5 * esc], fill=(150, 105, 60, 255), outline=(80, 50, 20, 255))
            lateral(-w / 2 + .6, 3, lambda p: rq(*p, .4, .25, fill=(180, 60, 50, 255)))
            for p in ao_longo(dd - 1.6, 2, -w / 4, w / 4):
                px, py = P(*p); d.line([px - .8 * esc, py - .6 * esc, px, py, px + .8 * esc, py - .6 * esc], fill=(230, 220, 190, 255), width=3)
        # balcao e NPC atras dele
        if not nome:
            return
        if tema not in ("hangar", "oficina"):
            linha(L(-w / 4, dd * .38), L(w / 4, dd * .38), (120, 85, 50, 255), .7)
            npc = L(0, dd * .58)
        else:
            npc = L(-w / 2 + 1.6, 1.4)
        rotulos.append((P(*npc), cor, letra, nome))

    for (x, y, fx, fy, comp, larg, nome) in m.pistas:
        rq(x, y, larg / 2, comp / 2, fill=(48, 48, 52, 255), outline=(230, 230, 230, 255), width=2)
        for k in range(int(comp / 8)):
            yy = y - comp / 2 + 4 + k * 8
            rq(x, yy, .4, 2, fill=(240, 240, 240, 255))
        for k in range(int(comp / 10) + 1):
            for sx_ in (-1, 1):
                circ(x + sx_ * (larg / 2 + 1.2), y - comp / 2 + k * 10, .6, fill=(255, 140, 40, 255))
        px, py = P(x, y + comp / 2 + 3)
        d.text((px, py - 4), nome, font=f(16), fill=LAR, anchor="mb")
    rotulos = []
    for b in m.bancas: banca(*b)
    for a, b in m.cercas:
        linha(a, b, (45, 40, 36, 255), .8)
        L_ = math.hypot(b[0] - a[0], b[1] - a[1]); n = max(1, int(L_ / 3.5))
        for i in range(n + 1):
            x = a[0] + (b[0] - a[0]) * i / n; y = a[1] + (b[1] - a[1]) * i / n
            px, py = P(x, y); r = max(3, .45 * esc); d.rectangle([px - r, py - r, px + r, py + r], fill=(95, 85, 70, 255))
    for t, x, y, nome in m.itens:
        px, py = P(x, y)
        if t == "luz":
            for rr, al in ((3.2, 35), (2.0, 60), (1.0, 110)): circ(x, y, rr, fill=(255, 220, 120, al))
            d.ellipse([px - 5, py - 5, px + 5, py + 5], fill=(255, 240, 180, 255))
        elif t == "caixa":
            rq(x, y, 1.5, 1.5, fill=(125, 90, 50, 255), outline=(70, 50, 25, 255), width=2)
            a, b = P(x - 1.5, y + 1.5), P(x + 1.5, y - 1.5); d.line([a, b], fill=(80, 55, 30, 255), width=2)
        elif t == "torre":
            rq(x, y, 2.4, 2.4, fill=(60, 55, 50, 255), outline=(30, 30, 30, 255), width=3)
            for rr, al in ((7, 30), (4, 55)): circ(x, y, rr, fill=(255, 220, 120, al))
        elif t == "fogueira":
            for rr, al in ((5, 45), (3, 85)): circ(x, y, rr, fill=(255, 140, 40, al))
            circ(x, y, 1.2, fill=(255, 90, 20, 255), outline=(90, 60, 40, 255), width=3)
        elif t == "barraca":
            r = 3 * esc; d.polygon([(px, py - r), (px - r, py + r * .8), (px + r, py + r * .8)], fill=(110, 120, 70, 255), outline=(50, 55, 30, 255))
        elif t == "varal":
            r = 2.5 * esc; d.line([px - r, py, px + r, py], fill=(90, 60, 30, 255), width=4)
            for k in (-1.5, 0, 1.5): d.rectangle([px + k * esc - 4, py, px + k * esc + 4, py + .9 * esc], fill=(170, 60, 50, 255))
        elif t == "banco_praca":
            rq(x, y, 1.6, .4, fill=(120, 85, 50, 255))
        elif t == "heliponto":
            circ(x, y, 7, fill=(55, 55, 58, 255), outline=(240, 240, 240, 255), width=3)
            d.text((px, py), "H", font=f(max(14, int(5 * esc))), fill=BRANCO, anchor="mm")
            d.text((px, py + 7 * esc + 14), "HELIPONTO", font=f(14), fill=LAR, anchor="mm")
        elif t == "veiculo":
            box = [px - 4.5 * esc, py - 3.5 * esc, px + 4.5 * esc, py + 3.5 * esc]
            d.rectangle(box, outline=LAR + (255,), width=3, fill=(30, 30, 30, 120))
            d.text((px, box[3] + 14), nome, font=f(16), fill=LAR, anchor="mm")
    for t, x, y, nome in m.itens:
        if t != "bandeira": continue
        px, py = P(x, y)
        d.line([px, py, px, py - 38], fill=(210, 210, 210, 255), width=3)
        d.rectangle([px, py - 38, px + 34, py - 16], fill=PRETO + (255,), outline=LAR + (255,), width=2)
        d.text((px + 17, py - 27), "DZ", font=f(14), fill=LAR, anchor="mm")
    for (px, py), cor, letra, nome in rotulos:
        r = 15 * ESC_NPC
        d.ellipse([px - r - 3, py - r - 3, px + r + 3, py + r + 3], fill=PRETO + (255,))
        d.ellipse([px - r, py - r, px + r, py + r], fill=cor + (255,))
        d.text((px, py), letra, font=f(int(16 * ESC_NPC)), fill=PRETO if sum(cor) > 400 else BRANCO, anchor="mm")
        fn = f(int(15 * ESC_NPC)); tw = d.textlength(nome, font=fn)
        bx = [px - tw / 2 - 7, py - r - 16 - 15 * ESC_NPC, px + tw / 2 + 7, py - r - 6]
        d.rounded_rectangle(bx, 5, fill=(0, 0, 0, 215), outline=LAR + (255,), width=2)
        d.text((px, (bx[1] + bx[3]) / 2), nome, font=fn, fill=BRANCO, anchor="mm")
    if not m.mostrar_entrada: return img
    ex, ey = P(*m.entrada)
    d.polygon([(ex, ey - 4), (ex - 14, ey + 20), (ex + 14, ey + 20)], fill=LAR + (255,))
    d.rounded_rectangle([ex - 48, ey + 24, ex + 48, ey + 46], 5, fill=(0, 0, 0, 190))
    d.text((ex, ey + 35), "ENTRADA", font=f(16), fill=LAR, anchor="mm")
    sx, sy = 24, H - 28
    d.rectangle([sx - 8, sy - 20, sx + 10 * esc + 44, sy + 14], fill=(0, 0, 0, 170))
    d.line([sx, sy, sx + 10 * esc, sy], fill=BRANCO, width=4); d.text((sx + 10 * esc + 6, sy), "10 m", font=f(14), fill=BRANCO, anchor="lm")
    d.rectangle([0, 0, W, TOP], fill=PRETO); d.rectangle([0, TOP - 4, W, TOP], fill=LAR)
    area = {"safe": "SAFE ZONE", "negro": "MERCADO NEGRO", "cacador": "CACADOR"}[tipo]
    d.rounded_rectangle([22, 22, 98, 98], 10, fill=LAR); d.text((60, 60), str(num), font=f(52), fill=PRETO, anchor="mm")
    d.text((118, 24), area + "  -  MODELO " + str(num), font=f(22), fill=LAR)
    d.text((118, 52), m.titulo.upper(), font=f(36), fill=BRANCO)
    for i, l in enumerate(m.desc):
        d.text((26 + (i % 2) * (W / 2 - 6), 118 + (i // 2) * 34), "- " + l, font=f(18, False), fill=(210, 210, 210))
    return img

def rodape(W, tipo):
    img = Image.new("RGB", (W, 120), PRETO); d = ImageDraw.Draw(img)
    x = 24
    itens = [((255, 230, 150), "luz"), ((125, 90, 50), "caixa"), ((45, 40, 36), "cerca/parede"), (LAR, "bandeira DZ")]
    if tipo == "safe": itens.append(((80, 230, 120), "limite da safe (sem PvP)"))
    for cor, txt in itens:
        d.rectangle([x, 20, x + 20, 40], fill=cor, outline=(120, 120, 120)); d.text((x + 28, 30), txt, font=f(18, False), fill=BRANCO, anchor="lm")
        x += 46 + d.textlength(txt, font=f(18, False)) + 24
    nota = {"safe": "Cada NPC segura o item da loja dele. Enfeites nao podem ser pegos. Hangar com heliponto pra pousar e pista de aviao do lado de fora.",
            "negro": "Armas e itens mais raros que a safe + RAID (C4 e explosivos). Heli/aviao raro comprado no hangar sai no heliponto ou na pista.",
            "cacador": "Cacador compra o que voce caca e paga em dinheiro. Hangar com heliponto do lado pra pousar."}[tipo]
    d.text((24, 78), nota, font=f(20, False), fill=(220, 220, 220), anchor="lm")
    return img

for tipo in ("safe", "negro", "cacador"):
    ms = [m for m in modelos if m.cod.startswith(tipo)]
    tiles = [desenhar(m, tipo, i + 1) for i, m in enumerate(ms)]
    for m, t in zip(ms, tiles): t.save(f"{OUT}/{m.cod}.jpg", quality=90)
    W = sum(t.width for t in tiles) + 20 * (len(tiles) + 1); H = tiles[0].height + 40 + 120
    full = Image.new("RGB", (W, H), (8, 8, 10))
    for i, t in enumerate(tiles): full.paste(t, (20 + i * (t.width + 20), 20))
    full.paste(rodape(W, tipo), (0, H - 125))
    full.save(f"{OUT}/modelos_{tipo}.jpg", quality=88)
print("ok")

# ======================= CANTINHOS DE PERTO =======================
CANT = [("armas", "ARMAS", "safe", "NPC com fuzil na mao", "Armas nas 3 paredes, caixas de municao"),
        ("roupas", "ROUPAS", "safe", "NPC de camiseta", "Manequins vestidos e araras de roupa"),
        ("equip", "EQUIPAMENTOS", "safe", "NPC com mochila e radio", "Mochilas, coletes e ferramentas na parede"),
        ("construtor", "CONSTRUTOR", "safe", "NPC de capacete com martelo", "Tabuas, paredes, caixas e geradores"),
        ("comida", "COMIDA", "safe", "NPC com lata e pao", "Prateleiras de comida e bebida"),
        ("remedios", "REMEDIOS", "safe", "NPC com kit medico", "Kits medicos, armario e maca"),
        ("vault", "VAULT", "safe", "NPC de terno com maleta", "Cofres empilhados no fundo"),
        ("banco", "BANCO / ATM", "safe", "Caixa eletronico", "3 caixas eletronicos"),
        ("oficina", "OFICINA", "safe", "Mecanico com chave inglesa", "Elevador com carro, pneus, bancada"),
        ("cacador", "CACADOR", "cacador", "Cacador com rifle e carne", "Couros, chifres e carne pendurada"),
        ("armas_n", "ARMAS PESADAS", "negro", "NPC de capuz com metralhadora", "Snipers, fuzis e lanca-misseis"),
        ("tatico", "TATICO", "negro", "NPC de colete e capacete", "Manequins com colete, ghillie e capacete"),
        ("raid", "RAID", "negro", "NPC com C4 na mao", "C4, cargas e explosivos de raid"),
        ("hangar", "HANGAR", "negro", "Piloto", "Helis e avioes raros; heli sai no heliponto, aviao na pista")]
tiles = []
for tema, nome, tipo, npc, enf in CANT:
    w, dd = {"oficina": (16, 12), "hangar": (20, 18)}.get(tema, (12, 9))
    mm = M("c_" + tema, nome, [], max(w, dd) * .62, -1.5); mm.mostrar_entrada = False
    mm.banca(tema, nome, 0, 0, 0, -1, w, dd)
    globals()["ESC_NPC"] = 1.5
    t = desenhar(mm, tipo, 0, PX=600).crop((0, 200, 600, 800))
    full = Image.new("RGB", (600, 720), PRETO); full.paste(t, (0, 120)); d = ImageDraw.Draw(full)
    d.rectangle([0, 116, 600, 120], fill=LAR)
    d.text((20, 14), nome, font=f(30), fill=LAR)
    d.text((20, 56), "NPC: " + npc, font=f(19, False), fill=BRANCO)
    d.text((20, 84), "Enfeite: " + enf, font=f(17, False), fill=(200, 200, 200))
    tiles.append(full)
cols = 4; rows = (len(tiles) + cols - 1) // cols
G = Image.new("RGB", (cols * 620 + 20, rows * 740 + 20 + 90), (8, 8, 10)); d = ImageDraw.Draw(G)
d.text((24, 22), "CANTINHOS DE CADA NPC  (vale pra qualquer modelo)  -  frente aberta embaixo, enfeites nao podem ser pegos", font=f(30), fill=BRANCO)
for i, t in enumerate(tiles): G.paste(t, (20 + (i % cols) * 620, 90 + (i // cols) * 740))
G.save(f"{OUT}/cantinhos.jpg", quality=88)
print("cantinhos ok")
