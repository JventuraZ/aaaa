# Documentação Técnica dos Scripts VR SNC

Atualizado em 19/10/2026. Explica como os scripts do protótipo se organizam, quais contratos cada um mantém e o que pode ser alterado com segurança. A lista completa dos 66 scripts, com os objetos onde estão, fica em `MAPA_DOS_SCRIPTS_VR_SNC.md`. As convenções de nomes estão em `CONVENCOES_CODIGO.md`.

## Visão geral da arquitetura

O código é organizado em cinco camadas, e cada uma só depende das anteriores:

1. **Dados anatômicos** (`Assets/Scripts/Anatomia`): o catálogo de fichas (`OraculoAnatomico`), o vínculo de cada objeto da cena com o catálogo (`VinculoAnatomico`) e os metadados das estruturas e grupos.
2. **Nós** (`NoInteracaoAnatomica`): a árvore navegável de 272 nós, com pai, filhos, nível, estado original e nome legível.
3. **Seleção e entrada:**
   - `GerenciadorSelecaoHierarquica` é a única fonte do foco, do hover e da seleção.
   - Controles (`MapeadorEntradaMetaQuest2`, `ControladorSelecaoPorRaio`), mãos (`PonteInteracaoMao` e afins) e painéis apenas pedem mudanças a ele.
4. **Ações sobre o modelo:** visibilidade e isolamento, vista explodida, expansão global e gesto de duas mãos, manipulação e reset.
5. **Interface** (`Assets/Scripts/Interface` e os painéis em `Interacao`): painel principal, janela espacial, oráculo no pulso e estilo dos botões.

O gerenciador avisa as mudanças de duas formas:

- chama diretamente o `PainelInformacoesAnatomicas` (`ExibirNo`, `ExibirFoco`, `Limpar`) e o `PainelNavegacaoAnatomica` (`Atualizar`);
- publica os eventos `emFocoAlterado`, `emNoEmFoco` (hover), `emNoSelecionado` e `emSelecaoLimpo`. Quem os escuta: o oráculo no pulso, as abas (`AbasPainelAnatomico`), o caminho clicável (`BarraCaminhoAnatomico`), o esmaecimento fora do foco, o nome ao apontar e a vibração e o som (`RetornoSensorialInteracao`).

Outros eventos usados pelo retorno sensorial: `ControladorVisibilidadeAnatomica.emVisibilidadeAlterada`, `ControladorVisaoExplodida.emExpansaoAlternada` e `GerenciadorRestauracao.emRestaurando` / `emRestaurado`.

## Dados anatômicos e fichas

O catálogo fica em `Assets/Resources/AnatomicalOracle.json`: 272 fichas e a lista de fontes, carregadas por `OraculoAnatomico`. O conteúdo é offline e versionado; nada é gerado em execução. Os campos usados pelos painéis são:

- `nomePortugues`, `resumoDidatico` e `descricaoCientifica`;
- `estadoVerificacao`, `revisaoDocente` e `notasRevisao`. Os valores de `estadoVerificacao` viram textos como "conferida nas fontes registradas" ou "especificidade limitada";
- `idArquivo`, `idFma`, `nomeIngles` e `nomeLatim`;
- `idsFontesInformacao` e `localizadoresFontes`, usados por `ObterBibliografia`.

Pontos de acesso:

- `OraculoAnatomico.TentarObter(vinculo, out registro)` localiza a ficha de um nó pelo seu `VinculoAnatomico`.
- `ObterEstadoVerificacao` e `ObterBibliografia` formatam o estado de verificação e as referências.
- `NoInteracaoAnatomica.NomeLegivel` usa o `nomePortugues` da ficha. Sem ficha, usa o nome de exibição sem o código do arquivo (por exemplo, `[FJ1234]`) e com a inicial maiúscula.
- `NoInteracaoAnatomica.NomeLegivelComParte` acrescenta "(parte 1)", "(parte 2)" quando irmãos têm o mesmo nome legível (a mesma estrutura dividida em malhas), na ordem do código do arquivo (`ObterCodigoArquivo`). Título da ficha, lista e nome ao apontar usam esse nome.
- `NoInteracaoAnatomica.NaoPossuiModelo3D` identifica grupos sem malha, como o `Plexo_Coroide`.
- `TextoEnumeracoesAnatomicas.Exibir(...)` converte `DivisaoAnatomicaPrincipal`, `SubdivisaoAnatomica` e `LoboCortical` em texto legível. Os identificadores dos enums continuam sem acento para preservar a serialização.
- `LeitorJsonCompativel` lê JSON antigo traduzindo apenas as chaves conhecidas. Uma chamada direta a `JsonUtility.FromJson` não faz essa migração.

