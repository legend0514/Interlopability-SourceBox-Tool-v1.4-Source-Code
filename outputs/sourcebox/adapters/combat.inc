int pendingDamage=0;unsigned long long protectedUntil=0;
void PrepareHitDetector(){if(Named("sourcebox_hit_relay"))return;auto*e=entities->CreateEntityByName("logic_branch");if(e){entities->SetKeyValue(e,"targetname","sourcebox_hit_relay");entities->DispatchSpawn(e);}}
void PollProxyDamage(){auto*e=Named("sourcebox_hit_relay");if(!e)return;char value[32]{};if(entities->GetKeyValue(e,"InitialValue",value,sizeof(value))&&(atoi(value)!=0||!strcmp(value,"true"))){entities->SetKeyValue(e,"InitialValue","0");bridge.Command({{"type","player_hit"}});}}
void ApplyPlayerDamage(unsigned long long now){if(!pendingDamage)return;int damage=pendingDamage;pendingDamage=0;if(now<protectedUntil||!player)return;auto*info=players->GetPlayerInfo(player);auto*network=player->GetNetworkable();if(!info||!network||info->IsDead()||info->IsObserver())return;auto*e=network->GetBaseEntity();entities->SetKeyValue(e,"targetname","sourcebox_local_player");int health=info->GetHealth(),maximum=info->GetMaxHealth();if(maximum<=0)maximum=100;char value[32];
 if(damage>=health){bridge.Command({{"type","player_death"}});snprintf(value,sizeof(value),"%d",maximum);ApplyCrossplayHealth(value);const char*spawn=!strcmp(SOURCEBOX_GAME,"hl1mp")?combatSpawnHL1:(!strcmp(SOURCEBOX_GAME,"hl2")?combatSpawnHL2:combatSpawnTF2);Vector pos;sscanf(spawn,"%f %f %f",&pos.x,&pos.y,&pos.z);Vector eye;QAngle look;if(ReadPortalEye(info,eye,look))SnapPortalPlayer(pos+(eye-info->GetAbsOrigin()),look);ApplyPortalVelocity(Vector(0,0,0));protectedUntil=now+SOURCEBOX_RESPAWN_PROTECTION_MS;CubeNotice("Respawned - health restored");}
 else{snprintf(value,sizeof(value),"%d",health-damage);ApplyCrossplayHealth(value);}
}
void ApplyCrossplayHealth(const char* value){
#ifdef SOURCEBOX_SDK
 auto* control=Named("sourcebox_player_control");if(!control){control=entities->CreateEntityByName("sourcebox_player_control");if(control){entities->SetKeyValue(control,"targetname","sourcebox_player_control");entities->DispatchSpawn(control);}}
 if(control)Input("sourcebox_player_control","ApplyHealth",value);
#else
 if(player&&player->GetNetworkable())entities->SetKeyValue(player->GetNetworkable()->GetBaseEntity(),"health",value);
#endif
}


