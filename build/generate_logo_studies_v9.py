"""Four integrated, borderless network/location logo explorations."""
from pathlib import Path
from PIL import Image, ImageDraw
import math
import generate_logo_studies_v5 as layout

OUT=Path(__file__).resolve().parents[1]/'docs'/'logo-studies-v9'
OPTIONS=[('01-folded-route','折页线路','折页轮廓与网路走线融合，定位点嵌入转折'),
         ('02-cable-point','线缆定位','线缆末端自然形成小定位环，与插头连成一体'),
         ('03-site-layers','场景叠层','不同地点的配置层叠，定位融入前层下沿'),
         ('04-direction-link','方向链路','链路节点与方向指针融合，表达位置与连接')]
BLUE=(69,135,193)
LIGHT=(139,190,229)
DEEP=(54,94,143)
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

    def poly(points,color=LIGHT,alpha=.22):
        layer=Image.new('RGBA',im.size)
        ImageDraw.Draw(layer).polygon([(x*S,y*S) for x,y in points],fill=color+(round(alpha*255),))
        im.alpha_composite(layer)
        svg.append(f'<polygon points="{" ".join(f"{x},{y}" for x,y in points)}" fill="#{color[0]:02x}{color[1]:02x}{color[2]:02x}" opacity="{alpha}"/>')

    def dot(x,y,r,color=BLUE,alpha=1):
        layer=Image.new('RGBA',im.size)
        ImageDraw.Draw(layer).ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill=color+(round(alpha*255),))
        im.alpha_composite(layer)
        svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="#{color[0]:02x}{color[1]:02x}{color[2]:02x}" opacity="{alpha}"/>')

    if i==0:
        poly([(53,85),(100,64),(100,174),(53,195)],LIGHT,.2)
        poly([(153,85),(203,64),(203,174),(153,195)],LIGHT,.22)
        line([(53,195),(53,85),(100,64),(153,85),(203,64),(203,174),(153,195),(100,174),(53,195)],7)
        line([(100,64),(100,174)],4,LIGHT,.8)
        line([(153,85),(153,195)],4,LIGHT,.8)
        line([(72,149),(97,130),(125,143),(151,126)],8,BLUE)
        # The final trace bends into a small location loop.
        line([(151,126),(142,112),(142,101),(151,94),(161,101),(161,112),(151,126)],5,BLUE)
        dot(151,104,3,LIGHT)
    elif i==1:
        # One continuous cable runs from a socket to the location-shaped endpoint.
        line([(54,69),(112,69),(112,114),(99,114),(99,130),(67,130),(67,114),(54,114),(54,69)],9)
        for x in (71,84,97):
            line([(x,82),(x,94)],4,LIGHT,.9)
        line([(83,130),(83,163),(112,189),(153,189),(177,167)],10,BLUE)
        t=[(177,167),(159,142),(159,128),(167,117),(179,113),(192,119),(198,131),(195,144),(177,167)]
        line(t,7,LIGHT,.85)
        dot(178,133,6,BLUE)
        line([(98,145),(98,157),(121,177)],3,LIGHT,.65)
    elif i==2:
        poly([(57,82),(128,44),(199,82),(128,120)],LIGHT,.22)
        line([(57,82),(128,44),(199,82),(128,120),(57,82)],7,LIGHT,.75)
        line([(57,122),(128,160),(199,122)],9,BLUE)
        # Location is a small downward fold in the front configuration layer.
        line([(57,163),(110,191),(128,212),(146,191),(199,163)],10,BLUE)
        line([(57,123),(57,163)],5,LIGHT,.65)
        line([(199,123),(199,163)],5,LIGHT,.65)
        line([(108,81),(126,71),(146,81)],4,BLUE)
        dot(128,189,5,LIGHT)
    else:
        # A network circuit with a shared directional centre, rather than a pin.
        line([(56,88),(93,88),(109,106)],9,BLUE)
        line([(200,168),(163,168),(147,150)],9,BLUE)
        line([(56,168),(94,168),(109,150)],8,LIGHT,.72)
        line([(200,88),(162,88),(147,106)],8,LIGHT,.72)
        poly([(108,148),(125,111),(149,91),(132,145)],LIGHT,.55)
        poly([(108,148),(125,111),(132,129)],BLUE,.95)
        line([(108,148),(125,111),(149,91),(132,145),(108,148)],4,BLUE)
        for x,y in ((56,88),(200,168)):
            dot(x,y,13,BLUE,.18)
            dot(x,y,7,BLUE)
        for x,y in ((56,168),(200,88)):
            dot(x,y,7,LIGHT,.8)
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
    d.text((52,26),'NetAdapterManager / 线路与场景',font=layout.font(34,True),fill='#263448')
    d.text((54,81),'四种新构图 · 位置融入结构 · 透明背景与叠色',font=layout.font(20),fill='#506078')
    board.save(OUT/'comparison.png')
    (OUT/'README.md').write_text('# 线路与场景 Logo\n\n四种新构图：折页线路、线缆定位、场景叠层、方向链路。位置元素融入走线、线缆末端、叠层下沿与方向节点。无方形底板或外围装饰框，透明画布、局部半透明。\n\n每款提供 SVG、1024px PNG 与九种尺寸 ICO，未替换应用图标。\n',encoding='utf-8')
