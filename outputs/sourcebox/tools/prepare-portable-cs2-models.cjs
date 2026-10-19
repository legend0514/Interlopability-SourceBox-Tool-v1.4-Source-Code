// Convert installed Source 1 models with Valve's installed Source 2 tools.
// All generated files are local cache content, never release payload content.
const fs=require('fs'),path=require('path'),cp=require('child_process'),crypto=require('crypto');
const {vpk,mdl}=require('./installed-assets.cjs');
const request=JSON.parse(fs.readFileSync(process.argv[2],'utf8').replace(/^\uFEFF/,''));
const root=request.csRoot,work=request.outputRoot,bin=path.join(root,'game/bin/win64');
for(const p of [root,work,...request.providers])if(!path.isAbsolute(p))throw Error('Absolute setup paths required');
const addon='sourcebox_models_'+crypto.randomUUID().replaceAll('-','');
const content=path.join(root,'content/csgo_addons',addon),compiled=path.join(root,'game/csgo_addons',addon);
const input=path.join(work,'input');fs.mkdirSync(input,{recursive:true});
const files=new Map();
for(const archive of request.archives??[])for(const item of vpk(archive))files.set(...item);
function safe(n){n=n.toLowerCase().replaceAll('\\','/');if(!/^(models|materials)\/[a-z0-9_./-]+$/.test(n)||n.split('/').includes('..'))throw Error('Invalid asset reference '+n);return n;}
function read(n){n=safe(n);for(const provider of request.providers){const file=path.join(provider,n);if(fs.existsSync(file))return fs.readFileSync(file);}return files.get(n)?.();}
const extracted=new Set(),materials=new Set(),modelSet=new Set();
function extract(n){n=safe(n);if(extracted.has(n))return true;const data=read(n);if(!data)return false;const dest=path.join(input,n);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,data);extracted.add(n);return true;}
function material(n){n=safe(n);if(materials.has(n))return;materials.add(n);if(!extract(n))throw Error('Missing material '+n);
 for(const match of read(n).toString().matchAll(/"([^"\r\n]+)"/g)){const value=match[1].toLowerCase().replaceAll('\\','/').replace(/^materials\//,'');if(!/^[a-z0-9_./-]+$/.test(value)||value.includes('..'))continue;const base=value.replace(/\.(vtf|vmt)$/,'');if(read('materials/'+base+'.vmt'))material('materials/'+base+'.vmt');if(read('materials/'+base+'.vtf'))extract('materials/'+base+'.vtf');}
}
function model(n){n=safe(n);if(modelSet.has(n))return {};modelSet.add(n);if(!extract(n))throw Error('Missing model '+n);
 for(const suffix of ['.vvd','.dx90.vtx','.phy','.ani'])extract(n.slice(0,-4)+suffix);
 const bytes=read(n),metadata=mdl(bytes);for(const include of metadata.includes)if(read(include))model(include);
 const str=o=>bytes.toString('utf8',o,bytes.indexOf(0,o));const dirs=Array.from({length:bytes.readInt32LE(212)},(_,i)=>str(bytes.readInt32LE(bytes.readInt32LE(216)+i*4)).replaceAll('\\','/'));
 const mapping={};for(let i=0;i<bytes.readInt32LE(204);i++){const offset=bytes.readInt32LE(208)+i*64,texture=str(offset+bytes.readInt32LE(offset)).replaceAll('\\','/');for(const dir of ['',...dirs]){const mat=safe('materials/'+dir+texture+'.vmt');if(read(mat)){material(mat);mapping[path.basename(texture).toLowerCase()+'.vmat']=mat.slice(0,-4)+'.vmat';break;}}}return mapping;
}
function run(exe,args,log,inputText){const result=cp.spawnSync(path.isAbsolute(exe)?exe:path.join(bin,exe),args,{cwd:work,windowsHide:true,encoding:'utf8',input:inputText,timeout:180000,maxBuffer:16*1024*1024});fs.writeFileSync(log,(result.stdout??'')+(result.stderr??''));if(result.status!==0||result.error)throw Error(exe+' failed; see '+log);return (result.stdout??'')+(result.stderr??'');}
function walk(dir,extension){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name),extension):e.name.endsWith(extension)?[path.join(dir,e.name)]:[]);}
fs.writeFileSync(path.join(input,'gameinfo.txt'),'"GameInfo" { "game" "SourceBox local conversion" "FileSystem" { "SteamAppId" "220" "SearchPaths" { "Game" "|gameinfo_path|." } } }');
const mappings=new Map(request.jobs.map(job=>[job.source,model(job.source)]));
const textureRequest=path.join(work,'textures-request.json');
fs.writeFileSync(textureRequest,JSON.stringify({sourceioRoot:request.sourceioRoot,input,content}));
run(request.blender,['-b','--factory-startup','--python-exit-code','1','--python',path.join(__dirname,'prepare-portable-cs2-textures.py'),'--',textureRequest],path.join(work,'textures.log'));
const results=[];
for(const job of request.jobs){
 const source=safe(job.source),target=safe(job.target);if(!target.endsWith('.vmdl'))throw Error('Expected model target');
 const mapping=mappings.get(job.source),base=path.basename(source,'.mdl'),local=path.join(work,'converted',String(results.length));fs.mkdirSync(local,{recursive:true});
 run('cs_mdl_import.exe',['-nop4','-i',input,'-o',local,source],path.join(local,'import.log'));
 const importedModel=walk(local,'.vmdl').find(file=>path.basename(file)===base+'.vmdl');
 if(!importedModel)throw Error('Importer produced no model: '+source);

 const relative=path.posix.dirname(target),destination=path.join(content,relative);fs.mkdirSync(destination,{recursive:true});fs.cpSync(path.dirname(importedModel),destination,{recursive:true});
 let modeldoc=fs.readFileSync(path.join(destination,base+'.vmdl'),'utf8').replaceAll('\"/'+source.slice(0,-4)+'_refs/','\"'+relative+'/'+base+'_refs/').replaceAll('\"'+source.slice(0,-4)+'_refs/','\"'+relative+'/'+base+'_refs/').replaceAll('"/'+base+'_refs/','"'+relative+'/'+base+'_refs/').replaceAll('"'+base+'_refs/','"'+relative+'/'+base+'_refs/');
 if(job.weaponBone){if(!/^[a-zA-Z0-9_]+$/.test(job.weaponBone))throw Error('Invalid attachment bone');modeldoc=modeldoc.replace(/(children\s*=\s*\[)/,'$1\n{ _class = "AttachmentList" children = [{ _class = "Attachment" name = "sourcebox_hand" parent_bone = "'+job.weaponBone+'" relative_origin = [0,0,0] relative_angles = [0,0,0] weight = 1 ignore_rotation = false }] },');}
 // Valve's importer leaves Source 1 bodygroup choices unnamed. Source 2 requires
 // a name even for the empty (hidden) choice; keep their order and meshes intact.
 let choiceIndex=0;
 modeldoc=modeldoc.replace(/(_class\s*=\s*"BodyGroupChoice")\s*(?=meshes\s*=)/g,()=> '_class = "BodyGroupChoice"\n name = "choice_'+choiceIndex+++'"\n ');
 fs.writeFileSync(path.join(content,target),modeldoc);
 for(const file of walk(destination,'.dmx').filter(f=>f.includes(path.sep+'mesh'+path.sep))){run('dmxconvert.exe',['-i',file,'-oe','keyvalues2'],path.join(work,'dmx-convert.log'));let text=fs.readFileSync(file,'utf8').replace(/("mtlName"\s+"string"\s+")([^"/]+\.vmat)/gi,(all,p,m)=>p+(mapping[m.toLowerCase()]||m.toLowerCase()));fs.writeFileSync(file,text);}
 const log=run('resourcecompiler.exe',['-retail','-nop4','-game',path.join(root,'game/csgo'),'-i',path.join(content,target)],path.join(local,'compile.log'));
 if(!/0 failed/.test(log)||/RESOURCE COMPILE ERROR|referencing missing material/.test(log)||!fs.existsSync(path.join(compiled,target+'_c')))throw Error('Incomplete model compile: '+target);
 results.push({source,target});console.log('Converted '+source+' -> '+target);
}
if(request.portalSurfaces){
 run(request.blender,['-b','--factory-startup','--python-exit-code','1','--python',path.join(__dirname,'prepare-portable-cs2-portal-surfaces.py'),'--',textureRequest],path.join(work,'portal-surfaces.log'));
 const matdir=path.join(content,'materials/sourcebox');fs.mkdirSync(matdir,{recursive:true});
 const white=Buffer.alloc(18+16,255);white.fill(0,0,18);white[2]=2;white.writeUInt16LE(2,12);white.writeUInt16LE(2,14);white[16]=32;white[17]=40;fs.writeFileSync(path.join(matdir,'portal_white.tga'),white);
 for(const kind of ['ring','monitor']){
  fs.writeFileSync(path.join(matdir,'portal_'+kind+'.vmat'),'"Layer0" { "Shader" "csgo_unlitgeneric.vfx" "F_RENDER_BACKFACES" "1" "TextureColor" "materials/sourcebox/portal_white.tga" }');
  const model='models/sourcebox/portals/'+kind+'/'+kind+'.vmdl';
  fs.writeFileSync(path.join(content,model),'<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:modeldoc41:version{12fc9d44-453a-4ae4-b4d9-7e2ac0bbd4e0} -->\n{ rootNode = { _class="RootNode" children=[{_class="RenderMeshList" children=[{_class="RenderMeshFile" name="'+kind+'" filename="models/sourcebox/portals/'+kind+'/mesh.fbx" import_scale=1}]}]}}');
  const log=run('resourcecompiler.exe',['-retail','-nop4','-game',path.join(root,'game/csgo'),'-i',path.join(content,model)],path.join(work,kind+'-compile.log'));
  if(!/0 failed/.test(log)||/RESOURCE COMPILE ERROR/.test(log)||!fs.existsSync(path.join(compiled,model+'_c')))throw Error('Portal surface compile failed');
 }
}
for(const folder of ['models','materials'])if(fs.existsSync(path.join(compiled,folder)))fs.cpSync(path.join(compiled,folder),path.join(work,folder),{recursive:true});
fs.writeFileSync(path.join(work,'converted-models.json'),JSON.stringify({localOnly:true,addon,results},null,2));
