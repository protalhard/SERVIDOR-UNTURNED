# Mod DZ: HUD novo, headshot, quem te acertou, tela de morte 2 e abas novas do menu

Tudo no mesmo pacote do Workshop (3831267100). O plugin já está pronto: cada peça liga sozinha quando o
servidor baixar o mod com estes IDs. Os nomes têm que ser **iguais** (maiúsculas contam). Efeito novo = GUID novo.
Estilo do DZ Menu (preto translúcido, laranja `#FF5A00`, âmbar `#FFB020`, vermelho `#FF4D4D`, verde `#5BE37D`).
Referência visual: os vídeos do Zumbas em `/mnt/project-files/` (2026-11-07 01-15-09, 01-24-12, 01-29-18, 01-38-14).

| ID | Nome no mod | Key | O que é |
|---|---|---|---|
| 51313 | `DZ_Status` (NOVO) | 5138 | quadrados de status, faixas EM COMBATE e SACOLA, painel da arma |
| 51314 | `DZ_Acerto` (NOVO) | 5136 | número de dano, cabecinha do headshot, marca de abate e os sons (pra quem atira) |
| 51315 | `DZ_Atingido` (NOVO) | 5137 | faixa "quem te acertou" (pra quem levou o tiro) |
| 51306 | `DZ_Morte` (já existe) | 5135 | ganha o cartão QUEM TE MATOU, reportar, Discord/site e sons |
| 51305 | `DZ_Avisos` (já existe) | 5134 | 4 tipos novos de faixa |
| DZ Menu | (já existe) | | abas EVENTOS e COMANDOS, opções no PERFIL, sons ao passar o mouse e clicar |

Se algum ID estiver ocupado, use outro e me diga: ele fica no `Sistemas` do plugin (`StatusEfeito`, `AcertoEfeito`, `AtingidoEfeito`).

---

## 1. DZ_Status (51313)

Não é modal. Canvas Sort Order -1. Sempre na tela (o plugin tira quando abre inventário, menu ou morre).
O plugin esconde as barras e a munição do próprio jogo enquanto isso está ligado.

**Embaixo à esquerda**, 4 quadrados coloridos de ~44 px com a % embaixo (igual ao print do Zumbas):

| Nome | Tipo | Exemplo |
|---|---|---|
| `Status_Vida_Txt` | texto | `100%` (quadrado vermelho com coração) |
| `Status_Fome_Txt` | texto | `98%` (laranja, talheres) |
| `Status_Sede_Txt` | texto | `97%` (azul, gota) |
| `Status_Energia_Txt` | texto | `100%` (amarelo, raio) |
| `Status_Radiacao` + `Status_Radiacao_Txt` | visibilidade + texto | 5º quadrado verde com símbolo de radiação, só aparece quando tem radiação |
| `Status_Vida_Baixo`, `Status_Fome_Baixo`, `Status_Sede_Baixo`, `Status_Energia_Baixo` | visibilidade (desligado) | borda vermelha piscando no quadrado (abaixo de 25%) |

**Faixas em cima dos quadrados** (empilhadas, a de combate em cima):

| Nome | Tipo | Exemplo |
|---|---|---|
| `Status_Combate` | visibilidade (desligado) | faixa com borda âmbar e ícone de bala |
| `Status_Combate_Txt` | texto | `EM COMBATE 14s` |
| `Status_Sacola` | visibilidade (desligado) | faixa com borda rosa/vermelha e ícone de sacola |
| `Status_Sacola_Txt` | texto | `SACOLA 9:32` |

Use um Vertical Layout Group pra quando só uma delas estiver ligada não ficar buraco.

**Embaixo à direita**, painel da arma (fundo preto translúcido, ~260 x 70):

| Nome | Tipo | Exemplo |
|---|---|---|
| `Arma` | visibilidade (desligado) | o painel inteiro (só com arma de fogo na mão) |
| `Arma_Nome` | texto | `M1911 "Colt"` (pequeno, em cima) |
| `Arma_Municao` | texto | `7` (grande, com ícone de bala amarelo do lado) |
| `Arma_Max` | texto | `/7` (pequeno, do lado) |
| `Arma_Modo` | texto | `SEMI` / `AUTO` / `RAJADA` / `TRAVA` (etiqueta laranja à direita) |
| `Arma_Vazia` | visibilidade (desligado) | deixa o número vermelho / piscando quando acabou a bala |

