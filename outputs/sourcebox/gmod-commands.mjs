// Commands belong to the core connection observed by the actual game.
// Retire old-connection commands rather than replaying hits or ownership requests.
export function pendingCommands(sample, connection, acknowledged) {
 const commands=sample.commands??[];
 if(sample.connection!==connection) return {
  acknowledged:commands.reduce((n,c)=>Number.isSafeInteger(c.id)?Math.max(n,c.id):n,acknowledged),
  commands:[]
 };
 return {acknowledged,commands:commands.filter(c=>Number.isSafeInteger(c.id)&&c.id>acknowledged)};
}
