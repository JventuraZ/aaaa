# Mapa dos Scripts VR SNC

Atualizado em 19/10/2026. Lista os 66 scripts de execução em `Assets/Scripts`, onde cada um está na cena e as ferramentas do Editor em `Assets/Editor`. O EEG do Protobrain tem documento próprio: `EEG_VR_SNC.md`. A descrição dos objetos da cena está em `ESTRUTURA_SAMPLESCENE_VR_SNC.md`; o funcionamento interno, em `DOCUMENTACAO_TECNICA_SCRIPTS_VR_SNC.md`.

## Visão geral da cena

A cena principal é `Assets/Scenes/SampleScene.unity`. Os objetos do protótipo são:

- `Sistema_Nervoso_Central`: a hierarquia anatômica, com 272 nós (a raiz, 31 grupos e 240 estruturas com malha) e os controladores de seleção, entrada, visibilidade, vista explodida e reset;
- `Painel_Anatomico_VR_SNC`: a janela espacial principal (400 × 550, cerca de 40 × 55 cm), no tema Grafite suave, com o **‹ Voltar** e o caminho clicável no cabeçalho, as abas **Ficha**, **Estruturas** e **EEG** e a barra de ações;
- `XR Origin Hands (XR Rig)/Camera Offset/Oraculo_Anatomico_Punho`: o oráculo no pulso, um menu de pulso com a ficha resumida e a bibliografia, que aparece quando o usuário vira o pulso (ou o controle) esquerdo para o rosto;
- `Ponteiro_Direito_SNC`, dentro do controle direito: o feixe visual do raio de seleção;
- `Rotulo_Estrutura_Apontada`, criado em execução: o nome da estrutura apontada, junto ao ponto do raio;
- `Controlador_EEG`: a gravação de EEG do Protobrain. Com a aba EEG (ou uma das janelas) aberta, cria em execução `EEG_Cabeca` (escalpo e 32 eletrodos em volta do cérebro) e, quando pedidas, `Janela_Mapa_EEG` e `Janela_Tracados_EEG`;
- `Gestos_Maos_VRSNC`: os gestos das mãos no ar (`GestosMaosNoAr`), fora da hierarquia anatômica;
- nos controles esquerdo e direito, a legenda dos botões (`LegendaControleVRSNC`), que aparece quando o usuário olha para o controle.

O modelo original `Brain` continua desativado e preservado.

## Fluxo de interação

A seleção funciona em camadas:

1. O `GerenciadorSelecaoHierarquica` mantém um foco atual e só aceita como alvo os filhos diretos desse foco.
2. O `ControladorSelecaoPorRaio` lança o raio do controle direito e marca em azul o nó apontado (hover). O `RotuloEstruturaApontada` mostra o nome dele junto ao ponto do raio.
3. O gatilho direito, lido pelo `MapeadorEntradaMetaQuest2`, confirma o clique:
   - num **grupo**, entra nele: o grupo vira o foco ("Em foco") e a aba **Estruturas** lista os filhos;
   - numa **estrutura final**, seleciona (amarelo) e abre a aba **Ficha**.
4. **Voltar** sobe um nível e deixa selecionado o grupo de onde o usuário saiu (a "concha"), em âmbar na cena e na lista. O botão do cabeçalho mostra o destino ("‹ Lobo frontal"). Tocar na concha de novo, ou **Entrar**, volta para dentro; no nível inicial, Voltar só desfaz a seleção. O caminho do cabeçalho (`BarraCaminhoAnatomico`) volta direto a qualquer nível, e **Início** volta ao nível inicial sem resetar nada.
5. Fora do nível inicial, o `ControladorEsmaecimentoForaDoFoco` deixa translúcido tudo o que está fora do foco.
6. O `PainelInformacoesAnatomicas` monta a ficha, o `PainelNavegacaoAnatomica` monta a lista e o `AbasPainelAnatomico` mostra uma aba por vez.
7. O oráculo no pulso (`PainelOraculoNoPulso`) acompanha a mesma seleção.
8. O `RetornoSensorialInteracao` vibra e toca um som curto a cada ação.

Os botões dos dois painéis chamam os mesmos controladores que os botões físicos. Com rastreamento de mãos, a `PonteInteracaoMao` faz o papel do controle: a pinça seleciona e as duas mãos podem separar estruturas (`CoordenadorGestoAnatomico`). Pinçando o ar, sem apontar nada, o `GestosMaosNoAr` gira o modelo com uma mão, move, gira e escala o SNC com as duas e faz o Voltar com uma pinça rápida da mão esquerda.

