# Montar uma trincheira nova do zero

ANJOCA Protocol · Unity 6 (HDRP) · 08/11/2026

Este guia é para quem nunca mexeu na trincheira. Usa só os prefabs que já existem em `Assets/ANJOCA/Prefabs/Trincheira/`, por isso não é preciso construir peças nem configurar scripts de raiz. Se quiseres saber como cada peça foi feita, ou o que faz cada campo, está tudo no `Trincheira.md`.

No fim tens uma trincheira em zigue-zague, vigiada por um sniper numa torre. Se o sniper te vir de pé, enche-se uma barra no ecrã. Se a barra encher, dá-se o alarme geral: o sniper dispara, os soldados da zona vêm atrás de ti e saem reforços escondidos à volta da trincheira.

---

## O que uma trincheira tem

| Peça | Prefab | Quantas | O que faz |
|---|---|---|---|
| Troços | `Troco_Aberto`, `Troco_Coberto`, `Troco_Baixo`, `Troco_Canto` | as que o traçado pedir | as paredes e o chão. Encaixam como Lego |
| Torre | `Torre_Sniper` | 1 por zona (podem ser mais) | o sniper com o holofote e o laser |
| Zona | `Zona_Trincheira` | 1 por cada pedaço de trincheira (a de teste cobre 34 m) | a caixa que controla a barra, o alarme e os reforços |
| Reforços | `Reforco_Trincheira` | uns 6 por zona | soldados desligados, escondidos à volta. Ligam-se no alarme |
| Soldados | os soldados normais da cena | opcional | soldados que já patrulham perto e que vêm atrás de ti no alarme |

Os quatro troços:

| Prefab | Tamanho | Altura das paredes | Para quê |
|---|---|---|---|
| `Troco_Aberto` | 4 m de comprido | 1,6 m | o normal. De pé vê-se a cabeça, abaixado estás tapado |
| `Troco_Coberto` | 4 m | 1,6 m, com teto a 1,5 m | passagem segura. Só se passa abaixado, e os soldados não te seguem por lá (não cabem) |
| `Troco_Baixo` | 4 m | 1 m | o troço perigoso. Mesmo abaixado, o sniper vê-te. Passa-se quando o holofote está virado para o outro lado |
| `Troco_Canto` | 2 × 2 m | 1,6 m | a curva de 90° |

Como fica na Hierarchy (troca `Norte` pelo nome da tua trincheira):

```
Trincheira                          (Empty na raiz, já existe)
└── Trincheira_Norte                (Empty: a trincheira nova)
    ├── Trocos                      (Empty, Position 0, 0, 0)
    │   ├── T1   (Troco_Aberto)
    │   ├── C1   (Troco_Canto)
    │   └── ...
    ├── Torre_Norte                 (Torre_Sniper)
    ├── Zona_Norte                  (Zona_Trincheira)
    └── Reforcos                    (Empty, Position 0, 0, 0)
        ├── Reforco_1               (Reforco_Trincheira, desligado)
        └── ...
```

---

## Antes de começar (só uma vez por projeto)

Se já fizeste pull do ramo com a trincheira de teste, isto já está tudo feito. Confirma só que nada falta:

| O que tem de existir | Onde ver | Se faltar |
|---|---|---|
| Os scripts `Sniper.cs`, `DetecaoZona.cs` e `HUDDetecao.cs` | Project, pasta dos scripts da trincheira | copia-os de `scripts/Trincheira/` |
| O `PlayerMovement.cs` novo, com o grupo **Andar abaixado** e a **Tecla Abaixar** em `Left Ctrl` | `Player` › **Player Movement** | `Andar_Abaixado.md`, Parte A |
| O `Weapon.cs` com as linhas do sniper (sem elas, não consegues matar o sniper) | procura `Sniper.Atingido` no `Weapon.cs` | `Trincheira.md`, passo 19 |
| O componente **HUD Detecao** no Player | `Player` › Inspector | `Trincheira.md`, passo 14. É só um por cena, serve para todas as trincheiras |
| O objeto do jogador chama-se `Player` | Hierarchy | o sniper encontra-o pelo nome |
| Os sons em `Assets/ANJOCA/Efeitos`: `Alarme_Geral`, `Alerta_1` a `3`, `Mira_Ativar`, `Mira_Desativar`, `Tiro_M4`, `Morte_1` e `2` | Project | estão em `assets/Efeitos/` |
| Os materiais `M_Trincheira_Lama`, `M_Trincheira_Madeira` e `M_Laser` | `Materials` | `Trincheira.md`, passos 1 e 8 |
| Os 7 prefabs em `Prefabs/Trincheira/` | Project | `Trincheira.md`, secção Prefabs |
| Um objeto **NavMesh** na cena | Hierarchy | é o mesmo que já usamos para os soldados |

