"""Six variations of the selected v9 cable/location logo."""
from pathlib import Path
from PIL import Image, ImageDraw
import math
from generate_logo_studies_v2 import font, paste_icon

OUT=Path(__file__).resolve().parents[1]/'docs'/'logo-cable-variants'
OPTIONS=[('01-soft-u','圆润 U 形','原方案细化，圆弧线缆'),
         ('02-compact','紧凑连接','缩短线缆，轮廓更集中'),
         ('03-sweep','流线弧形','长弧线连接，轻盈舒展'),
         ('04-diagonal','斜向连接','斜向插头，更有动势'),
         ('05-square-u','几何折线','直角转折，工具感更强'),
         ('06-dual-trace','双线连接','粗细双线，增加层次')]
BLUE=(69,135,193)
LIGHT=(139,190,229)
S=4


def make_icon(i):
    im=Image.new('RGBA',(1024,1024))
    svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">',f'<title>{OPTIONS[i][1]} / NetAdapterManager</title>']

    def line(points,width=9,color=BLUE,alpha=1):
        layer=Image.new('RGBA',im.size)
        d=ImageDraw.Draw(layer)
        pts=[(round(x*S),round(y*S)) for x,y in points]
        c=color+(round(alpha*255),)
        d.line(pts,fill=c,width=width*S,joint='curve')
        for x,y in pts:
            r=width*S/2
            d.ellipse((x-r,y-r,x+r,y+r),fill=c)
        im.alpha_composite(layer)
        svg.append(f'<polyline points="{" ".join(f"{x:.2f},{y:.2f}" for x,y in points)}" fill="none" stroke="#{color[0]:02x}{color[1]:02x}{color[2]:02x}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round" opacity="{alpha}"/>')

    def dot(x,y,r,color=BLUE,alpha=1):
        layer=Image.new('RGBA',im.size)
        ImageDraw.Draw(layer).ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill=color+(round(alpha*255),))
        im.alpha_composite(layer)
        svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="#{color[0]:02x}{color[1]:02x}{color[2]:02x}" opacity="{alpha}"/>')

    def bezier(p0,p1,p2,p3):
        return [tuple((1-t)**3*p0[k]+3*(1-t)**2*t*p1[k]+3*(1-t)*t*t*p2[k]+t**3*p3[k] for k in (0,1)) for t in (n/48 for n in range(49))]

    def plug(cx=81,top=64,w=56,h=60,angle=0,width=9):
        a=math.radians(angle)
        def tr(p):
            x,y=p[0]-cx,p[1]-(top+h/2)
            return (cx+x*math.cos(a)-y*math.sin(a),top+h/2+x*math.sin(a)+y*math.cos(a))
        pts=[(cx-w/2,top),(cx+w/2,top),(cx+w/2,top+h*.72),(cx+w*.26,top+h*.72),(cx+w*.26,top+h),(cx-w*.26,top+h),(cx-w*.26,top+h*.72),(cx-w/2,top+h*.72),(cx-w/2,top)]
        line([tr(p) for p in pts],width)
        for x in (cx-w*.23,cx,cx+w*.23):
            line([tr((x,top+h*.22)),tr((x,top+h*.4))],4,LIGHT,.9)
        return tr((cx,top+h))

    def pin(cx,top,w=38,h=51,width=7):
        p0=(cx,top+h)
        pts=[p0]
        for p1,p2,p3 in [((cx-w*.28,top+h*.78),(cx-w*.5,top+h*.52),(cx-w*.5,top+h*.32)),
                         ((cx-w*.5,top-h*.1),(cx+w*.5,top-h*.1),(cx+w*.5,top+h*.32)),
                         ((cx+w*.5,top+h*.52),(cx+w*.28,top+h*.78),(cx,top+h))]:
            pts+=bezier(p0,p1,p2,p3)[1:]
            p0=p3
        line(pts,width,LIGHT,.9)
        dot(cx,top+h*.34,5,BLUE)
        return (cx,top+h)

    if i==0:
        p=plug()
        q=pin(182,113)
        line([p,(81,150)]+bezier((81,150),(81,207),(147,210),q)[1:],10)
    elif i==1:
        p=plug(82,68,62,65,width=10)
        q=pin(173,101,36,49)
        line([p,(82,151)]+bezier((82,151),(82,183),(149,188),q)[1:],10)
    elif i==2:
        p=plug(72,67,51,57,width=8)
        q=pin(189,108,35,48,6)
        line(bezier(p,(49,213),(192,232),q),8)
        line(bezier((85,143),(85,180),(131,192),(150,178)),3,LIGHT,.6)
    elif i==3:
        p=plug(93,57,55,62,-38,9)
        q=pin(181,135,37,50)
        line(bezier(p,(72,169),(130,218),q),10)
    elif i==4:
        p=plug(76,62,57,63,width=9)
        q=pin(183,111,38,51)
        line([p,(76,185),(151,185),q],10)
        line([(90,144),(90,171),(123,171)],3,LIGHT,.65)
    else:
        p=plug(79,60,57,62,width=9)
        q=pin(181,107,38,51)
        line([p,(79,153),(106,183),(155,183),q],9)
        line([(93,138),(93,149),(112,169),(149,169),(169,150)],3,LIGHT,.8)
    svg.append('</svg>')
    return im,'\n'.join(svg)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    board=Image.new('RGB',(1740,1150),'#EDF1F6')
    d=ImageDraw.Draw(board)
    d.text((48,27),'NetAdapterManager / 线缆定位 · 六款变体',font=font(34,True),fill='#263448')
    d.text((50,82),'围绕第 2 款细化 · 插头与定位一体连接 · 透明背景与半透明细节',font=font(20),fill='#506078')
    for i,(slug,title,desc) in enumerate(OPTIONS):
        icon,svg=make_icon(i)
        (OUT/f'{slug}.svg').write_text(svg,encoding='utf-8')
        icon.save(OUT/f'{slug}.png')
        icon.resize((256,256),Image.Resampling.LANCZOS).save(OUT/f'{slug}.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
        x,y=36+(i%3)*568,138+(i//3)*497
        d.rounded_rectangle((x,y,x+548,y+476),radius=18,fill='white')
        d.text((x+22,y+18),f'0{i+1}  {title}',font=font(24,True),fill='#263448')
        d.text((x+22,y+59),desc,font=font(17),fill='#506078')
        for j,bg in enumerate(('#F0F4F8','#101720')):
            bx,by=x+18+j*263,y+100
            d.rounded_rectangle((bx,by,bx+249,by+247),radius=12,fill=bg)
            paste_icon(board,icon,(bx+8,by+2),233)
            d.text((bx+12,by+222),'浅色背景' if j==0 else '深色背景',font=font(12),fill='#506078' if j==0 else '#94A3BC')
        bx,by=x+18,y+364
        for cy in range(0,88,11):
            for cx in range(0,512,11):
                d.rectangle((bx+cx,by+cy,bx+min(cx+10,511),by+cy+10),fill='#E1E7EF' if (cx//11+cy//11)%2 else '#F3F5F9')
        for dx,size in ((30,16),(88,24),(155,32),(232,48),(327,64)):
            paste_icon(board,icon,(bx+dx,by+44-size//2),size)
        d.text((bx+420,by+29),'16 / 24 / 32\n48 / 64 px',font=font(11),fill='#506078')
    board.save(OUT/'comparison.png')
    (OUT/'README.md').write_text('# 线缆定位：图 2 的六种变化\n\n'+ '\n'.join(f'- {title}：{desc}。`{slug}.svg` / `.png` / `.ico`。' for slug,title,desc in OPTIONS)+'\n\n透明画布、半透明定位线条，无外框与底板。PNG 为 1024px，ICO 含九种尺寸。未替换当前应用图标。生成：`python build/generate_cable_logo_variants.py`。\n',encoding='utf-8')
    print(OUT/'comparison.png')


if __name__=='__main__':
    main()
