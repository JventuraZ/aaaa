# Estrutura da SampleScene VR SNC

Atualizado em 19/10/2026. Descreve os objetos de `Assets/Scenes/SampleScene.unity`: o que está ativo, o que vem do template VR, o que é do protótipo e como cada parte é usada. O mapa completo dos scripts está em `MAPA_DOS_SCRIPTS_VR_SNC.md`.

## Resumo da cena

- O rig XR do template fornece a câmera, os controles, as mãos e os eventos de interface.
- `Sistema_Nervoso_Central` contém a anatomia e os controladores do protótipo.
- `Painel_Anatomico_VR_SNC` é a janela espacial principal, no tema Grafite suave, com **‹ Voltar** e caminho clicável no cabeçalho, abas **Ficha**, **Estruturas** e **EEG** e uma barra de ações.
- `Gestos_Maos_VRSNC` guarda os gestos das mãos no ar, fora da hierarquia anatômica.
- `Oraculo_Anatomico_Punho`, dentro do rig, é o menu de pulso com a ficha resumida e a bibliografia. Aparece ao virar o pulso esquerdo para o rosto.
- `Brain` (modelo original), `UI` (interface do template) e `Teleport Area Setup` ficam desativados e preservados.

## Objetos principais

| Objeto (raiz da cena) | Estado | Origem | Função |
|---|---|---|---|
| `XR Origin Hands (XR Rig)` | Ativo | Template VR (`Complete XR Origin Set Up Hands Variant`) | Câmera, controles, mãos, oráculo no pulso e ponteiro direito. |
| `UI` | Desativado | Template VR | Interface original do template, preservada como referência. |
| `Environment` | Ativo | Template VR | Piso, paredes e céu. |
| `Teleport Area Setup` | Desativado | Template VR | Teleporte; o protótipo não usa locomoção. |
| `Lighting` | Ativo | Template VR | Iluminação. |
| `EventSystem` | Ativo | Template VR | Eventos de interface dos painéis (XR UI Input Module). |
| `Hands Permissions Manager` | Ativo | Template VR | Pede a permissão de rastreamento de mãos no Quest. |
| `Brain` | Desativado | Modelo original | Preservado; não é usado pela seleção. |
| `Sistema_Nervoso_Central` | Ativo | Protótipo | Hierarquia anatômica e controladores. |
| `Painel_Anatomico_VR_SNC` | Ativo | Protótipo | Janela espacial principal. |
| `Controlador_EEG` | Ativo | Protótipo (EEG do Protobrain) | `ControladorEEG`, `CabecaEEG` e `ApontadorEletrodosEEG`. Fica fora da hierarquia anatômica. Em execução cria `EEG_Cabeca` (escalpo e eletrodos que seguem a pose do SNC) e, quando pedidas, `Janela_Mapa_EEG` e `Janela_Tracados_EEG`. Veja `EEG_VR_SNC.md`. |
| `Gestos_Maos_VRSNC` | Ativo | Protótipo | `GestosMaosNoAr`: girar o modelo com uma mão, mover, girar e escalar o SNC com as duas e a pinça rápida da mão esquerda que faz o Voltar. Fica fora da hierarquia anatômica; é criado por **Ferramentas > VR SNC > Mãos > Instalar gestos no ar**. |

`Painel_Antigo_SNC` e `Botao_Reset_SNC`, citados em versões anteriores destes documentos, não existem mais. O reset fica no botão **Resetar SNC** e no botão Y do controle esquerdo.

## Posição de partida do usuário

Nos óculos, a cabeça começa na posição do `XR Origin Hands (XR Rig)`, olhando para a frente dele. Dali, o cérebro fica cerca de 0,66 m à frente e o painel principal 0,92 m adiante, 27° à direita, voltado para o usuário. Para mudar o ponto de partida, mova o `XR Origin`. Não desloque o `Camera Offset` nem a `Main Camera`: os óculos sobrescrevem a pose da câmera ao iniciar.

## Sistema_Nervoso_Central

Componentes na raiz:

- seleção: `NoInteracaoAnatomica`, `VinculoAnatomico`, `GerenciadorSelecaoHierarquica` e `ControladorSelecaoPorRaio`;
- entrada: `MapeadorEntradaMetaQuest2` e `RoteadorEntradaXR`;
- mãos: `PonteInteracaoMao` e `CoordenadorGestoAnatomico`;
- manipulação: `ManipuladorObjetoSelecionado` e `ManipuladorModeloAnatomico`;
- visualização e reset: `ControladorVisibilidadeAnatomica`, `ControladorVisaoExplodida`, `ControladorExpansaoGlobal`, `ControladorEsmaecimentoForaDoFoco` e `GerenciadorRestauracao`;
- retorno ao usuário: `RotuloEstruturaApontada` (nome ao apontar, que cria `Rotulo_Estrutura_Apontada` na raiz da cena em execução) e `RetornoSensorialInteracao` (vibração e som).