**Nota para o git:** os prefabs só funcionam noutro computador se os ficheiros `.meta` forem juntos no commit. Se ao abrir a cena aparecer `Missing Prefab` (a vermelho), faltou um `.meta`. E como a cena é um ficheiro só, dois de nós a mexer nela ao mesmo tempo dá conflitos difíceis de resolver: avisa os outros antes de montares a trincheira na cena principal.

---

## Passo 1: desenhar o traçado

Antes de abrir o Unity, desenha a trincheira vista de cima, num papel ou em quadrículas. Regras:

1. **Zigue-zague.** Nunca mais de 2 ou 3 troços retos seguidos sem uma curva. Uma reta comprida deixa o sniper ver ao comprido para dentro dela.
2. **Pensa já onde fica a torre.** Tem de ficar a uns 60 m, **na diagonal** dos troços. Se os troços correm para norte e para este, a torre fica a noroeste ou a sudeste. Nenhum troço pode apontar direito para a torre: se apontar, as paredes ficam dos lados e o sniper vê-te mesmo abaixado.
3. **Mistura os troços.** A maior parte `Troco_Aberto`, um ou dois `Troco_Coberto` (um sítio seguro para respirar) e um `Troco_Baixo` onde se quiser tensão.
4. **Escolhe um sítio plano** ou com pouca inclinação, sem árvores com collider por cima da trincheira (se houver um ramo por cima, o jogador não se consegue levantar).
5. **Comprimento:** a zona de teste cobre 34 m, com uma torre. Para uma trincheira bem maior, divide-a em duas ou mais zonas, cada uma com a sua torre. Se o holofote de uma torre (varre 60°) não apanhar a zona toda, põe uma segunda torre na mesma zona.

---

## Passo 2: montar os troços

### As medidas que precisas de saber

- Todos os troços têm o pivô no meio, ao nível do chão.
- Um troço reto com **Rotation Y** `0` corre ao longo do **Z** (norte–sul). Com `90`, corre ao longo do **X** (este–oeste).
- A distância entre os centros é sempre:

| De | Para | Distância entre os centros |
|---|---|---|
| troço reto | troço reto seguinte | **4 m** |
| troço reto | curva | **3 m** (2 m de meio troço + 1 m de meia curva) |
| curva | troço reto | **3 m** |

- A curva (`Troco_Canto`) tem dois lados abertos. Escolhe a rotação pelos lados por onde a trincheira entra e sai:

| Rotation Y da curva | Aberta para |
|---|---|
| `0` | sul e este |
| `90` | sul e oeste |
| `180` | norte e oeste |
| `270` | norte e este |

  Exemplo: a trincheira vem de sul e vira para este, Rotation Y `0`. Vem de oeste e vira para norte, `180`.

### Montar

1. Na Hierarchy, dentro do Empty `Trincheira` (que já existe na raiz), cria um Empty **Trincheira_Norte**. Põe-no no sítio do mapa onde vai ficar, com **Scale** `1, 1, 1`. **Não** o ponhas dentro do `Terrain` nem do `World_objects_placeholders`.
2. Dentro dele, cria um Empty **Trocos** com **Position** e **Rotation** `0, 0, 0`.
3. Arrasta os prefabs dos troços da pasta `Prefabs/Trincheira` para dentro do `Trocos` e escreve as Positions (locais) com as regras de cima. É mais rápido com **Ctrl + D** num troço e somar 4 ao Z (ou ao X).
4. Dá-lhes nomes curtos pela ordem do caminho: `T1`, `T2`... para os retos e `C1`, `C2`... para as curvas. Ajuda a falar deles nos testes.

### Exemplo: o traçado de teste

Este é o traçado da `Trincheira_Teste`, que já sabemos que funciona. São 34 m, com 7 troços e 3 curvas. Podes copiá-lo tal e qual para começar, e acrescentar troços depois.