## Seleção em camadas

Regras do `GerenciadorSelecaoHierarquica`:

- Só filhos diretos do foco atual podem receber hover ou seleção (`DefinirNoSobFoco`, `SelecionarNo`).
- Com `entrarAutomaticamenteNoSelecionado` ligado (o mapeador liga pelo campo `abrirGrupoAoClicar`, padrão verdadeiro), `SelecionarNo` num grupo limpa a seleção e entra nele: o grupo vira o foco em vez de ficar selecionado e todo amarelo. Numa estrutura final, seleciona normalmente.
- `EntrarNoSelecionado` limpa a seleção e torna o selecionado o novo foco; `EntrarNo` entra num nó qualquer; `RedefinirFoco` volta à raiz.
- `VoltarAoNoPai` sobe um nível deixando a "concha" selecionada: guarda o foco atual, limpa a seleção, entra no pai e seleciona o antigo foco com `SelecionarSemEntrar` (a seleção sem a entrada automática). Na raiz, só limpa a seleção. `DestinoVoltar` devolve o nome que o botão mostra: o foco atual quando ele tem pai, "Início" na raiz com seleção e vazio quando não há para onde voltar. O botão A, o painel, o oráculo, a tecla Q e a pinça rápida da mão esquerda usam o mesmo método.
- `AlternarIsolamento` delega ao `ControladorVisibilidadeAnatomica`. Sem seleção e fora da raiz, isola o foco atual.
- Os destaques usam `MaterialPropertyBlock` (`_BaseColor`): azul para o hover e amarelo para a seleção. Os materiais importados não são trocados.
- Ao limpar o hover, as peças que continuam selecionadas recebem de novo o amarelo, e o mesmo vale no caso inverso (`RestaurarRenderizadores` com a cor mantida).

O `ControladorSelecaoPorRaio` lança o raio a partir de `Right Controller Stabilized Attach`, o ponto estabilizado do controle direito onde fica o `Ponteiro_Direito_SNC` (a origem é definida pelo mapeador). Ele procura o `NoInteracaoAnatomica` no objeto atingido ou nos pais e aplica histerese (`HistereseCandidatoSelecao`) para o alvo não piscar. `SelecionarFocoAtual` confirma a seleção. `PontoAcertoAtual` expõe o ponto atingido (ou nulo), usado pelo `RotuloEstruturaApontada`.

**Esmaecimento fora do foco.** O `ControladorEsmaecimentoForaDoFoco` escuta `emFocoAlterado` e, só em Play Mode e fora da raiz, troca os `sharedMaterials` dos renderers fora do foco por `Assets/Materials/Anatomia_Fantasma.mat` (URP Unlit transparente, sem escrita de profundidade). Os materiais originais voltam ao mudar de foco, ao resetar e ao desligar o componente. Os destaques por `MaterialPropertyBlock`, a visibilidade e a vista explodida não mudam.

## Entrada

**Controles do Quest.** O `MapeadorEntradaMetaQuest2` resolve o Action Map `BrainControls` e liga cada ação a um controlador; a tabela de botões está no `MAPA_DOS_SCRIPTS_VR_SNC.md`. Ele não contém regra anatômica, só prioridades. Com o raio sobre um painel, a interface vence: o grip e o analógico não movem a anatomia (`ZonaProximidadePainelInterface`). O analógico esquerdo rola a aba aberta do painel (`AbasPainelAnatomico.Rolar`, a 420 px/s: a ficha ou, na aba Estruturas, `PainelNavegacaoAnatomica.Rolar`).

