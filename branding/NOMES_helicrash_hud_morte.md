# Mod DZ: relógio com ícones de status, avisos no HUD, tela de morte e fumaça do Heli Crash

Tudo vai no mesmo pacote do Workshop (3831267100). O plugin já está pronto: cada peça liga sozinha
assim que o servidor baixar o mod com estes IDs. Até lá o servidor usa o que já existe (tela de morte
do jogo, aviso no chat, fumaça vermelha da caixa de airdrop).

| ID | Nome no mod | O que é |
|---|---|---|
| 51299 | `DZ_RelogioFixo` (já existe) | ganha 3 ícones de status e o "piscar" de cada barra |
| 51305 | `DZ_Avisos` (efeito, NOVO) | faixas de aviso com contagem (TPA, teleporte, combate, raid, heli crash, horda) |
| 51306 | `DZ_Morte` (efeito, NOVO) | tela VOCÊ MORREU com as estatísticas e o botão Renascer |
| 51307 | `DZ_Fumaca` (efeito, NOVO) | coluna de fumaça preta com fogo, pros destroços do Heli Crash |

Os nomes têm que ser **iguais** (maiúsculas e minúsculas contam). Cada efeito novo precisa de GUID novo.
Estilo: o do DZ Menu (preto translúcido, laranja `#FF5A00`, âmbar `#FFB020`, fonte Inter Tight / JetBrains Mono).
Os ícones prontos estão em `branding/hud-status/` (64, 128 e 512 px): `icone_sangrando`, `icone_quebrado`, `icone_radiacao`.

---

## 1. Relógio fixo (51299): o que entra

| Nome | Tipo | O que o plugin faz |
|---|---|---|
| `Rel_Sangrando` | visibilidade (desligado no prefab) | liga enquanto o jogador sangra |
| `Rel_Quebrado` | visibilidade (desligado no prefab) | liga com a perna quebrada |
| `Rel_Radiacao` | visibilidade (desligado no prefab) | liga na zona de radiação ou com imunidade em 25% ou menos |
| `Rel_Vida_Baixo`, `Rel_Fome_Baixo`, `Rel_Sede_Baixo`, `Rel_Imunidade_Baixo` | visibilidade (desligado) | liga com 25% ou menos |
| `Rel_Stamina_Baixo`, `Rel_Oxigenio_Baixo` | visibilidade (desligado) | liga com 5% ou menos |

- Os 3 ícones ficam numa fileira logo acima do relógio (onde ficavam os ícones do jogo), 40 a 48 px cada, nessa ordem.
- Cada ícone tem um **Animator piscando** (alpha 1 → 0,35 → 1, 0,8 s, em loop). O plugin só liga e desliga.
- `_Baixo` é um contorno/brilho vermelho por cima daquele pedaço do anel, também piscando.

## 2. Avisos no HUD (51305, `DZ_Avisos`)

Não é modal (sem cursor). Canvas **Sort Order -1**. Coluna no meio da tela, um pouco acima do centro
(ou canto de cima à esquerda, se preferir), estilo das faixas do print do Zumbas.

4 faixas empilhadas, `i` = 1 a 4 (1 em cima):

| Nome | Tipo | Exemplo |
|---|---|---|
| `Av_i` | visibilidade (desligado no prefab) | a faixa inteira |
| `Av_i_Texto` | texto | `PEDIDO DE TPA  41s  Joao` |
| `Av_i_Tpa` | visibilidade | ícone de sino + borda azul `#4DA3FF` |
| `Av_i_Teleporte` | visibilidade | ícone de pino + borda azul `#4DA3FF` |
| `Av_i_Combate` | visibilidade | ícone de bala + borda âmbar `#FFB020` |
| `Av_i_Raid` | visibilidade | ícone de escudo riscado + borda vermelha `#FF3B30` |
| `Av_i_Heli` | visibilidade | ícone de helicóptero + borda laranja `#FF5A00` |
| `Av_i_Horda` | visibilidade | ícone de caveira + borda vermelha `#FF3B30` |
| `Av_i_Limpa` | visibilidade | ícone de caveira + borda verde `#3DDC84` (HORDA LIMPA) |

O plugin liga **um** tipo por faixa. Deixe os 7 tipos desligados no prefab.
Som: ponha um AudioSource curto (bipe) com Play On Awake em `Av_i_Tpa` e `Av_i_Heli`, assim toca quando o aviso aparece.

## 3. Tela de morte (51306, `DZ_Morte`)

**Modal** (o plugin liga o cursor e esconde a tela de morte do jogo). Fundo escuro em tela cheia, painel no meio.

| Nome | Tipo | Exemplo |
|---|---|---|
| `Morte_Titulo` | texto | `VOCE MORREU` (caveira vermelha em cima, fixa no prefab) |
| `Morte_Por` | texto (rich text) | `Morto por [SUPREMO] RNexe · na cabeca` ou `Sangrou ate morrer` |
| `Morte_Matador` | visibilidade | bloco com os 4 campos abaixo (some quando não foi jogador) |
| `Morte_Arma` | texto | `Maplestrike` |
| `Morte_Distancia` | texto | `143 m` |
| `Morte_Matador_Vida` | texto | `62%` (vida que sobrou no matador) |
| `Morte_Vivo` | texto | `12:30` (tempo vivo) |
| `Morte_Abates` | texto | `3` (abates nessa vida) |
| `Morte_DanoDado` | texto | `340` |
| `Morte_DanoLevado` | texto | `512` |
| `Morte_Kd` | texto | `1.75` (K/D geral) |
| `Morte_Renascer` | botão | renasce no spawn |
| `Morte_Renascer_Texto` | texto dentro do botão | `RENASCER EM 5s` → `RENASCER` |
| `Morte_Casa` | botão (desligado no prefab) | renasce na cama; o plugin liga se o jogador tem cama |
| `Morte_Casa_Texto` | texto dentro do botão | `NA CAMA EM 20s` → `RENASCER NA CAMA` |

Rótulos fixos no prefab: ARMA, DISTANCIA, VIDA DELE, TEMPO VIVO, ABATES, DANO DADO, DANO LEVADO, K/D.

## 4. Fumaça do Heli Crash (51307, `DZ_Fumaca`)

Efeito comum (não é UI). O plugin dispara ele a cada 8 s nos destroços e a cada 0,25 s atrás do heli caindo.

- Particle System, Looping off, Duration ~8 s, Start Lifetime 10 a 14 s, Simulation Space **World**.
- Fumaça preta subindo (velocidade Y 4 a 6 m/s, tamanho 4 a 9 m, crescendo com o tempo, cor `#1A1A1A` → transparente).
- Algumas faíscas/fogo laranja embaixo nos primeiros 2 s.
- Lifetime do `.dat` 15 e `Relevant_Distance` grande (o plugin manda pra todo mundo).
- Deve ser visível de uns 1.000 m.
