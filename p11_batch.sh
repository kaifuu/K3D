#!/bin/bash
# P11 总验收:10 模式全部无头跑批(每模式 EXIT 0 = 通过)
U="/c/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe"
P="F:\\98_eyasclaw\\03_3d"
cd /f/98_eyasclaw/03_3d || exit 1
PASS=0; FAIL=0
run () {
  local m=$1; local shots=$2
  "$U" -batchmode -projectPath "$P" -executeMethod DroneSimEditor.SimRunner.SetupAndCapture \
       -dsMode=$m -shots=$shots -logFile Logs/headless_p11x_$m.log
  local code=$?
  if [ $code -eq 0 ]; then PASS=$((PASS+1)); echo "PASS $m (exit 0)";
  else FAIL=$((FAIL+1)); echo "FAIL $m (exit $code)"; cp Screenshots/state.txt "Screenshots/batchfail_$m.txt"; fi
}
run regulator 10,27,53
run manual    8,20
run route     12,36
run env       6,18,30
run recon     10,20
run formation 8,20
run combat    10,20
run tactics   8,20
run fault     16,38
run full      24,56
echo "==== BATCH DONE: PASS=$PASS FAIL=$FAIL ===="
