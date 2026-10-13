"""Copy ground consumable artwork verbatim from the legacy project."""
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[2]
LEGACY = Path('P:/Godot-Project/ZMXY_BHYH/Art/ItemIcon')
DESTINATION = ROOT / 'Godot/GodotProject/TheGame/Sprites/Items'
MAPPING = {
    'peach.png': ['consumables/pickup_small_peach.png', 'consumables/pickup_large_peach.png'],
    'Bble.png': ['consumables/pickup_mana_bottle.png'],
}


def main():
    """Preserve asset bytes and refuse to replace a manually changed destination."""
    for source, targets in MAPPING.items():
        for target in targets:
            destination = DESTINATION / target
            destination.parent.mkdir(parents=True, exist_ok=True)
            if destination.exists() and destination.read_bytes() != (LEGACY / source).read_bytes():
                raise RuntimeError(f'Refusing to overwrite changed asset: {destination}')
            if not destination.exists():
                shutil.copyfile(LEGACY / source, destination)
            print(f'{source} -> {target}')


if __name__ == '__main__':
    main()
