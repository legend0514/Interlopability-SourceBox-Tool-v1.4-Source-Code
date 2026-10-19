import fs from 'node:fs';
import readline from 'node:readline';
// Stream large native logs; command success alone is not momentum evidence.
const input=process.argv[2],output=process.argv[3];
if(!input||!output)throw Error('Usage: node measure-portal-momentum.mjs core.jsonl result.json');
const pending=new Map(),results=[];
for await(const line of readline.createInterface({input:fs.createReadStream(input),crlfDelay:Infinity})){
 let m;try{m=JSON.parse(line)}catch{continue}
 const time=Date.parse(m.time);
 if(m.type==='portal_crossing'){
  const prior=pending.get(m.game);if(prior)results.push(prior);
  pending.set(m.game,{event:m.event,game:m.game,time:m.time,incoming:m.incomingVelocity,requested:m.outgoingVelocity,samples:[],applied:null});
 }
 const p=pending.get(m.game);if(!p)continue;
 if(m.type==='portal_applied'&&m.event===p.event)p.applied=m.applied;
 if(m.type==='state'&&time>=Date.parse(p.time)&&time-Date.parse(p.time)<=400){
  const speed=Math.hypot(...p.requested),alignment=speed?m.velocity.reduce((s,v,i)=>s+v*p.requested[i],0)/speed:0;
  p.samples.push({time:m.time,position:m.position,velocity:m.velocity,grounded:m.grounded,outwardSpeed:alignment,ratio:speed?alignment/speed:null});
 }
 if(time-Date.parse(p.time)>400){results.push(p);pending.delete(m.game);}
}
results.push(...pending.values());
for(const p of results){
 const speed=Math.hypot(...p.requested);
 // Observed direction/speed is evidence; no universal pass based on ack alone.
 p.bestOutwardRatio=speed&&p.samples.length?Math.max(...p.samples.map(s=>s.ratio)):null;
 p.floorToWall=p.incoming?.[2]<-100&&Math.abs(p.requested?.[2])<1&&speed>100;
}
fs.writeFileSync(output,JSON.stringify({source:input,generatedAt:new Date().toISOString(),note:'Native core telemetry; no claim of visual correctness. Ratios compare requested exit direction to observed velocity within 400ms.',events:results},null,2));
console.log(JSON.stringify(results.filter(p=>p.floorToWall).map(p=>({game:p.game,event:p.event,applied:p.applied,bestOutwardRatio:p.bestOutwardRatio,samples:p.samples.length}))));
