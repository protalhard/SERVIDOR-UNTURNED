from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
import random
random.seed(11)
INTER="/usr/share/fonts/opentype/inter/Inter-Black.otf"
MONO="/usr/share/fonts/truetype/jetbrains-mono/JetBrainsMono-ExtraBold.ttf"
OUT="/home/user/servidor-unturned/branding/"
C=(255,90,0); C2=(255,176,32); FUNDO=(5,5,6); TIT=(245,246,247)

def distress(mask, amount=0.10, scratches=14, seed=0):
    rnd=random.Random(seed)
    w,h=mask.size
    noise=Image.effect_noise((w,h),90).filter(ImageFilter.GaussianBlur(1.6))
    noise=noise.point(lambda v:255 if v>int(255*(0.5+amount)) else 0)
    m=ImageChops.subtract(mask,noise)
    d=ImageDraw.Draw(m)
    for _ in range(scratches):
        x=rnd.randint(0,w); y=rnd.randint(0,h); L=rnd.randint(w//10,w//3)
        d.line([(x,y),(x+L,y+rnd.randint(-L//4,L//4))],fill=0,width=rnd.randint(2,4))
    for _ in range(int(w*h/900)):
        x=rnd.randint(0,w); y=rnd.randint(0,h); r=rnd.randint(1,3)
        d.ellipse([x,y,x+r,y+r],fill=0)
    return m

def logo(text_main, size, out_w, out_h, path, sub=None):
    im=Image.new("RGBA",(out_w,out_h),FUNDO+(255,))
    gl=Image.new("RGBA",(out_w,out_h),(0,0,0,0)); gd=ImageDraw.Draw(gl)
    gd.ellipse([out_w*0.1,out_h*0.05,out_w*0.9,out_h*0.95],fill=C+(70,))
    im.alpha_composite(gl.filter(ImageFilter.GaussianBlur(out_w//5)))
    f=ImageFont.truetype(INTER,size)
    # main word: "Day" white + "Z" orange, tight tracking, slight skew
    mask_w=Image.new("L",(out_w,out_h),0); mask_z=Image.new("L",(out_w,out_h),0)
    dw=ImageDraw.Draw(mask_w); dz=ImageDraw.Draw(mask_z)
    a,b=text_main
    wa=dw.textlength(a,font=f); wb=dw.textlength(b,font=f)
    x0=(out_w-(wa+wb-size*0.05))/2; y=out_h*0.42 if sub else out_h/2
    dw.text((x0,y),a,font=f,fill=255,anchor="lm")
    dz.text((x0+wa-size*0.05,y),b,font=f,fill=255,anchor="lm")
    amt=0.08 if out_w>400 else 0.30; sc=10 if out_w>400 else 2
    mask_w=distress(mask_w,amt,sc,1); mask_z=distress(mask_z,amt*(0.6 if out_w>400 else 1.3),sc//2,2)
    # glow under Z
    zg=Image.new("RGBA",(out_w,out_h),C+(0,)); zg.putalpha(mask_z.filter(ImageFilter.GaussianBlur(size/8)))
    im.alpha_composite(zg); im.alpha_composite(zg)
    white=Image.new("RGBA",(out_w,out_h),TIT+(255,)); white.putalpha(mask_w)
    orange=Image.new("RGBA",(out_w,out_h),C+(255,)); orange.putalpha(mask_z)
    im.alpha_composite(white); im.alpha_composite(orange)
    # slash scratch across (claw marks) in orange
    d=ImageDraw.Draw(im)
    if sub:
        fs=ImageFont.truetype(MONO,int(size*0.24))
        sm=Image.new("L",(out_w,out_h),0); ImageDraw.Draw(sm).text((out_w/2,out_h*0.88),sub,font=fs,fill=255,anchor="mm")
        sm=distress(sm,0.30,0,3)
        y2=Image.new("RGBA",(out_w,out_h),C2+(255,)); y2.putalpha(sm); im.alpha_composite(y2)
    return im

# icon 256: rounded dark square, "DZ"
ic=logo(("D","Z"),165,256,256,None,sub=None)
mask=Image.new("L",(256,256),0); ImageDraw.Draw(mask).rounded_rectangle([0,0,255,255],radius=56,fill=255)
out=Image.new("RGBA",(256,256),(0,0,0,0)); out.paste(ic,(0,0),mask)
ImageDraw.Draw(out).rounded_rectangle([2,2,253,253],radius=54,outline=C+(255,),width=5)
out.save(OUT+"icon.png"); out.resize((64,64),Image.LANCZOS).save(OUT+"icon64.png")
# standalone logo
lg=logo(("Day","Z"),300,1200,520,None,sub="BRASIL OFICIAL")
lg.save(OUT+"logo.png")
print("ok")
