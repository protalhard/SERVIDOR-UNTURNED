# Servidor Unturned "DAY Z BRASIL OFICIAL" — guia pro Claude

Leia antes de mexer. Responda ao dono (Pedro) em português, simples e direto.

## Onde fica o quê
- `plugins/SobrevivenciaCore/` — plugin RocketModFix (C#, net48). Build: `$HOME/.dotnet/dotnet build -c Release` dentro da pasta
  (instale o .NET 8 SDK se faltar). Copie `bin/Release/net48/SobrevivenciaCore.dll` pra `server-files/Rocket/Plugins/`.
- `painel/` — site (`cmd/dz-site`) e bot do Discord (`cmd/dz-bot`) em Go. Os binários em `painel/bin/` vão commitados:
  `CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o bin/dz-site ./cmd/dz-site` (idem dz-bot). `web/` e `novidades.md` são embed.
- `server-files/browser.txt` — nome e textos da lista de servidores (o `scripts/aplicar-browser.sh` copia pro Config.txt).
- `scripts/` — tudo que roda na VPS (aplicar.sh, atualizar-jogo.sh, dz-update-jogo.py, unidades systemd).

## Como chega no servidor (VPS AWS, IP 54.94.93.87, porta 27015)
- `unturned-sync.timer` a cada 5 min: `git reset --hard origin/main` e roda `scripts/aplicar.sh` (dll, site, bot, permissões).
- O jogo NÃO reinicia sozinho com dll nova: entra no reinício diário (07:00 Brasília) ou quando pedirem.
- Reiniciar já (com SteamCMD): some 1 no número de `server-files/ATUALIZAR-JOGO`. Cai ~20-60 s.
- `dz-update-jogo.timer` (10 min) compara o build da Steam com o instalado; saiu versão nova, avisa e atualiza sozinho.
- Status ao vivo: `https://dayz.snaplag.com.br/api/status` (o plugin grava status.json a cada 15 s; >90 s parado = offline).
- Caminhos na VPS: repo `/home/unturned/repo`, jogo `/home/unturned/server`, servidor `Servers/dayz`, unidade `unturned.service`.
- Git: force push é bloqueado. Faça merge de `origin/main` na branch, abra PR e faça squash merge (SHA de 40 caracteres).

## Ler o código do jogo (Unturned é de código aberto pra ler)
O Assembly-CSharp vem no pacote NuGet do redist (`~/.nuget/packages/rocketmodfix.unturned.redist.server/<versão>/lib/net48/`).
Descompile uma classe quando precisar saber como o jogo faz algo, em vez de chutar:
```
dotnet tool install -g ilspycmd --version 8.2.0.7535
DOTNET_ROLL_FORWARD=Major ~/.dotnet/tools/ilspycmd -t SDG.Unturned.ZombieManager <Assembly-CSharp.dll> > ZombieManager.cs
```

## O que já aprendemos do jogo (economiza tempo)
- **Zumbis**: cada área de navegação (`LevelNavigation.tryGetBounds`) tem um pool fixo em `ZombieManager.regions[bound].zombies`.
  Dá pra criar zumbi novo em qualquer área com `ZombieManager.instance.addZombie(bound, tipo, ...)`, mas o cliente só vê
  depois de receber a lista da área de novo: chamar por reflexão o privado `SendZombiesToPlayer(conexão, bound)` pros
  jogadores com `player.movement.bound == bound`, e só então `zombie.sendRevive(...)`. `askZombies` está vazio (obsoleto).
  Tipo = índice em `LevelZombies.tables` (tabela "milit..." = militar). Feito em `Eventos.cs` `NascerZumbis`.
  Lugar sem área de zumbi (ex.: Costa Mesa Mall) não tem bound: usar zumbis das áreas vizinhas.
- **Chão no California 2**: raio de cima com `RayMasks.BLOCK_COLLISION` a partir de y=1000 bate num colisor invisível no céu.
  Use `Terreno(x, z)` (Montar.cs: raio com `GROUND|GROUND2`) e só depois um raio curto de ~40 m pra telhado/pedra.
- **Veículos**: vida 0 = explode depois de 4 s; sem dono e sem motorista o servidor simula e replica a posição.
  O Chopper 4031 é motor "PLANE" no .dat. `Limites.cs` apaga aeronaves soltas não trancadas longe de jogadores.
- **Barricadas soltas** (`BarricadeManager.dropNonPlantedBarricade`) ficam salvas no mundo depois do reinício.
- **Config.txt** do servidor é regravado pelo jogo ao ligar; mudanças nele valem no próximo início.
- **UI dos mods** (Workshop 3831267100 "DZ Menu"): posição/tamanho dos elementos só muda no projeto Unity + reenvio no Workshop.
  Efeitos: 51299 DZ_RelogioFixo, 51305 DZ_Avisos (key 5134, topo da tela), 51306 DZ_Morte (key 5135), 51307 DZ_Fumaca.

## Eventos do plugin
Heli Crash (`HeliCrash.cs`, /helicrash [aqui|parar]), clima com baú (`Eventos.cs`, /evento), comboio (`Comboio.cs`),
airdrop automático (`AirdropSorteio.cs`, `Admin.cs`). Todos usam `NascerZumbis` pros zumbis.
Heli Crash: a cada 30 min contados do sumiço do anterior; some em 15 min, ou 10 min depois que alguém chega.
