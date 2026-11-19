"""Line-based logo studies with real alpha transparency, SVG/PNG/ICO exports."""
from pathlib import Path
from PIL import Image, ImageDraw
from generate_logo_studies_v2 import font, paste_icon

OUT = Path(__file__).resolve().parents[1] / 'docs' / 'logo-studies-v3'
S = 4
OPTIONS = [('01-port','线框网口','网口轮廓 + 三根触点'),
           ('02-route-n','流线路由','N 字路径 + 独立端点'),
           ('03-presets','叠层配置','双层线框 + 配置刻度'),
           ('04-bridge','链路桥接','两端接口 + 双向链路')]


def make_icon(index):
    im=Image.new('RGBA',(1024,1024))
    svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">',
         f'<title>{OPTIONS[index][1]} / NetAdapterManager</title>']

    def shape(tag, attrs, draw_fn, color, alpha):
        layer=Image.new('RGBA',im.size)
        draw_fn(ImageDraw.Draw(layer),color+(round(alpha*255),))
        im.alpha_composite(layer)
        rgb='#%02x%02x%02x'%color
        svg.append(f'<{tag} {attrs} {"stroke" if tag=="polyline" else "fill"}="{rgb}" opacity="{alpha}"/>')

    def rect(x,y,w,h,r,color=(38,52,72),alpha=.58):
        shape('rect',f'x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}"',
              lambda d,c:d.rounded_rectangle((x*S,y*S,(x+w)*S,(y+h)*S),r*S,fill=c),color,alpha)

    def line(points,width=7,color=(225,239,251),alpha=1):
        def draw(d,c):
            pts=[(x*S,y*S) for x,y in points]
            d.line(pts,fill=c,width=width*S,joint='curve')
            for x,y in pts:
                r=width*S/2
                d.ellipse((x-r,y-r,x+r,y+r),fill=c)
        shape('polyline',f'points="{" ".join(f"{x},{y}" for x,y in points)}" fill="none" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"',draw,color,alpha)

    blue=(130,192,236)
    # Actual translucent plate and a fine inset keyline; no baked-in background.
    rect(9,9,238,238,46)
    layer=Image.new('RGBA',im.size)
    ImageDraw.Draw(layer).rounded_rectangle((10*S,10*S,246*S,246*S),45*S,outline=(163,201,231,150),width=2*S)
    im.alpha_composite(layer)
    svg.append('<rect x="10" y="10" width="236" height="236" rx="45" fill="none" stroke="#A3C9E7" stroke-opacity="0.59" stroke-width="2"/>')
    # Discrete corner detail adds a second line rhythm without visual clutter.
    line([(34,76),(34,57),(39,45),(51,36),(73,36)],2,blue,.6)
    line([(184,220),(204,220),(218,207),(221,186)],2,blue,.35)
    if index==0:
        line([(70,77),(186,77),(186,153),(158,153),(158,182),(98,182),(98,153),(70,153),(70,77)],8)
        for x in (104,128,152):
            line([(x,98),(x,122)],6,blue)
        line([(115,163),(141,163)],3,blue,.7)
    elif index==1:
        line([(76,178),(76,78),(180,178),(180,78)],9)
        line([(92,82),(164,154)],2,blue,.7)
        for x,y in ((76,78),(180,178)):
            rect(x-10,y-10,20,20,6,blue,1)
        line([(61,177),(61,133)],2,blue,.65)
        line([(195,79),(195,123)],2,blue,.65)
    elif index==2:
        line([(76,165),(62,165),(62,66),(162,66),(162,80)],6,blue,.65)
        line([(88,91),(188,91),(188,191),(88,191),(88,91)],7)
        for y,end in ((116,167),(141,154),(166,167)):
            line([(109,y),(end,y)],5,blue)
        rect(119,109,6,14,3,(225,239,251),1)
        rect(143,134,6,14,3,(225,239,251),1)
    else:
        line([(93,78),(66,78),(66,178),(93,178)],8)
        line([(163,78),(190,78),(190,178),(163,178)],8)
        line([(92,113),(162,113),(150,101)],6,blue)
        line([(164,143),(94,143),(106,155)],6,blue)
        line([(52,101),(52,155)],2,blue,.6)
        line([(204,101),(204,155)],2,blue,.6)
    svg.append('</svg>')
    return im,'\n'.join(svg)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    board=Image.new('RGB',(1600,1190),'#EDF1F6')
    d=ImageDraw.Draw(board)
    d.text((52,28),'NetAdapterManager / 线条 · 半透明',font=font(35,True),fill='#263448')
    d.text((54,83),'真实 Alpha 透明底板 58% · 冰蓝线条 · 深浅背景预览',font=font(20),fill='#506078')
    for i,(slug,title,desc) in enumerate(OPTIONS):
        icon,svg=make_icon(i)
        (OUT/f'{slug}.svg').write_text(svg,encoding='utf-8')
        icon.save(OUT/f'{slug}.png')
        icon.resize((256,256),Image.Resampling.LANCZOS).save(OUT/f'{slug}.ico',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(128,128),(256,256)])
        x,y=48+(i%2)*772,140+(i//2)*510
        d.rounded_rectangle((x,y,x+732,y+482),radius=18,fill='white')
        d.text((x+24,y+18),f'0{i+1}  {title}',font=font(25,True),fill='#263448')
        d.text((x+24,y+59),desc,font=font(16),fill='#506078')
        for j,bg in enumerate(('#DFE8F1','#101720')):
            bx,by=x+24+j*348,y+102
            d.rounded_rectangle((bx,by,bx+336,by+254),radius=12,fill=bg)
            # Background strokes continue behind the icon, revealing transparency.
            for off in (0,70,140,210):
                d.line((bx+12,by+off+20,bx+324,by+off+73),fill='#C5D3E1' if j==0 else '#293B50',width=2)
            paste_icon(board,icon,(bx+66,by+15),204)
            d.text((bx+16,by+226),'浅色背景' if j==0 else '深色背景',font=font(13),fill='#506078' if j==0 else '#94A3BC')
        # Checkerboard strip confirms transparent canvas and partially transparent plate.
        bx,by=x+24,y+372
        for cy in range(0,86,10):
            for cx in range(0,684,10):
                d.rectangle((bx+cx,by+cy,bx+min(cx+9,683),by+min(cy+9,85)),fill='#E3E8EF' if (cx//10+cy//10)%2 else '#F3F5F9')
        d.text((bx+14,by+30),'透明效果',font=font(15),fill='#506078')
        for dx,size in ((136,16),(194,24),(260,32),(336,48),(424,64)):
            paste_icon(board,icon,(bx+dx,by+43-size//2),size)
        d.text((bx+522,by+29),'16 / 24 / 32\n48 / 64 px',font=font(12),fill='#506078')
    board.save(OUT/'comparison.png')
    (OUT/'README.md').write_text('# 线条与半透明 Logo\n\n'
        '四款方案均提供 SVG、1024px PNG、多尺寸 ICO。底板不透明度 58%，画布其余部分完全透明；前景主线保持清晰。\n\n'
        'comparison.png 的斜线背景用于展示透底效果，不属于图标内容。小尺寸适用性需要在最终选型后针对托盘继续精修。\n\n'
        '未替换应用当前图标。生成：`python build/generate_logo_studies_v3.py`。\n',encoding='utf-8')
    print(OUT/'comparison.png')


if __name__=='__main__':
    main()
