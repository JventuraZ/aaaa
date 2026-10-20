# DEV-VrSNC

Projeto Unity de exploração do sistema nervoso central em realidade virtual, com seleção de estruturas, navegação hierárquica, isolamento, expansão, restauração e consulta de fichas anatômicas. Também mostra uma gravação de EEG processada pelo [Protobrain](#eeg-protobrain): topografia no escalpo em volta do cérebro, mapa 2D (topoplot), traçados e o giro sob cada eletrodo. O desenvolvimento usa C# e nomes próprios em português.

## Ambiente

- Unity **6000.3.5f2**, conforme `ProjectSettings/ProjectVersion.txt`.
- Android Build Support, Android SDK/NDK e OpenJDK instalados pelo Unity Hub para compilar para Meta Quest.
- Git e Git LFS para obter os modelos e imagens do repositório.
- Acesso à internet na primeira abertura para resolver os pacotes Unity versionados no manifesto.

Os pacotes têm versões explícitas em `Packages/manifest.json`; a resolução do Unity é registrada em `Packages/packages-lock.json`. A execução e a compilação usam o próprio Unity Editor. Node.js é opcional e serve somente ao verificador externo de invariantes da cena.

## Abrir o projeto

1. Clone o repositório e execute `git lfs pull` na pasta clonada.
2. No Unity Hub, adicione a pasta do projeto e abra com Unity 6000.3.5f2.
3. Aguarde a resolução dos pacotes, importação dos ativos e compilação.
4. Abra `Assets/Scenes/SampleScene.unity`.
5. Use Play Mode para os testes no Editor (sem óculos, veja a seção seguinte). Para Quest, configure o dispositivo e use Android como plataforma-alvo.

A primeira importação recria `Library/`, `Temp/`, `Logs/` e os arquivos locais do Editor. Essas pastas não são versionadas. Os arquivos `.meta` acompanham os ativos e preservam as referências da cena.

## Testar sem óculos

O menu **Ferramentas > VR SNC > Entrada > Testar sem óculos (simulador XR)** liga ou desliga o XR Interaction Simulator, uma amostra oficial do XR Interaction Toolkit. Quando o item está marcado, o Play Mode cria uma cabeça e dois controles simulados. Eles usam o mesmo caminho de interação dos óculos: o raio, o gatilho, o painel e a seleção. O simulador só existe no Editor; a cena e o APK do Quest não mudam.

Clique na aba Game antes de usar o teclado. Comandos principais:

| Tecla | Efeito |
|---|---|
| Botão direito do mouse (segurar) + mover | Olhar ao redor; os controles acompanham |
| W, A, S, D / Q, E | Andar / descer e subir |
| T | Gatilho direito: selecionar a estrutura apontada ou clicar no painel |
| G | Grip direito: agarrar |
| 1 / 2 / 3 | Botões A / B / clique do analógico: Voltar / Entrar no grupo selecionado / Explodir |
| I, J, K, L | Analógico direito: mover e girar a estrutura selecionada (sem seleção, o grupo em foco) |
| Shift + T, G, 1, 2, 3 | Controle esquerdo: Isolar, Ocultar, Limpar seleção, Resetar SNC (segure Shift + 2 por 1 s), Minimizar/mostrar painel |
| Shift + I / K | Analógico esquerdo: rolar a aba aberta do painel (ficha ou lista) |
| ] | Mover só o controle direito com o mouse, para mirar mais alto ou mais baixo; **Tab** volta a mover cabeça e controles juntos |
| R | Recolocar os controles à frente da cabeça |

O simulador também mostra na aba Game um painel com os comandos.

Cuidados:

- Desligue o simulador antes de testar com o Quest pelo Link. Ele substitui a cabeça e os controles reais, e o Console avisa quando há óculos conectados com o simulador ligado.
- Apertar `]` duas vezes seguidas troca os controles por mãos simuladas; apertar `]` de novo volta aos controles.
- Com o simulador ligado, os atalhos de teclado próprios do projeto (R, Q, E, C, I, V e Enter) ficam desativados, porque o simulador usa as mesmas teclas. Com ele desligado, voltam a funcionar.
- O oráculo do pulso só aparece quando o pulso (ou o controle) esquerdo se volta para o rosto, o que é difícil de reproduzir com o mouse. Para vê-lo o tempo todo durante um teste, desmarque **Mostrar So Ao Virar Pulso** no `AncoraEspacialPulso` (`Oraculo_Anatomico_Punho/Follow GameObject`) com o Play rodando; ao sair do Play, o valor volta sozinho.

## Uso

