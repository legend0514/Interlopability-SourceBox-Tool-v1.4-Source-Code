// Summarize fresh coordinator evidence without turning visual or client-side
// native travel into an invented pass. Run against a dedicated test-session log.
import fs from 'node:fs';
const [input,output]=process.argv.slice(2);
if(!input||!output)throw Error('Provide the session JSONL and output JSON');
const lines=fs.readFileSync(input,'utf8').trim().split('\n');
const events=lines.flatMap((line,index)=>{try{return [JSON.parse(line)];}catch(error){if(index===lines.length-1)return [];throw error;}});
const games=['hl2','tf2','hl1mp','gmod','portal2','cs2'];
const report={createdUtc:new Date().toISOString(),sessionStart:events[0]?.time,sessionEnd:events.at(-1)?.time,scope:'Local installed-content development build; not portable release acceptance',games:games.map(game=>{
 const local=events.filter(e=>e.game===game),states=local.filter(e=>e.type==='state');
 const crossings=local.filter(e=>e.type==='portal_crossing');
 const applied=new Map(local.filter(e=>e.type==='portal_applied').map(e=>[e.event,e.applied]));
 return {game,connections:local.filter(e=>e.type==='connected').map(e=>({time:e.time,pid:e.pid})),worldsObserved:[...new Set(states.map(e=>e.world))],crossings:crossings.map(e=>({time:e.time,pair:e.pair,acknowledged:applied.get(e.event)??null,incomingVelocity:e.incomingVelocity,outgoingVelocity:e.outgoingVelocity})),visualAcceptance:'not-certified',note:['gmod','portal2'].includes(game)?'Native engine traversal is not counted by coordinator acknowledgement events. World changes alone do not prove every portal works.':undefined};
}),limitations:['Controlled traversal and rendering are separate checks.','User movement and native fixtures occurred in this session.','No fresh-PC gameplay test or public expanded-world package has passed.']};
fs.writeFileSync(output,JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report.games.map(g=>({game:g.game,worlds:g.worldsObserved,acknowledgedCrossings:g.crossings.filter(c=>c.acknowledged===true).length})),null,2));