**Reset seguro.** A ação `Left_ResetAll` tem a interação `Hold(duration=1)` em `VR_SNC_MetaQuest2_InputActions.inputactions`: o Y precisa ficar apertado por 1 segundo, e nenhum código extra trata isso. No painel, `PainelNavegacaoAnatomica.ConfirmarOuResetar` pede um segundo toque em até 3 segundos.

**Atalhos do Editor.** Sem óculos e com o simulador desligado, o mapeador aceita os atalhos de teclado:

| Tecla | Ação |
|---|---|
| R | Resetar |
| Q | Voltar |
| E | Explodir |
| C | Limpar seleção |
| I | Isolar |
| V | Ocultar |
| Enter ou clique esquerdo | Selecionar a estrutura apontada |

Enquanto o XR Interaction Simulator estiver ativo (`MapeadorEntradaMetaQuest2.SimuladorXRAtivo`), esses atalhos ficam desligados, porque o simulador usa as mesmas teclas. O `RoteadorEntradaXR` é a alternativa genérica e só processa atalhos quando o mapeador está desligado (`DeveProcessarAtalhos`).

**Mãos rastreadas.** A `PonteInteracaoMao` liga o rig de mãos do XRI à seleção em camadas, à captura com uma mão e ao `CoordenadorGestoAnatomico`. Os componentes de apoio são:

- `ElementoInteragivelMao`: um alvo XRI por malha;
- `AdaptadorEmissorInteracaoProxima` e `AdaptadorEmissorInteracaoDistante`, com `ContextoEmissorRaioMao`: registram se a consulta é próxima ou distante;
- `FiltroAlvoEstavelRaioMao`: histerese no raio distante;
- `PonteRaioInterfaceMao`: raio de interface próprio para cada mão;
- `PincaMao`: o ponto entre o polegar e o indicador no espaço do mundo, a partir do `XRHandSubsystem` e da XR Origin.

A reserva de mão (`PonteInteracaoMao.DefinirMaoReservada`) conta os pedidos por mão: o apontador de eletrodos e os gestos no ar reservam e liberam a mesma mão sem um soltar o pedido do outro. Mão reservada não dá foco nem pinça à anatomia.

**Gestos no ar.** O `GestosMaosNoAr`, em `Gestos_Maos_VRSNC` (fora do SNC, instalado por **Mãos > Instalar gestos no ar**), lê a mesma pinça que o XRI usa para selecionar (`selectInput` do `NearFarInteractor`). Cada mão tem um estado: livre, pinça, girando, duas mãos ou esperando soltar. A pinça só vira gesto "no ar" quando a mão está rastreada, sem hover XRI, sem alvo de UI, longe dos painéis, sem reserva e sem segurar nada; pinçar estruturas continua com a ponte.

- Uma mão: depois de 1,5 cm de arraste, `ManipuladorObjetoSelecionado.GirarPorArraste` gira o alvo ativo em torno do centro visual (300° por metro; o horizontal gira em torno do eixo vertical, o vertical em torno do eixo da direita da câmera).
- Duas mãos: `TransformarDuasMaos` (estática e testada na suíte) leva o SNC pelo meio entre as mãos, gira pelo ângulo das mãos no plano horizontal e escala pela razão das distâncias, em torno do centro do modelo, com o fator total preso entre `EscalaMinima` (0,5) e `EscalaMaxima` (2,5) do tamanho original.
- Pinça rápida da esquerda: de `DuracaoMinimaToque` a `DuracaoMaximaToque` (80 a 350 ms), com menos de 2,5 cm de movimento, palma longe do rosto e 0,6 s desde o último, chama `VoltarAoNoPai` e mostra o destino com `RotuloEstruturaApontada.DefinirRotuloExterno`.
- Durante o giro e as duas mãos, as mãos ficam reservadas. `GerenciadorRestauracao.emRestaurando` e a perda de rastreamento encerram o gesto sem ação.

O `XRInputModalityManager` do XRI alterna entre mão e controle; `DiagnosticosModalidadeEntrada` apenas relata o estado.

O raio do controle sai de `Right Controller Teleport Stabilized Origin`, que é irmã do `Right Controller`, não filha: quando a mão assume, ela continua ativa, parada na última pose do controle. `ControladorSelecaoPorRaio.OrigemRaioDisponivel` olha o grupo que o `XRTransformStabilizer` segue; com o controle desligado, o raio não dá foco nem rótulo, e o `VisualPonteiroLaser` some.