As ações dos controles vêm do Action Map `BrainControls`, em `Assets/Input/VR_SNC_MetaQuest2_InputActions.inputactions`:

| Controle | Botão | Ação |
|---|---|---|
| Direito | Gatilho | Selecionar a estrutura apontada; clicar no painel |
| Direito | Grip | Agarrar a estrutura selecionada; sem seleção, o grupo em foco (no nível inicial, o SNC inteiro) |
| Direito | A | Voltar um nível, deixando selecionado o grupo de onde saiu |
| Direito | B | Entrar no grupo selecionado, como a concha que o Voltar deixou selecionada. Clicar num grupo não selecionado já entra nele |
| Direito | Analógico | Aproximar, afastar e girar a estrutura selecionada (sem seleção, o grupo em foco) |
| Direito | Clique do analógico | Explodir / Restaurar |
| Esquerdo | Y (segurar 1 s) | Resetar SNC. Um toque rápido não faz nada, para evitar reset por engano |
| Esquerdo | X | Limpar seleção |
| Esquerdo | Gatilho | Isolar / Restaurar (sem seleção, isola o grupo em foco) |
| Esquerdo | Grip | Ocultar ou mostrar a estrutura selecionada |
| Esquerdo | Analógico (vertical) | Rolar a aba aberta do painel (ficha ou lista) |
| Esquerdo | Clique do analógico | Minimizar / mostrar o painel |

No painel, **Resetar SNC** pede confirmação: o primeiro toque troca o rótulo por "Confirmar reset?" por 3 segundos, e só o segundo toque reseta.

Olhando por um instante para um controle, o usuário vê esta tabela resumida junto dele (`LegendaControleVRSNC`). Os balões de comandos do template (`Affordance Callouts Left/Right`) ficam desligados, porque descreviam comandos que este app não usa, como "Move" e "Turn".

## Mapa dos scripts

Tipos:

- **Essencial**: o protótipo não funciona sem ele.
- **Auxiliar**: melhora ou complementa um fluxo.
- **Mãos**: usado só com rastreamento de mãos.
- **Diagnóstico**: só observa e relata.
- **Código**: classe sem componente na cena.

### `Assets/Scripts/Anatomia`

| Script | Nome funcional | Função | Onde está | Tipo |
|---|---|---|---|---|
| `EnumeracoesAnatomicas` | Enumerações Anatômicas do SNC | Define divisões, subdivisões, lados, lobos e status de revisão usados nos metadados. | Usado por `InformacaoEstruturaAnatomica` e `GrupoAnatomico` | Código |
| `GrupoAnatomico` | Grupo Anatômico | Registra nome, caminho e divisão de um grupo da hierarquia. | 31 grupos sob `Sistema_Nervoso_Central` | Auxiliar |
| `InformacaoEstruturaAnatomica` | Informações Acadêmicas da Estrutura | Guarda nome anatômico, nome em português, divisão, subdivisão, lobo e descrição breve. | 240 estruturas | Essencial |
| `LeitorJsonCompativel` | Leitor de JSON Compatível | Lê contratos JSON antigos, traduzindo apenas as chaves conhecidas. | — | Código |
| `OraculoAnatomico` | Oráculo Anatômico | Carrega as 272 fichas de `Assets/Resources/AnatomicalOracle.json` e fornece resumo, descrição, verificação e bibliografia. O conteúdo é offline; nada é gerado em execução. | — | Código (essencial) |
| `TextoEnumeracoesAnatomicas` | Texto das Categorias Anatômicas | Converte as categorias dos enums em nomes legíveis em português, como "Córtex cerebral". | — | Código |
| `VinculoAnatomico` | Vínculo Anatômico | Liga cada objeto da cena à sua identidade no catálogo (IDs FJ e FMA, grupo ou estrutura). | 272 nós | Essencial |

### `Assets/Scripts/Interacao`

