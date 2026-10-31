from PIL import Image, ImageDraw, ImageFont, ImageFilter
import random, math
random.seed(7)
F="/usr/share/fonts/opentype/inter/Inter-Black.otf"
import os
if not os.path.exists(F): F="/usr/share/fonts/opentype/inter/Inter-ExtraBold.otf"
OUT="/home/user/servidor-unturned/branding/"
GREEN=(0,156,59); YEL=(255,223,0); RED=(200,16,16); BLUE=(0,39,118)

def grunge(img, n=1800, alpha=40):
    d=ImageDraw.Draw(img,"RGBA")
    w,h=img.size
    for _ in range(n):
        x,y=random.randrange(w),random.randrange(h); r=random.randint(1,3)
        c=random.choice([(0,0,0,alpha),(255,255,255,alpha//3)])
        d.ellipse([x,y,x+r,y+r],fill=c)

def text_outline(d,xy,t,f,fill,stroke=(0,0,0),sw=6,anchor="mm"):
    d.text(xy,t,font=f,fill=fill,stroke_width=sw,stroke_fill=stroke,anchor=anchor)

# ---------- ICON 256 ----------
S=256
ic=Image.new("RGB",(S,S),(14,18,14))
d=ImageDraw.Draw(ic,"RGBA")
# radial red glow
glow=Image.new("RGBA",(S,S),(0,0,0,0)); g=ImageDraw.Draw(glow)
for r in range(130,0,-4):
    a=int(110*(1-r/130)); g.ellipse([S/2-r,S/2-r,S/2+r,S/2+r],fill=(150,0,0,a))
ic.paste(glow,(0,0),glow)
# brazil diamond behind
d.polygon([(S/2,22),(S-22,S/2),(S/2,S-22),(22,S/2)],fill=(0,156,59,200))
d.ellipse([S/2-62,S/2-62,S/2+62,S/2+62],fill=(0,39,118,220))
f=ImageFont.truetype(F,128)
text_outline(d,(S/2-4,S/2-6),"DZ",f,YEL,sw=8)
f2=ImageFont.truetype(F,34)
d.rectangle([0,S-46,S,S],fill=(150,0,0,235))
text_outline(d,(S/2,S-23),"BRASIL",f2,(255,255,255),sw=3)
d.rectangle([2,2,S-3,S-3],outline=YEL,width=5)
grunge(ic,900,50)
ic.save(OUT+"icon.png"); ic.resize((64,64),Image.LANCZOS).save(OUT+"icon64.png")

# ---------- THUMBNAIL / CAPA 1280x720 ----------
W,H=1280,720
im=Image.new("RGB",(W,H),(8,10,8))
d=ImageDraw.Draw(im,"RGBA")
# sky gradient dark red -> black
for y in range(H):
    t=y/H; c=(int(70*(1-t)+8),int(10*(1-t)+8),int(8*(1-t)+8)); d.line([(0,y),(W,y)],fill=c)
# blood moon
for r in range(170,0,-3):
    a=int(140*(1-r/170)**1.5); d.ellipse([920-r,170-r,920+r,170+r],fill=(200,30,20,a))
d.ellipse([850,100,990,240],fill=(170,25,15,255))
for _ in range(25):
    cx_,cy_=random.randint(870,970),random.randint(120,220); rr=random.randint(4,14); d.ellipse([cx_-rr,cy_-rr,cx_+rr,cy_+rr],fill=(130,15,10,160))
# dead trees / city silhouette
for i in range(0,W,6):
    h=random.randint(40,90)+ (120 if random.random()<0.06 else 0)
    d.rectangle([i,H-140-h,i+6,H],fill=(5,6,5,255))
for _ in range(14):
    x=random.randint(0,W); y0=H-140
    hh=random.randint(160,300)
    d.line([(x,y0),(x+random.randint(-10,10),y0-hh)],fill=(5,6,5),width=7)
    for k in range(6):
        yy=y0-random.randint(60,hh); L=random.randint(30,80); s=random.choice([-1,1])
        d.line([(x,yy),(x+s*L,yy-random.randint(10,50))],fill=(5,6,5),width=4)
# ground
d.rectangle([0,H-140,W,H],fill=(6,7,6))
# fog
fog=Image.new("RGBA",(W,H),(0,0,0,0)); fg=ImageDraw.Draw(fog)
for _ in range(60):
    x=random.randint(-200,W); y=random.randint(H-260,H-80); w=random.randint(200,500)
    fg.ellipse([x,y,x+w,y+60],fill=(120,120,110,18))
fog=fog.filter(ImageFilter.GaussianBlur(18)); im.paste(fog,(0,0),fog)
d=ImageDraw.Draw(im,"RGBA")
# brazil stripe
d.rectangle([0,40,W,52],fill=GREEN); d.rectangle([0,52,W,58],fill=YEL)
f1=ImageFont.truetype(F,46); text_outline(d,(70,140),"[BR]",f1,(255,255,255),sw=4,anchor="lm")
fT=ImageFont.truetype(F,150)
text_outline(d,(70,265),"DAY Z",fT,(230,20,20),sw=10,anchor="lm")
fB=ImageFont.truetype(F,92)
text_outline(d,(70,390),"BRASIL",fB,YEL,sw=8,anchor="lm")
text_outline(d,(470,390),"OFICIAL",fB,(255,255,255),sw=8,anchor="lm")
fM=ImageFont.truetype(F,40)
text_outline(d,(72,470),"CALIFORNIA 2",fM,(0,220,90),sw=4,anchor="lm")
# feature bar
bar_y=H-95
d.rectangle([0,bar_y,W,H],fill=(0,0,0,200))
d.rectangle([0,bar_y,W,bar_y+4],fill=(200,16,16))
feats=["SAFE ZONE","ATM","MERCADO NEGRO","CAÇA","AIRDROPS","PVP"]
fF=ImageFont.truetype(F,30)
ws=[d.textlength(t,font=fF) for t in feats]
gap=(W-sum(ws))/(len(feats)+1)
x=gap
for i,t in enumerate(feats):
    col=(0,230,90) if i%2==0 else YEL
    text_outline(d,(x+ws[i]/2,bar_y+50),t,fF,col,sw=3)
    if i: d.ellipse([x-gap/2-5,bar_y+45,x-gap/2+5,bar_y+55],fill=(200,16,16))
    x+=ws[i]+gap
grunge(im,6000,45)
# vignette
vig=Image.new("L",(W,H),0); vd=ImageDraw.Draw(vig)
for i in range(60):
    vd.rectangle([i*4,i*3,W-i*4,H-i*3],outline=255-i*4)
im.save(OUT+"capa.png"); im.convert("RGB").save(OUT+"capa.jpg",quality=90)
print("ok")
