"""Export native static 2-2 terrain layers; traps remain separate animated nodes."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as E
from export_official_battle_menu import shape_sprite


def export(args):
    """Retain each official terrain shape's own registration point and alpha."""
    args.work.mkdir(parents=True, exist_ok=True)
    profile=args.work/'profile'
    profile.mkdir(exist_ok=True)
    tree=E.parse(args.xml)
    tags=tree.getroot().find('tags')
    entries=[]
    for sid,name in [(115,'qixing_ground'),(116,'qixing_upper_platform'),(117,'qixing_lower_platform')]:
        source=next(t for t in tags if t.get('shapeId')==str(sid))
        bounds=source.find('shapeBounds')
        entries.append({'name':name,'sprite':31000+sid,'position':[int(bounds.get('Xmin'))/20,int(bounds.get('Ymin'))/20]})
        tags.insert(len(tags)-1,shape_sprite(sid,31000+sid))
    tree.write(args.work/'terrain.xml',encoding='utf-8',xml_declaration=True)
    command=[str(args.java),f'-Duser.home={args.work}','-jar',str(args.ffdec)]
    env=dict(os.environ,APPDATA=str(profile.resolve()))
    with (args.work/'export.log').open('w',encoding='utf8') as log:
        subprocess.run(command+['-xml2swf',str(args.work/'terrain.xml'),str(args.work/'terrain.swf')],env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
        subprocess.run(command+['-selectid',','.join(str(e['sprite']) for e in entries),'-ignorebackground','-export','sprite',str(args.work/'png'),str(args.work/'terrain.swf')],env=env,stdout=log,stderr=subprocess.STDOUT,check=True)
    args.output.mkdir(parents=True,exist_ok=True)
    for entry in entries:
        folder=next(p for p in (args.work/'png').iterdir() if p.name.split('_')[1]==str(entry['sprite']))
        source=folder/'1.png'
        target=args.output/(entry['name']+'.png')
        if target.exists() and target.read_bytes()!=source.read_bytes():
            raise ValueError(f'Refusing to replace edited terrain {target}')
        shutil.copyfile(source,target)
        entry['path']='res://TheGame/Sprites/Levels/zm1_05_qixing_cave/'+target.name
    (args.work/'layout.json').write_text(json.dumps(entries,indent=2)+'\n',encoding='utf8')
    print('QIXING TERRAIN EXPORT PASS: three independent terrain layers')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('xml','java','ffdec','work','output'):parser.add_argument('--'+name,type=Path,required=True)
    export(parser.parse_args())
