// Assemble bounded snapshots atomically; never display half of a new revision.
export function createPortalSnapshotReader(){
 let session=null,revision=-1,pending=null,current=null;
 return {
  reset(nextSession){session=nextSession;revision=-1;pending=null;current=null;},
  read(){return current;},
  accept(m){
   if(m?.type!=='portal_snapshot'||m.session!==session||!Number.isSafeInteger(m.revision)||m.revision<0||m.revision<=revision||!Number.isInteger(m.count)||m.count<1||m.count>6||!Number.isInteger(m.index)||m.index<0||m.index>=m.count||!Array.isArray(m.pairs)||m.pairs.length>1)return false;
   if(pending&&m.revision<pending.revision)return false;
   if(!pending||m.revision>pending.revision)pending={revision:m.revision,count:m.count,environment:m.environment,parts:new Map()};
   if(m.count!==pending.count||m.environment!==pending.environment)return false;
   pending.parts.set(m.index,m.pairs);
   if(pending.parts.size!==pending.count)return false;
   const pairs=Array.from({length:pending.count},(_,i)=>pending.parts.get(i)).flat();
   if(new Set(pairs.map(p=>p?.owner)).size!==pairs.length){pending=null;return false;}
   current={revision:pending.revision,environment:pending.environment,pairs};revision=pending.revision;pending=null;return true;
  }
 };
}
