#!/bin/bash
# 离线编译检查(工程被编辑器锁住时用):csc 全量编译 Assets 下所有 C#
P=/f/98_eyasclaw/03_3d
UROOT="/c/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data"
mkdir -p "$P/Temp"
ls "$UROOT"/Managed/UnityEngine/*.dll | cygpath -m -f - | sed 's/^/-r:"/; s/$/"/' > "$P/Temp/pc.rsp"
NETSTD=$(find "$UROOT/NetStandard" -name 'netstandard.dll' | head -1)
echo "-r:\"$(cygpath -m "$NETSTD")\"" >> "$P/Temp/pc.rsp"
find "$P/Assets" -name '*.cs' | cygpath -m -f - | sed 's/^/"/; s/$/"/' >> "$P/Temp/pc.rsp"
"$UROOT/DotNetSdk/dotnet.exe" exec "$UROOT/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" \
  -nologo -target:library -langversion:9.0 -define:UNITY_EDITOR -nowarn:0169,0649,0414 \
  -out:"$(cygpath -m "$P/Temp/pc.dll")" @"$(cygpath -m "$P/Temp/pc.rsp")" && echo COMPILE_OK