**Sem óculos.** O menu **Ferramentas > VR SNC > Entrada > Testar sem óculos (simulador XR)** liga o XR Interaction Simulator, amostra oficial do XRI 3.4.1 em `Assets/Samples/XR Interaction Toolkit/3.4.1/XR Interaction Simulator`. O simulador só existe no Play Mode do Editor (`ConfiguracaoSimuladorSemOculos`) e cria uma cabeça e dois controles simulados que passam pelo mesmo caminho do XRI usado nos óculos. Como ele remove o headset real da sessão, desligue-o antes de testar pelo Link. O Editor avisa no Console quando há óculos conectados e o simulador está ligado.

## Ações sobre o modelo

- **Visibilidade** (`ControladorVisibilidadeAnatomica`): oculta, mostra, isola e restaura nós. Guarda os estados originais de renderers e objetos.
  - Lembra o que o usuário ocultou (`EstaOculto`, `QuantidadeOcultas`). Isolar e mostrar tudo reaplicam essas ocultações; elas só são desfeitas por **Mostrar** na própria estrutura, por `MostrarOcultas` ou pelo reset.
  - Publica `emVisibilidadeAlterada`, usado pela lista e pelo botão Ocultar/Mostrar.
- **Vista explodida** (`ControladorVisaoExplodida`): afasta as estruturas finais do foco atual a partir do centro dos limites visíveis e as devolve depois.
  - Funciona também no nível inicial.
  - Os marcadores do gesto global ficam fora da hierarquia, e `ObterLimitesRenderizaveis` ignora renderers inativos para não deslocar esse centro.
- **Expansão global** (`ControladorExpansaoGlobal`): separa as 240 estruturas por um fator de 0 a 1, a partir de uma referência imutável de limites visuais.
  - Nunca altera hierarquia, malhas, escala ou rotações.
  - No fator 0, as folhas voltam ao lugar e a pose que o usuário deu aos grupos é preservada.
  - As contagens esperadas vêm da própria cena; se o índice não fechar, um aviso é registrado no Console.
- **Gesto de duas mãos** (`CoordenadorGestoAnatomico`): com uma estrutura agarrada em cada mão, afastar as mãos separa as estruturas do grupo selecionado ou, sem seleção, todo o catálogo.
- **Manipulação** (`ManipuladorObjetoSelecionado`):
  - o analógico direito aproxima, afasta e gira a estrutura selecionada; sem seleção, o grupo em foco, e no nível inicial o SNC inteiro (`ResolverAlvoAtivo`);
  - o grip agarra o alvo com suavização;
  - `inverterEixoHorizontal` define o sentido do giro;
  - `GirarPorArraste(alvo, pivô, grausHorizontal, grausVertical)` gira em torno do pivô sem deslocar o centro visual; é o caminho dos gestos no ar;
  - `ManipuladorModeloAnatomico` atende apenas o caminho genérico do `RoteadorEntradaXR`.
- **Reset** (`GerenciadorRestauracao.RestaurarTudo`): encerra a captura e restaura vista explodida, visibilidade, transformações, hover, seleção e foco.

## Interface

**Painel principal** (`Painel_Anatomico_VR_SNC`, 400 × 550):

