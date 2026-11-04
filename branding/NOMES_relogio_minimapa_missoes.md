# Mod DZ: relógio fixo, minimapa, tela de missões, NPC de missão e TAG

Tudo isso vai no mesmo pacote do Workshop (3831267100). O plugin já está pronto: assim que o servidor
baixar o mod com esses IDs, cada coisa liga sozinha. Até lá nada muda no jogo.

| ID | Nome no mod | O que é |
|---|---|---|
| 51299 | `DZ_RelogioFixo` (efeito) | relógio modelo A, fixo no canto de baixo à esquerda |
| 51300 | `DZ_Minimapa` (efeito) | minimapa redondo no canto de cima à direita |
| 51301 | `DZ_Missoes` (efeito) | tela das missões diárias (com cursor) |
| 51302 | `DZ_NpcMissoes` (barricada) | boneco de missão: aperta F e abre a tela |
| 51303 | `DZ_Tag` (item) | TAG da recompensa na cabeça |

Cada um precisa de GUID novo. Os nomes abaixo têm que ser **iguais** (maiúsculas e minúsculas contam).

---

## 1. Relógio fixo (51299) — modelo A com ☣

Imagem aprovada: `relogio-modelo-A2.png`. Não é modal: sem cursor, não bloqueia clique.

- Canvas com **Sort Order -1** (fica por baixo das outras telas do servidor). Inventário e menus cobrem ele;
  o plugin também esconde o relógio enquanto o inventário ou um menu está aberto, e quando a pessoa morre.
- Canto de baixo à esquerda, no lugar das barras do jogo. O plugin some com as barras do Unturned sozinho.

| Nome | Tipo | Exemplo |
|---|---|---|
| `Rel_Hora` | texto | `14:32` |
| `Rel_Dia` | texto | `TARDE` (MADRUGADA, MANHÃ, TARDE, NOITE) |
| `Rel_Vida_Txt`, `Rel_Fome_Txt`, `Rel_Sede_Txt`, `Rel_Imunidade_Txt`, `Rel_Stamina_Txt`, `Rel_Oxigenio_Txt` | texto | `87%` |
| `Rel_Vida_1` … `Rel_Vida_10` (e igual pros outros 5) | visibilidade | os 10 tracinhos de cada pedaço do anel (o plugin liga N de 10) |

Os 6 estados: Vida (vermelho ♥), Fome (âmbar 🍴), Sede (azul 💧), Imunidade (verde ☣, é a contaminação),
Stamina (branco ⚡), Oxigênio (azul claro ○). "DAY Z BRASIL" é texto fixo no prefab.
Os `_Txt` podem ficar escondidos no prefab se você não quiser número; o plugin manda mesmo assim.

## 2. Minimapa (51300)

Modelo: `minimapa-modelo.png`. Não é modal. Canvas **Sort Order -1**, canto de cima à direita
(o painel DZ_Hud fica no meio da altura, então não bate).

| Nome | Tipo | O que faz |
|---|---|---|
| `Mini_Mapa` | **RawImage** | o plugin manda a imagem do mapa por URL (256x256, o jogador no meio, norte pra cima) |
| `Mini_Seta_0` … `Mini_Seta_7` | visibilidade | 8 setas no centro, uma pra cada direção, já giradas: 0 = norte (pra cima), 1 = nordeste, 2 = leste, 3 = sudeste, 4 = sul, 5 = sudoeste, 6 = oeste, 7 = noroeste. Deixe todas desligadas no prefab; o plugin liga uma |
| `Mini_Local` | texto | nome do lugar mais perto (`Oasis`) |

Como montar o redondo: um objeto com **Mask** (imagem de círculo) e o `Mini_Mapa` dentro, tamanho uns 220x220.
Borda laranja `#FF5A00` por cima, "N" fixo em cima. As setas ficam no centro exato do círculo.
A imagem vem do site (`/mini/x_y.jpg`) e muda a cada 8 m andados.

Hoje aparece pra todo mundo. Se quiser só pra quem tem GPS na mochila, é uma chave no plugin (`MiniMapaPrecisaGps`).

## 3. Tela de missões (51301)

Modal (com cursor), estilo do DZ Menu: preto translúcido com laranja `#FF5A00` e âmbar `#FFB020`.
Abre pelo NPC de missão ou por `/missoes`.

| Nome | Tipo | Exemplo |
|---|---|---|
| `Mis_Data` | texto | `MISSOES DE HOJE  04/11` |
| `Mis_Aviso` | texto | `Recebido! O dinheiro foi pro banco.` ou `Receba dentro da safe.` |
| `Mis_Fechar` | botão | fecha |

Três cartões iguais, um embaixo do outro, com `i` = 1 (fácil), 2 (média), 3 (difícil):

| Nome | Tipo | Exemplo |
|---|---|---|
| `Mis_i_Nivel` | texto | `FACIL` / `MEDIA` / `DIFICIL` |
| `Mis_i_Titulo` | texto | `Mate 30 zumbis` |
| `Mis_i_Premio` | texto | `$700 + 100 XP` |
| `Mis_i_Prog` | texto | `12/30` |
| `Mis_i_Barra_1` … `Mis_i_Barra_10` | visibilidade | barra de progresso (o plugin liga N de 10) |
| `Mis_i_Receber` | botão (desligado no prefab) | aparece quando a missão está pronta |
| `Mis_i_Recebida` | visibilidade (desligado no prefab) | selo "RECEBIDA" |

Exemplo: `Mis_1_Titulo`, `Mis_2_Barra_7`, `Mis_3_Receber`.

## 4. NPC de missão (51302)

Igual aos outros vendedores: barricada com **Storage** (é o que faz o F funcionar), só com o boneco diferente
(colete, prancheta na mão, algo assim). Copie o .dat de um NPC que já existe e troque ID, GUID e modelo.
O plugin já trata o 51302 como NPC: imortal, ninguém desmonta, e o F abre a tela de missões.
Depois de publicar, coloque ele na safe com `/item 51302` e plante onde quiser.

## 5. TAG da recompensa (51303)

Item pequeno 1x1, tipo **Supply**, que **não nasce no mapa** (não coloque em nenhuma tabela de spawn).
Modelo: uma plaquinha de identificação militar (dog tag) com corrente. Nome no jogo: `TAG de Recompensa`,
descrição: `Leve até a safe para receber a recompensa.` Raridade Epic.

O plugin cuida do resto: quando o procurado morre ela cai no chão, quem chegar com ela na safe recebe o
dinheiro, e cada TAG só vale uma vez (ninguém consegue copiar ou dar `/item` pra ganhar).
