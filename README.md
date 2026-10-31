# Servidor Unturned estilo DayZ

Servidor de Unturned com RocketMod (RocketModFix) e um plugin próprio, o **SobrevivenciaCore**, que traz:

- **Dinheiro físico + ATM**: o dinheiro são as notas e moedas do próprio Unturned ($1, $2, $5, $10, $20, $50, $100). Fica no inventário e você perde se morrer. No ATM você deposita no banco (fica seguro) e saca.
- **Safe zone**: dentro dela não tem PvP (nem zumbi). Saindo dela, PvP liberado. Aparece aviso ao entrar e sair.
- **Lojas**: Armas, Mercado (comida e bebida), Loja Geral, Caçador (compra carne, couro e peixe) e **Mercado Negro** (armas militares, mais caro; coloque fora da safe zone).
- **Kit inicial**: dado na primeira entrada e a cada vez que o jogador renasce (comida, água, bandagens, lanterna, canivete, mochila e $50).
- **Airdrops**: plugin AirdropManager3.

## Instalar na VPS (Ubuntu 24.04)

Entre na VPS como `root` e rode:

```bash
curl -fsSL https://raw.githubusercontent.com/protalhard/SERVIDOR-UNTURNED/main/install.sh | bash
```

Ele pede o token **GSLT** (gere em https://steamcommunity.com/dev/managegameservers com o App ID `304930`).
Dá pra mudar nome, mapa e vagas assim:

```bash
curl -fsSL https://raw.githubusercontent.com/protalhard/SERVIDOR-UNTURNED/main/install.sh | SERVER_NAME="Meu Server" MAP=Russia MAX_PLAYERS=24 bash
```

O que o instalador faz: instala dependências, cria swap de 4 GB, cria o usuário `unturned`, baixa o jogo pelo SteamCMD, instala o RocketModFix, os plugins, cria o serviço `unturned` (liga sozinho com a VPS) e libera as portas 27015-27017.

**RAM:** o Unturned precisa de pelo menos 2 GB. Com 1 GB ele roda só com ajuda da swap e pode ficar lento.

## Comandos na VPS

| O que | Comando |
|---|---|
| Ver o log ao vivo | `journalctl -u unturned -f` |
| Reiniciar | `systemctl restart unturned` |
| Parar / ligar | `systemctl stop unturned` / `systemctl start unturned` |
| Atualizar tudo | `bash /home/unturned/repo/scripts/update-all.sh` |
| Backup | `bash /home/unturned/repo/scripts/backup.sh` |

Arquivos do servidor: `/home/unturned/server/Servers/dayz/`

## Primeira configuração dentro do jogo (admin)

Você é o dono (`Owner`) e está no grupo `admin` do Rocket. Vá até o lugar e digite:

1. `/addsafe Centro 80` cria a safe zone com 80 m de raio no lugar onde você está.
2. `/addatm Banco1` cria um ATM onde você está (raio 4 m). Pode criar vários.
3. `/setloja armas`, `/setloja comida`, `/setloja geral`, `/setloja caca` posicionam as lojas (raio 6 m).
4. `/setloja mercadonegro` em algum lugar escondido **fora** da safe zone.
5. `/setairdropspawn <nome>` marca pontos de airdrop (AirdropManager3).

Dica: construa uma barraquinha, placa ou NPC no lugar de cada loja/ATM pra ficar visível.

Outros comandos de admin: `/delsafe <nome>`, `/delatm <nome>`, `/dardinheiro <jogador> <valor>`, `/darkit [jogador]`, `/airdrop`, `/airdrophere`, `/massairdrop`.

## Comandos dos jogadores

| Comando | O que faz |
|---|---|
| `/saldo` | Mostra banco e dinheiro no bolso |
| `/depositar <valor\|tudo>` | Deposita no ATM |
| `/sacar <valor\|tudo>` | Saca no ATM |
| `/lojas` | Distância até lojas, ATMs e safe zones |
| `/loja [página]` | Lista os itens da loja onde você está |
| `/comprar <número> [qtd]` | Compra (paga com o dinheiro do bolso) |
| `/vender <número> [qtd\|tudo]` | Vende pra loja |
| `/vendertudo` | Vende toda a caça (carne, couro, peixe) |
| `/whenairdrop` | Tempo até o próximo airdrop |

## Mudar preços, itens e kit

Edite `/home/unturned/server/Servers/dayz/Rocket/Plugins/SobrevivenciaCore/SobrevivenciaCore.configuration.xml`
(aparece depois da primeira vez que o servidor liga) e no jogo digite `/rocket reload SobrevivenciaCore`.

Em cada item: `Id` = ID do item no Unturned, `Compra` = quanto o jogador paga (0 = não vende), `Venda` = quanto a loja paga pro jogador (0 = não compra).

Os preços padrão estão em [`plugins/SobrevivenciaCore/src/LojasPadrao.cs`](plugins/SobrevivenciaCore/src/LojasPadrao.cs).

## Mods da Workshop

Coloque os IDs em `/home/unturned/server/Servers/dayz/WorkshopDownloadConfig.json`, no campo `File_IDs`, e reinicie:

```json
"File_IDs": [1234567890, 9876543210],
```

## Compilar o plugin

```bash
cd plugins/SobrevivenciaCore
dotnet build -c Release
cp bin/Release/net48/SobrevivenciaCore.dll ../../server-files/Rocket/Plugins/
```

## Estrutura

```
install.sh                       instalador completo
scripts/                         update, backup e serviço systemd
server-files/                    configs que vão pro servidor
  Commands.dat.template          nome, mapa, dono, GSLT
  Rocket/Permissions.config.xml  quem pode usar cada comando
  Rocket/Plugins/                plugin SobrevivenciaCore já compilado
plugins/SobrevivenciaCore/       código-fonte do plugin (C#)
```