A hierarquia tem 272 nós (a raiz, 31 grupos e 240 estruturas com malha). Os quatro grupos do primeiro nível são:

- `Cerebro`: `Telencefalo` (córtex e lobos, núcleos da base, substância branca) e `Diencefalo`;
- `Cerebelo`: hemisférios esquerdo e direito;
- `Tronco_Encefalico`: `Mesencefalo`, `Ponte` e `Bulbo_Medula_Oblonga`;
- `Sistema_Ventricular`: ventrículos laterais, terceiro ventrículo, aqueduto cerebral, quarto ventrículo e `Plexo_Coroide`, que é um grupo sem modelo 3D.

Cada nó tem um `VinculoAnatomico` com sua identidade no catálogo. Cada grupo tem um `GrupoAnatomico`. Cada estrutura com malha tem uma `InformacaoEstruturaAnatomica` e um `ElementoInteragivelMao`, que é o alvo XRI das mãos. As estruturas mantêm os nomes de origem (em inglês). Os painéis mostram o nome em português da ficha do oráculo.

A hierarquia anatômica é conferida pelo verificador externo. Não a reconstrua, não a reclassifique e não renomeie seus objetos.

## XR Origin Hands (XR Rig)

Dentro de `Camera Offset`:

- `Main Camera`: a cabeça.
- `Left Controller` e `Right Controller`, cada um com um `Near-Far Interactor`, que aponta e clica na interface e agarra; um `Poke Interactor`; o `Teleport Interactor` do template, que não é usado porque não há área de teleporte ativa; e a `LegendaControleVRSNC`, o cartão com os comandos daquele controle que aparece quando o usuário olha para ele. Os balões de comandos do template (`Affordance Callouts Left/Right`) ficam desativados.
- `Left Hand` e `Right Hand`, para o rastreamento de mãos. Ficam inativos quando as mãos não estão rastreadas.
- `Oraculo_Anatomico_Punho`, o oráculo no pulso.
- `Right Controller Teleport Stabilized Origin/Right Controller Stabilized Attach/Ponteiro_Direito_SNC`: o feixe visual do raio de seleção (`VisualPonteiroLaser`). O raio do `ControladorSelecaoPorRaio` parte de `Right Controller Stabilized Attach`.

O `XRInputModalityManager` do rig alterna sozinho entre mãos e controles em cada lado.

## Painel_Anatomico_VR_SNC

Canvas em espaço de mundo, de 400 × 550 (cerca de 40 × 55 cm), com `PainelInformacoesAnatomicas`, `PainelNavegacaoAnatomica`, `AbasPainelAnatomico`, `BarraCaminhoAnatomico`, `AcompanhamentoCameraPainelInterface`, `ControlePainelMundo` e `JanelaEspacialPrincipal`. O layout vem de **Ferramentas > VR SNC > Interface > Aplicar Painel Compacto com Abas**. Faixas de `ExpandedRoot`, de cima para baixo:

