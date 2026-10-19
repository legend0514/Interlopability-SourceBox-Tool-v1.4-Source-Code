import fs from 'node:fs';
const file='work/sourcebox-sdk/src/game/server/entity_tools_server.cpp';
let code=fs.readFileSync(file,'utf8');
// This mod's local host uses the MP runtime. Explicit client entity selection remains unchanged.
code=code.replaceAll('pServerPlayer ? ( CBasePlayer* )pServerPlayer : UTIL_GetLocalPlayer()',
 'pServerPlayer ? ( CBasePlayer* )pServerPlayer : (gpGlobals->maxClients > 1 && !engine->IsDedicatedServer() ? UTIL_GetListenServerHost() : UTIL_GetLocalPlayer())');
fs.writeFileSync(file,code);