## 2. DZ_Acerto (51314)

Não é modal, sem fundo, Sort Order -1. O plugin manda de novo a cada acerto, então as animações começam do zero.

| Nome | Tipo | O que é |
|---|---|---|
| `Acerto_Dano` | texto | `-54`, aparece um pouco à direita da mira, sobe e some em ~0,8 s (Animator) |
| `Acerto_Cabeca` | visibilidade (desligado) | ícone de cabecinha/caveira vermelha no meio da tela, cresce e some em ~0,6 s |
| `Acerto_Abate` | visibilidade (desligado) | marca de abate (X vermelho em volta da mira) |
| `Acerto_Som` | visibilidade (desligado) | só um AudioSource, Play On Awake: tique curto de acerto |
| `Acerto_Som_Cabeca` | visibilidade (desligado) | só um AudioSource, Play On Awake: som de headshot ("ding" metálico forte) |

Os sons ficam desligados no prefab; o plugin liga o que deve tocar (respeita a opção "Sons da interface").

## 3. DZ_Atingido (51315)

Não é modal, Sort Order -1. Faixa embaixo no meio, acima da hotbar, some sozinha em ~3 s (Animator de fade).

| Nome | Tipo | Exemplo |
|---|---|---|
| `Atingido_Foto` | **Image** (não RawImage) | foto da Steam de quem atirou |
| `Atingido_Nome` | texto | `Pozzebonn` |
| `Atingido_Arma` | texto | `Gangster` (amarelo, embaixo do nome) |
| `Atingido_Dano` | texto | `-80` (vermelho) |
| `Atingido_Cabeca` | visibilidade (desligado) | ícone de mira/cabeça à direita quando foi na cabeça |

## 4. DZ_Morte (51306): o que entra

Cartão **QUEM TE MATOU** à esquerda (igual ao Zumbas) e botões embaixo à direita:

| Nome | Tipo | Exemplo |
|---|---|---|
| `Morte_Matador_Foto` | **Image** | foto da Steam do matador (quadrado com borda) |
| `Morte_Matador_Nome` | texto | `[Sobrevivente] Pozzebonn's In the Game` |
| `Morte_Matador_Stats` | texto | `11 abates · K/D 0.31 · HS 63%` |
| `Morte_Cabeca` | visibilidade (desligado) | etiqueta rosa `NA CABEÇA` ao lado de `Morte_Distancia` e `Morte_Arma` |
| `Morte_Reportar` | botão + visibilidade | botão largo `REPORTAR` (some quando não foi jogador) |
| `Morte_Reportar_Texto` | texto | `REPORTAR` / `REPORTADO` |
| `Morte_Report` | visibilidade (desligado) | janela por cima: título, aviso e campo |
| `Morte_Report_Titulo` | texto | `REPORTAR Pozzebonn` |
| (texto fixo) | | caixa vermelha "ATENÇÃO: report falso é punido. Use só se você realmente suspeita de cheat." |
| `Morte_Report_Motivo` | **InputField** | placeholder `Motivo (opcional)` |
| `Morte_Report_Cancelar` | botão | `CANCELAR` |
| `Morte_Report_Enviar` | botão | `ENVIAR REPORT` |
| `Morte_Discord` | botão | `DISCORD` (roxo `#5865F2`) |
| `Morte_Site` | botão | `SITE` |

Sons da tela de morte (todos AudioSource):
- Ao abrir: um som grave de morte (Play On Awake no objeto raiz).
- Cada botão: tique ao passar o mouse e clique ao apertar (Event Trigger → PointerEnter / PointerClick → AudioSource.PlayOneShot).

## 5. DZ_Avisos (51305): tipos novos

Mais 4 tipos em cada faixa (`i` = 1 a 4), desligados no prefab:

