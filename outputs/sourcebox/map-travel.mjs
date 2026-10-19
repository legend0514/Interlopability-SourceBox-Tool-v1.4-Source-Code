// Fail closed on an explicit travel/world record. Old installs have no record.
export function mapTravelPaused(world, hub='sourcebox_room_001', loadedWorlds=[]) {
  if (world === undefined) return false;
  if(!world||world.transitioning!==false)return true;
  if(world.map===hub)return false;
  // Imported worlds retain the same native server and shared coordinates.
  // An actual changelevel must still isolate the adapter until its map agrees.
  return world.nativeMap!==hub||!loadedWorlds.includes(world.map);
}
