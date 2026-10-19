import fs from 'node:fs';
const file=process.argv[2]??new URL('./logs/core.jsonl',import.meta.url);
const lines=fs.readFileSync(file,'utf8').split('\n');const events=[];
for(const line of lines){try{events.push(JSON.parse(line));}catch{}}
const result={generated:new Date().toISOString(),scope:'Core receive logs only; does not prove rendering or visual synchronization latency',games:{}};
for(const game of ['hl2','tf2']){
 const connected=events.filter(e=>e.type==='connected'&&e.game===game).at(-1);
 const all=events.filter(e=>e.type==='state'&&e.game===game&&(!connected||e.time>=connected.time));
 const rows=all.slice(-600),gaps=rows.slice(1).map((e,i)=>Date.parse(e.time)-Date.parse(rows[i].time)).sort((a,b)=>a-b);
 const range=key=>[0,1,2].map(i=>rows.length?[Math.min(...rows.map(e=>e[key][i])),Math.max(...rows.map(e=>e[key][i]))]:null);
 result.games[game]={lastConnection:connected??null,movementSamples:all.length,windowSamples:rows.length,observedHz:rows.length>1?(rows.length-1)*1000/(Date.parse(rows.at(-1).time)-Date.parse(rows[0].time)):null,interarrivalMs:{median:gaps[Math.floor(gaps.length*.5)]??null,p95:gaps[Math.floor(gaps.length*.95)]??null,max:gaps.at(-1)??null},positionRanges:range('position'),rotationRanges:range('rotation'),groundedStates:[...new Set(rows.map(e=>e.grounded))],velocityObserved:rows.some(e=>e.velocity.some(v=>Math.abs(v)>1)),latest:rows.at(-1)??null};
}
console.log(JSON.stringify(result,null,2));