| Nome | Ícone e borda | Exemplo |
|---|---|---|
| `Av_i_Base` | escudo quebrado, vermelho `#FF3B30`, **bipe de alarme** | `SUA BASE ESTA SENDO ATACADA  120 m  Joao` |
| `Av_i_Comboio` | caminhão, laranja `#FF5A00` | `COMBOIO  2.3 km  EXPLODE EM 12:30` |
| `Av_i_Evento` | raio/nuvem, âmbar `#FFB020` | `TEMPESTADE  4:59` |
| `Av_i_Dica` | "i" azul `#4DA3FF` | `Use /menu para acessar o menu!` |

## 6. DZ Menu: abas novas e sons

Botões de aba `Aba_Eventos` e `Aba_Comandos` (com `Aba_Eventos_Sel` / `Aba_Comandos_Sel`), páginas `Pag_Eventos` e `Pag_Comandos`.

**Pag_Eventos** (cartões, um por evento, com contagem grande):

| Nome | Tipo | Exemplo |
|---|---|---|
| `Ev_Heli_Status` | texto | `Proximo em 12:30` / `CAIU perto de Costa Mesa · some em 9:12` |
| `Ev_Heli_Ativo` | visibilidade | brilho vermelho no cartão quando está acontecendo |
| `Ev_Marcar_Heli` | botão + visibilidade | `MARCAR NO MAPA` |
| `Ev_Comboio_Status`, `Ev_Comboio_Ativo`, `Ev_Marcar_Comboio` | igual ao heli | |
| `Ev_Airdrop_Status` | texto | `Proximo em 18:02` |
| `Ev_Clima_Status`, `Ev_Clima_Ativo` | texto + visibilidade | evento de clima com baú |
| `Ev_Online` | texto | `37 / 120 online` |
| `Ev_Ultimo_1` .. `Ev_Ultimo_6` | texto + visibilidade | `Joao chegou primeiro no Heli Crash · ha 4 min` |
| `Ev_Vazio` | visibilidade | "Nenhum evento ainda hoje" |

**Pag_Comandos**:

| Nome | Tipo | Exemplo |
|---|---|---|
| `Cmd_Atalho_1` .. `Cmd_Atalho_5` | texto | ao lado de uma tecla desenhada `1`..`5`: `Abre e fecha o menu`, `Kits`, `Vaults`, `Homes e TP pra safe`, `Liga e desliga o painel de stats` |
| `Cmd_Dica` | texto | `Configure em Esc > Options > Controls > Plugin Hotkey 1 a 5` |
| `Cmd_Grupo_1_Titulo` .. `Cmd_Grupo_6_Titulo` | texto | `TELEPORTE`, `KITS E LOJA`, `DINHEIRO`, `BASE E COFRE`, `FACCAO`, `INFO` |
| `Cmd_Grupo_1_Texto` .. `Cmd_Grupo_6_Texto` | texto (várias linhas) | `/safe ou /warp safe  ir pra safe zone` ... |

**Pag_Perfil**, bloco "SUA INTERFACE" (4 linhas com interruptor):

| Botão | Visto/ligado | Texto da linha |
|---|---|---|
| `Perfil_Op_HudNovo` | `Perfil_Op_HudNovo_On` | HUD novo (status e arma) |
| `Perfil_Op_Stats` | `Perfil_Op_Stats_On` | Painel de stats |
| `Perfil_Op_Dano` | `Perfil_Op_Dano_On` | Números de dano e quem te acertou |
| `Perfil_Op_Sons` | `Perfil_Op_Sons_On` | Sons da interface |

**Sons do menu inteiro**: todo botão com tique ao passar o mouse e clique ao apertar (Event Trigger, igual à tela de morte).
Um som curto ao abrir o menu também fica bom.

## 7. Sons pra achar (livres de direitos)

| Som | Onde |
|---|---|
| tique de hover (bem curto, ~40 ms) | todos os botões |
| clique | todos os botões |
| morte (grave, ~1 s) | DZ_Morte ao abrir |
| headshot ("ding" metálico) | `Acerto_Som_Cabeca` |
| acerto (tique seco) | `Acerto_Som` |
| alarme de base (bipe duplo) | `Av_i_Base` |