| Nome | Prefab | Position | Rotation Y |
|---|---|---|---|
| T1 | Troco_Aberto | 0, 0, 2 | 0 |
| T2 | Troco_Aberto | 0, 0, 6 | 0 |
| C1 | Troco_Canto | 0, 0, 9 | 0 |
| T3 | Troco_Aberto | 3, 0, 9 | 90 |
| T4 | Troco_Coberto | 7, 0, 9 | 90 |
| C2 | Troco_Canto | 10, 0, 9 | 180 |
| T5 | Troco_Baixo | 10, 0, 12 | 0 |
| T6 | Troco_Aberto | 10, 0, 16 | 0 |
| C3 | Troco_Canto | 10, 0, 19 | 0 |
| T7 | Troco_Aberto | 13, 0, 19 | 90 |

```
                          C3 ── T7 ──▶ saída
                          │
                          T6
                          │
                          T5  (baixo)
                          │
     C1 ── T3 ── T4 ───── C2
     │        (coberto)
     T2
     │
     T1
     │
   entrada
```

Ocupa de `0, 0, 0` a `15, 0, 20` (vais precisar disto para a zona, no passo 4).

### Pôr no terreno

1. Com o modo **Pivot** ligado (botão em cima, à esquerda da Scene; não **Center**), muda o **Y** do `Trincheira_Norte` até o chão da trincheira ficar à altura do terreno ou um pouco acima. O terreno **não pode** ficar acima do chão da trincheira, senão aparece lá dentro. Um pouco abaixo não faz mal: o chão tem 1 m enterrado.
2. Se o terreno for inclinado, podes mudar o Y de cada troço à mão. Degraus pequenos (até uns 0,3 m) o jogador sobe sem saltar.
3. Se quiseres a trincheira noutra direção, roda o `Trincheira_Norte` em Y. Os troços rodam todos juntos e continuam encaixados.
4. No `Terrain`, **Paint Details** com **Shift** por cima da trincheira, para a relva não sair do chão.

O terreno não é alterado: as paredes estão todas acima do chão.

---

## Passo 3: a torre

1. Arrasta o prefab **`Torre_Sniper`** para dentro do `Trincheira_Norte` e muda-lhe o nome para `Torre_Norte`.
2. Põe-na a uns **60 m** do meio da trincheira, **na diagonal** dos troços (passo 1, regra 2). Com o traçado de teste, é a noroeste ou a sudeste.
3. Para a encaixar no chão: com **Pivot** ligado, segura **Ctrl + Shift** e arrasta a torre pela seta do meio. Ela cola-se à superfície do terreno.
4. Roda-a **só em Y** até a seta azul (a frente) apontar para o meio da trincheira.
5. Tira a relva de baixo dela (Paint Details com Shift).
6. Abre a torre até ao **Pivo** e, no componente **Sniper**, acerta só a **Inclinacao** pela distância a que a puseste. O resto já vem certo do prefab.

| Distância da torre à trincheira | Inclinacao |
|---|---|
| 40 m | `12` |
| 60 m | `8` |
| 80 m | `6` |

Não rodes o Pivo em X: a inclinação é aplicada só no Play. Antes do Play, com o Pivo selecionado, o gizmo na Scene mostra para onde ele vai olhar e, a amarelo, as pontas do varrimento. Confirma que a trincheira fica dentro.

Os valores que já vêm no prefab e não é preciso mexer: **Alcance** `120`, **Angulo Visao** `20`, **Angulo Varrimento** `30`, **Dano Tiro** `25`, **Intervalo Tiro** `2.5`, **Tempo Mira** `1`, **Vida** `100`, **Laser Sempre** desligado. O **Corpo**, a **Ponta Arma**, o **Clarao**, a **Luz Disparo**, o **Holofote** e os sons também têm de aparecer preenchidos. Se algum disser `None`, o prefab não foi guardado com as últimas mudanças (`Trincheira.md`, passo 22).

---

## Passo 4: a zona