| Faixa | Conteúdo |
|---|---|
| `Header` | Na faixa de cima, o botão **‹ destino** (`Button_Back`, que saiu da barra de ações) à esquerda e o estado (`Status`: "Em foco", "Estrutura selecionada"…) à direita. Abaixo, o nome em português em uma linha (`Title`) e o caminho clicável `Caminho_Navegavel`, criado em execução no lugar de `Path`: "Início › … › últimos níveis". Cada nível volta àquele grupo; o caminho se ajusta à largura. |
| `Abas_Painel` | Abas **Ficha**, **Estruturas** e **EEG**. Entrar num grupo abre Estruturas; selecionar uma estrutura final abre Ficha; com a aba EEG aberta nada troca sozinho. |
| `EEGPanel` (aba EEG) | Montada em execução pelo `PainelEEG`: reprodução e linha do tempo, bandas, normalizações e voltagem, o eletrodo escolhido (córtex sob ele e giro do modelo), os botões **Mapa 2D**, **Traçados**, **Ficha do giro** e o do escalpo, e o aviso de leitura. **Mapa 2D** e **Traçados** abrem janelas próprias (`Janela_Mapa_EEG` e `Janela_Tracados_EEG`). |
| `InfoPanel` (aba Ficha) | Categoria (`DisplayName`: "Estrutura anatômica", "Grupo anatômico · N estruturas", "Sem modelo 3D no acervo atual" ou "Como explorar") e a ficha com rolagem (`DescriptionViewport`, cerca de 12 linhas em letra 20). A linha `Division` fica oculta: a classificação aparece no topo da ficha. |
| `NavigationPanel` (aba Estruturas) | "Estruturas de: {foco}", o filtro **Todos / Esquerdo / Direito** (quando a lista tem lados), a linha **Mostrar N estruturas ocultas** (quando há) e a lista dos filhos do foco, em ordem alfabética, com itens de 34 px (cerca de 7 visíveis). A seleção atual fica em âmbar e a lista rola até ela; uma lista curta fica encostada no topo. |
| `Controls` | Barra de ações em 3 × 2: Isolar, Ocultar (ou Mostrar), Explodir / Limpar seleção, Entrar, Resetar SNC (em vermelho suave). Entrar só fica ativo com um grupo selecionado. Os inativos ficam translúcidos. `Button_ToggleList` continua na cena, desativado. |
| `Controles_Espaciais_Painel` | SEGUIR, RECENTRAR, FIXAR e **Minimizar** (o antigo "Ocultar painel"). O modo ativo fica destacado. |
| `Barra_Manipulacao_Mundo` | Alça inferior: agarre para mover; com as duas mãos, escala; clique duplo maximiza ou restaura. |
| `Bordas_Redimensionar_Janela` | 8 alças de borda e canto para redimensionar. |

`CollapsedTab` é a aba compacta exibida depois de **Minimizar**. O botão **Mostrar painel** reabre a janela.

O visual é o tema Grafite suave (`TemaVRSNC`): fundo grafite, cartões um pouco mais claros, texto claro, azul só no que está ativo (aba aberta, modo espacial), âmbar na seleção e vermelho suave no **Resetar SNC**. Os botões não têm mais o contorno azul do template e clareiam ao serem apontados. As janelas do EEG e o nome ao apontar, criados em execução, usam o mesmo tema.

A ficha segue esta ordem:

1. em letra menor: a classificação (como "Telencéfalo › Córtex cerebral › Lobo frontal") e um selo curto de revisão ("Revisão docente pendente · fontes conferidas", por exemplo);
2. resumo didático;
3. descrição científica;
4. em letra menor, no fim: estado de verificação, revisão docente e limitações, códigos FJ e FMA e nomes em inglês e latim.

No nível inicial, antes de qualquer escolha, a categoria mostra **Como explorar** e a ficha começa pelas instruções de uso.

## Oraculo_Anatomico_Punho

É uma cópia do Hand Menu do XRI, com `PainelOraculoNoPulso`, e `AncoraEspacialPulso` e `ControlePainelMundo` em `Follow GameObject`:

- Fica de 12 a 20 cm acima do pulso esquerdo (ou do controle esquerdo), voltado para o rosto, com cerca de 14 cm de largura (escala 0,00035).
- Acoplado ao pulso, só aparece quando o dorso do pulso (ou o lado do controle) se volta para o rosto e o pulso está no campo de visão; some em fade ao abaixar. Escondido, não recebe cliques nem bloqueia o raio. A regra é a do Hand Menu do XRI, configurada em `VRTemplateAssets/UI/FollowPresets/OracleDorsal*`.
- Mostra o nome da estrutura, o resumo didático e o estado de verificação.
- **Fonte** mostra a bibliografia.
- Os botões **Voltar** e **Isolar** agem sobre a mesma seleção do painel principal.
- Agarrando o cabeçalho, o oráculo pode ser solto no mundo; solto, fica sempre visível.

## Fluxos

**Seleção:**

1. O raio do controle direito aponta uma estrutura.
2. O `GerenciadorSelecaoHierarquica` aceita apenas filhos diretos do foco e aplica o hover azul. O nome da estrutura aparece junto ao ponto do raio, e o controle direito vibra de leve.
3. O gatilho confirma o clique. Num grupo, entra nele (o grupo vira o foco e a aba Estruturas mostra os filhos). Numa estrutura final, seleciona (amarelo) e abre a aba Ficha.
4. Os dois painéis são atualizados.

Ao sair do hover, uma peça que continua selecionada volta ao amarelo.

**Navegação:**

