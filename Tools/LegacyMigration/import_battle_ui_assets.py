"""Copy settlement/backpack artwork verbatim; legacy scenes and scripts stay read-only."""

from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
LEGACY = Path('P:/Godot-Project/ZMXY_BHYH')
GAME = ROOT / 'Godot/GodotProject/TheGame'
MAPPING = {
    'Art/Level/Settlement/623.png': 'Sprites/UI/settlement/settlement_backdrop.png',
    'Art/Level/Settlement/637.png': 'Sprites/UI/settlement/settlement_success.png',
    'Art/Level/Settlement/622.png': 'Sprites/UI/settlement/settlement_failure.png',
    'Art/Level/Settlement/436.png': 'Sprites/UI/settlement/settlement_results.png',
    'Art/Level/Settlement/630.png': 'Sprites/UI/settlement/settlement_map_normal.png',
    'Art/Level/Settlement/632.png': 'Sprites/UI/settlement/settlement_map_hover.png',
    'Art/Level/Settlement/457.png': 'Sprites/UI/settlement/settlement_retry_normal.png',
    'Art/Level/Settlement/459.png': 'Sprites/UI/settlement/settlement_retry_hover.png',
    'Art/BackPack/BackPackLight.png': 'Sprites/UI/inventory/inventory_preview_light.png',
    'Art/BackPack/207.png': 'Sprites/UI/inventory/inventory_panels.png',
    'Art/BackPack/28.png': 'Sprites/UI/inventory/inventory_ink_frame.png',
    'Art/BackPack/113.png': 'Sprites/UI/inventory/inventory_cell.png',
    'Art/BackPack/244.png': 'Sprites/UI/inventory/inventory_attributes.png',
    'Art/BackPack/219.png': 'Sprites/UI/inventory/inventory_level_badge.png',
    'Art/BackPack/zdlnc.png': 'Sprites/UI/inventory/inventory_name_plate.png',
    'Art/BackPack/lh_wk.png': 'Sprites/UI/inventory/inventory_soul_plate.png',
    'Art/BackPack/209.png': 'Sprites/UI/inventory/inventory_exp_fill.png',
    'Art/BackPack/211.png': 'Sprites/UI/inventory/inventory_exp_frame.png',
    'Art/BackPack/123.png': 'Sprites/UI/inventory/inventory_page_one.png',
    'Art/BackPack/118.png': 'Sprites/UI/inventory/inventory_page_two.png',
    'Art/BackPack/zb_button.png': 'Sprites/UI/inventory/inventory_tab_normal.png',
    'Art/BackPack/zb_button_choose.png': 'Sprites/UI/inventory/inventory_tab_selected.png',
}
for name, filename in [('weapon', 'wq'), ('armor', 'fj'), ('accessory', 'sp'),
                       ('treasure', 'fb'), ('costume', 'sz'), ('title', 'tx')]:
    MAPPING[f'Art/BackPack/{filename}.png'] = f'Sprites/UI/inventory/inventory_slot_{name}.png'


def main():
    """Never replace an existing asset whose contents have been edited."""
    for source, target in MAPPING.items():
        destination = GAME / target
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists() and destination.read_bytes() != (LEGACY / source).read_bytes():
            raise RuntimeError(f'Refusing to overwrite changed asset: {destination}')
        if not destination.exists():
            shutil.copyfile(LEGACY / source, destination)
        print(f'{source} -> {target}')


if __name__ == '__main__':
    main()