1. Arrasta o prefab **`Zona_Trincheira`** para dentro do `Trincheira_Norte` e muda-lhe o nome para `Zona_Norte`.
2. Ajusta a caixa (componente **Box Collider**) para cobrir a trincheira toda com **8 m de margem** para cada lado:
   - **Position** da zona: o meio da trincheira.
   - **Center**: `0, 3, 0`
   - **Size**: largura da trincheira + 16, `10`, comprimento + 16

   Com o traçado de teste (de `0, 0, 0` a `15, 0, 20`): **Position** `7, 0, 10` e **Size** `32, 10, 36`. Na Scene, a caixa aparece a laranja.
3. Confirma que a **Layer** é `Ignore Raycast` (já vem do prefab). Sem isto, as balas e a visão dos soldados param na caixa.
4. No componente **Detecao Zona**, preenche as três listas. Vêm vazias porque um prefab não pode guardar ligações a objetos da cena:

| Lista | O que pôr | Cuidado |
|---|---|---|
| **Snipers** | o **Pivo** da `Torre_Norte` (não a torre em si) | sem isto, a barra nunca sobe |
| **Soldados** | os soldados normais que patrulham perto da trincheira | opcional. Não ponhas aqui os reforços |
| **Reforcos** | os `Reforco_Trincheira` do passo 5 | preenches no fim do passo 5 |

**Dica para as listas:** seleciona a zona e carrega no **cadeado** no canto de cima do Inspector, para ele não mudar quando clicas noutro objeto. Depois seleciona vários objetos na Hierarchy (com Ctrl) e larga-os em cima do nome da lista. Volta a carregar no cadeado no fim.

Os sons e os tempos já vêm do prefab: **Som Alarme** `Alarme_Geral`, **Vozes Alarme** `Alerta_1` a `3`, a barra enche em 2 s e o alarme acaba com 10 s sem ninguém te ver.

**Trincheira com duas zonas:** põe duas `Zona_Trincheira`, uma para cada metade, cada uma com a sua torre na lista **Snipers**. Faz as caixas encostadas, sem se sobreporem muito. Se estiveres nas duas ao mesmo tempo, o HUD mostra a barra mais cheia.

---

## Passo 5: os reforços

Os reforços estão desligados na cena, escondidos à volta da trincheira. Quando a barra enche, a zona escolhe até 3, só entre os que estão a **15 m a 60 m** de ti, de preferência os que não estás a ver, e liga-os. Quando o alarme acaba, voltam ao sítio e desligam-se outra vez.

1. Dentro do `Trincheira_Norte`, cria um Empty **Reforcos** com **Position** `0, 0, 0`.
2. Arrasta o prefab **`Reforco_Trincheira`** para dentro dele. Repete até teres uns **6**: `Reforco_1` a `Reforco_6`.
3. Espalha-os à volta da trincheira, a 10 m ou 12 m dela, fora das paredes: dos dois lados, ao pé da entrada, a meio e ao pé da saída. Numa trincheira comprida, mete-os ao longo dela toda, para em qualquer ponto haver alguns entre 15 m e 60 m do jogador.
4. Se puderes, esconde cada um atrás de uma encosta, de um arbusto ou de sacos de areia. Assim aparecem sem o jogador os ver chegar.
5. **Altura:** o objeto de fora do soldado é a cápsula, com o centro a 1 m dos pés. Encaixa-o no chão com **Ctrl + Shift** e depois soma `1` ao **Y** da Position, senão fica meio enterrado.
6. **Desliga-os todos**: tira o visto ao lado do nome, no topo do Inspector. Têm de ficar desligados na cena.
7. Na zona, lista **Reforcos**: põe os 6.

Exemplo, com o traçado de teste (Positions locais dentro do `Reforcos`):

| Reforço | Position | Onde fica |
|---|---|---|
| Reforco_1 | -10, 1, -2 | a sudoeste da entrada (T1) |
| Reforco_2 | 12, 1, -6 | a sudeste da entrada |
| Reforco_3 | -10, 1, 16 | a oeste do C1 |
| Reforco_4 | 24, 1, 6 | a este do T5 |
| Reforco_5 | 4, 1, 30 | a norte, depois do C3 |
| Reforco_6 | 24, 1, 26 | a nordeste da saída (T7) |

O Y `1` só está certo se o terreno ali estiver à mesma altura que a trincheira. Acerta-o a olho até os pés tocarem no chão.

---

## Passo 6: NavMesh e guardar

