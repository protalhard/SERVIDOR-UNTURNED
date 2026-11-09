# O que dá pra fazer no Unturned pelo plugin (servidor 3.26.3.x)

Resumo do código do jogo (Assembly-CSharp descompilado: 2.234 arquivos, ~1.900 classes). Só o que roda no
servidor e serve pra plugin. Antes de implementar, descompile a classe e leia como o jogo faz (ver CLAUDE.md).
Regra geral: o servidor manda, o cliente só vê o que o servidor replica. Mudar um objeto direto (transform,
lista interna) sem a função "Server.../send..." não aparece pros jogadores.

## Jogador (Player e componentes, `player.life`, `player.inventory` etc.)
- Vida: `life.serverModifyHealth/Food/Water/Virus/Stamina/Warmth`, `serverSetBleeding`, `serverSetLegsBroken`,
  `ServerRespawn(naCama)`, `sendRevive`. Eventos: `onHurt`, `onPlayerDied`, `OnPreDeath`, `OnFallDamageRequested`,
  `OnSelectingRespawnPoint` (escolher onde renasce).
- Teleporte: `teleportToLocation/Unsafe`, `teleportToBed`, `teleportToPlayer`, `teleportToRandomSpawnPoint`.
- Movimento: `movement.sendPluginSpeedMultiplier / JumpMultiplier / GravityMultiplier` (velocidade, pulo, gravidade
  por jogador). `stance` (agachar/deitar), evento `OnStanceChanged_Global`.
- Tela: `ServerShowHint(texto, segundos)`, `enable/disablePluginWidgetFlag` (esconder HUD do jogo, menu de morte,
  Modal pra mouse livre), `look.sendFreecamAllowed / sendSpecStatsAllowed / sendWorkzoneAllowed`,
  `quests.ServerSetCutsceneModeActive` (modo cinema), `sendBrowserRequest(msg, url)` (abre link no navegador do jogador),
  `sendScreenshot` (pede print da tela pro anti-cheat).
- Inventário: `inventory.tryAddItem / forceAddItem / removeItem / FindItemsByAsset / search`, `ItemTool.tryForceGiveItem`,
  abrir baú/porta-malas pelo servidor (`openStorage`, `openTrunk`). Equipar: `equipment.ServerEquip`, `dequip`,
  `ServerBindItemHotkey`. Eventos `OnUseableChanged_Global`, `OnInspectingUseable_Global`.
- Roupa: `clothing.sendSwapShirt/Pants/Hat/Vest/Backpack/Mask/Glasses`, `ServerSetFace`, `ServerSetVisualToggleState`.
- Skills/XP: `skills.ServerModifyExperience / ServerSetExperience / ServerSetSkillLevel / ServerUnlockAllSkills`, `modRep`
  (reputação). Eventos `OnExperienceChanged_Global`, `OnSkillUpgraded_Global`, `OnReputationChanged_Global`.
- Missões e flags do jogo: `quests.setFlag(id, valor)`, `ServerAddQuest(asset)`, `CompleteQuest`, `trackZombieKill` etc.
  Dá pra fazer missões nativas (assets de Quest/NPC num mod) e o plugin só liga/desliga flags.
- Grupo: `quests.ServerAssignToGroup`, `GroupManager.addGroup/getGroupInfo/deleteGroup` (facções nativas do jogo).
- Voz: `voice.ServerSetPermissions`, evento `onRelayVoice` (rádio/proximidade customizados).
- Admin: `Provider.kick`, `Provider.ban`, eventos `onBanPlayerRequested`, `OnAdminUsageChanged`.

## Chat e UI
- `ChatManager.serverSendMessage(texto, cor, de, para, modo, iconURL, richText)` — mensagem com ícone por URL.
  Eventos `onChatted` (filtrar/bloquear), `onServerFormattingMessage`, votação nativa (`onVotingStart`...).
- UI de mod (EffectManager): `sendUIEffect(id, key, conexão, reliable)`, `sendUIEffectText`, `sendUIEffectVisibility`,
  `sendUIEffectImageURL` (imagem da internet num elemento), `askEffectClearByID`, `ClearEffectByID_AllPlayers`.
  Eventos `onEffectButtonClicked` e `onEffectTextCommitted` (campo de texto). Posição/tamanho só no Unity.

## Efeitos, som e explosões
- `EffectManager.triggerEffect(new TriggerEffectParameters(asset){position, relevantDistance, reliable})` — qualquer
  efeito (fumaça, fogo, som). `relevantDistance` = até onde os jogadores recebem (EffectManager.INSANE = mapa todo).
- `DamageTool.explode(ExplosionParameters)` — explosão de verdade (dano em jogador, zumbi, carro, base).
- `DamageTool.damagePlayer/damageZombie/damageAnimal`, `isPlayerAllowedToDamagePlayer` (PvP por área, ex.: safe).
  Eventos `damagePlayerRequested`, `damageZombieRequested` (cancelar/alterar dano).

## Zumbis (ZombieManager)
- Pool fixo por área de navegação (`LevelNavigation.tryGetBounds(pos, out bound)`), em `regions[bound].zombies`.
- Reviver/mover: `zombie.sendRevive(tipo, especialidade, roupas, posição, ângulo)`; tipos em `LevelZombies.tables`.
- CRIAR zumbi novo: `ZombieManager.instance.addZombie(bound, tipo, ...)`, mas o cliente só recebe a lista da área uma vez
  (ao entrar nela). Crie antes de ter jogador na área (reserva no carregamento do mapa) e depois só reavive. Ver `Eventos.cs`.