- Aponte o controle direito para o modelo: o nome da estrutura aparece junto ao raio, e o controle vibra de leve.
- O gatilho num grupo entra nele; numa estrutura final, seleciona e abre a ficha. Fora do nível inicial, o que está fora do foco fica translúcido.
- O botão **‹ Voltar**, no alto do painel (ou o botão A), mostra para onde vai, como "‹ Lobo frontal". Ele sobe um nível e deixa selecionado, em âmbar na cena e na lista, o grupo de onde você saiu (a "concha"). Tocar nele de novo, ou **Entrar** (botão B), volta para dentro. No nível inicial, Voltar só desfaz a seleção.
- O caminho no topo do painel volta a qualquer nível, e **Início** volta ao começo sem resetar nada.
- O painel tem as abas **Ficha**, **Estruturas** e **EEG**; as duas primeiras trocam sozinhas. O analógico esquerdo rola a aba aberta. A lista vem em ordem alfabética, com filtro **Todos / Esquerdo / Direito**; a estrutura selecionada fica em âmbar e a lista rola até ela.
- **Ocultar** esconde a estrutura selecionada e vira **Mostrar**. Na aba Estruturas, **Mostrar N estruturas ocultas** traz todas de volta.
- Para recomeçar, segure **Y** por 1 segundo ou toque duas vezes em **Resetar SNC**. Um toque só não reseta.
- **Minimizar** recolhe o painel; **Mostrar painel** o reabre.
- Vire o pulso esquerdo para o rosto para ver a ficha resumida no pulso.
- Olhe por um instante para um controle para ver, junto dele, o que cada botão faz.

Com as mãos, sem controles:

- pinçar uma estrutura a seleciona e a segura; com uma estrutura presa em cada mão, afastar as mãos separa as estruturas;
- pinçar o ar, sem apontar nada, e arrastar gira o modelo (a seleção, o grupo em foco ou o SNC inteiro);
- pinçar o ar com as duas mãos move, gira e escala o SNC inteiro, de 0,5× a 2,5× do tamanho original: afastar as mãos aumenta, aproximar diminui. O reset devolve o tamanho original;
- uma pinça rápida da mão esquerda no ar faz o Voltar, e um rótulo junto à mão mostra o destino. Com a palma virada para o rosto, que é o gesto de sistema da Meta, ela não conta.

A tabela completa dos botões está em `Docs/MAPA_DOS_SCRIPTS_VR_SNC.md`.

## EEG (Protobrain)

A aba **EEG** do painel mostra uma gravação de EEG (BioSemi, 32 canais, 3,2 min de repouso) processada pelo pipeline do Protobrain (Python/MNE: ICA + ICLabel, potência por banda, spline esférica, atlas Desikan e Brodmann). Ao abrir a aba:

- um escalpo do fsaverage aparece em volta do cérebro, alinhado ao modelo, com a topografia animada (bandas delta a gama baixo, potência absoluta, relativa, variação no tempo ou voltagem no instante);
- os 32 eletrodos ficam sobre o escalpo. Apontar (com o controle ou com a mão) mostra o nome; o gatilho, ou a pinça com as mãos, escolhe o eletrodo, seleciona o giro equivalente do modelo (por exemplo, Fp1 → giro frontal médio esquerdo) e liga os dois por uma linha âmbar;
- a aba tem reprodução e linha do tempo, bandas, normalizações e voltagem, e o eletrodo escolhido com o córtex sob ele (giro, lobo, área de Brodmann, distância);
- **Mapa 2D** abre uma janela própria com o topoplot do EEGLAB: nomes dos eletrodos, barra de cores, bandas, tocar/pausar e o eletrodo escolhido. Tocar no mapa escolhe o eletrodo mais próximo, como tocar no escalpo;
- **Traçados** abre uma janela com os 32 canais (referência média ou dupla banana, 7 µV/mm, filtros 0,5–70 Hz e notch opcional, página de 10 s), com o cursor no mesmo tempo do escalpo;
- as duas janelas podem ser levadas pela alça de baixo e, ao soltar, voltam a ficar de frente para o rosto; **Fechar** as recolhe;
- o botão do escalpo alterna entre translúcido, opaco e desligado.

É uma ferramenta de ensino: a cor fica no escalpo porque é onde a medida existe (espaço de sensores); o giro sob o eletrodo é correspondência anatômica num cérebro-modelo, não a fonte do sinal, e o mapa não é diagnóstico. Detalhes, limitações e a origem dos dados estão em `Docs/EEG_VR_SNC.md`.

**Atualizar a gravação.** O pipeline continua no projeto do Protobrain:

1. No Protobrain, rode `python python.py build` (gera `data/build/`).
2. No Unity, **Ferramentas > VR SNC > EEG > Escolher pasta do Protobrain** (a pasta `data/build`; o padrão é `Documentos/protobrain-main/protobrain-main/data/build`).
3. **Ferramentas > VR SNC > EEG > Importar dados do Protobrain** copia os binários para `Assets/EEG/Dados`.
4. **Ferramentas > VR SNC > EEG > Instalar EEG na cena** recalcula o alinhamento e as ligações eletrodo → giro e salva a cena (exige a cena salva e sem alterações).