1. Seleciona o objeto **NavMesh** › **Bake**.
2. Com o NavMesh visível na Scene, confirma:
   - o azul entra pelas pontas e segue a trincheira toda, **menos os troços cobertos**. Isso é normal: os soldados não cabem debaixo do teto e têm de dar a volta;
   - há azul por baixo de **cada** reforço. Se não houver, muda o reforço de sítio.
3. **Ctrl + S** para guardar a cena.
4. No commit: a cena e, se mudaste algum prefab, o prefab com o `.meta`.

---

## Testar

Testa de noite, para veres o holofote. Durante o Play, seleciona o **Pivo** da torre ou a zona para veres no Inspector o que estão a fazer.

| O que fazes | O que deve acontecer |
|---|---|
| Olhas para a torre | o holofote varre de um lado para o outro, com uma pausa em cada ponta |
| De pé, dentro da caixa laranja, com o holofote em cima | o laser vermelho aponta-te, toca o `Mira_Ativar` e aparece a barra em baixo à esquerda, com `ALERTA: SNIPER - ABAIXA-TE!` |
| Abaixas-te (Ctrl) num troço aberto | o laser apaga-se, a barra para 1 s e desce |
| Abaixado num troço baixo, com o holofote em cima | a barra sobe na mesma |
| Num troço coberto | a barra não sobe. De pé não entras |
| Ficas 2 s à vista | sirene, uma voz e `ALARME GERAL`. 1 s depois o sniper dispara, de 2,5 em 2,5 s. Aparece `REFORÇOS A CAMINHO (3)` e 3 reforços ligam-se e vêm atrás de ti |
| Escondes-te 10 s | o alarme acaba. Os reforços voltam ao sítio e desligam-se |
| Disparas contra o sniper | um tiro na cabeça mata-o: apaga o holofote, cai e deixa de contar para a barra |

---

## Problemas comuns

| Problema | Causa provável |
|---|---|
| O sniper vê-te mesmo abaixado num troço aberto | a torre está no prolongamento de um troço e olha ao comprido para dentro dele: põe-na mais na diagonal. Se já estiver na diagonal, está perto demais (afasta-a para 60 m ou mais) |
| O sniper nunca te vê e não sabes porquê | durante o Play, seleciona o **Pivo** e lê o campo **Motivo** do `Sniper`: diz `longe`, `fora do cone` ou `tapado por: <objeto>` |
| O laser aponta-te mas a barra não aparece | estás fora da caixa da zona, ou o Pivo não está na lista **Snipers** |
| Dentro da caixa, as tuas balas param no ar ou os soldados não te acertam | a zona não está na layer `Ignore Raycast` |
| Não se ouve a sirene | a zona não tem **Audio Source** ou está em 3D (**Spatial Blend** tem de estar a `0`) |
| Uma lista da zona tem `Missing` ou `None` | apagaste ou trocaste esse objeto. Arrasta-o outra vez para a lista |
| O laser sai do sítio errado | a **Ponta Arma** do `Sniper` está vazia ou a apontar para outra coisa (tem de ser o `ShootingArea` da arma do soldado da torre) |
| Os teus tiros não fazem nada ao sniper | o `Weapon.cs` não tem as linhas do sniper, ou o **Range** da arma não chega à torre |
| Não aparece `REFORÇOS A CAMINHO` | os reforços estão ligados na cena, a lista **Reforcos** está vazia, ou estão todos a menos de 15 m ou a mais de 60 m de ti |
| Um reforço aparece logo no início do Play | está ligado na cena: tira-lhe o visto |
| A Console diz "SetDestination can only be called on an active agent..." | não há NavMesh por baixo de um reforço: muda-o de sítio e refaz o Bake |
| O terreno aparece por dentro da trincheira | a trincheira está baixa demais: sobe o Y do `Trincheira_Norte` |
| Há buracos entre os troços | uma Position ou uma Rotation Y está mal. Confirma com as distâncias do passo 2 (4 m entre retos, 3 m até à curva) |
| Abaixado, não te levantas fora do troço coberto | há um collider por cima (um ramo ou uma árvore). Afasta a trincheira ou tira esse collider |
| `Missing Prefab` ao abrir a cena | faltou um `.meta` no commit de quem fez o prefab |

Para tudo o resto, os problemas de cada parte estão no `Trincheira.md`.
