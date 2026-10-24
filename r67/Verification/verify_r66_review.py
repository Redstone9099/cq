"""Offline source/asset checks. Does not compile C# or execute Direct3D/the game."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
import argparse

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--changes',default='Evidence/R6.6/changes.json',help='Cumulative changes since R6.5')
parser.add_argument('--cumulative',default='Evidence/R6.6/cumulative-changes.json',help='Cumulative changes since R6.4')
args=parser.parse_args()

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools'))
from portrait_library import Library
checks=[]
def read(path): return (ROOT/path).read_text(encoding='utf-8-sig')
def check(name,value):
    if not value: raise AssertionError(name)
    checks.append(name)
changes={r['path']:r for r in json.loads(read(args.changes))}
for entry in json.loads(read('Evidence/R6.6/base-r65-manifest.json')):
    path=entry['path']; change=changes.get(path)
    if change: check('R6.5 original hash: '+path,change['before_sha256']==entry['sha256'])
    expected=change['after_sha256'] if change else entry['sha256']
    check('R6.5 retained or declared change: '+path,hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==expected)
for path,entry in changes.items():
    check('Declared R6.6 output: '+path,hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==entry['after_sha256'])

meta=json.loads(read('Evidence/v6/skills-runtime.json'))
lib=Library(ROOT/'client/Data/UI/SkillIconsV6.Lib')
check('Uploaded skill library remains byte-identical',hashlib.sha256(lib.data).hexdigest()==meta['sha256']=='17fbe6ed0eff5c48ee1eb50c96e9b5041881287d175f58bd4533fee0ac2a177a')
enums=read('Source/Shared/Enums.cs').split('public enum Spell : byte')[1].split('public enum SpellEffect')[0]
values={n:int(i) for n,i in re.findall(r'\b(\w+)\s*=\s*(\d+)',enums)}
ids={s['id'] for s in meta['skills']}
check('110 unique skills in 256 frame slots',len(meta['skills'])==len(ids)==110 and len(lib.positions)==256)
check('All player/custom spell IDs covered; map events are not skill icons',ids=={v for v in values.values() if 0<v<200})
for s in meta['skills']:
    frame=lib.frame(s['runtime_frame'])
    check('Spell/frame ID: '+s['name'],values[s['name']]==s['id']==s['runtime_frame'])
    check('Source pixels and size: '+s['name'],frame['size']==(128,128) and hashlib.sha256(frame['image'].tobytes()).hexdigest()==s['sha256_rgba'])
    check('No transparent padding shrinking icon: '+s['name'],frame['image'].getbbox()==(0,0,128,128))
check('Unassigned frames contain no artwork',{i for i in range(256) if lib.frame(i)}==ids)

router=read('Source/Client/MirControls/OriginalSkillArtwork.cs')
module=read('Source/Client/MirControls/SkillIconsV6Module.cs')
dialogs=read('Source/Client/MirScenes/Dialogs/OriginalDialogs.cs')
main=read('Source/Client/MirScenes/Dialogs/MainDialogs.cs')
bar=main.split('public sealed partial class SkillBarDialog :')[1].split('public sealed partial class MiniMapDialog')[0]
check('Module routes by Spell ID','(int)spell' in module and '[ModuleInitializer]' in module and 'OriginalSkillArtwork.Renderer = Draw' in module)
check('One live binding per hotbar slot','int slot = i;' in dialogs and 'OriginalSkillArtwork.Bind(Cells[i], () => originalSkillSpells[slot]);' in dialogs)
check('Unbinding clears both old native and new spell identity','Cells[i - 1].Index = -1;\n                originalSkillSpells[i - 1] = Spell.None;' in bar)
check('Actual magic Spell supplies artwork identity','originalSkillSpells[i - 1] = magic.Spell;' in bar)
check('Spell dispatch preserves second bar offset','GameScene.Scene.UseSpell(j + (8 * BarIndex))' in bar)
check('Showing a previously hidden bar refreshes bindings','Visible = true;\n            Update();' in bar)
check('Book and assignment share the router','OriginalSkillArtwork.Bind(magic.SkillButton, () => magic.Magic?.Spell ?? Spell.None)' in dialogs and 'OriginalSkillArtwork.Bind(MagicImage, () => Magic?.Spell ?? Spell.None)' in read('Source/Client/MirScenes/Dialogs/OriginalNativeOverlays.cs'))
check('Hotbar and book masks share icon bounds','OriginalSkillArtwork.BindCooldown(CoolDowns[i], Cells[i])' in dialogs and 'OriginalSkillArtwork.BindCooldown(magic.CoolDown, magic.SkillButton)' in dialogs)
check('Cooldown does not steal clicks or apply native offsets','mask.NotControl = true;' in router and 'mask.UseOffSet = false;' in router and 'mask.Size = icon.Size;' in router and 'mask.Location = icon.Location;' in router)
check('No extra screen scale in artwork code','UIViewport.Scale' not in router and 'DXManager.UiScale' not in router and 'OriginalUILayout.Scale' not in router)
image=read('Source/Client/MirControls/MirImageControl.cs')
check('Custom drawing and click bounds use control size','if (OriginalDraw != null) return Size;' in image and 'if (OriginalDraw != null) return base.IsMouseOver(p)' in image)
check('Masked image uses the scaled renderer','mask.OriginalDraw = OriginalUI.ClassicImage;' in router and 'library.DrawScaled(index,point,size,colour,opacity)' in read('Source/Client/MirControls/OriginalUI.ClassicFooter.cs'))
check('Windows regression gate is before publish',read('Build-R6.ps1').index('SkillArtworkRegression')<read('Build-R6.ps1').index('dotnet publish'))
check('Skill integrity is checked before regressions',read('Build-R6.ps1').index('Verify-SkillPackage.ps1')<read('Build-R6.ps1').index('SkillArtworkRegression'))
check('New test entry point excluded from UiRegression','<Compile Remove="SkillArtwork/**/*.cs" />' in read('Verification/UiRegression.csproj'))
check('Build copies skill assets with UI directory',"Get-ChildItem -LiteralPath $uiRoot -Recurse -File" in read('Build-R6.ps1'))

previous=subprocess.run([sys.executable,str(ROOT/'Verification/verify_r65_review.py'),'--changes',args.cumulative],capture_output=True,text=True)
if previous.returncode: raise RuntimeError(previous.stderr or previous.stdout)
previous=json.loads(previous.stdout)
check('Prior effect/reward/resource checks pass with declared changes',previous['checks']>0)
print(json.dumps({'kind':'R6.6 offline source and actual Lib pixel checks','checks':len(checks),'passed':checks,
    'prior_review_checks':previous['checks'],'syntax':previous['syntax'],
    'csharp_compiled':False,'skill_regression_executed':False,'windows_gameplay_executed':False,'fps_measured':False},ensure_ascii=False,indent=2))