| Script | Nome funcional | Função | Onde está | Tipo |
|---|---|---|---|---|
| `AdaptadorEmissorInteracaoDistante` | Adaptador do Emissor Distante | Envolve o caster Far do `NearFarInteractor` da mão e registra a origem da consulta para o filtro de alvo. | `Left/Right Hand/Near-Far Interactor` | Mãos |
| `AdaptadorEmissorInteracaoProxima` | Adaptador do Emissor Próximo | Mesmo papel para o caster Near. | `Left/Right Hand/Near-Far Interactor` | Mãos |
| `ContextoEmissorRaioMao` | Contexto do Raio da Mão | Guarda se a consulta atual é próxima ou distante. | — | Código (mãos) |
| `ControladorEsmaecimentoForaDoFoco` | Esmaecimento Fora do Foco | Fora do nível inicial, troca o material de tudo o que está fora do foco por `Assets/Materials/Anatomia_Fantasma.mat` (translúcido) e devolve os originais ao mudar de foco, resetar ou desligar. | `Sistema_Nervoso_Central` | Auxiliar |
| `ControladorExpansaoGlobal` | Expansão Global | Separa temporariamente as 240 estruturas por um fator de 0 a 1, a partir de limites visuais fixos, sem alterar hierarquia, malhas ou escala. | `Sistema_Nervoso_Central` | Auxiliar |
| `ControladorSelecaoPorRaio` | Seleção por Raio | Lança o raio do controle direito, informa o nó em hover e o ponto atingido, e confirma a seleção. Alvos prioritários (`IAlvoRaioPrioritario`, como os eletrodos do EEG) ganham o raio e o gatilho quando estão à frente da anatomia. Com a mão rastreada no lugar do controle, o raio (e o laser do `VisualPonteiroLaser`) se cala (`OrigemRaioDisponivel`). | `Sistema_Nervoso_Central` | Essencial |
| `ControladorVisaoExplodida` | Vista Explodida | Afasta temporariamente as estruturas finais do foco atual (botão **Explodir**) e as devolve ao lugar. | `Sistema_Nervoso_Central` | Essencial |
| `ControladorVisibilidadeAnatomica` | Visibilidade Anatômica | Oculta, mostra, isola e restaura nós, preservando os estados originais. Lembra o que o usuário ocultou, para **Mostrar ocultas** e para manter essas estruturas ocultas ao isolar ou navegar. | `Sistema_Nervoso_Central` | Essencial |
| `CoordenadorGestoAnatomico` | Gesto Anatômico de Duas Mãos | Separação reversível com as duas mãos: só as estruturas do grupo selecionado ou, sem seleção, todo o catálogo. | `Sistema_Nervoso_Central` | Mãos |
| `DiagnosticosModalidadeEntrada` | Diagnóstico de Modalidade | Relata, sem alterar nada, se cada lado está usando mão ou controle. | `XR Origin Hands (XR Rig)` | Diagnóstico |
| `ElementoInteragivelMao` | Elemento Interagível pela Mão | Alvo XRI de cada malha física; o nó lógico selecionado continua sendo o da hierarquia. | 240 estruturas | Mãos |
| `FiltroAlvoEstavelRaioMao` | Filtro de Alvo Estável | Aplica histerese ao alvo do raio distante da mão. | `Left/Right Hand/Near-Far Interactor` | Mãos |
| `GerenciadorRestauracao` | Reset do SNC | Restaura seleção, foco, visibilidade, vista explodida, captura e transformações (**Resetar SNC**). | `Sistema_Nervoso_Central` | Essencial |
| `GerenciadorSelecaoHierarquica` | Seleção Anatômica em Camadas | Controla foco, hover, seleção, isolamento e navegação. Clicar num grupo entra nele; clicar numa estrutura final a seleciona. Voltar sobe um nível e deixa a concha selecionada (`DestinoVoltar` dá o nome para o botão). Aplica os destaques azul (hover) e amarelo (seleção). | `Sistema_Nervoso_Central` | Essencial |
| `GestosMaosNoAr` | Gestos das Mãos no Ar | Pinça no vazio (sem anatomia, interface, painel ou eletrodo na mira): uma mão arrastando gira o alvo ativo; as duas movem, giram e escalam o SNC inteiro (0,5× a 2,5×), com uma linha e o fator entre as mãos; uma pinça rápida da mão esquerda faz o Voltar e mostra o destino junto à mão. Cancela no reset. | `Gestos_Maos_VRSNC` | Mãos |
| `HistereseCandidatoSelecao` | Histerese de Candidato | Evita que o alvo do raio pisque entre estruturas vizinhas. | — | Código |
| `ManipuladorModeloAnatomico` | Manipulador do Modelo | Gira, aproxima e agarra o modelo inteiro pelo caminho de entrada genérico do `RoteadorEntradaXR`. | `Sistema_Nervoso_Central` | Auxiliar (legado) |
| `ManipuladorObjetoSelecionado` | Manipulador da Estrutura Selecionada | Move e gira com o analógico e agarra com o grip a estrutura selecionada; sem seleção, o grupo em foco, e no nível inicial o SNC inteiro. `GirarPorArraste` gira o alvo em torno do centro visual para os gestos no ar. | `Sistema_Nervoso_Central` | Essencial |
| `MapeadorEntradaMetaQuest2` | Mapeador de Entrada Meta Quest 2 | Liga os botões dos controles às ações. No Editor, oferece atalhos de teclado enquanto o simulador XR estiver desligado. Contém o menu **Configurar Entrada do Meta Quest 2 e Painel Anatômico**. | `Sistema_Nervoso_Central` | Essencial |
| `NoInteracaoAnatomica` | Nó de Interação Anatômica | Guarda pai, filhos, nível e estado original de cada nó, além do nome legível em português usado nos painéis. Partes da mesma estrutura recebem "(parte 1)", "(parte 2)" na ordem do código do arquivo. | 272 nós | Essencial |
| `PainelInformacoesAnatomicas` | Painel de Informações | Monta a ficha: título, categoria e conteúdo do oráculo. No topo, em letra menor, ficam a classificação e um selo curto de revisão; os detalhes de verificação, códigos e nomes de origem ficam no fim. No nível inicial, mostra a orientação **Como explorar**. | `Painel_Anatomico_VR_SNC` | Essencial |
| `PainelNavegacaoAnatomica` | Painel de Navegação | Monta a lista da aba **Estruturas**: ordem alfabética com pares esquerdo/direito juntos, filtro **Todos / Esquerdo / Direito**, linha **Mostrar N estruturas ocultas** e itens ocultos marcados "(oculta)". Marca a seleção em âmbar e rola até ela. Liga **‹ Voltar** (com o nome do destino), **Entrar**, **Isolar**, **Explodir**, **Ocultar/Mostrar**, **Limpar seleção** e **Resetar SNC** (com confirmação). | `Painel_Anatomico_VR_SNC` | Essencial |
| `PainelOraculoNoPulso` | Oráculo no Pulso | Mostra a ficha resumida, a verificação e a bibliografia (**Fonte**) no menu de pulso, com os botões **Voltar** e **Isolar**. | `Oraculo_Anatomico_Punho` | Essencial |
| `PincaMao` | Ponto da Pinça | Ponto entre o polegar e o indicador da mão rastreada, no espaço do mundo; usado pela ponte e pelos gestos no ar. | — | Código (mãos) |
| `PonteInteracaoMao` | Ponte de Interação da Mão | Liga o rig de mãos do XRI à seleção hierárquica, à captura com uma mão e ao gesto de duas mãos. A reserva de mão (`DefinirMaoReservada`) conta os pedidos dos eletrodos e dos gestos no ar. | `Sistema_Nervoso_Central` | Mãos |
| `PonteRaioInterfaceMao` | Raio de Interface da Mão | Dá a cada mão rastreada um raio de UI próprio do XRI. | `Left/Right Hand UI Ray` | Mãos |
| `RetornoSensorialInteracao` | Vibração e Som | Vibração leve no controle direito ao apontar uma estrutura; média ao selecionar, entrar, voltar, isolar, ocultar e explodir; forte nos dois controles ao resetar. Toca sons curtos e baixos dos ativos do template. | `Sistema_Nervoso_Central` | Auxiliar |
| `RoteadorEntradaXR` | Roteador de Entrada XR | Caminho de entrada genérico e alternativa ao mapeador, usado pela suíte. Só processa atalhos se o mapeador estiver desligado. | `Sistema_Nervoso_Central` | Auxiliar |
| `VisualPonteiroLaser` | Ponteiro Direito | Desenha o feixe do raio de seleção e muda de cor no hover. | `Ponteiro_Direito_SNC` | Auxiliar |

