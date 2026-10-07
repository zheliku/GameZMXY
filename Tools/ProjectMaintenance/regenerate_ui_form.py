"""Generate UI framework fields from a scene's explicit root bindings and GGF template."""

import argparse
from pathlib import Path
import re


def generate(scene_path: Path, namespace: str) -> None:
    """Keep business partials untouched and derive field types from scene scripts."""
    project = Path(__file__).resolve().parents[2] / "Godot/GodotProject"
    scene = scene_path.read_text(encoding="utf-8")
    resources = dict(re.findall(r'\[ext_resource[^\n]*path="([^"]+)"[^\n]*id="([^"]+)"\]', scene))
    resources = {resource_id: project / path.removeprefix("res://") for path, resource_id in resources.items()}
    nodes = re.findall(r'\[node ([^\n]+)\]\n(.*?)(?=\n\[|\Z)', scene, re.S)
    root_header, root_body = nodes[0]
    class_name = re.search(r'name="([^"]+)"', root_header)[1]
    parent_type = re.search(r'type="([^"]+)"', root_header)[1]
    binding_header = re.search(r'node_paths=PackedStringArray\((.*?)\)', root_header)[1]
    fields = []
    imported_namespaces = set()
    for field_name in re.findall(r'"([^"]+)"', binding_header):
        node_path = re.search(rf'^{re.escape(field_name)} = NodePath\("([^"]+)"\)', root_body, re.M)[1]
        for header, body in nodes[1:]:
            node_name = re.search(r'name="([^"]+)"', header)[1]
            parent_path = re.search(r'parent="([^"]+)"', header)[1]
            full_path = node_name if parent_path == "." else f"{parent_path}/{node_name}"
            if full_path != node_path:
                continue
            field_type = re.search(r'type="([^"]+)"', header)[1]
            script_id = re.search(r'^script = ExtResource\("([^"]+)"\)', body, re.M)
            if script_id:
                script = resources[script_id[1]].read_text(encoding="utf-8")
                script_namespace = re.search(r'namespace\s+([\w.]+)', script)[1]
                script_class = re.search(r'public\s+(?:\w+\s+)*class\s+(\w+)', script)[1]
                field_type = script_class
                if script_namespace != namespace:
                    imported_namespaces.add(script_namespace)
            fields.append(f"\t\t[Export]\n\t\tprivate {field_type} {field_name};")
            break
        else:
            raise ValueError(f"Missing root binding: {field_name} -> {node_path}")

    template_path = project / "Framework/GodotGameFrameworkCore/Templet/UIFormTemplet.txt"
    template = template_path.read_text(encoding="utf-8")
    output = template.replace("_NAMESPACE_", namespace).replace("_PARENT_", parent_type)
    output = output.replace("_CLASSNAME_", class_name).replace("_CHILDNODES_", "\n".join(fields))
    output = "".join(f"using {name};\n" for name in sorted(imported_namespaces)) + output
    root_script_id = re.search(r'^script = ExtResource\("([^"]+)"\)', root_body, re.M)[1]
    output_path = resources[root_script_id]
    output_path.write_text(output, encoding="utf-8", newline="\r\n")
    print(f"Generated {output_path.relative_to(project)}: {len(fields)} scene bindings")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scene", type=Path)
    parser.add_argument("--namespace", default="GameLogic")
    args = parser.parse_args()
    generate(args.scene, args.namespace)
