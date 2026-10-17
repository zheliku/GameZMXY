"""Import only frontend artwork/music; the legacy project remains read-only.

Images and music are copied verbatim. No legacy scripts, scenes or import
metadata are copied. Character cards use export_official_character_cards.py.
"""

from pathlib import Path
import math
import shutil
import struct
import wave

ROOT = Path(__file__).resolve().parents[2]
LEGACY = Path('P:/Godot-Project/ZMXY_BHYH')
GAME = ROOT / 'Godot/GodotProject/TheGame'
MAPPING = {
    'Art/MainGame/Bg1.png': 'Sprites/UI/frontend/title_backdrop.png',
    'Art/game_pic/183.png': 'Sprites/UI/frontend/journey_map.png',
    'Music/MainSceneMusic/zmxy_1BGM.mp3': 'Audios/Music/map_theme.mp3',
    'Music/level/1_music.mp3': 'Audios/Music/huaguoshan_theme.mp3',
    'Art/game_pic/679.PNG': 'Sprites/UI/frontend/menu_separator.png',
    'Art/set_menu/653.png': 'Sprites/UI/frontend/settings_scroll.png',
    'Art/BackPack/49.png': 'Sprites/UI/frontend/menu_close.png',
    'Art/MagicWeapon/222.png': 'Sprites/UI/frontend/menu_close_hover.png',
    'Art/Game_2/Button/ButtonReturnMainMenu.png': 'Sprites/UI/frontend/map_return_title.png',
    'Art/game_pic/level_choose/294.png': 'Sprites/UI/frontend/map_save_game.png',
    'Art/game_pic/ArchivePicture/87.png': 'Sprites/UI/frontend/save_slots_backdrop.png',
    'Art/HeroPicture/RoleProperiesBox/718.png': 'Sprites/UI/hud/hud_ws_frame.png',
    'Art/MainGame/BasicPicCd_1.png': 'Sprites/UI/frontend/save_slot_normal.png',
    'Art/MainGame/BasicPicCd_2.png': 'Sprites/UI/frontend/save_slot_hover.png',
    'Font/Aa我读山月山月独我_mianfeiziti.com.ttf': 'MainPack/Fonts/aa_shan_yue.ttf',
    'Font/Aa文徵明琴赋小楷_mianfeiziti.com.ttf': 'MainPack/Fonts/aa_wen_zheng_ming.ttf',
}
for name, normal, hover in [
    ('huaguoshan', '185', '188'), ('shuiliandong', '192', '195'),
    ('taohuayuan', '199', '202'), ('bianfuku', '206', '209'),
    ('qixingdong', '220', '223'), ('jialuoluo', '213', '216'),
    ('qianan', 'qa', 'ShallowShore'), ('donghai', 'dh', 'EastSea'),
    ('shazhiwoliu', '267', 'SharkVortex'), ('niumogu', '234', '237')]:
    for state, filename in [('normal', normal), ('hover', hover)]:
        folder = 'Art/game_pic' if filename in ('qa', 'dh') else 'Art/game_pic/level_choose'
        MAPPING[f'{folder}/{filename}.png'] = f'Sprites/UI/frontend/map_{name}_{state}.png'
for name, normal, hover, caption in [
    ('inventory', '757', '759', '756'), ('settings', '751', '753', '750'),
    ('skills', '763', '765', '762'), ('weapon', '769', '771', '768'),
    ('pet', '775', '777', '774')]:
    for state, filename in [('normal', normal), ('hover', hover), ('caption', caption)]:
        MAPPING[f'Art/HeroPicture/RoleProperiesBox/{filename}.png'] = f'Sprites/UI/hud/menu_{name}_{state}.png'
for source, target in MAPPING.items():
    destination = GAME / target
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not destination.exists():
        shutil.copyfile(LEGACY / source, destination)
    elif destination.read_bytes() != (LEGACY / source).read_bytes():
        raise RuntimeError(f'Refusing to overwrite changed asset: {destination}')
    print(f'{source} -> {target}')

# Original short wood-like UI cue, generated here so its provenance is explicit.
cue = GAME / 'Audios/UI/menu_confirm.wav'
cue.parent.mkdir(parents=True, exist_ok=True)
if not cue.exists():
    rate = 44100
    samples = [0.22 * math.exp(-t * 65) * (math.sin(2 * math.pi * 880 * t) +
               0.35 * math.sin(2 * math.pi * 1760 * t)) for t in (i / rate for i in range(int(rate * 0.12)))]
    with wave.open(str(cue), 'wb') as output:
        output.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        output.writeframes(b''.join(struct.pack('<h', int(value * 32767)) for value in samples))