### `Assets/Scripts/Interface`

| Script | Nome funcional | Função | Onde está | Tipo |
|---|---|---|---|---|
| `AbasPainelAnatomico` | Abas do Painel | Mostra uma aba por vez, **Ficha**, **Estruturas** ou **EEG**, e troca sozinha: entrar num grupo abre a lista; selecionar uma estrutura final abre a ficha. Com a aba EEG aberta nada troca sozinho. A aba aberta fica em azul e negrito. O analógico esquerdo rola a ficha ou a lista. | `Painel_Anatomico_VR_SNC` | Essencial |
| `AcompanhamentoCameraPainelInterface` | Painel Seguindo a Câmera | Implementa o modo **SEGUIR** e a posição usada por **RECENTRAR**: 0,82 m à frente e 0,42 m ao lado. | `Painel_Anatomico_VR_SNC` | Essencial |
| `AlcaJanelaFlutuante` | Alça das Janelas do EEG | Barra inferior das janelas do mapa 2D e dos traçados: pinça ou grip levam a janela junto com a mão, e ao soltar ela gira até ficar de frente para o rosto. Enquanto está presa, a mesma mão não move a anatomia. | Criada em execução nas janelas do EEG | Auxiliar |
| `AncoraEspacialPulso` | Âncora do Pulso | Mantém o oráculo de 12 a 20 cm do pulso (ou do controle esquerdo), voltado para o rosto. Acoplado, só aparece quando o dorso do pulso (ou o lado do controle) se volta para o rosto, com a mesma regra do Hand Menu do XRI, e some em fade ao abaixar. Solto no mundo pelo cabeçalho, fica sempre visível. | `Oraculo_Anatomico_Punho/Follow GameObject` | Essencial |
| `BarraCaminhoAnatomico` | Caminho Clicável | Cabeçalho "Início › … › últimos níveis": cada nível volta direto àquele grupo, e o caminho se ajusta à largura sem cortar nomes. | `Painel_Anatomico_VR_SNC` | Auxiliar |
| `ControlePainelMundo` | Controle do Painel no Mundo | Usa a barra inferior como alça: mover com uma mão, escalar com duas. Implementa **SEGUIR**, **FIXAR** e **RECENTRAR** e destaca o modo ativo. Também conta as alças das janelas do EEG seguras, para a mão que segura uma janela não mover a anatomia. | `Painel_Anatomico_VR_SNC` e oráculo | Essencial |
| `EstiloBotaoInterface` | Estilo dos Botões | Deixa o botão inativo translúcido, com o rótulo esmaecido, nas cores do tema. | — | Código |
| `IntermediarioSelecaoControladorJanelaPrincipal` | Gatilho na Janela Principal | Permite usar o gatilho do controle nas alças da janela; o grip continua valendo para o resto. | `Left/Right Controller/Near-Far Interactor` | Auxiliar |
| `JanelaEspacialPrincipal` | Janela Espacial Principal | Redimensiona pelas bordas; um clique duplo na barra inferior maximiza ou restaura. | `Painel_Anatomico_VR_SNC` | Auxiliar |
| `LegendaControleVRSNC` | Legenda do Controle | Cartão pequeno junto ao controle com os comandos reais daquele lado ("Gatilho · Selecionar", "A · Voltar"…). Aparece depois de 0,6 s olhando para o controle; só a legenda mais bem alinhada com o olhar aparece. | `Left Controller` e `Right Controller` | Auxiliar |
| `LimiteRedimensionamentoJanelaPrincipal` | Borda de Redimensionamento | Cada uma das 8 alças de borda e canto. | `Bordas_Redimensionar_Janela/*` | Auxiliar |
| `RotuloEstruturaApontada` | Nome ao Apontar | Rótulo pequeno no ponto atingido pelo raio, sempre voltado para a câmera e com tamanho angular constante, com o nome legível (e "· grupo" quando for o caso). Some sem hover. | `Sistema_Nervoso_Central` (cria `Rotulo_Estrutura_Apontada`) | Auxiliar |
| `TemaVRSNC` | Tema Grafite Suave | Cores e estilos únicos da interface: painel grafite, cartões, texto claro, azul só no ativo, âmbar na seleção, vermelho suave no perigo. Os botões guardam a cor-base na imagem e clareiam ao apontar. Também deixa nítido o texto dos canvas criados em execução. | — | Código |
| `ZonaProximidadePainelInterface` | Zona de Proximidade do Painel | Dá prioridade à interface quando a mão ou o controle está perto de um painel, para não mexer na anatomia por engano. | `ExpandedRoot`, painel do pulso e janelas do EEG | Auxiliar |

