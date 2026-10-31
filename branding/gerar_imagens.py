from PIL import Image, ImageDraw, ImageFont, ImageFilter
import random
random.seed(3)
INTER="/usr/share/fonts/opentype/inter/Inter-Black.otf"
INTERB="/usr/share/fonts/opentype/inter/Inter-Bold.otf"
MONO="/usr/share/fonts/truetype/jetbrains-mono/JetBrainsMono-ExtraBold.ttf"
OUT="/home/user/servidor-unturned/branding/"
C=(255,90,0); C2=(255,176,32); CF=(255,122,46); FUNDO=(5,5,6); TIT=(245,246,247); TXT=(201,205,211); FRACO=(138,144,153)

def glow_text(base,xy,t,font,fill,glow=C,radius=16,anchor="lm",strength=2):
    layer=Image.new("RGBA",base.size,(0,0,0,0)); ld=ImageDraw.Draw(layer)
    ld.text(xy,t,font=font,fill=glow+(255,),anchor=anchor)
    g=layer.filter(ImageFilter.GaussianBlur(radius))
    for _ in range(strength): base.alpha_composite(g)
    ImageDraw.Draw(base).text(xy,t,font=font,fill=fill,anchor=anchor)

# ---------- ICON ----------
S=256
ic=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(ic)
d.rounded_rectangle([0,0,S-1,S-1],radius=56,fill=C)
# bolt watermark
d.polygon([(144,24),(64,140),(116,140),(104,232),(192,108),(136,108)],fill=(255,122,46))
f=ImageFont.truetype(INTER,120)
d.text((S/2,S/2-8),"DZ",font=f,fill=(26,26,26),anchor="mm")
f2=ImageFont.truetype(MONO,30)
d.text((S/2,S-40),"BRASIL",font=f2,fill=(26,26,26),anchor="mm")
ic.save(OUT+"icon.png"); ic.resize((64,64),Image.LANCZOS).save(OUT+"icon64.png")

# ---------- CAPA 1280x720 ----------
W,H=1280,720
im=Image.new("RGBA",(W,H),FUNDO+(255,)); d=ImageDraw.Draw(im,"RGBA")
# grid
ov=Image.new("RGBA",(W,H),(0,0,0,0)); od=ImageDraw.Draw(ov)
for x in range(0,W,40): od.line([(x,0),(x,H)],fill=(255,255,255,9))
for y in range(0,H,40): od.line([(0,y),(W,y)],fill=(255,255,255,9))
im.alpha_composite(ov)
# orange glows
gl=Image.new("RGBA",(W,H),(0,0,0,0)); gd=ImageDraw.Draw(gl)
gd.ellipse([700,-200,1500,500],fill=C+(90,)); gd.ellipse([-300,450,500,1000],fill=C+(50,))
im.alpha_composite(gl.filter(ImageFilter.GaussianBlur(140)))
d=ImageDraw.Draw(im,"RGBA")
# big bolt on right
bolt=[(980,70),(820,360),(930,360),(890,640),(1110,300),(990,300)]
bl=Image.new("RGBA",(W,H),(0,0,0,0)); bd=ImageDraw.Draw(bl); bd.polygon(bolt,fill=C+(255,))
im.alpha_composite(bl.filter(ImageFilter.GaussianBlur(30)))
d=ImageDraw.Draw(im,"RGBA"); d.polygon(bolt,fill=(26,14,6,255),outline=C+(255,)); d.line(bolt+[bolt[0]],fill=C+(255,),width=5)
# tag pill
fm=ImageFont.truetype(MONO,26)
ov=Image.new("RGBA",(W,H),(0,0,0,0)); od=ImageDraw.Draw(ov)
od.rounded_rectangle([70,92,372,138],radius=23,fill=(255,90,0,28),outline=(255,90,0,115),width=2)
im.alpha_composite(ov); d=ImageDraw.Draw(im)
d.ellipse([88,108,102,122],fill=(91,227,125))
d.text((114,115),"[BR] ONLINE 24/7",font=fm,fill=C2,anchor="lm")
fT=ImageFont.truetype(INTER,170)
glow_text(im,(62,265),"DAY Z",fT,C,radius=22)
fB=ImageFont.truetype(INTER,84)
d=ImageDraw.Draw(im,"RGBA")
d.text((70,395),"BRASIL OFICIAL",font=fB,fill=TIT,anchor="lm")
fM=ImageFont.truetype(MONO,38)
d.text((72,470),"// CALIFORNIA 2",font=fM,fill=C2,anchor="lm")
# feature chips
feats=["SAFE ZONE","ATM","BLACK MARKET","CAÇA","AIRDROPS","PVP"]
fF=ImageFont.truetype(INTERB,26)
x=70; y=560
for t in feats:
    w=d.textlength(t,font=fF)+44
    ov=Image.new("RGBA",(W,H),(0,0,0,0)); od=ImageDraw.Draw(ov)
    od.rounded_rectangle([x,y,x+w,y+56],radius=18,fill=(22,23,26,215),outline=(255,90,0,140),width=2)
    im.alpha_composite(ov); d=ImageDraw.Draw(im)
    d.text((x+w/2,y+28),t,font=fF,fill=TIT,anchor="mm")
    x+=w+14
# footer
fs=ImageFont.truetype(MONO,20)
d.text((W-40,H-30),"powered by SNAPLAG",font=fs,fill=FRACO,anchor="rm")
d.rectangle([0,0,W,6],fill=C)
im.convert("RGB").save(OUT+"capa.jpg",quality=92); im.save(OUT+"capa.png")
print("ok")
