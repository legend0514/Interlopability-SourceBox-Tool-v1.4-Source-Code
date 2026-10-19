import os,subprocess,pathlib,sys
root=pathlib.Path(__file__).resolve().parents[3]
env={k.upper():v for k,v in os.environ.items()}
env['PATH']='C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/python313;'+env.get('PATH','')
msbuild='C:/SourceBox-dev/Documents/Codex/2026-10-10/make/work/BuildTools/MSBuild/Current/Bin/MSBuild.exe'
name=sys.argv[1] if len(sys.argv)>1 else 'tf2'
if name not in ('hl2','tf2'):
 raise SystemExit('Expected hl2 or tf2')
subprocess.run(['node',str(root/'outputs/sourcebox/preflight.mjs')],check=True,env=env)
with (root/f'work/phase6/sdk-{name}-build.log').open('w') as out:
 r=subprocess.run([msbuild,str(root/f'work/sourcebox-sdk/src/sourcebox-{name}.sln'),'/m:2','/nr:false','/p:Configuration=Release','/p:Platform=win64','/p:PlatformToolset=v143','/p:WindowsTargetPlatformVersion=10.0.22621.0','/v:minimal','/nologo'],env=env,stdout=out,stderr=subprocess.STDOUT)
print('Build exit',r.returncode)
if r.returncode == 0:
 mod='mod_hl2' if name=='hl2' else 'mod_tf'
 for binary in ('client.dll','server.dll'):
  if not (root/f'work/sourcebox-sdk/game/{mod}/bin/x64/{binary}').is_file():
   raise SystemExit('Missing expected game binary: '+binary)
sys.exit(r.returncode)
