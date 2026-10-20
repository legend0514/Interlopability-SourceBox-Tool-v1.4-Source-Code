import http from 'node:http';
// Exclusive local lease: a second bridge must never overwrite incoming.json.
// The OS releases it on crashes, so there are no stale PID/lock-file hazards.
export async function acquireBridgeLease(port,identity={}){
 const lease=http.createServer((request,response)=>{
  const url=new URL(request.url,'http://127.0.0.1');
  if(request.method!=='GET'||url.pathname!=='/sourcebox-bridge'){response.writeHead(404);response.end();return;}
  const nonce=url.searchParams.get('nonce');
  if(!nonce||!/^[a-f0-9-]{36}$/.test(nonce)){response.writeHead(400);response.end();return;}
  response.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'});
  response.end(JSON.stringify({protocol:1,component:'sourcebox-gmod-bridge',pid:process.pid,...identity,nonce}));
 });
 lease.requestTimeout=2000;lease.headersTimeout=2000;
 await new Promise((resolve,reject)=>{lease.once('error',reject);lease.listen(port,'127.0.0.1',resolve);});
 return lease;
}