## Compilar para Quest

O menu **Ferramentas > VR SNC > Quest** oferece:

- **01 Compilar e Executar (base)**: gera um APK de desenvolvimento e solicita execução no aparelho conectado.
- **02 Corrigir e Executar**: aplica alterações somente de código sobre o APK-base. Alterações de cena, ativos, pacotes, shaders ou plugins exigem uma nova compilação-base.
- **03 Compilar Versão de Apresentação**: gera o APK sem solicitar instalação automática.

Os APKs ficam em `Builds/Quest/`, fora do controle de versão. A instalação exige um Quest com modo de desenvolvedor e depuração USB autorizada. A assinatura de publicação deve ser configurada localmente; chaves e senhas não fazem parte do repositório.

## Verificações

No Editor, execute **Ferramentas > VR SNC > Executar verificações de preparação do commit**. A suíte (14 verificações) confere atalhos, migração dos dados, restauração, seleção, referências da cena, configuração da interface, o HUD (Voltar com a concha selecionada, a matemática do gesto com as duas mãos e o tema aplicado na cena) e o EEG (dados importados, interpolação, filtros em sinais sintéticos, cores, montagens, as 32 ligações eletrodo → giro e o toque no mapa 2D). O resultado é gravado em `outputs/validacao-commit/editor.json`.

Com Node.js instalado, o verificador externo pode ser executado na raiz:

```text
node Tools/VR/testar-verificador-invariantes.mjs
```

Ele compara a cena com a referência versionada e exige que alterações anatômicas, referência ausente e referência adulterada sejam detectadas. Para conferir só a cena atual, use `node Tools/VR/verificar-invariantes-cena-anatomica.mjs`; o resultado esperado é `PASS`.

A cena pode ser salva normalmente no Unity: o verificador converte os nomes antigos dos campos (`[FormerlySerializedAs]`) para os atuais antes de comparar. Ao sair do Play Mode, o Unity pode marcar a cena como alterada só porque recalculou o layout dos painéis; nesse caso não há nada a salvar.

Todas as ferramentas do projeto ficam no menu **Ferramentas > VR SNC**. A lista completa está em `Docs/MAPA_DOS_SCRIPTS_VR_SNC.md`. Quase todas registram as mudanças no Desfazer sem salvar. As exceções exigem a cena salva e sem alterações e salvam ao terminar: **Interface > Aplicar Painel Compacto com Abas**, **Mãos > Instalar gestos no ar** e **EEG > Instalar EEG na cena**.

## Organização

| Pasta | Conteúdo |
| --- | --- |
| `Assets/Scripts/Anatomia` | Catálogo científico, leitura e vínculos anatômicos |
| `Assets/Scripts/Interacao` | Entrada, gestos, seleção e manipulação do modelo |
| `Assets/Scripts/Interface` | Painéis, interface espacial e o tema visual (`TemaVRSNC`) |
| `Assets/Scripts/EEG` | EEG do Protobrain: dados, escalpo, eletrodos, aba EEG, mapa 2D e traçados |
| `Assets/EEG` | Gravação importada (`DadosEEG.asset`, binários em `Dados/`), materiais e shader do escalpo |
| `Assets/Editor` | Configuração, verificações e comandos de compilação |
| `Assets/Resources/AnatomicalOracle.json` | Fichas anatômicas carregadas em execução |
| `Assets/Scenes` | Cena principal e ativos associados |
| `Assets/Samples` | Amostras do XR Interaction Toolkit e do XR Hands (Starter Assets, Hands Interaction Demo, XR Interaction Simulator) |
| `Docs/Anatomy` | Fontes, mapeamento e documentação científica |
| `Tools/VR` | Verificação externa e referência da cena |
| `Packages` | Manifesto e resolução das dependências |
| `ProjectSettings` | Configuração compartilhada do Unity |

## Estado do desenvolvimento

O projeto permanece em desenvolvimento. O EEG é uma gravação já processada pelo Protobrain e importada para o projeto; aquisição em tempo real e classificação ainda não estão implementadas. O catálogo contém fichas com situações editoriais distintas e informa a ausência de revisão docente. Fontes e limitações devem acompanhar o conteúdo exibido.

As verificações no Editor não medem rastreamento físico, conforto visual, desempenho, memória ou temperatura no Quest. Esses resultados precisam ser obtidos no aparelho. Outras versões do Unity devem ser avaliadas em uma cópia separada antes de migrar o projeto.

## Ativos e atribuições

O catálogo preserva as fontes e referências dos modelos BodyParts3D e das fichas científicas. Consulte `Docs/Anatomy`, os dados do catálogo e as licenças incluídas nos ativos e amostras de terceiros. Este repositório não altera os termos de uso desses materiais.