- Especialidades: NORMAL, MEGA (chefe), CRAWLER, SPRINTER, FLANKER_*, BURNER, ACID, SPIRIT, BOSS_ELECTRIC/WIND/FIRE/MAGMA/NUCLEAR/SPIRIT/ALL, DL_* (Dying Light).
- `getZombiesInRadius`, `teleportZombieBackIntoMap`, ondas (`waveIndex`, `onWaveUpdated`), beacon de horda (BeaconManager).

## Animais
- `AnimalManager.spawnAnimal(id, pos, rot)`, `getAnimalsInRadius`, `sendAnimalAlive/Dead`. Dá pra eventos de caça.

## Veículos (VehicleManager / InteractableVehicle)
- Criar: `spawnVehicleV2(asset, pos, rot)`, `SpawnVehicleV3(...)` (skin, combustível, bateria, sirene, farol),
  `spawnLockedVehicleForPlayerV2` / `VehicleTool.giveVehicle` (já trancado pro jogador).
- Mudar: `sendVehicleHealth/Fuel/BatteryCharge/Lock/Skin/Headlights`, `ServerSetVehicleLock`, `repair`, `damage`
  (vida 0 = explode depois de 4 s), `askVehicleDestroy`, `ServerForcePassengerIntoVehicle`, `forceRemovePlayer`.
- Eventos: `onEnterVehicleRequested`, `onExitVehicleRequested`, `onDamageVehicleRequested`, `onVehicleLockpicked`,
  `onVehicleCarjacked`, `OnVehicleExploded`, `onSiphonVehicleRequested`, `OnPreDestroyVehicle`.
- Sem motorista o servidor simula e replica a posição (dá pra mover por plugin, como o heli crash).

## Bases (BarricadeManager / StructureManager)
- Criar: `dropNonPlantedBarricade` (fica salva no mundo), `dropPlantedBarricade` (em veículo), `dropStructure`.
- Mudar: `ServerSetBarricadeTransform`, `changeOwnerAndGroup`, `ServerSetDoorOpen`, `ServerSetSignText`,
  `ServerSetGeneratorPowered`, `ServerSetFireLit`, `ServerSetStereoTrack`, `ServerClaimBedForPlayer`, `repair`, `damage`,
  `destroyBarricade`, `DestroyBarricadesInSphere`, `getBarricadesInRadius`, `tryGetBed`.
- Eventos (cancelar/alterar): `onDeployBarricadeRequested`, `onDeployStructureRequested`, `onDamageBarricadeRequested`,
  `onDamageStructureRequested`, `onSalvageBarricadeRequested`, `onOpenStorageRequested`, `onModifySignRequested`,
  `onTransformRequested`, `onHarvestPlantRequested`, `onBarricadeSpawned`.
- Baú: `InteractableStorage.items` (`clear`, `tryAddItem`); conteúdo salva sozinho.

## Itens no chão e loot
- `ItemManager.dropItem(item, pos, efeito, jogado, espalhar)`, `ServerClearItemsInSphere`, `getItemsInRadius`,
  evento `onTakeItemRequested` (bloquear pegar), `onServerSpawningItemDrop` (mudar loot que nasce no mapa).
- Tabelas do mapa: `LevelItems.tables`, `LevelZombies.tables`, `LevelVehicles.tables`; `SpawnAsset` (tabelas de spawn dos mods).
- Airdrop nativo com AVIÃO voando: `LevelManager.airdrop(ponto, idDaTabelaSpawn, velocidade)` ou
  `SpawnAirdrop(ponto, SpawnAsset)`; nós do mapa: `GetRandomAirdropNode`.

## Mundo
- Clima e tempo: `LightingManager.time`, `day`, `isDaytime`; clima: `ActivatePerpetualWeather(asset)`,
  `ForecastWeatherImmediately`, `SetScheduledWeather(asset, previsão, duração)`, `DisableWeather`.
- Objetos do mapa (portões, portas de bunker, máquinas): `ObjectManager.forceObjectBinaryState(transform, usado)`,
  `useObjectDropper`, `useObjectQuest`, `getObjectsInRadius`.
- Árvores/pedras: `ResourceManager.ServerSetResourceAlive/Dead`, `getResourcesInRadius`, `damage`.
- Evento de NPC/mapa: `NPCEventManager.BroadcastEvent(jogador, id, modo)` (dispara gatilhos de mods/mapa).
- Chão: no California 2 use `Terreno(x, z)` (raio GROUND|GROUND2); BLOCK_COLLISION de cima bate num colisor no céu.
- Navegação: `LevelNavigation.tryGetBounds`, `checkSafe`; `Level.isPointWithinValidHeight`, `Level.size`.

## Servidor
- `Provider.clients` (SteamPlayer), `PlayerTool.getPlayer/getSteamPlayer/getPlayersInRadius/EnumeratePlayers`.
- `Provider.modeConfigData` (tudo do Config.txt em memória: loot, zumbis, timers de respawn, PvP, decaimento).
- `Provider.shutdown(segundos, motivo)`, `SaveManager.save()`, `Provider.isPvP`, `Provider.maxPlayers`.
- Salvar dados: `ServerSavedata.serializeJSON/deserializeJSON/fileExists` (pasta do servidor) ou arquivo próprio na pasta do plugin.
- Log: `CommandWindow.Log`, `UnturnedLog.info`; eventos `onCommandWindowInputted` (comandos do console).

## Limites (o que NÃO dá só com plugin)
- Modelo 3D, som novo, imagem de UI, posição de UI: precisa de mod no Workshop (Unity) e todo jogador baixa.
- Mexer no mapa (terreno, prédios, nav de zumbi): só no editor do mapa.
- Cliente não roda código do plugin: tudo que é "visual no jogador" passa por efeito/UI de mod ou função do jogo.
