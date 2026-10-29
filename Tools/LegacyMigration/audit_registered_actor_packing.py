"""Compare registered actor/effect atlases with all official source frames."""
import argparse
import json
import math
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw


def unpack(layer, atlas, cell):
    """Restore the unchanged virtual canvas from a regular or compact atlas."""
    width, height = layer['cell_size']
    if not layer.get('atlas_regions'):
        x, y = cell % layer['columns'] * width, cell // layer['columns'] * height
        return atlas.crop((x, y, x + width, y + height))
    entry = layer['atlas_regions'][str(cell)]
    x, y, w, h = entry['region']
    actual = Image.new('RGBA', (width, height))
    actual.paste(atlas.crop((x, y, x + w, y + h)), tuple(entry['margin'][:2]))
    return actual


def audit(args):
    """Verify visible RGBA and registration without allowing image-difference tolerance."""
    data = json.loads(args.manifest.read_text(encoding='utf-8'))
    actors = data['actors'] + data.get('effects', [])
    actions = sum(len(actor['actions']) for actor in actors)
    contact = Image.new('RGBA', (1200, math.ceil(actions / 5) * 240), '#293139')
    draw = ImageDraw.Draw(contact)
    samples = 0
    tile = 0
    for actor in actors:
        layer = actor['layers'][0]
        width, height = layer['cell_size']
        origin_x = layer['sprite_offset'][0] - width / 2
        origin_y = layer['sprite_offset'][1] - height / 2
        atlas = Image.open(layer['output']).convert('RGBA')
        for action in actor['actions']:
            cells = layer['poses'][action['name']]
            for frame, cell in enumerate(cells, 1):
                source = Image.open(Path(action['input']) / f'{frame}.png').convert('RGBA')
                expected = Image.new('RGBA', (width, height))
                expected.paste(source, (math.floor(action['bounds'][0] + action['matrix'][4] - origin_x),
                                        math.floor(action['bounds'][1] + action['matrix'][5] - origin_y)))
                actual = unpack(layer, atlas, cell)
                # Transparent RGB outside the original visible image is not a gameplay pixel.
                assert all(a[3] == b[3] and (not a[3] or a == b)
                           for a, b in zip(actual.getdata(), expected.getdata())), (actor['name'], action['name'], frame)
                samples += 1
            image = unpack(layer, atlas, cells[len(cells) // 2])
            scale = min(1, 210 / width, 185 / height)
            image = image.resize((round(width * scale), round(height * scale)))
            tile_x, tile_y = tile % 5 * 240, tile // 5 * 240
            # Center the unchanged virtual canvas within the contact tile, not each
            # visible pose. Remote effects can have their source root outside it.
            image_x = tile_x + (240 - image.width) // 2
            image_y = tile_y + 35 + (195 - image.height) // 2
            if actor.get('blend_mode') == 'add':
                # Black is zero light in Flash's ADD mode, not an opaque plate.
                # Keep the original raster bytes and apply the native blend in
                # the review sheet instead of color-keying away source pixels.
                background = contact.crop((image_x, image_y, image_x + image.width, image_y + image.height))
                alpha = image.getchannel('A')
                light = Image.merge('RGB', tuple(ImageChops.multiply(image.getchannel(channel), alpha)
                                                for channel in ('R', 'G', 'B')))
                blended = ImageChops.add(background.convert('RGB'), light).convert('RGBA')
                contact.paste(blended, (image_x, image_y))
            else:
                contact.alpha_composite(image, (image_x, image_y))
            root_x, root_y = round(image_x - origin_x * scale), round(image_y - origin_y * scale)
            if tile_x + 5 <= root_x < tile_x + 235 and tile_y + 35 <= root_y < tile_y + 235:
                draw.line((root_x - 5, root_y, root_x + 5, root_y), fill='#00cbe3')
                draw.line((root_x, root_y - 5, root_x, root_y + 5), fill='#00cbe3')
            else:
                draw.text((tile_x + 4, tile_y + 224), 'source root outside canvas', fill='#00cbe3')
            draw.text((tile_x + 4, tile_y + 4), actor['name'] + '\n' + action['name'], fill='white')
            tile += 1
    args.output.mkdir(parents=True, exist_ok=True)
    contact.save(args.output / 'registered_contact.png')
    report = {'source_frames_compared': samples, 'failures': 0,
              'manifest': str(args.manifest.resolve()), 'source_sha256': data['source_sha256']}
    (args.output / 'registered_packing_report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(f'REGISTERED PACKING PASS: {samples} source frames preserve pixels and source origin')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    audit(parser.parse_args())
