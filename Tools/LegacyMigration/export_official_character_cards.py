"""Render the four original SelectRole vector cards with FFDec; copy PNGs verbatim.

Input is the read-only decoded OtherMat_v9.swf documented in
Docs/OfficialHuaguoshan.md. No ActionScript or SWF program is copied into Godot.
"""

import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import tempfile


def export_cards(java: Path, ffdec: Path, swf: Path) -> None:
    """Render complete cards, including their masks, ink frames and baked names."""
    game = Path(__file__).resolve().parents[2] / "Godot/GodotProject/TheGame"
    with tempfile.TemporaryDirectory(prefix="gamezmxy-select-role-") as temporary:
        # Aloader places bytes 100..109 before bytes 0..99, then appends the rest.
        content = swf.read_bytes()
        if content[:3] not in (b"FWS", b"CWS", b"ZWS"):
            content = content[100:110] + content[:100] + content[110:]
        if hashlib.sha256(content).hexdigest() != "fa7681aad99fb06f50228530b7f70f6334a1d7052a7bb6ef2ce3c3d81867fd5e":
            raise ValueError("Expected the documented OtherMat_v9.swf; refusing a different source")
        readable = Path(temporary) / "other_mat_readable.swf"
        readable.write_bytes(content)
        subprocess.run([
            str(java), f"-Duser.home={temporary}", "-jar", str(ffdec),
            "-selectid", "511,515,518,521", "-ignorebackground",
            "-export", "sprite", temporary, str(readable),
        ], check=True)
        for name, sprite_id in [("wukong", 511), ("sanzang", 521), ("bajie", 515), ("wujing", 518)]:
            source = Path(temporary) / f"DefineSprite_{sprite_id}/1.png"
            destination = game / f"Sprites/UI/frontend/character_{name}_card.png"
            shutil.copyfile(source, destination)
            print(f"DefineSprite {sprite_id} -> {destination.relative_to(game)}")
        # Complete cards already include the frame; remove the obsolete overlay.
        for name in ("character_brush_frame.png", "character_brush_frame.png.import"):
            (game / "Sprites/UI/frontend" / name).unlink(missing_ok=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--java", type=Path, required=True)
    parser.add_argument("--ffdec", type=Path, required=True, help="FFDec 26.3.0 ffdec.jar")
    parser.add_argument("--swf", type=Path, required=True, help="Original or decoded OtherMat_v9.swf, kept outside the project")
    args = parser.parse_args()
    export_cards(args.java, args.ffdec, args.swf)
