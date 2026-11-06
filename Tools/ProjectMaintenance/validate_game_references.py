"""Read-only checks for resource paths, duplicate scene nodes, script UIDs and optional staged geometry.

Run from any directory:
    python Tools/ProjectMaintenance/validate_game_references.py
    python Tools/ProjectMaintenance/validate_game_references.py --compare-staged-geometry
"""

import argparse
from functools import lru_cache
import os
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "Godot/GodotProject"
GAME = PROJECT / "TheGame"


@lru_cache(maxsize=None)
def directory_names(directory):
    """Read actual names instead of relying on Windows' case-insensitive lookup."""
    return {entry.name.casefold(): entry.name for entry in directory.iterdir()}


def stored_resource_uri(uri):
    """Return a resource's on-disk spelling, or None if it does not exist."""
    directory = PROJECT
    parts = []
    for part in uri[6:].split("/"):
        if not directory.is_dir():
            return None
        actual = directory_names(directory).get(part.casefold())
        if actual is None:
            return None
        parts.append(actual)
        directory /= actual
    return "res://" + "/".join(parts) if directory.is_file() else None


def missing_project_uids(project):
    """Check UID-only settings, including the application icon and autoloads, against source metadata."""
    settings = (project / "project.godot").read_text(encoding="utf-8-sig")
    required = set(re.findall(r'"\*?(uid://[^"\n]+)"', settings))
    for directory, folders, files in os.walk(project):
        folders[:] = [name for name in folders if name != ".godot"]
        for name in files:
            source = Path(directory) / name
            if source.suffix not in {".uid", ".import", ".tscn", ".tres"}:
                continue
            if source.suffix in {".uid", ".import"} and not source.with_suffix("").is_file():
                continue
            with source.open(encoding="utf-8-sig") as metadata:
                header = metadata.read(512)
            if source.suffix in {".tscn", ".tres"}:
                header = header.splitlines()[0]
            # Only the resource's own UID defines it; nested ext_resources are references.
            own_uid = re.search(r'uid://[A-Za-z0-9_]+', header)
            if own_uid:
                required.discard(own_uid[0])
    return sorted(required)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compare-staged-geometry", action="store_true")
    args = parser.parse_args()
    failures = [f"project.godot: missing {uid}" for uid in missing_project_uids(PROJECT)]
    path_count = 0
    for source in GAME.rglob("*"):
        if source.suffix not in {".tscn", ".tres", ".cs"}:
            continue
        if "GameProto" in source.parts and source.name != "ResourcesCollectionConstant.cs":
            continue
        content = source.read_text(encoding="utf-8-sig")
        # Scene properties and C# constants may contain paths without an ext_resource declaration.
        for uri in re.findall(r'"(res://[^"\n]+\.(?:tscn|tres|cs|gd|gdshader|png|ttf|ttc|otf|mp3|wav|ogg))"', content):
            if uri.startswith("res://.godot/") or "{" in uri:
                continue  # Smoke captures and interpolated output names are created at runtime.
            path_count += 1
            stored_uri = stored_resource_uri(uri)
            if stored_uri is None:
                failures.append(f"{source.relative_to(ROOT)}: missing {uri}")
            elif stored_uri != uri:
                failures.append(f"{source.relative_to(ROOT)}: case mismatch {uri}; stored as {stored_uri}")
        if source.suffix == ".tscn":
            # Framework-bound main-pack windows retain the names referenced by read-only UIExtension.
            if source.parent != GAME / "MainPack/UI" and not re.fullmatch(r"[a-z][a-z0-9_]*", source.stem):
                failures.append(f"{source.relative_to(ROOT)}: scene filename must use snake_case")
            # Godot may silently rename duplicates at runtime, while the editor reports a load error.
            nodes = set()
            for header in re.findall(r'(?m)^\[node ([^\n]+)\]$', content):
                name = re.search(r'\bname="([^"\n]+)"', header).group(1)
                parent = re.search(r'\bparent="([^"\n]+)"', header)
                node_path = parent.group(1) + "/" + name if parent else "."
                if node_path in nodes:
                    failures.append(f"{source.relative_to(ROOT)}: duplicate node {node_path}")
                nodes.add(node_path)

    uids = {}
    for source in GAME.rglob("*.uid"):
        uid = source.read_text(encoding="utf-8").strip()
        if uid in uids:
            failures.append(f"Duplicate UID {uid}: {source} / {uids[uid]}")
        uids[uid] = source
        if not Path(str(source)[:-4]).is_file():
            failures.append(f"Orphan UID: {source.relative_to(ROOT)}")

    if args.compare_staged_geometry:
        # Opt-in only: intentional terrain work should not fail normal reference checks.
        scene = "Godot/GodotProject/TheGame/Scenes/Levels/zm1_01_huaguoshan/level_1.tscn"
        tracked = subprocess.check_output(["git", "ls-files", "--", str(Path(scene).parent)],
                                          cwd=ROOT, text=True, encoding="utf-8").splitlines()
        # The user owns the index; before staging the directory move its scene stays at the old flat path.
        staged_scene = next((path for path in tracked if path.casefold() == scene.casefold()), None)
        if staged_scene is None:
            staged_scene = "Godot/GodotProject/TheGame/Scenes/level_1.tscn"
        staged = subprocess.check_output(["git", "show", ":" + staged_scene], cwd=ROOT, text=True, encoding="utf-8")
        current = (ROOT / scene).read_text(encoding="utf-8")
        pattern = r'(?ms)^\[node[^\n]*parent="World/Geometry[^\n]*\n.*?(?=^\[|\Z)'
        staged_nodes = [node.strip() for node in re.findall(pattern, staged)]
        current_nodes = [node.strip() for node in re.findall(pattern, current)]
        if staged_nodes != current_nodes:
            failures.append("Level_1 geometry differs from the current Git index")
        print(f"STAGED GEOMETRY: {len(staged_nodes)} nodes compared")

    for failure in failures:
        print("REFERENCE FAIL: " + failure)
    if failures:
        raise SystemExit(1)
    print(f"REFERENCE PASS: {path_count} serialized paths and {len(uids)} UIDs")


if __name__ == "__main__":
    main()
