"""Generate UI framework fields from a scene's explicit root bindings and GGF template."""

import argparse
from pathlib import Path
import re


def project_field_name(match: re.Match) -> str:
    """Translate framework-template fields to the project's Microsoft C# naming convention."""
    name = match[0][2:].replace("_", "")
    last = 0
    while last + 1 < len(name) and name[last + 1].isupper():
        last += 1
    if last > 0 and last + 1 < len(name) and name[last + 1].islower():
        last -= 1
    return "_" + name[:last + 1].lower() + name[last + 1:]


def generate(scene_path: Path, namespace: str, add_bindings: list[str]) -> None:
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
    bindings = {field_name: re.search(rf'^{re.escape(field_name)} = NodePath\("([^"]+)"\)', root_body, re.M)[1]
                for field_name in re.findall(r'"([^"]+)"', binding_header)}
    # Bootstrap new exports before Godot can serialize fields from the next C# build.
    for binding in add_bindings:
        field_name, node_path = binding.split('=', 1)
        if field_name in bindings:
            raise ValueError(f"Binding already exists: {field_name}")
        bindings[field_name] = node_path
    for field_name, node_path in bindings.items():
        for header, body in nodes[1:]:
            node_name = re.search(r'name="([^"]+)"', header)[1]
            parent_path = re.search(r'parent="([^"]+)"', header)[1]
            full_path = node_name if parent_path == "." else f"{parent_path}/{node_name}"
            if full_path != node_path:
                continue
            type_match = re.search(r'type="([^"]+)"', header)
            if type_match is None:
                # Child scenes keep their root script/type; generated bindings must use that concrete widget.
                instance_id = re.search(r'instance=ExtResource\("([^"]+)"\)', header)[1]
                instance = resources[instance_id].read_text(encoding="utf-8")
                instance_header, instance_body = re.findall(r'\[node ([^\n]+)\]\n(.*?)(?=\n\[|\Z)', instance, re.S)[0]
                field_type = re.search(r'type="([^"]+)"', instance_header)[1]
                instance_script = re.search(r'^script = ExtResource\("([^"]+)"\)', instance_body, re.M)
                if instance_script:
                    script_path = next(path for path, resource_id in re.findall(
                        r'\[ext_resource[^\n]*path="([^"]+)"[^\n]*id="([^"]+)"\]', instance)
                        if resource_id == instance_script[1])
                    script = (project / script_path.removeprefix("res://")).read_text(encoding="utf-8")
                    field_type = re.search(r'public\s+(?:\w+\s+)*class\s+(\w+)', script)[1]
                    imported_namespaces.add(re.search(r'namespace\s+([\w.]+)', script)[1])
            else:
                field_type = type_match[1]
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
    output = re.sub(r"\bm_[A-Za-z0-9_]+\b", project_field_name, output)
    root_script_id = re.search(r'^script = ExtResource\("([^"]+)"\)', root_body, re.M)[1]
    output_path = resources[root_script_id]
    output_path.write_text(output, encoding="utf-8", newline="\r\n")
    print(f"Generated {output_path.relative_to(project)}: {len(fields)} scene bindings")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scene", type=Path)
    parser.add_argument("--namespace", default="GameLogic")
    parser.add_argument("--add-binding", action="append", default=[], metavar="FIELD=NODE_PATH",
                        help="Generate a new export before the next build; bind and save it in Godot afterwards.")
    args = parser.parse_args()
    generate(args.scene, args.namespace, args.add_binding)
