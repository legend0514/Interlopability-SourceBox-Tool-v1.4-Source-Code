// Generated from sheets/cs2-models.json
public partial class SourceboxCS2 {
 private record PlayerModel(string Model,string Idle,string Walk,string Run,string Air,string WeaponAttachment);
 private static readonly Dictionary<string,PlayerModel> PlayerModels=new(){["hl2"]=new("models/sourcebox/hl2/player.vmdl","idle01","walk","run","idle01","sourcebox_hand"),["tf2"]=new("models/sourcebox/tf2/heavy/heavy.vmdl","idle","run","run","air","sourcebox_hand"),["hl1mp"]=new("models/sourcebox/hl1mp/gordon.vmdl","idle","walk","run","jump","sourcebox_hand"),["gmod"]=new("models/sourcebox/gmod/kleiner/kleiner.vmdl","idle","run","run","air","sourcebox_hand"),["goblin"]=new("models/sourcebox/gmod/goblin/goblin.vmdl","idle","run","run","air","sourcebox_hand"),["portal2"]=new("models/sourcebox/portal2/chell/chell.vmdl","idle","run","run","air","sourcebox_hand")};
}
