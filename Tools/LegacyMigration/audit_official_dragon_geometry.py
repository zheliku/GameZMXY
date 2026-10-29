"""Read Dragon King source rectangles while respecting Flash clipping depths.

Rendering canvases can include a 1536px tiled bitmap behind a 192px mask. Those
unclipped storage bounds are never suitable as native gameplay hit rectangles.
The resulting report is input to native authoring only, not a runtime format.
"""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as E

from export_official_wukong import matrix
from export_official_sea_monsters import compose, merge
from prepare_official_garuda_level import _probe_reader


def intersect(first, second):
    """Intersect two world-space source rectangles, retaining empty masks."""
    if first is None or second is None:
        return None
    x,y=max(first[0],second[0]),max(first[1],second[1])
    right,bottom=min(first[0]+first[2],second[0]+second[2]),min(first[1]+first[3],second[1]+second[3])
    return [x,y,right-x,bottom-y] if right>x and bottom>y else None


def main(args):
    """Preserve source parent matrices and mask lifetimes for all six attacks."""
    data=json.loads(args.manifest.read_text(encoding='utf-8'))
    tags=E.parse(args.xml).getroot().find('tags')
    definitions={int(t.get('spriteId') or t.get('shapeId')):t for t in tags if t.get('spriteId') or t.get('shapeId')}
    frames, raw_bounds=_probe_reader(tags)

    def clipped(character, frame, transform):
        if definitions[character].get('type')!='DefineSpriteTag':
            return raw_bounds(character,frame,transform)
        history=frames(character)
        display=history[frame%len(history)]
        held=frame if len(history)==1 else 0
        regions=[]
        masks=[]
        for child,age in sorted(display,key=lambda item:int(item[0].get('depth'))):
            if not child.get('characterId'):
                continue
            depth=int(child.get('depth'))
            masks=[(end,box) for end,box in masks if end>=depth]
            box=clipped(int(child.get('characterId')),age+held,compose(transform,matrix(child)))
            if child.get('clipDepth'):
                masks.append((int(child.get('clipDepth')),box))
                continue
            for _,mask in masks:
                box=intersect(box,mask)
            regions.append(box)
        return merge(regions)

    report={'source_sha256':data['source_sha256'],'actor':{},'effects':{}}
    for action in data['actors'][0]['actions']:
        boxes=[]
        for display in frames(action['sprite']):
            boxes.append(merge([clipped(int(child.get('characterId')),age,compose(action['matrix'],matrix(child)))
                for child,age in display if child.get('name')=='stick']))
        report['actor'][action['name']]=boxes
    for record in data['effects']:
        if record['name']=='dragon_king_ice_aura':
            continue
        count=record['actions'][0]['frames']
        report['effects'][record['name']]=[clipped(record['sprite'],frame,[1,0,0,1,0,0])
            if frame<count-1 else None for frame in range(count)]
    args.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    for name,boxes in (list(report['actor'].items())+list(report['effects'].items())):
        valid=[box for box in boxes if box]
        if valid:
            print(name,len(valid),'maximum size',max(b[2] for b in valid),max(b[3] for b in valid))
    print('DRAGON CLIPPED GEOMETRY SOURCE PASS: source rectangles and clipping-depth intersections')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for key in ('manifest','xml','output'):
        parser.add_argument('--'+key,type=Path,required=True)
    main(parser.parse_args())
