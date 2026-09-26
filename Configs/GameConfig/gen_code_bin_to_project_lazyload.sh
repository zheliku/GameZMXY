#!/bin/bash

cd "$(dirname "$0")"
echo "当前目录: $(pwd)"

export WORKSPACE="$(realpath ../../)"
export LUBAN_DLL="${WORKSPACE}/Tools/Luban/Luban.dll"
export CONF_ROOT="$(pwd)"
export DATA_OUTPATH="${WORKSPACE}/Godot/GodotProject/TheGame/DataTables/GameConfigs"
export CODE_OUTPATH="${WORKSPACE}/Godot/GodotProject/TheGame/GameScripts/GameProto/GameConfig/"

cp -R "${CONF_ROOT}/CustomTemplate/ConfigSystem.cs" \
   "${WORKSPACE}/Godot/GodotProject/TheGame/GameScripts/GameProto/ConfigSystem.cs"

dotnet "${LUBAN_DLL}" \
    -t client \
    -c cs-bin \
    -d bin \
    --conf "${CONF_ROOT}/luban.conf" \
    --customTemplateDir "${CONF_ROOT}/CustomTemplate/CustomTemplate_Client_LazyLoad" \
    -x code.lineEnding=crlf \
    -x outputCodeDir="${CODE_OUTPATH}" \
    -x outputDataDir="${DATA_OUTPATH}"
