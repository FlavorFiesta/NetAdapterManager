"""Borderless, transparent logo studies with new line-based silhouettes."""
from pathlib import Path
from PIL import Image, ImageDraw
from generate_logo_studies_v2 import font, paste_icon

OUT=Path(__file__).resolve().parents[1]/'docs'/'logo-studies-v4'
OPTIONS=[('01-flow','流转链路','两条回转路径，表达网络配置切换'),
         ('02-prism-n','折线 N','折线路径与半透明交叠，形成字母 N'),
         ('03-signal','信号脉冲','横向链路与中心脉冲，表达连接状态'),
         ('04-orbit','环绕节点','两条开放弧线围绕节点，表达网络协同')]
BLUE=(90,164,221)
LIGHT=(159,207,241)
DEEP=(60,103,153)
S=4


def make_icon(i):
    im=Image.new('RGBA',(1024,1024))
    svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">',f'<title>{OPTIONS[i][1]} / NetAdapterManager</title>']

    def line(points,width=12,color=BLUE,alpha=1):
        layer=Image.new('RGBA',im.size)
        d=ImageDraw.Draw(layer)
        pts=[(x*S,y*S) for x,y in points]
        fill=color+(round(255*alpha),)
        d.line(pts,fill=fill,width=width*S,joint='curve')
        for x,y in pts:
            r=width*S/2
            d.ellipse((x-r,y-r,x+r,y+r),fill=fill)
        im.alpha_composite(layer)
        svg.append(f'<polyline points="{" ".join(f"{x},{y}" for x,y in points)}" fill="none" stroke="#{color[0]:02x}{color[1]:02x}{color[2]:02x}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round" opacity="{alpha}"/>')

    def dot(x,y,r,color=BLUE,alpha=1):
        layer=Image.new('RGBA',im.size)
        ImageDraw.Draw(layer).ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill=color+(round(alpha*255),))
        im.alpha_composite(layer)
        svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="#{color[0]:02x}{color[1]:02x}{color[2]:02x}" opacity="{alpha}"/>')

    if i==0:
        line([(59,102),(86,65),(165,65),(194,94),(194,120)],15,BLUE)
        line([(175,106),(194,125),(213,106)],12,BLUE)
        line([(197,154),(170,191),(91,191),(62,162),(62,136)],15,LIGHT,.72)
        line([(43,150),(62,131),(81,150)],12,LIGHT,.72)
        line([(103,108),(153,148)],5,DEEP,.65)
        dot(103,108,7,LIGHT)
        dot(153,148,7,BLUE)
    elif i==1:
        line([(63,191),(63,65),(193,191)],24,BLUE,.86)
        line([(63,65),(193,191),(193,65)],12,LIGHT,.8)
        line([(84,191),(84,119)],4,DEEP,.75)
        line([(172,65),(172,137)],4,BLUE,.7)
        dot(63,65,9,LIGHT)
        dot(193,191,9,BLUE)
    elif i==2:
        line([(45,132),(88,132),(108,84),(143,176),(166,120),(211,120)],13,BLUE)
        line([(45,151),(76,151)],4,LIGHT,.7)
        line([(180,101),(211,101)],4,LIGHT,.7)
        dot(45,132,17,BLUE,.22)
        dot(45,132,7,BLUE)
        dot(211,120,17,LIGHT,.3)
        dot(211,120,7,LIGHT)
        line([(112,65),(126,65)],4,DEEP,.65)
        line([(130,195),(144,195)],4,LIGHT,.7)
    else:
        # Open elliptical paths rather than an enclosing frame.
        import math
        def arc(start,end,rx,ry,tilt):
            pts=[]
            for step in range(49):
                t=math.radians(start+(end-start)*step/48)
                a=math.radians(tilt)
                x,y=rx*math.cos(t),ry*math.sin(t)
                pts.append((round(128+x*math.cos(a)-y*math.sin(a),2),round(128+x*math.sin(a)+y*math.cos(a),2)))
            return pts
        p=arc(25,290,90,43,-38)
        q=arc(50,302,90,43,38)
        line(q,11,LIGHT,.6)
        line(p,11,BLUE,.95)
        dot(*p[0],9,BLUE)
        dot(*q[-1],9,LIGHT,.85)
        dot(128,128,22,BLUE,.18)
        dot(128,128,9,BLUE)
    svg.append('</svg>')
    return im,'\n'.join(svg)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    board=Image.new('RGB',(1600,1170),'#EDF1F6')
    d=ImageDraw.Draw(board)
    d.text((52,26),'NetAdapterManager / 无边框 · 悬浮线条',font=font(34,True),fill='#263448')
    d.text((54,81),'全透明画布 / 局部半透明叠色 / 四个新方向',font=font(20),fill='#506078')
    for i,(slug,title,desc) in enumerate(OPTIONS):
        icon,svg=make_icon(i)
        (OUT/f'{slug}.svg').write_text(svg,encoding='utf-8')
        icon.save(OUT/f'{slug}.png')
        icon.resize((256,256),Image.Resampling.LANCZOS).save(OUT/f'{slug}.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
        x,y=48+(i%2)*772,138+(i//2)*503
        d.rounded_rectangle((x,y,x+732,y+476),radius=18,fill='white')
        d.text((x+24,y+18),f'0{i+1}  {title}',font=font(25,True),fill='#263448')
        d.text((x+24,y+58),desc,font=font(17),fill='#506078')
        for j,bg in enumerate(('#F0F4F8','#101720')):
            bx,by=x+24+j*348,y+100
            d.rounded_rectangle((bx,by,bx+336,by+248),radius=12,fill=bg)
            paste_icon(board,icon,(bx+54,by+2),228)
            d.text((bx+14,by+220),'浅色背景' if j==0 else '深色背景',font=font(13),fill='#506078' if j==0 else '#94A3BC')
        bx,by=x+24,y+364
        for cy in range(0,88,11):
            for cx in range(0,684,11):
                d.rectangle((bx+cx,by+cy,bx+min(cx+10,683),by+cy+10),fill='#E1E7EF' if (cx//11+cy//11)%2 else '#F3F5F9')
        d.text((bx+14,by+31),'透明预览',font=font(15),fill='#506078')
        for dx,size in ((138,16),(196,24),(260,32),(334,48),(420,64)):
            paste_icon(board,icon,(bx+dx,by+44-size//2),size)
        d.text((bx+522,by+29),'16 / 24 / 32\n48 / 64 px',font=font(12),fill='#506078')
    board.save(OUT/'comparison.png')
    (OUT/'README.md').write_text('# 无边框 Logo 新提案\n\n全透明画布，无外边框、无方形底板。局部线条和节点使用半透明叠色。\n\n四个方向：流转链路、折线 N、信号脉冲、环绕节点。每款提供可编辑 SVG、1024px PNG 与多尺寸 ICO。\n\n未替换应用图标。生成：`python build/generate_logo_studies_v4.py`。\n',encoding='utf-8')
    print(OUT/'comparison.png')


if __name__=='__main__':
    main()