### `Assets/Scripts/EEG`

| Script | Nome funcional | Função | Onde está | Tipo |
|---|---|---|---|---|
| `DadosEEG` | Gravação de EEG | ScriptableObject com o manifest do Protobrain já tipado (canais, córtex sob cada eletrodo, bandas, escalas, eventos) e os binários como TextAsset. | `Assets/EEG/DadosEEG.asset` | Código (essencial) |
| `ConjuntoEEG` | Gravação Carregada | Lê os binários em arrays, dá o quadro e a amostra de cada instante e interpola o campo (preso na faixa medida). | — | Código |
| `ControladorEEG` | Controlador do EEG | Fonte única do tempo e da vista (banda, normalização, potência ou voltagem, eletrodo, filtros, montagem); filtra o traçado fora da thread principal e leva a navegação ao giro do eletrodo. Guarda o alinhamento e as ligações eletrodo → giro calculados pelo instalador. | `Controlador_EEG` | Essencial |
| `CabecaEEG` | Cabeça EEG | Escalpo do fsaverage com a topografia, os 32 eletrodos e a linha até o giro; segue a pose do SNC. Translúcido, opaco ou desligado. | `Controlador_EEG` (cria `EEG_Cabeca`) | Essencial |
| `ApontadorEletrodosEEG` | Apontar Eletrodos | Deixa apontar e tocar eletrodos como a anatomia: o raio do controle direito com o gatilho (alvo prioritário do `ControladorSelecaoPorRaio`) e o raio distante de cada mão rastreada com a pinça (reserva a mão na `PonteInteracaoMao`, para a pinça não agarrar a anatomia atrás). | `Controlador_EEG` | Auxiliar |
| `CorrespondenciaGirosEEG` | Desikan → Modelo | Tabela da região Desikan para a estrutura equivalente do modelo, por nome e lado; marca as equivalências aproximadas. | — | Código |
| `PainelEEG` | Aba EEG | Monta em execução a aba EEG: reprodução, linha do tempo, bandas, normalizações, voltagem, eletrodo escolhido, os botões **Mapa 2D**, **Traçados**, **Ficha do giro** e o do escalpo, e o aviso de leitura. Abre e fecha as duas janelas. | `Painel_Anatomico_VR_SNC` | Essencial |
| `TopoplotEEG` | Topoplot | Topoplot 2D do EEGLAB numa textura (256 px na janela do mapa), com isolinhas e o eletrodo escolhido. `Calcular` roda fora da thread principal e `Enviar` sobe a textura; `CanalMaisProximo` acha o eletrodo de um ponto tocado. | — | Código |
| `JanelaMapaEEG` | Janela do Mapa 2D | Janela própria do topoplot: tocar/pausar e tempo, as 5 bandas, o mapa com os nomes dos 32 eletrodos (botão **Nomes**), a barra de cores com limites e unidade, o eletrodo escolhido e a ressalva de leitura. Tocar no mapa escolhe o eletrodo mais próximo. | Criada em execução (`Janela_Mapa_EEG`) | Essencial |
| `JanelaTracadosEEG` | Janela de Traçados | Navegador de traçados: 32 canais ou dupla banana, 7 µV/mm, filtros, página de 10 s, cursor sincronizado; tocar uma faixa escolhe o eletrodo. | Criada em execução (`Janela_Tracados_EEG`) | Essencial |
| `MolduraJanelaEEG` | Moldura das Janelas do EEG | Base comum das duas janelas: canvas de mundo com texto nítido, fundo do tema, título com **Fechar**, zona de proximidade e a alça inferior. Na primeira abertura, ou se a janela ficou fora da vista, a põe na posição padrão. | — | Código |
| `GraficoFaixaEEG` | Faixa do Traçado | Desenha uma faixa como envoltória mínimo-máximo por coluna. | Dentro da janela de traçados | Código |
| `FiltrosEEG` | Filtros de Leitura | Butterworth de fase zero (biquads, ida e volta) e notch da rede, portados do navegador do Protobrain. | — | Código |
| `MontagensEEG` | Montagens | Referência média em ordem de leitura e dupla banana (ACNS). | — | Código |
| `MapaCoresEEG` | Rampas de Cor | Rampas divergente e sequencial do Protobrain, em OKLab. | — | Código |
| `InterfaceEEG` | Peças de Interface | Botões, textos, linhas e linha do tempo montados em execução, nas cores do tema. | — | Código |

