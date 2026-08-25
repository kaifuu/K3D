#!/bin/bash
# 离线编译检查(工程被编辑器锁住时用):csc 全量编译 Assets 下所有 C#
P=/f/98_eyasclaw/03_3d
UROOT="/c/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data"
mkdir -p "$P/Temp"
# 只引 *Module.dll 真定义:门面 UnityEngine.dll/UnityEditor.dll 里有缺成员的旧类型副本,
# 会与 CoreModule 打架(ReflectionProbe 枚举解析失败即此因)
ls "$UROOT"/Managed/UnityEngine/*.dll | grep -E '(UnityEngine|UnityEditor)\.[A-Za-z0-9]+Module\.dll$' | cygpath -m -f - | sed 's/^/-r:"/; s/$/"/' > "$P/Temp/pc.rsp"
NETSTD=$(find "$UROOT/NetStandard" -name 'netstandard.dll' | head -1)
echo "-r:\"$(cygpath -m "$NETSTD")\"" >> "$P/Temp/pc.rsp"
find "$P/Assets" -name '*.cs' | cygpath -m -f - | sed 's/^/"/; s/$/"/' >> "$P/Temp/pc.rsp"
"$UROOT/DotNetSdk/dotnet.exe" exec "$UROOT/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" \
  -nologo -target:library -langversion:9.0 -define:UNITY_EDITOR -nowarn:0169,0649,0414 \
  -out:"$(cygpath -m "$P/Temp/pc.dll")" @"$(cygpath -m "$P/Temp/pc.rsp")" && echo COMPILE_OK
