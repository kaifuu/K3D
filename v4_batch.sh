#!/bin/bash
# V4 总验收:11 模式全部无头跑批(每模式 EXIT 0 = 通过)。
# shots 末点 = 各模式剧本最后一个 sc.At 时刻(防截图截断陷阱:SimRunner 在
# 最后一个截图时刻+余量后退出,更晚的断言会静默不触发):
#   env78 manual95 route76 recon44 tactics46 fault55 full61 formation48 regulator57 battle62
#   combat 默认拦截剧本 4 断言 ≤20s,40s 为 -scenario 逃逸变体不在默认批内。
U="/c/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe"
P="F:\\98_eyasclaw\\03_3d"
cd /f/98_eyasclaw/03_3d || exit 1
mkdir -p Logs
PASS=0; FAIL=0
run () {
  local m=$1; local shots=$2
  "$U" -batchmode -projectPath "$P" -executeMethod DroneSimEditor.SimRunner.SetupAndCapture \
       -dsMode=$m -shots=$shots -logFile Logs/headless_v4_$m.log
  local code=$?
  if [ $code -eq 0 ]; then PASS=$((PASS+1)); echo "PASS $m (exit 0)";
  else FAIL=$((FAIL+1)); echo "FAIL $m (exit $code)"; cp Screenshots/state.txt "Screenshots/batchfail_$m.txt"; fi
}
run regulator 10,27,57
run manual    8,20,95
run route     12,36,76
run env       6,34,78
run recon     10,20,44
run formation 8,20,48
run combat    10,20,26
run tactics   8,20,46
run fault     16,38,55
run full      24,56,61
run battle    6,26,46,62
echo "==== BATCH DONE: PASS=$PASS FAIL=$FAIL ===="