- O layout vem de `ConfiguracaoPainelCompactoAbas.Aplicar` (Editor): cabeçalho (faixa de cima com `Button_Back` e `Status`; título e caminho embaixo), abas, conteúdo, barra de ações 3 × 2 e linha espacial. A mesma classe define a escala tipográfica: título 26, corpo e itens da lista 20, botões e abas 18, caminho 16, texto secundário 15 e nada abaixo de 14. Rótulos longos encolhem em uma linha (ajuste ao espaço com corte vertical) em vez de quebrar e vazar. No fim, ela chama `AplicacaoTemaGrafite.Aplicar`.
- **Tema** (`TemaVRSNC`): os tokens de cor do Grafite suave e os auxiliares `AplicarBotao`, `Marcar` (azul do ativo), `MarcarSelecao` (âmbar), `AplicarTexto` e `PrepararCanvasMundo`. A cor-base do botão fica na `Image`, e o `ColorBlock` usa multiplicador 1,25 com normal 0,8, apontado 1,0 e pressionado 0,68: num tema escuro, apontar clareia em vez de escurecer. Os canvas criados em execução recebem `CanvasScaler.dynamicPixelsPerUnit = 4`; com o valor 1, o texto legado ficava borrado no Quest. O painel serializado é pintado no Editor por `AplicacaoTemaGrafite`; as interfaces de execução (abas, caminho, lista, modos espaciais, janelas do EEG, rótulo) chamam o tema direto.
- `AbasPainelAnatomico` mostra **Ficha** (`InfoPanel`) ou **Estruturas** (`NavigationPanel`), uma por vez. Troca sozinha: no nível inicial ou ao selecionar uma estrutura final, Ficha; ao entrar num grupo, Estruturas. `Rolar` rola a aba aberta.
- `BarraCaminhoAnatomico` cria em execução `Header/Caminho_Navegavel` no lugar do texto `Path` (que fica desligado): "Início › … › últimos níveis". Mede cada rótulo com um `Text` invisível e, se o caminho não couber, troca os níveis mais antigos por "…" e, por último, encurta o nome atual. "Início" chama `RedefinirFoco`; cada nível chama `LimparSelecao` e `EntrarNo`.
- `PainelInformacoesAnatomicas` monta a ficha. `ExibirFoco` na raiz acrescenta a orientação **Como explorar** antes da ficha, e `Limpar` mostra só a orientação. `MontarConteudo` começa por uma linha discreta (classificação e selo de revisão) e deixa verificação, códigos e nomes de origem no fim, em letra 15. A classificação só vai para a ficha quando a linha `Division` está oculta (`ClassificacaoTemLinhaPropria`).
- `PainelNavegacaoAnatomica`:
  - cria um botão de 34 px por filho do foco, em ordem alfabética pt-BR com os pares esquerdo/direito juntos;
  - mostra o filtro **Todos / Esquerdo / Direito** quando a lista tem lados, e **Mostrar N estruturas ocultas** quando o usuário ocultou alguma;
  - marca "(oculta)" e "(sem modelo 3D)", esmaecidos;
  - marca o item selecionado em âmbar e, quando a seleção muda ou o foco muda, rola o mínimo para ele ficar inteiro na área (`RolarAte`). Com a aba Estruturas fechada (selecionar abre a Ficha), a rolagem fica pendente e acontece quando a lista volta a aparecer;
  - uma troca de foco volta a lista ao topo; o conteúdo da lista tem o pivô no topo, então uma lista curta fica encostada no alto da área em vez de centralizada;
  - remonta a lista em `emSelecaoLimpo`, para o item não ficar marcado depois de Limpar ou do reset;
  - liga **Ocultar/Mostrar** (`botaoOcultarMostrar`), **Entrar** e a confirmação de **Resetar SNC**;
  - escreve no Voltar "‹ " e o `DestinoVoltar` encurtado para 22 caracteres, ou "‹ Voltar" inativo quando não há destino;
  - atualiza o estado dos botões em `AtualizarEstadosControles`.
- `EstiloBotaoInterface` dá a aparência comum: botão inativo translúcido (alfa 0,32), com o rótulo esmaecido.
- `LegendaControleVRSNC`, em cada controle, monta um cartão de mundo com os comandos daquele lado. Mostra-o depois de 0,6 s olhando para o controle (ângulo e distância parecidos com os balões do template, que ficam desligados); se os dois controles estão na mira, só aparece o mais bem alinhado com o olhar.

**Retorno ao usuário:**

- `RotuloEstruturaApontada` cria `Rotulo_Estrutura_Apontada`, um Canvas em espaço de mundo na raiz da cena, desenhado por cima da anatomia. Ele fica no ponto do raio (ou no centro da peça), sempre voltado para a câmera, com altura angular constante (`alturaAngularGraus`, 2,2°).
- `RetornoSensorialInteracao`:
  - vibra pelo `SendHapticImpulseCommand` do Input System em cada `XRController`;
  - toca sons curtos de `VRTemplateAssets/Audio` e dos Starter Assets, em volume 0,35;
  - ignora os avisos da montagem inicial e os passos intermediários do reset.

