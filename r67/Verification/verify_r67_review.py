"""Verify R6.7 source/asset integrity and retained Windows CI evidence.
This script itself does not compile C# or execute the game.
"""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
def read(path): return (ROOT/path).read_text(encoding='utf-8-sig')
def sha(path): return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()
checks=[]
def check(name,value):
    if not value: raise AssertionError(name)
    checks.append(name)
changes={x['path']:x for x in json.loads(read('Evidence/R6.7/changes.json'))}
for record in json.loads(read('Evidence/R6.7/base-r66-manifest.json')):
    path=record['path']; change=changes.get(path)
    if change: check('R6.6 before hash: '+path,change['before_sha256']==record['sha256'])
    check('Retained or declared R6.6 file: '+path,sha(path)==(change['after_sha256'] if change else record['sha256']))
for path,change in changes.items(): check('Declared R6.7 output: '+path,sha(path)==change['after_sha256'])
reward=read('Verification/RewardsRegression.cs')
check('Mixed initializer removed','GroupedDrop=new GroupDropInfo { Random=true,\n' not in reward)
check('Random drop item still included','grouped.GroupedDrop.Add(new DropInfo { Chance=1,Item=material });' in reward)
check('Production incoming multipliers initialize neutral','ArmourRate = 1F, DamageRate = 1F;' in read('Source/Server/MirObjects/MapObject.cs'))
for marker in ['dealt==600 && monster.HP==400 && !monster.Dead','monster.Dead && monster.HP==0 && monster.EXPOwner==a',
               'prize.CanBePickedUpBy(a) && !prize.CanBePickedUpBy(b)','monster.DamageRate=0F','monster.PoisonDamage(-10000,b)']:
    check('Reward regression checks effective behavior: '+marker,marker in reward)
for script in ['Build-R6.ps1','Build-Server-R6.7.ps1']:
    text=read(script)
    check('Reward gate before publish: '+script,text.index('RewardsRegression')<text.index('dotnet publish'))
    check('Build fails on regression exit code: '+script,'$LASTEXITCODE -ne 0' in text)
    check('Fresh compilation precedes regression run: '+script,text.index('--no-incremental')<text.index('dotnet run --no-build'))
check('Legacy server entry uses current guarded build',read('Build-Server-R6.5.ps1')==read('Build-Server-R6.7.ps1'))
check('Standalone startup test not duplicated in UI executable','<Compile Remove="DirectLaunchRegression.cs" />' in read('Verification/UiRegression.csproj'))
check('Standalone startup test still executes after publish',read('Build-R6.ps1').index('Verification\\DirectLaunchRegression.csproj')>read('Build-R6.ps1').index('dotnet publish'))
for script in ['Source/Client/MirScenes/Dialogs/InventoryDialog.cs','Source/Client/MirScenes/Dialogs/HeroDialogs.cs']:
    text=read(script)
    check('Rotation control and method removed: '+script,'RotateButton' not in text and 'void Flip()' not in text)
check('Legacy rotation enum ID retained without active binding','BeltFlip,' in read('Source/Client/KeyBindSettings.cs') and
      re.search(r'function\s*=\s*KeybindOptions.BeltFlip',read('Source/Client/KeyBindSettings.cs')) is None)
check('Rotation key has no action','case KeybindOptions.BeltFlip:' not in read('Source/Client/MirScenes/GameScene.cs'))
control=read('Source/Client/MirScenes/Dialogs/OriginalSkillBarControls.cs')
check('Collapse removes whole body and hitbox','foreach (var control in Controls)' in control and 'control.Visible = !originalBarCollapsed;' in control and 'new Size(20, 20)' in control)
check('Restore control remains usable','if (control == originalBarToggle) continue;' in control and 'OriginalIcon.Expand' in control)
check('Numbers are not the hide target','originalKeyNamesVisible' not in control and '隐藏键位' not in control)
check('Collapsed background stops drawing','if(originalBarCollapsed)return;' in read('Source/Client/MirScenes/Dialogs/OriginalDialogs.cs'))
main=read('Source/Client/MirScenes/Dialogs/MainDialogs.cs')
check('Collapsed masks cannot be reactivated','if (!Visible || originalBarCollapsed) return;' in main)
check('Global show expands body','ResetOriginalBarCollapse();\n            Visible = true;' in main)
prior=subprocess.run([sys.executable,str(ROOT/'Verification/verify_r66_review.py'),'--changes','Evidence/R6.7/changes-since-r65.json',
    '--cumulative','Evidence/R6.7/changes-since-r64.json'],capture_output=True,text=True)
if prior.returncode: raise RuntimeError(prior.stderr or prior.stdout)
prior=json.loads(prior.stdout)
ci_path=ROOT/'Evidence/R6.7/windows-ci.json'
ci=json.loads(ci_path.read_text()) if ci_path.exists() else None
if ci:
    check('Windows workflow completed successfully',ci['conclusion']=='success')
    for entry in ci['tested_inputs']:
        check('Windows-tested input unchanged: '+entry['path'],sha(entry['path'])==entry['sha256'])
print(json.dumps({'kind':'R6.7 offline checks plus retained CI evidence','checks':len(checks),'passed':checks,
    'prior_review_checks':prior['checks']+prior['prior_review_checks'],'syntax':prior['syntax'],
    'windows_ci':ci['run_url'] if ci else None,'csharp_build_and_regressions_passed':ci is not None,
    'windows_gameplay_executed':False,'fps_measured':False},ensure_ascii=False,indent=2))
