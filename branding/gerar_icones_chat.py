"""Gera os icones que aparecem do lado das mensagens no chat (branding/chat/*.png, 64x64).
O plugin usa a URL raw do GitHub (Sistemas.IconesChatBase). Rodar: python3 branding/gerar_icones_chat.py"""
import os
from PIL import Image, ImageDraw

AQUI = os.path.dirname(os.path.abspath(__file__))
SAIDA = os.path.join(AQUI, "chat")
G = 256  # desenha grande e reduz pra ficar liso

FUNDO = (21, 22, 26, 255)
CORES = {
    "vermelho": (255, 77, 77, 255),
    "laranja": (255, 90, 0, 255),
    "amarelo": (255, 176, 32, 255),
    "verde": (91, 227, 125, 255),
    "cinza": (150, 156, 166, 255),
    "azul": (80, 160, 255, 255),
    "roxo": (170, 110, 255, 255),
}


def base(cor):
    im = Image.new("RGBA", (G, G), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((8, 8, G - 8, G - 8), radius=44, fill=FUNDO, outline=cor, width=14)
    return im, d


def caveira(d, cor, fundo=FUNDO):
    d.ellipse((58, 44, 198, 170), fill=cor)
    d.rounded_rectangle((86, 140, 170, 200), radius=14, fill=cor)
    d.ellipse((84, 92, 122, 134), fill=fundo)
    d.ellipse((134, 92, 172, 134), fill=fundo)
    d.polygon([(128, 138), (118, 156), (138, 156)], fill=fundo)
    for x in (104, 124, 144):
        d.rectangle((x, 176, x + 8, 200), fill=fundo)


def mira(d, cor):
    d.ellipse((60, 60, 196, 196), outline=cor, width=16)
    d.ellipse((112, 112, 144, 144), fill=cor)
    for a, b in (((120, 36), (136, 92)), ((120, 164), (136, 220)), ((36, 120), (92, 136)), ((164, 120), (220, 136))):
        d.rectangle((*a, *b), fill=cor)


def exclamacao(d, cor):
    d.polygon([(128, 46), (214, 200), (42, 200)], fill=cor)
    d.rounded_rectangle((118, 96, 138, 160), radius=8, fill=FUNDO)
    d.ellipse((116, 168, 140, 190), fill=FUNDO)


def check(d, cor):
    d.line([(64, 132), (108, 176), (194, 84)], fill=cor, width=28, joint="curve")


def sacola(d, cor):
    d.arc((92, 50, 164, 122), 180, 360, fill=cor, width=16)
    d.rounded_rectangle((58, 92, 198, 206), radius=22, fill=cor)
    d.rounded_rectangle((110, 118, 146, 140), radius=6, fill=FUNDO)


def interrogacao(d, cor):
    d.arc((78, 46, 178, 146), 200, 90, fill=cor, width=24)
    d.rectangle((116, 132, 140, 166), fill=cor)
    d.ellipse((114, 180, 142, 208), fill=cor)


def coroa(d, cor):
    d.polygon([(54, 184), (54, 84), (96, 130), (128, 68), (160, 130), (202, 84), (202, 184)], fill=cor)
    d.rectangle((54, 192, 202, 210), fill=cor)


def raio(d, cor):
    d.polygon([(146, 36), (70, 140), (124, 140), (104, 220), (186, 108), (132, 108)], fill=cor)


ICONES = {
    "caveira": ("vermelho", caveira),       # alguem matou alguem
    "cabeca": ("vermelho", mira),           # abate com tiro na cabeca
    "zumbi": ("verde", caveira),            # morto por zumbi, infectado ou bicho
    "morte": ("cinza", caveira),            # morreu sozinho (queda, fome, frio...)
    "combate": ("laranja", exclamacao),     # entrou em modo de combate
    "ok": ("verde", check),                 # combate acabou, coisa deu certo
    "sacola": ("amarelo", sacola),          # suas coisas no chao
    "dica": ("azul", interrogacao),         # dicas e ajuda
    "vip": ("amarelo", coroa),              # VIP
    "evento": ("laranja", raio),            # eventos (heli crash, comboio, airdrop)
}


def main():
    os.makedirs(SAIDA, exist_ok=True)
    for nome, (cor, desenho) in ICONES.items():
        im, d = base(CORES[cor])
        desenho(d, CORES[cor])
        im.resize((64, 64), Image.LANCZOS).save(os.path.join(SAIDA, nome + ".png"))
    print("ok:", ", ".join(ICONES))


if __name__ == "__main__":
    main()