## Ferramentas do Editor

Todos os menus ficam em **Ferramentas > VR SNC**. As ferramentas "Aplicar", "Instalar" e "Conectar" alteram a cena aberta. Elas registram as mudanças no Desfazer e não salvam nada, com três exceções, que exigem a cena salva e sem alterações e salvam ao terminar: **Aplicar Painel Compacto com Abas**, **Instalar gestos no ar** e **Instalar EEG na cena**. Confira o resultado, salve a cena e rode o verificador externo.

| Script (`Assets/Editor`) | Menu | Para que serve |
|---|---|---|
| `VerificacoesPreparacaoCommit` | Executar verificações de preparação do commit | Suíte com 14 verificações (EEG, Voltar com concha, gestos com as duas mãos e tema na cena entre elas); não salva a cena. |
| `VerificacoesInterfaceNova` | Interface > Validar HUD novo (Voltar, gestos, tema) | O Voltar com a concha numa hierarquia temporária, a matemática do gesto com as duas mãos (escala, giro, deslocamento e limites) e o tema na cena: cores, botões sem contorno, Voltar no cabeçalho, Entrar na barra, lista com o pivô no topo, legendas, balões do template desligados e gestos instalados. Também roda na suíte. |
| `InstaladorGestosMaos` | Mãos > Instalar gestos no ar | Cria `Gestos_Maos_VRSNC`, fora do `Sistema_Nervoso_Central`, com o `GestosMaosNoAr` ligado à seleção, ao manipulador, à ponte das mãos e ao reset. Salva a cena. |
| `InstaladorEEG` | EEG > Escolher pasta do Protobrain, Importar dados do Protobrain, Instalar EEG na cena | Importa o `data/build` do Protobrain para `Assets/EEG` e instala o EEG na cena (alinhamento, ligações eletrodo → giro, `Controlador_EEG`, aba EEG). A instalação salva a cena. |
| `VerificacoesEEG` | EEG > Validar EEG (Modo de Edição) | Dados, interpolação, filtros em sinais sintéticos, rampas, montagens, as 32 ligações, a reserva de mão e o toque no mapa 2D (cada eletrodo tocado volta a si mesmo). Também roda na suíte. |
| `MenuRegressaoAgarreControlador` | Executar Teste de Regressão de Agarrar com Controle | Pivô e posse da captura pelo controle. |
| `MenuRegressaoGestoSeparacaoAnatomica` | sem menu próprio | Seis cenários do gesto de separação, executados pela suíte. |
| `MapeadorEntradaMetaQuest2` (parte do Editor) | Configurar Entrada do Meta Quest 2 e Painel Anatômico | Reconfigura entradas e painel; altera a cena. |
| `CompilarAplicarEExecutarQuest` | Quest > 01, 02, 03 e Reparar Sistema de Eventos | Compila e instala o APK (exige Android Build Support). |
| `EditorMapeamentoAnatomico` | Anatomia > Validar Mapeamento Atual | Confere os 240 vínculos e os 32 grupos do catálogo. |
| `EditorRotulosCandidatosOraculo` | Anatomia > Candidato do Oráculo > Simular / Aplicar Rótulos Revisados | Aplica rótulos revisados. Precisa de `outputs/...candidate.json` e avisa quando ele não existe. |
| `ConfiguracaoPainelCompactoAbas` | Interface > Aplicar Painel Compacto com Abas | Layout atual da janela (400 × 550): cabeçalho com **‹ Voltar** e estado na faixa de cima, título e caminho clicável embaixo; abas Ficha / Estruturas / EEG (a de EEG só aparece com uma gravação instalada); barra de ações 3 × 2 (Isolar, Ocultar, Explodir / Limpar seleção, Entrar, Resetar SNC); linha SEGUIR / RECENTRAR / FIXAR / Minimizar; escala tipográfica (título 26, corpo 20, botões 18, caminho 16, nada abaixo de 14). Também reduz o oráculo do pulso, liga a regra de virar o pulso e, no fim, aplica o tema (`AplicacaoTemaGrafite`). Salva a cena. |
| `AplicacaoTemaGrafite` | sem menu próprio | Chamada no fim do painel compacto: pinta painel, cartões, cabeçalho, alças, botões e textos com o tema Grafite suave, tira os contornos azuis, repinta o fundo e os textos do oráculo do pulso, instala as legendas dos controles e desliga os balões do template. |
| `ConfiguracaoInterfacePainelMundo` | Interface > Aplicar Painel Espacial Principal (Preservar Oráculo) | Monta a janela e, no fim, aplica o painel compacto. Altera a cena. |
| `AcabamentoInterfaceEspacial` | Interface > Aplicar Ajustes da Interface Espacial | Cores e faixas dos painéis e, no fim, o painel compacto. Altera a cena. |
| `ValidacaoInterfacePainelMundo` | Interface > Validar Pré-requisitos de Entrada do Painel Principal | Relata pose, alças e interactors do painel. |
| `ValidacaoCoberturaRaioInterface` | Interface > Validar Cobertura de Raycast, Validar Raio do Botão Alternativo, Aplicar Cobertura de Raycast | Confere se o raio alcança cada botão; a opção "Aplicar" altera a cena. |
| `VerificacoesRegressaoInterfaceEspacial` | Interface > Validar Configuração / Validar Estados em Execução | Configuração do painel e do pulso; estados em Play Mode. |
| `VerificacaoVisualInterfaceEspacial` | Interface > Pré-visualizar / Restaurar Pré-visualização | Pré-visualização em Play Mode. |
| `MenuRegressaoJanelaEspacialPrincipal` | Interface > Executar Teste de Regressão da Janela Espacial Principal | Bordas, arrasto, maximizar e restaurar, em Play Mode. |
| `ConfiguracaoEmissorRaioMao` | Mãos > Instalar Contexto de Raio Próximo/Distante | Instala os adaptadores de raio das mãos. Altera a cena. |
| `ConfiguracaoInteracaoMao` | Mãos > Validar Execução em Tempo Real, Validar Catálogo Anatômico, Instalar Painel Nativo do Oráculo no Pulso, Conectar Rig de Mãos Existente | Validações em Play Mode e instaladores do rig de mãos. |
| `ConfiguracaoRaioInterfaceMao` | Mãos > Aplicar / Validar raio de UI apenas das mãos | Raio de UI das mãos. |
| `VerificacoesHistereseSelecaoRaio` | Mãos > Validar Histerese do Raio | Teste determinístico. |
| `VerificacoesRegressaoContextoRaioMao` | Mãos > Validar Contexto de Raio Próximo/Distante | Teste determinístico. |
| `VerificacoesRegressaoEntradaMaoAberta` | Mãos > Validar Mecanismo de Entrada com Mão Aberta | Teste determinístico. |
| `VerificacoesRegressaoGestos` | Mãos > Validar Expansão Global (Modo de Jogo) | Fatores 0 a 1, reversão e preservação da pose dada pelo usuário. |
| `ConfiguracaoModalidadeEntrada` | Entrada > Instalar / Validar / Relatar modalidade automática de mão e controle | Alternância automática entre mão e controle. |
| `ConfiguracaoSimuladorSemOculos` | Entrada > Testar sem óculos (simulador XR) | Liga e desliga o XR Interaction Simulator no Play Mode do Editor (veja o README). |

## Limitações conhecidas

- O grupo `Plexo_Coroide` não tem modelo 3D no acervo atual. Ele aparece na lista como "Plexo coroide (sem modelo 3D)", esmaecido, e a ficha avisa a ausência.
- Os 240 objetos anatômicos mantêm os nomes de origem (em inglês). Os painéis mostram o nome em português da ficha do oráculo; quando dois irmãos têm o mesmo nome, eles aparecem como "(parte 1)", "(parte 2)".
- O esmaecimento fora do foco usa um material transparente. Se pesar no Quest, a alternativa mais leve é escurecer sem transparência.
- As verificações do Editor e o simulador XR não medem rastreamento físico, conforto, desempenho ou temperatura no Quest. Esses itens precisam ser testados nos óculos.
- Os gestos no ar, a pinça rápida e as legendas dos controles foram testados com o simulador. A sensibilidade (30° a cada 10 cm de arraste, pinça de 80 a 350 ms) pode precisar de ajuste depois do uso nos óculos; as constantes ficam no `GestosMaosNoAr`.
