"""Network-first marks with a small secondary location indicator."""
from pathlib import Path
from PIL import Image, ImageDraw
import generate_logo_studies_v5 as layout

OUT=Path(__file__).resolve().parents[1]/'docs'/'logo-studies-v7'
OPTIONS=[('01-port-location','网口 · 地点标记','网口为主体，右下角仅保留小定位标记'),
         ('02-switch-location','切换 · 地点标记','双向路径为主体，定位作为辅助信息'),
         ('03-network-location','节点 · 地点标记','网络拓扑为主体，位置标记移至侧边'),
         ('04-controls-location','配置 · 地点标记','配置滑杆为主体，定位缩为角落点缀')]
BLUE=(76,143,201)
LIGHT=(143,195,234)
DEEP=(55,91,135)
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

    if i==0:
        line([(60,67),(178,67),(178,145),(149,145),(149,178),(89,178),(89,145),(60,145),(60,67)],11)
        for x in (92,119,146):
            line([(x,88),(x,111)],7,LIGHT,.85)
        line([(103,157),(135,157)],3,LIGHT,.7)
    elif i==1:
        line([(55,91),(184,91)],13)
        line([(163,70),(184,91),(163,112)],11)
        line([(180,151),(51,151)],13,LIGHT,.72)
        line([(72,130),(51,151),(72,172)],11,LIGHT,.72)
        line([(91,113),(147,129)],3,DEEP,.7)
    elif i==2:
        line([(119,87),(119,127),(63,127),(63,169)],9)
        line([(119,127),(175,127),(175,169)],9,LIGHT,.75)
        for x,y in ((119,72),(63,180),(175,180)):
            dot(x,y,19,BLUE,.18)
            dot(x,y,10,BLUE if x!=175 else LIGHT,.95)
        dot(119,127,6,DEEP,.9)
    else:
        for y in (75,118,161):
            line([(59,y),(179,y)],9,BLUE,.95)
        for x,y in ((91,75),(149,118),(107,161)):
            dot(x,y,15,LIGHT,.4)
            dot(x,y,9,LIGHT,.95)
        line([(60,184),(135,184)],3,DEEP,.6)

    # Same small location mark on each option, detached from the main silhouette.
    cx,top,w,h=210,174,29,39
    p0=(cx,top+h)
    pts=[p0]
    segs=[((cx-w*.28,top+h*.78),(cx-w*.5,top+h*.52),(cx-w*.5,top+h*.32)),
          ((cx-w*.5,top-h*.1),(cx+w*.5,top-h*.1),(cx+w*.5,top+h*.32)),
          ((cx+w*.5,top+h*.52),(cx+w*.28,top+h*.78),(cx,top+h))]
    for p1,p2,p3 in segs:
        for n in range(1,21):
            t=n/20
            pts.append(tuple((1-t)**3*p0[k]+3*(1-t)**2*t*p1[k]+3*(1-t)*t*t*p2[k]+t**3*p3[k] for k in (0,1)))
        p0=p3
    line(pts,4,LIGHT,.7)
    dot(cx,top+13,4,LIGHT,.7)
    svg.append('</svg>')
    return im,'\n'.join(svg)


if __name__=='__main__':
    layout.OUT=OUT
    layout.OPTIONS=OPTIONS
    layout.make_icon=make_icon
    layout.main()
    board=Image.open(OUT/'comparison.png')
    d=ImageDraw.Draw(board)
    d.rectangle((0,0,1600,125),fill='#EDF1F6')
    d.text((52,26),'NetAdapterManager / 网络为主 · 位置为辅',font=layout.font(34,True),fill='#263448')
    d.text((54,81),'位置标记缩至角落 · 降低线宽与不透明度 · 保留透明背景',font=layout.font(20),fill='#506078')
    board.save(OUT/'comparison.png')
    (OUT/'README.md').write_text('# 网络为主，位置为辅\n\n将位置图钉缩为 29×39 坐标单位的角落标记，使用较细线条与 70% 不透明度。网络主体为网口、双向切换、拓扑节点和配置滑杆。\n\n每款提供 SVG、1024px PNG 和多尺寸 ICO。未替换应用图标。\n',encoding='utf-8')
