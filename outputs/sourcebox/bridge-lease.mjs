import net from 'node:net';
// Exclusive local lease: a second bridge must never overwrite incoming.json.
// The OS releases it on crashes, so there are no stale PID/lock-file hazards.
export async function acquireBridgeLease(port){
 const lease=net.createServer(socket=>socket.destroy());
 await new Promise((resolve,reject)=>{lease.once('error',reject);lease.listen(port,'127.0.0.1',resolve);});
 return lease;
}
