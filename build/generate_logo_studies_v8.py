"""Integrated network/location marks: shared geometry, no corner badges."""
from pathlib import Path
from PIL import Image, ImageDraw
import math
import generate_logo_studies_v5 as layout

OUT=Path(__file__).resolve().parents[1]/'docs'/'logo-studies-v8'
OPTIONS=[('01-port-point','网口落点','网口底部融入定位尖角，共用一个轮廓'),
         ('02-route-point','线路落点','定位点接入连续线路，成为路径的一部分'),
         ('03-node-point','位置节点','用小定位节点替代拓扑中心，共用连接线'),
         ('04-switch-point','切换落点','切换路径穿过定位中心，形成一体结构')]
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

    def ring(x,y,r,width=5,color=LIGHT,alpha=.9):
        line([(x+r*math.cos(n*math.pi/40),y+r*math.sin(n*math.pi/40)) for n in range(81)],width,color,alpha)

    def pin(cx,top,w,h,width=7,color=BLUE,alpha=1):
        p0=(cx,top+h)
        pts=[p0]
        for p1,p2,p3 in [((cx-w*.28,top+h*.78),(cx-w*.5,top+h*.52),(cx-w*.5,top+h*.32)),
                         ((cx-w*.5,top-h*.1),(cx+w*.5,top-h*.1),(cx+w*.5,top+h*.32)),
                         ((cx+w*.5,top+h*.52),(cx+w*.28,top+h*.78),(cx,top+h))]:
            for n in range(1,25):
                t=n/24
                pts.append(tuple((1-t)**3*p0[k]+3*(1-t)**2*t*p1[k]+3*(1-t)*t*t*p2[k]+t**3*p3[k] for k in (0,1)))
            p0=p3
        line(pts,width,color,alpha)

    if i==0:
        # Location is a small change to the socket silhouette, never a separate pin.
        line([(65,65),(191,65),(191,147),(158,147),(158,171),(128,200),(98,171),(98,147),(65,147),(65,65)],11)
        for x in (100,128,156):
            line([(x,89),(x,114)],7,LIGHT,.85)
        line([(111,153),(145,153)],4,LIGHT,.8)
        dot(128,175,5,LIGHT,.9)
    elif i==1:
        # The pin is a single waypoint interrupting and reconnecting a circuit trace.
        line([(52,169),(52,128),(105,128)],10,BLUE)
        line([(151,128),(204,128),(204,87)],10,BLUE)
        pin(128,95,46,65,7)
        ring(128,116,8,4)
        line([(128,160),(128,182),(167,182)],6,LIGHT,.65)
        dot(52,174,13,BLUE,.23)
        dot(52,174,7,BLUE)
        dot(204,82,13,LIGHT,.3)
        dot(204,82,7,LIGHT)
        line([(72,110),(92,110)],3,LIGHT,.7)
        line([(164,146),(184,146)],3,LIGHT,.7)
    elif i==2:
        # The small pin IS the central junction, connecting all three adapters.
        line([(128,74),(128,105)],9,BLUE)
        line([(128,153),(128,171),(66,171),(66,194)],9,BLUE)
        line([(128,171),(190,171),(190,194)],9,LIGHT,.72)
        pin(128,104,42,49,7)
        dot(128,120,5,LIGHT)
        for x,y in ((128,61),(66,202),(190,202)):
            dot(x,y,18,BLUE,.18)
            dot(x,y,10,LIGHT if x==190 else BLUE,.95)
    else:
        # The upper route shares the pin's horizontal centre, the lower route its tip.
        line([(52,103),(107,103)],11,BLUE)
        line([(149,103),(204,103)],11,BLUE)
        line([(184,83),(204,103),(184,123)],9,BLUE)
        pin(128,85,42,69,7)
        dot(128,106,5,LIGHT,.95)
        line([(204,154),(128,154),(52,154)],10,LIGHT,.72)
        line([(72,134),(52,154),(72,174)],9,LIGHT,.72)
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
    d.text((52,26),'NetAdapterManager / 位置融入网络',font=layout.font(34,True),fill='#263448')
    d.text((54,81),'共用轮廓与连接线 · 网络为主 · 无底板 · 半透明细节',font=layout.font(20),fill='#506078')
    board.save(OUT/'comparison.png')
    (OUT/'README.md').write_text('# 位置融入网络\n\n位置元素参与主体结构，不再使用独立角标。\n\n01 网口落点：定位尖角融入网口轮廓。\n02 线路落点：位置作为线路中的站点。\n03 位置节点：位置替代拓扑中心节点。\n04 切换落点：位置与双向切换路径共用连接点。\n\n透明背景，局部半透明。每款提供 SVG、1024px PNG 与九种尺寸 ICO。未替换应用图标。\n',encoding='utf-8')