**Janela espacial:**

- `ControlePainelMundo` usa a barra inferior como alça (`XRGrabInteractable`, escala com duas mãos).
  - `DefinirAcoplamento(true/false)` implementa **SEGUIR** e **FIXAR**; `RecentralizarPainel` implementa **RECENTRAR**.
  - O botão do modo ativo fica destacado.
- `AcompanhamentoCameraPainelInterface` calcula a posição de seguir e de recentrar.
- `JanelaEspacialPrincipal` cuida do redimensionamento (8 `LimiteRedimensionamentoJanelaPrincipal`) e do clique duplo na barra, que maximiza ou restaura e desliga o SEGUIR.
- `IntermediarioSelecaoControladorJanelaPrincipal` permite usar o gatilho do controle nessas alças.
- As janelas do EEG usam `AlcaJanelaFlutuante`: `XRGrabInteractable` na raiz da janela, colisor só na barra inferior, mesma configuração da alça do painel. Ao segurar, `ControlePainelMundo.DefinirAgarreJanelaFlutuante` registra o interator, e `AgarrePainelAtivoParaMao` impede a mesma mão de mover a anatomia. Ao soltar, a janela gira em 0,2 s até ficar de frente para o rosto.

**Oráculo no pulso:**

- `PainelOraculoNoPulso` mostra o nome legível, o resumo didático e o estado de verificação; a bibliografia aparece com **Fonte**.
- `AncoraEspacialPulso` posiciona o menu acima do pulso esquerdo (ou do controle esquerdo) e o vira para o rosto.
  - Com `mostrarSoAoVirarPulso`, o menu acoplado só aparece quando o eixo de referência do preset (o dorso da mão, `Up`, ou o lado do controle, `Right`) aponta para o rosto e o pulso está a até 50° do olhar. Usa `FollowPreset.ComputeDotProductThresholds`, `GetReferenceAxisForTrackingAnchor`, a folga para esconder e o `hideDelaySeconds` dos presets `OracleDorsalHandsFollowPreset` e `OracleDorsalControllerFollowPreset`.
  - O `CanvasGroup` de `Hand Scroll View` faz o fade. Escondido, o menu não recebe cliques, não pode ser agarrado e não bloqueia o raio.
- Agarrando o cabeçalho, o menu é solto no mundo e fica sempre visível.

## EEG do Protobrain

O funcionamento completo, os métodos e as limitações estão em `EEG_VR_SNC.md`. Contratos que outras partes do código precisam conhecer:

- **Raio.** `ControladorSelecaoPorRaio.RegistrarAlvoPrioritario(IAlvoRaioPrioritario)` deixa outro sistema disputar o raio com a anatomia sem usar física. O alvo mais próximo que a estrutura atingida ganha o hover e o próximo gatilho (`SelecionarFocoAtual` chama `Confirmar`). Os eletrodos usam isso; o raio da anatomia e as zonas de painel continuam iguais.
- **Mãos.** O `ApontadorEletrodosEEG` testa também o raio distante do `NearFarInteractor` de cada mão rastreada (o painel e a anatomia à frente do eletrodo ficam com o raio), e a pinça (`selectInput`) confirma. Enquanto aponta um eletrodo, a mão fica reservada (`PonteInteracaoMao.DefinirMaoReservada`): a anatomia atrás não recebe foco nem pinça dela (`ElementoInteragivelMao.IsHoverableBy` e `PonteInteracaoMao.PodeSelecionar`).
- **Rótulo.** `RotuloEstruturaApontada.DefinirRotuloExterno(texto, ponto)` mostra outro nome no lugar do da anatomia, só no quadro em que foi pedido.
- **Abas.** `AbasPainelAnatomico` tem a terceira aba (`Aba.EEG`), publica `AbaAlterada` e esconde a aba quando a cena não tem gravação (`DefinirAbaEEGDisponivel`). Com a aba EEG aberta, selecionar um giro pelo eletrodo não troca de aba.
- **Navegação.** `ControladorEEG.MostrarGiroDoCanal` limpa a seleção, entra no lobo (`EntrarNo`) e seleciona o giro (`SelecionarNo`), respeitando a regra de filhos diretos do gerenciador.
- **Dados.** Os binários ficam em `Assets/EEG/Dados/*.bytes`, marcados como binários no `.gitattributes`. `ConjuntoEEG` os lê com `Buffer.BlockCopy` (little-endian em todas as plataformas do Unity).
- **Janelas.** `MolduraJanelaEEG` é a base das duas janelas (canvas de mundo, título com Fechar, zona de proximidade, alça). `JanelaMapaEEG.TocarNoMapa` converte o ponto tocado para o topoplot (`TopoplotEEG.DeNormalizado`), procura o eletrodo mais próximo (`CanalMaisProximo`) e chama `ControladorEEG.SelecionarCanal(canal, true)`, o mesmo caminho do toque no escalpo.
- **Execução.** Os filtros do traçado e o topoplot da janela do mapa correm numa `Task`: `TopoplotEEG.Calcular` não toca a API do Unity, e `Enviar` sobe a textura na thread principal. O escalpo repinta a 4 Hz na potência e no máximo a 15 Hz na voltagem; o mapa 2D, no máximo a 10 Hz na voltagem.