- Clicar num grupo (no modelo ou na lista) entra nele. As estruturas mais profundas, como os giros do lobo frontal, ficam a 5 cliques do início (antes eram 9, com selecionar e Entrar a cada nível).
- **Voltar** (botão A, ou **‹ destino** no cabeçalho) sobe um nível e deixa selecionado o grupo de onde o usuário saiu (a "concha"), em âmbar na cena e na lista; a aba Ficha mostra a ficha dele. O botão mostra o destino, como "‹ Lobo frontal" ou, na raiz com seleção, "‹ Início". No nível inicial, Voltar só desfaz a seleção.
- Tocar na concha de novo, ou **Entrar** (botão B), volta para dentro dela. Clicar num grupo não selecionado continua entrando direto.
- O caminho no cabeçalho volta direto a qualquer nível, e **Início** volta ao nível inicial sem resetar nada.
- Fora do nível inicial, tudo o que está fora do foco fica translúcido (`ControladorEsmaecimentoForaDoFoco`).

**Isolar, ocultar e explodir:**

- **Isolar** deixa visível só o selecionado ou, sem seleção, o grupo em foco; **Restaurar** desfaz.
- **Ocultar** esconde a estrutura selecionada e vira **Mostrar**. Na aba Estruturas, os itens ocultos aparecem esmaecidos com "(oculta)", e a linha **Mostrar N estruturas ocultas** traz todos de volta.
- **Explodir** afasta as estruturas finais do foco atual, inclusive no nível inicial, e **Restaurar** as devolve.
- Com as mãos rastreadas, agarrar uma estrutura com cada mão e afastar as mãos separa as estruturas do grupo selecionado ou, sem seleção, todo o catálogo (`CoordenadorGestoAnatomico` e `ControladorExpansaoGlobal`).

**Mãos no ar** (`GestosMaosNoAr`, em `Gestos_Maos_VRSNC`). Valem quando a mão não aponta anatomia, interface, painel nem eletrodo e não segura nada:

- uma mão pinçando e arrastando gira o alvo ativo (a seleção, o grupo em foco ou o SNC inteiro), 30° a cada 10 cm;
- as duas mãos pinçando movem, giram (no plano horizontal) e escalam o SNC inteiro em torno do centro do modelo, de 0,5× a 2,5× do tamanho original. Uma linha entre as mãos mostra o fator ("×1,4"), e a cabeça do EEG acompanha;
- uma pinça rápida da mão esquerda (de 80 a 350 ms, quase parada) faz o Voltar, e um rótulo junto à mão mostra o destino ("‹ Córtex cerebral"). Com a palma virada para o rosto, que é o gesto de sistema da Meta, ela não conta.

O reset cancela qualquer gesto em curso e devolve o tamanho original.

**Reset:** **Resetar SNC** restaura posições, rotações, escala, visibilidade, vista explodida, seleção e foco a partir dos estados originais guardados nos nós. Não remove componentes nem reconstrói a hierarquia. Para evitar reset por engano, é preciso segurar Y por 1 segundo; no painel, o primeiro toque em **Resetar SNC** mostra "Confirmar reset?" por 3 segundos, e só o segundo toque reseta. Os dois controles vibram forte ao resetar.

**Painel no espaço:**

- **SEGUIR**: o painel acompanha a cabeça.
- **FIXAR**: o painel fica parado onde está.
- **RECENTRAR**: traz o painel para a frente do usuário.
- Depois de agarrar e soltar, o painel fica fixo no mundo.
- Com o raio sobre um painel, a interface tem prioridade: o grip e o analógico não movem a anatomia.
- As janelas do EEG (mapa 2D e traçados) têm uma alça embaixo: agarrar leva a janela junto com a mão, e ao soltar ela gira até ficar de frente para o rosto. Enquanto uma janela está presa, a mesma mão não move a anatomia. Ao reabrir, a janela volta à posição padrão só se ficou fora da vista.

## Salvar a cena

Pode salvar a cena normalmente no Unity. O verificador externo (`Tools/VR/verificar-invariantes-cena-anatomica.mjs`) converte os nomes históricos dos campos (`[FormerlySerializedAs]`) para os nomes atuais antes de comparar. Ele também junta as linhas longas que o Unity quebra ao salvar. Depois de salvar, rode o verificador; o resultado deve ser `PASS`.

Ao sair do Play Mode, o Unity pode marcar a cena como alterada só porque recalculou o layout dos painéis ("Driving RectTransform"). Nesse caso não há nada a salvar: dá para recarregar a cena ou salvar sem risco.

## Limitações conhecidas

- `Plexo_Coroide` não tem modelo 3D; a lista e a ficha avisam isso.
- `Brain`, `UI` e `Teleport Area Setup` ficam na cena apenas como referência.
- O cenário claro do template foi mantido de propósito: uma prévia com fundo escuro foi avaliada em 17/10/2026 e descartada.
