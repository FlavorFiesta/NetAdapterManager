"""New borderless network utility marks, with transparent vector and icon assets."""
from pathlib import Path
from PIL import Image, ImageDraw
from generate_logo_studies_v2 import font, paste_icon

OUT=Path(__file__).resolve().parents[1]/'docs'/'logo-studies-v5'
OPTIONS=[('01-cable','线缆插头','斜向网线与触点，直接表达网络接口'),
         ('02-chip','网卡芯片','芯片引脚与内部线路，突出硬件管理'),
         ('03-branch','配置分流','一条输入，两条配置路径，表达灵活切换'),
         ('04-weave','交织链路','两条折线路径穿插，表达适配与连接')]
BLUE=(76,143,201)
LIGHT=(143,195,234)
DEEP=(55,91,135)
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

    def poly(points,color=LIGHT,alpha=.3):
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
        # Diagonal plug, cropped corner and cable: an object silhouette, no badge.
        body=[(94,112),(148,58),(198,108),(144,162)]
        poly(body,LIGHT,.25)
        line(body+[body[0]],10)
        line([(111,96),(161,146)],5,LIGHT,.85)
        for x,y in ((144,81),(157,94),(170,107)):
            line([(x,y),(x-14,y+14)],5,DEEP,.95)
        line([(119,137),(94,162),(70,162),(54,178),(54,202)],12,BLUE)
        line([(135,153),(110,178),(86,178)],4,LIGHT,.8)
    elif i==1:
        # A diamond die with sparse pin groups, preserving air around the shape.
        diamond=[(128,65),(191,128),(128,191),(65,128),(128,65)]
        poly(diamond,LIGHT,.2)
        line(diamond,9)
        for offset in (0,23):
            line([(87+offset,88-offset),(70+offset,71-offset)],7,LIGHT,.8)
            line([(168+offset,87+offset),(185+offset,70+offset)],7,BLUE)
            line([(169-offset,168+offset),(186-offset,185+offset)],7,LIGHT,.8)
            line([(88-offset,169-offset),(71-offset,186-offset)],7,BLUE)
        line([(102,138),(102,116),(130,116),(130,142),(154,142)],7,BLUE)
        dot(102,138,7,LIGHT)
        dot(154,142,7,BLUE)
    elif i==2:
        line([(48,128),(106,128),(145,84),(193,84)],13,BLUE)
        line([(106,128),(145,172),(193,172)],13,LIGHT,.65)
        line([(170,67),(194,84),(170,101)],8,BLUE)
        line([(170,155),(194,172),(170,189)],8,LIGHT,.65)
        dot(48,128,20,BLUE,.18)
        dot(48,128,9,BLUE)
        dot(106,128,8,DEEP,.85)
        line([(77,103),(91,103)],3,LIGHT,.8)
        line([(77,153),(91,153)],3,LIGHT,.8)
    else:
        # Two offset ribbons, interrupted at crossings to show the weave.
        line([(58,88),(96,88),(160,168),(198,168)],19,BLUE,.9)
        line([(58,168),(96,168),(113,147)],12,LIGHT,.8)
        line([(141,112),(160,88),(198,88)],12,LIGHT,.8)
        line([(62,110),(87,110),(143,180)],3,DEEP,.8)
        line([(113,77),(169,147),(194,147)],3,LIGHT,.7)
        dot(58,88,7,LIGHT)
        dot(198,168,7,BLUE)
    svg.append('</svg>')
    return im,'\n'.join(svg)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    board=Image.new('RGB',(1600,1170),'#EDF1F6')
    d=ImageDraw.Draw(board)
    d.text((52,26),'NetAdapterManager / 新一组 · 网络与线路',font=font(34,True),fill='#263448')
    d.text((54,81),'无外边框 · 无底板 · 透明画布 · 局部半透明',font=font(20),fill='#506078')
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
    (OUT/'README.md').write_text('# 网络与线路 Logo 新提案\n\n无外边框、无底板，透明背景与局部半透明线条。\n\n四个方向：线缆插头、网卡芯片、配置分流、交织链路。每款提供可编辑 SVG、1024px PNG 与多尺寸 ICO。\n\n未替换应用图标。生成：`python build/generate_logo_studies_v5.py`。\n',encoding='utf-8')
    print(OUT/'comparison.png')


if __name__=='__main__':
    main()