## Serialização, cena e verificação

- Os campos foram renomeados para português com `[FormerlySerializedAs]`, e as classes com `[MovedFrom]`. Preserve esses atributos e os GUIDs dos `.meta`.
- **Pode salvar a cena.** O verificador externo (`Tools/VR/verificar-invariantes-cena-anatomica.mjs`):
  - lê os `[FormerlySerializedAs]` de `VinculoAnatomico.cs`, `NoInteracaoAnatomica.cs` e `ElementoInteragivelMao.cs`;
  - converte os nomes históricos para os atuais e junta as linhas longas que o Unity quebra ao salvar;
  - compara a hierarquia do SNC com `Tools/VR/Referencias/CenaAnatomica.unity`.

  O teste `node Tools/VR/testar-verificador-invariantes.mjs` cobre o caso da cena salva com os nomes atuais.
- **Suíte do Editor:** **Ferramentas > VR SNC > Executar verificações de preparação do commit** executa 14 verificações (incluindo a do EEG e as do HUD: Voltar com concha, gestos com as duas mãos e tema na cena) e não salva a cena. Os validadores de Play Mode estão listados no mapa dos scripts.

## Scripts que não devem ser alterados sem necessidade

- `NoInteracaoAnatomica`, `VinculoAnatomico`, `InformacaoEstruturaAnatomica`, `GrupoAnatomico` e `EnumeracoesAnatomicas`: sustentam a serialização da anatomia, que é conferida pelo verificador.
- `GerenciadorSelecaoHierarquica`: regra de foco e filhos diretos usada por todas as entradas.
- `OraculoAnatomico` e o formato de `AnatomicalOracle.json`: mudanças exigem atualizar o catálogo e as verificações.

Ajustes de menor risco:

- textos e layout dos painéis (o layout atual e a escala tipográfica ficam em `ConfiguracaoPainelCompactoAbas`, chamada no fim de `ConfiguracaoInterfacePainelMundo` e `AcabamentoInterfaceEspacial`);
- velocidades do `ManipuladorObjetoSelecionado` e da rolagem da lista;
- distâncias do `AcompanhamentoCameraPainelInterface`;
- cores de destaque do gerenciador;
- cores do tema (`TemaVRSNC`; depois de mudar, reaplique o painel compacto para o painel serializado acompanhar);
- sensibilidade e limites dos gestos no ar (constantes no topo do `GestosMaosNoAr`).

## Limitações conhecidas

- As verificações do Editor e o simulador são sintéticos. Rastreamento físico, conforto, desempenho e temperatura precisam ser medidos no Quest.
- O rastreamento de mãos pelo Link no PC depende da DLL `UnityOpenXRHands`, que o Controle Inteligente de Aplicativos do Windows pode bloquear. O APK do Quest não é afetado.
- O catálogo informa fichas sem revisão docente. Fontes e limitações devem acompanhar o conteúdo exibido.
