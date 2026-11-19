"""Transparent location + networking logo studies."""
from PIL import Image, ImageDraw
from pathlib import Path
import math
import generate_logo_studies_v5 as layout

OPTIONS=[('01-location-port','地点网口','位置图钉内嵌网口，表达不同地点的网络配置'),
         ('02-place-switch','地点切换','两个地点与切换路径，表达办公场景切换'),
         ('03-waypoints','路径站点','位置图钉连接两个站点，表达网络路径'),
         ('04-location-controls','位置配置','图钉与配置滑杆结合，表达按地点管理预设')]
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
        fill=color+(round(255*alpha),)
        d.line(pts,fill=fill,width=width*S,joint='curve')
        for x,y in pts:
            r=width*S/2
            d.ellipse((x-r,y-r,x+r,y+r),fill=fill)
        im.alpha_composite(layer)
        svg.append(f'<polyline points="{" ".join(f"{x:.2f},{y:.2f}" for x,y in points)}" fill="none" stroke="#{color[0]:02x}{color[1]:02x}{color[2]:02x}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round" opacity="{alpha}"/>')

    def circle(x,y,r,width=7,color=BLUE,alpha=1,fill=False):
        layer=Image.new('RGBA',im.size)
        d=ImageDraw.Draw(layer)
        c=color+(round(alpha*255),)
        d.ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill=c if fill else None,outline=None if fill else c,width=width*S)
        im.alpha_composite(layer)
        rgb=f'#{color[0]:02x}{color[1]:02x}{color[2]:02x}'
        # Pillow places the outline inside the supplied box.
        svg.append(f'<circle cx="{x}" cy="{y}" r="{r if fill else r-width/2}" fill="{rgb if fill else "none"}" stroke="{rgb if not fill else "none"}" stroke-width="{width}" opacity="{alpha}"/>')

    def pin(cx,top,w,h,color=BLUE,alpha=1,width=9):
        # Four cubic segments make a familiar teardrop marker.
        start=(cx,top+h)
        segs=[((cx-w*.28,top+h*.78),(cx-w*.5,top+h*.52),(cx-w*.5,top+h*.32)),
              ((cx-w*.5,top-h*.1),(cx+w*.5,top-h*.1),(cx+w*.5,top+h*.32)),
              ((cx+w*.5,top+h*.52),(cx+w*.28,top+h*.78),(cx,top+h))]
        pts=[start]
        p0=start
        for p1,p2,p3 in segs:
            for n in range(1,31):
                t=n/30
                pts.append(tuple((1-t)**3*p0[k]+3*(1-t)**2*t*p1[k]+3*(1-t)*t*t*p2[k]+t**3*p3[k] for k in (0,1)))
            p0=p3
        line(pts,width,color,alpha)

    if i==0:
        pin(128,47,120,160,width=10)
        circle(128,207,28,color=LIGHT,alpha=.2,fill=True)
        line([(95,91),(161,91),(161,130),(144,130),(144,146),(112,146),(112,130),(95,130),(95,91)],6,LIGHT,.95)
        for x in (113,128,143):
            line([(x,101),(x,113)],4,BLUE)
        line([(84,218),(172,218)],3,LIGHT,.55)
    elif i==1:
        pin(78,48,73,108,BLUE,1,9)
        circle(78,83,13,5,LIGHT,.9)
        pin(180,94,73,108,LIGHT,.68,9)
        circle(180,129,13,5,BLUE,.9)
        line([(58,181),(103,181),(131,153)],7,BLUE)
        line([(117,154),(131,153),(130,168)],6,BLUE)
        line([(195,67),(150,67),(128,89)],6,LIGHT,.7)
        line([(128,74),(128,89),(143,89)],5,LIGHT,.7)
    elif i==2:
        # A large location marker with a branching network route beneath it.
        pin(128,43,91,120,BLUE,1,9)
        circle(128,83,18,6,LIGHT,.9)
        line([(128,164),(128,186),(67,186),(67,210)],7,BLUE)
        line([(128,186),(189,186),(189,210)],7,LIGHT,.7)
        circle(67,215,12,color=BLUE,fill=True)
        circle(189,215,12,color=LIGHT,alpha=.7,fill=True)
        circle(128,186,6,color=DEEP,fill=True)
    else:
        pin(114,45,112,158,BLUE,1,9)
        circle(114,91,18,6,LIGHT,.85)
        # Controls overlap only the lower right edge, keeping the pin recognizable.
        for y,x in ((140,163),(165,188),(190,164)):
            line([(145,y),(211,y)],6,DEEP,.8)
            circle(x,y,9,color=LIGHT,alpha=.85,fill=True)
        line([(72,217),(135,217)],3,LIGHT,.6)
    svg.append('</svg>')
    return im,'\n'.join(svg)


if __name__=='__main__':
    layout.OUT=Path(__file__).resolve().parents[1]/'docs'/'logo-studies-v6'
    layout.OPTIONS=OPTIONS
    layout.make_icon=make_icon
    layout.main()
    # Give this set its own accurate comparison title and description.
    board=Image.open(layout.OUT/'comparison.png')
    d=ImageDraw.Draw(board)
    d.rectangle((0,0,1600,125),fill='#EDF1F6')
    d.text((52,26),'NetAdapterManager / 位置 × 网络',font=layout.font(34,True),fill='#263448')
    d.text((54,81),'新一组地点图标 · 无外边框与底板 · 局部半透明',font=layout.font(20),fill='#506078')
    board.save(layout.OUT/'comparison.png')
    (layout.OUT/'README.md').write_text('# 位置与网络 Logo\n\n四款新方向：地点网口、地点切换、路径站点、位置配置。无外边框、无底板，透明画布与局部半透明。每款提供 SVG、1024px PNG 和九种尺寸 ICO。未替换应用图标。\n\n生成：`python build/generate_logo_studies_v6.py`。\n',encoding='utf-8')
