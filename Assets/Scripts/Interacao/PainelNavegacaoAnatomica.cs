using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using VRSNC.Interface;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Painel de Navegação Anatômica
    // Nome técnico do script: PainelNavegacaoAnatomica
    // Função: lista os filhos diretos do foco anatômico (aba Estruturas), em ordem alfabética e com filtro por lado,
    // e liga os botões voltar, isolar, explodir, ocultar/mostrar, limpar seleção e resetar (com confirmação).
    // Relação com o protótipo: recebe o foco do GerenciadorSelecaoHierarquica e cria botões para navegar no SNC por camadas.
    // Objetos da cena: usado no Painel_Anatomico_VR_SNC.
    /// <summary>
    /// Controla a lista de estruturas navegáveis do painel.
    /// A seleção continua limitada aos filhos diretos do foco atual para preservar a organização anatômica em camadas.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainNavigationPanel")]
    public sealed class PainelNavegacaoAnatomica : MonoBehaviour
    {
        // Gerenciador central que sabe qual nó está em foco e qual nó está selecionado.
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        // Transform que recebe os botões gerados para cada filho direto do foco.
        [UnityEngine.Serialization.FormerlySerializedAs("listaContêiner")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("listContainer")]
        private Transform conteinerLista;
        // Raiz visual da lista, usada para mostrar/ocultar sem destruir o painel.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("listRoot")]
        private GameObject listaRaiz;
        // Prefab opcional de botão; quando ausente, o script cria um botão simples em runtime.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("buttonPrefab")]
        private Button botaoPrefab;
        // Título da lista, exibido como "Estruturas de: ...".
        [UnityEngine.Serialization.FormerlySerializedAs("titulotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("titleText")]
        private Text textoTitulo;
        // Texto do botão que alterna entre mostrar e ocultar a lista.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("toggleListText")]
        private Text alternarListaTexto;
        // Botões utilitários do painel VR.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("backButton")]
        private Button voltarBotao;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("enterButton")]
        private Button entrarBotao;
        [UnityEngine.Serialization.FormerlySerializedAs("redefinirbotao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("resetButton")]
        private Button botaoRestaurar;
        [UnityEngine.Serialization.FormerlySerializedAs("expandirbotao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("explodeButton")]
        private Button botaoExpandir;
        [UnityEngine.Serialization.FormerlySerializedAs("isolarbotao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("isolateButton")]
        private Button botaoIsolar;
        [UnityEngine.Serialization.FormerlySerializedAs("limparselecaobotao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("clearSelectionButton")]
        private Button botaoLimparSelecao;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("toggleListButton")]
        private Button alternarListaBotao;
        // Oculta ou mostra a estrutura selecionada; sem seleção, o grupo em foco.
        [SerializeField]
        private Button botaoOcultarMostrar;
        // Controladores acionados pelos botões do painel.
        [UnityEngine.Serialization.FormerlySerializedAs("redefinirgerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("resetManager")]
        private GerenciadorRestauracao gerenciadorRestauracao;
        [UnityEngine.Serialization.FormerlySerializedAs("expandidoVisaoControlador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("explodedViewController")]
        private ControladorVisaoExplodida controladorVisaoExplodida;
        // Altura de cada item gerado na lista de estruturas, em unidades de UI.
        private const float AlturaItemLista = 34f;
        private const float AlturaFiltroLados = 30f;
        // Mesma letra do corpo da ficha; nomes longos encolhem até o mínimo em vez de serem cortados.
        private const int FonteItemLista = 20;
        private const int FonteMinimaItemLista = 14;
        private const int FonteFiltro = 15;
        private const int FonteMinimaFiltro = 13;
        // Botões criados em runtime para a lista de filhos; são removidos e recriados a cada Refresh.
        private readonly List<GameObject> botoesGerados = new();
        // Estado visual da lista de filhos.
        private bool listaExpandida = true;
        // Foco para o qual a lista foi montada por último. A rolagem deve sobreviver a
        // atualizações repetidas do mesmo foco e voltar ao topo apenas ao navegar.
        private NoInteracaoAnatomica ultimoRenderizadoFoco;
        // Seleção marcada na última montagem: rolar até ela só quando muda, sem atrapalhar a leitura.
        private NoInteracaoAnatomica ultimoRenderizadoSelecionado;
        private RectTransform itemPendenteRolagem;
        // Filtro por lado das listas com pares esquerdo/direito; volta a "Todos" a cada troca de foco.
        private enum FiltroLado { Todos, Esquerdo, Direito }
        private FiltroLado filtroLado = FiltroLado.Todos;
        private GameObject linhaFiltroLados;
        private GameObject linhaMostrarOcultas;
        private ControladorVisibilidadeAnatomica controladorVisibilidade;
        // Resetar pede um segundo toque: até este instante, o botão mostra "Confirmar reset?".
        private float confirmacaoResetAte = -1f;
        private const float JanelaConfirmacaoResetSegundos = 3f;
        private static readonly Color CorConfirmacaoReset = TemaVRSNC.Hex(0x6B2B2B);
        // "‹ " + o nome do grupo que o Voltar vai deixar selecionado, encurtado para caber no botão do cabeçalho.
        private const int CaracteresMaximosDestinoVoltar = 22;
        private static readonly Regex PalavraLado = new(@"\b(esquerd[oa]s?|direit[oa]s?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex SufixoParte = new(@"\(parte \d+\)", RegexOptions.CultureInvariant);
        private static readonly CompareInfo ComparacaoPortugues = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        /// <summary>
        /// Resolve referências locais e liga os botões utilitários sem recriar a arquitetura do painel.
        /// </summary>
        private void Awake()
        {
            if (gerenciadorSelecao == null)
            {
                gerenciadorSelecao = FindFirstObjectByType<GerenciadorSelecaoHierarquica>();
            }

            ResolverControladorVisibilidade();
            ConectarUtilitarioBotoes();
            DefinirRotulosBotoesUtilitarios();
        }

        private void OnEnable()
        {
            ResolverControladorVisibilidade();
            if (controladorVisibilidade != null)
                controladorVisibilidade.emVisibilidadeAlterada += AoAlterarVisibilidade;
            gerenciadorSelecao?.emSelecaoLimpo.AddListener(AoLimparSelecao);
        }

        private void OnDisable()
        {
            if (controladorVisibilidade != null)
                controladorVisibilidade.emVisibilidadeAlterada -= AoAlterarVisibilidade;
            gerenciadorSelecao?.emSelecaoLimpo.RemoveListener(AoLimparSelecao);
        }

        // O gerenciador não remonta a lista ao limpar a seleção (botão X, Limpar, reset): sem isto o item continuaria
        // marcado e o Voltar mostraria o destino antigo.
        private void AoLimparSelecao()
        {
            if (Application.isPlaying && ultimoRenderizadoFoco != null)
                Atualizar(ultimoRenderizadoFoco);
            else
                AtualizarEstadosControles();
        }

        private void Update()
        {
            if (confirmacaoResetAte > 0f && Time.unscaledTime > confirmacaoResetAte)
                CancelarConfirmacaoReset();
            if (itemPendenteRolagem != null)
                TentarRolarPendente();
        }

        private void ResolverControladorVisibilidade()
        {
            if (controladorVisibilidade == null && gerenciadorSelecao != null)
                controladorVisibilidade = gerenciadorSelecao.GetComponent<ControladorVisibilidadeAnatomica>();
        }

        // A lista marca o que está oculto; por isso é remontada a cada mudança de visibilidade.
        private void AoAlterarVisibilidade()
        {
            if (Application.isPlaying && ultimoRenderizadoFoco != null)
                Atualizar(ultimoRenderizadoFoco);
        }

        /// <summary>
        /// Gera a lista somente depois que todos os componentes da cena concluíram Awake.
        /// </summary>
        private void Start()
        {
            Atualizar(gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null);
        }

        /// <summary>
        /// Configuração compacta usada por versões antigas do painel.
        /// </summary>
        public void Configurar(GerenciadorSelecaoHierarquica selecao, Transform contêiner, Text titulo, GerenciadorRestauracao redefinir, ControladorVisaoExplodida expandido)
        {
            gerenciadorSelecao = selecao;
            conteinerLista = contêiner;
            textoTitulo = titulo;
            gerenciadorRestauracao = redefinir;
            controladorVisaoExplodida = expandido;
            ConectarUtilitarioBotoes();
            DefinirRotulosBotoesUtilitarios();
            Atualizar(gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null);
        }

        /// <summary>
        /// Configura todas as referências do painel VR atual, incluindo lista, título e botões.
        /// </summary>
        public void ConfigurarVR(GerenciadorSelecaoHierarquica selecao, Transform contêiner, GameObject listaPainelRaiz, Text titulo, Text alternarTexto, Button voltar, Button entrar, Button redefinir, Button expandir, Button isolar, Button limpar, Button alternarLista, GerenciadorRestauracao redefinirControlador, ControladorVisaoExplodida controladorExpansao)
        {
            gerenciadorSelecao = selecao;
            conteinerLista = contêiner;
            listaRaiz = listaPainelRaiz;
            textoTitulo = titulo;
            alternarListaTexto = alternarTexto;
            voltarBotao = voltar;
            entrarBotao = entrar;
            botaoRestaurar = redefinir;
            botaoExpandir = expandir;
            botaoIsolar = isolar;
            botaoLimparSelecao = limpar;
            alternarListaBotao = alternarLista;
            gerenciadorRestauracao = redefinirControlador;
            controladorVisaoExplodida = controladorExpansao;
            ConectarUtilitarioBotoes();
            DefinirRotulosBotoesUtilitarios();
            DefinirListaExpandida(listaExpandida);
            Atualizar(gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null);
        }

        /// <summary>
        /// Atualiza a lista de botões para os filhos diretos do foco atual.
        /// </summary>
        public void Atualizar(NoInteracaoAnatomica focoNo)
        {
            var focoAlterado = ultimoRenderizadoFoco != focoNo;
            LimparGeradoBotoes();
            if (textoTitulo != null)
            {
                textoTitulo.text = $"Estruturas de: {ResolverNoTituloNavegacao(focoNo)}";
            }

            // Configure/ConfigureVR também são chamados pela ferramenta de editor. Os botões
            // navegáveis pertencem ao runtime e não devem ser serializados sem listeners válidos.
            if (!Application.isPlaying || focoNo == null || conteinerLista == null)
            {
                if (focoAlterado)
                {
                    RedefinirRolagemNavegacao();
                }

                ultimoRenderizadoFoco = focoNo;
                AtualizarEstadosControles();
                return;
            }

            if (focoAlterado)
            {
                filtroLado = FiltroLado.Todos;
            }

            var filhos = new List<NoInteracaoAnatomica>();
            foreach (var filho in focoNo.NosFilhos)
            {
                if (filho != null && filho.EhSelecionavel)
                    filhos.Add(filho);
            }

            // Ordem alfabética com os pares esquerdo/direito lado a lado; listas com lados ganham o filtro no topo.
            var rotulos = MontarRotulosFilhos(filhos);
            filhos.Sort((a, b) => CompararParaLista(rotulos[a], rotulos[b]));
            int esquerdos = 0, direitos = 0;
            foreach (var filho in filhos)
            {
                var lado = LadoDoRotulo(rotulos[filho]);
                if (lado == FiltroLado.Esquerdo)
                    esquerdos++;
                else if (lado == FiltroLado.Direito)
                    direitos++;
            }

            if (esquerdos > 0 || direitos > 0)
                CriarFiltroLados(filhos.Count, esquerdos, direitos);
            // Fica acima do filtro: as duas linhas usam SetAsFirstSibling.
            if (controladorVisibilidade != null && controladorVisibilidade.QuantidadeOcultas > 0)
                CriarLinhaMostrarOcultas(controladorVisibilidade.QuantidadeOcultas);
            var selecionado = gerenciadorSelecao != null ? gerenciadorSelecao.NoSelecionado : null;
            RectTransform itemSelecionado = null;
            foreach (var filho in filhos)
            {
                var lado = LadoDoRotulo(rotulos[filho]);
                if (filtroLado != FiltroLado.Todos && lado != FiltroLado.Todos && lado != filtroLado)
                {
                    continue;
                }

                // Um grupo sem malha continua consultável pela ficha, mas não destaca nada na cena.
                var semModelo = filho.NaoPossuiModelo3D;
                var oculto = controladorVisibilidade != null && controladorVisibilidade.EstaOculto(filho);
                var botao = CriarBotao(rotulos[filho] + (semModelo ? " (sem modelo 3D)" : string.Empty) + (oculto ? " (oculta)" : string.Empty));
                // A seleção atual (por exemplo, a concha de onde o Voltar saiu) fica em âmbar, como na cena.
                if (filho == selecionado)
                {
                    TemaVRSNC.MarcarSelecao(botao, true);
                    itemSelecionado = (RectTransform)botao.transform;
                }

                if (semModelo || oculto)
                    EstiloBotaoInterface.EsmaecerRotulos(botao);
                // Cada botão seleciona exatamente o filho direto mostrado, preservando a seleção por camada.
                botao.onClick.AddListener(() => gerenciadorSelecao?.SelecionarNo(filho));
                botoesGerados.Add(botao.gameObject);
            }

            if (focoAlterado)
            {
                RedefinirRolagemNavegacao();
            }

            // Uma seleção nova que ficou fora da área visível (ex.: a concha depois do Voltar) é trazida para a vista.
            // Com a aba Estruturas fechada o layout não é calculado: a rolagem espera a lista aparecer.
            if (itemSelecionado != null && (focoAlterado || selecionado != ultimoRenderizadoSelecionado))
            {
                itemPendenteRolagem = itemSelecionado;
                TentarRolarPendente();
            }

            ultimoRenderizadoFoco = focoNo;
            ultimoRenderizadoSelecionado = selecionado;
            AtualizarEstadosControles();
        }

        private void TentarRolarPendente()
        {
            if (itemPendenteRolagem == null)
                return;
            var rolagem = ObterRolagem();
            if (rolagem == null || !rolagem.isActiveAndEnabled || !itemPendenteRolagem.gameObject.activeInHierarchy)
                return;
            var item = itemPendenteRolagem;
            itemPendenteRolagem = null;
            RolarAte(rolagem, item);
        }

        /// <summary>Rola a lista o mínimo necessário para o item ficar inteiro na área visível.</summary>
        private static void RolarAte(ScrollRect rolagem, RectTransform item)
        {
            if (rolagem == null || rolagem.content == null || item == null)
                return;
            var areaVisivel = rolagem.viewport != null ? rolagem.viewport : rolagem.transform as RectTransform;
            if (areaVisivel == null)
                return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rolagem.content);
            var excedente = rolagem.content.rect.height - areaVisivel.rect.height;
            if (excedente <= 0f)
                return;
            // Limites do item no espaço do conteúdo, medidos a partir do topo (0 = topo do conteúdo).
            var cantos = new Vector3[4];
            item.GetWorldCorners(cantos);
            var topoConteudo = rolagem.content.rect.yMax;
            var topoItem = topoConteudo - rolagem.content.InverseTransformPoint(cantos[1]).y;
            var baseItem = topoConteudo - rolagem.content.InverseTransformPoint(cantos[0]).y;
            var deslocamento = (1f - rolagem.verticalNormalizedPosition) * excedente;
            if (topoItem < deslocamento)
                deslocamento = topoItem;
            else if (baseItem > deslocamento + areaVisivel.rect.height)
                deslocamento = baseItem - areaVisivel.rect.height;
            rolagem.StopMovement();
            rolagem.verticalNormalizedPosition = Mathf.Clamp01(1f - deslocamento / excedente);
        }

        /// <summary>
        /// Nomes legíveis dos filhos do foco. Partes de uma mesma estrutura aparecem como "(parte N)", nunca com o código do arquivo.
        /// </summary>
        private static Dictionary<NoInteracaoAnatomica, string> MontarRotulosFilhos(List<NoInteracaoAnatomica> filhos)
        {
            var rotulos = new Dictionary<NoInteracaoAnatomica, string>();
            foreach (var filho in filhos)
                rotulos[filho] = filho.NomeLegivelComParte;
            return rotulos;
        }

        private static FiltroLado LadoDoRotulo(string rotulo)
        {
            var palavra = PalavraLado.Match(rotulo);
            if (!palavra.Success)
                return FiltroLado.Todos;
            return palavra.Value.StartsWith("e", StringComparison.OrdinalIgnoreCase) ? FiltroLado.Esquerdo : FiltroLado.Direito;
        }

        /// <summary>
        /// Compara pelo nome sem o lado e sem "(parte N)" (ignorando acentos e maiúsculas); depois põe o esquerdo
        /// antes do direito e as partes em ordem.
        /// </summary>
        private static int CompararParaLista(string a, string b)
        {
            var baseA = Regex.Replace(PalavraLado.Replace(SufixoParte.Replace(a, " "), " "), @"\s+", " ").Trim();
            var baseB = Regex.Replace(PalavraLado.Replace(SufixoParte.Replace(b, " "), " "), @"\s+", " ").Trim();
            var comparacao = ComparacaoPortugues.Compare(baseA, baseB, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
            if (comparacao != 0)
                return comparacao;
            comparacao = ((int)LadoDoRotulo(a)).CompareTo((int)LadoDoRotulo(b));
            return comparacao != 0 ? comparacao : ComparacaoPortugues.Compare(a, b, CompareOptions.IgnoreCase);
        }

        /// <summary>
        /// Linha "Todos | Esquerdo | Direito" no topo da lista. Estruturas sem lado aparecem em qualquer filtro.
        /// </summary>
        private void CriarFiltroLados(int total, int esquerdos, int direitos)
        {
            linhaFiltroLados = new GameObject("Filtro_Lados", typeof(RectTransform));
            linhaFiltroLados.transform.SetParent(conteinerLista, false);
            linhaFiltroLados.transform.SetAsFirstSibling();
            // A lista não controla a altura dos filhos: o tamanho do próprio retângulo é o que vale.
            ((RectTransform)linhaFiltroLados.transform).sizeDelta = new Vector2(260f, AlturaFiltroLados);
            var organizacao = linhaFiltroLados.AddComponent<LayoutElement>();
            organizacao.minHeight = AlturaFiltroLados;
            organizacao.preferredHeight = AlturaFiltroLados;
            var linha = linhaFiltroLados.AddComponent<HorizontalLayoutGroup>();
            linha.spacing = 6f;
            linha.padding = new RectOffset(0, 0, 2, 2);
            linha.childControlWidth = true;
            linha.childControlHeight = true;
            linha.childForceExpandWidth = true;
            linha.childForceExpandHeight = true;
            CriarChipFiltro($"Todos ({total})", FiltroLado.Todos);
            CriarChipFiltro($"Esquerdo ({esquerdos})", FiltroLado.Esquerdo);
            CriarChipFiltro($"Direito ({direitos})", FiltroLado.Direito);
        }

        /// <summary>Linha "Mostrar ocultas (N)" no topo da lista, quando o usuário ocultou alguma estrutura.</summary>
        private void CriarLinhaMostrarOcultas(int quantidade)
        {
            linhaMostrarOcultas = new GameObject("Mostrar_Ocultas", typeof(RectTransform));
            linhaMostrarOcultas.transform.SetParent(conteinerLista, false);
            linhaMostrarOcultas.transform.SetAsFirstSibling();
            ((RectTransform)linhaMostrarOcultas.transform).sizeDelta = new Vector2(260f, AlturaFiltroLados);
            var organizacao = linhaMostrarOcultas.AddComponent<LayoutElement>();
            organizacao.minHeight = AlturaFiltroLados;
            organizacao.preferredHeight = AlturaFiltroLados;
            var linha = linhaMostrarOcultas.AddComponent<HorizontalLayoutGroup>();
            linha.padding = new RectOffset(0, 0, 2, 2);
            linha.childControlWidth = true;
            linha.childControlHeight = true;
            linha.childForceExpandWidth = true;
            linha.childForceExpandHeight = true;
            var botao = CriarBotaoAlternativo(linhaMostrarOcultas.transform);
            botao.name = "Botao_Mostrar_Ocultas";
            var elemento = botao.GetComponent<LayoutElement>();
            elemento.minHeight = AlturaFiltroLados - 4f;
            elemento.preferredHeight = AlturaFiltroLados - 4f;
            var texto = botao.GetComponentInChildren<Text>();
            texto.text = quantidade == 1 ? "Mostrar 1 estrutura oculta" : $"Mostrar {quantidade} estruturas ocultas";
            texto.fontSize = FonteFiltro;
            texto.resizeTextMinSize = FonteMinimaFiltro;
            texto.resizeTextMaxSize = FonteFiltro;
            texto.alignment = TextAnchor.MiddleCenter;
            // Chamada para ação discreta, em âmbar como a seleção: há estruturas fora da vista por escolha do usuário.
            TemaVRSNC.MarcarSelecao(botao, true);
            botao.onClick.AddListener(() => controladorVisibilidade?.MostrarOcultas());
        }

        private void CriarChipFiltro(string rotulo, FiltroLado filtro)
        {
            var chip = CriarBotaoAlternativo(linhaFiltroLados.transform);
            chip.name = "Filtro_" + filtro;
            var elemento = chip.GetComponent<LayoutElement>();
            elemento.minHeight = AlturaFiltroLados - 4f;
            elemento.preferredHeight = AlturaFiltroLados - 4f;
            var texto = chip.GetComponentInChildren<Text>();
            texto.text = rotulo;
            texto.fontSize = FonteFiltro;
            texto.resizeTextMinSize = FonteMinimaFiltro;
            texto.resizeTextMaxSize = FonteFiltro;
            texto.alignment = TextAnchor.MiddleCenter;
            // Margem menor que a dos itens: "Esquerdo (10)" precisa caber inteiro num terço da linha.
            var retanguloTexto = (RectTransform)texto.transform;
            retanguloTexto.offsetMin = new Vector2(4f, 1f);
            retanguloTexto.offsetMax = new Vector2(-4f, -1f);
            TemaVRSNC.Marcar(chip, filtroLado == filtro);
            chip.onClick.AddListener(() =>
            {
                filtroLado = filtro;
                Atualizar(ultimoRenderizadoFoco);
            });
        }

        private const float VelocidadeRolagemPixelsPorSegundo = 420f;

        /// <summary>
        /// Rola a lista de estruturas em velocidade constante, independente do tamanho da lista.
        /// Valores positivos sobem a lista. Usado pelo analógico esquerdo do controle.
        /// </summary>
        public void Rolar(float entradaVezesSegundos)
        {
            var rolagem = ObterRolagem();
            if (rolagem == null || rolagem.content == null)
                return;
            var areaVisivel = rolagem.viewport != null ? rolagem.viewport : rolagem.transform as RectTransform;
            var excedente = rolagem.content.rect.height - (areaVisivel != null ? areaVisivel.rect.height : 0f);
            if (excedente <= 0f)
                return;
            rolagem.StopMovement();
            rolagem.verticalNormalizedPosition = Mathf.Clamp01(rolagem.verticalNormalizedPosition + entradaVezesSegundos * VelocidadeRolagemPixelsPorSegundo / excedente);
        }

        private ScrollRect ObterRolagem()
        {
            var rolagem = listaRaiz != null ? listaRaiz.GetComponent<ScrollRect>() : null;
            if (rolagem == null && listaRaiz != null)
                rolagem = listaRaiz.GetComponentInParent<ScrollRect>();
            if (rolagem == null && conteinerLista != null)
                rolagem = conteinerLista.GetComponentInParent<ScrollRect>();
            return rolagem;
        }

        /// <summary>
        /// Deixa a primeira estrutura visível depois de uma troca real de foco, sem
        /// perturbar a leitura quando o mesmo foco é atualizado repetidamente.
        /// </summary>
        private void RedefinirRolagemNavegacao()
        {
            var rolagem = ObterRolagem();
            if (rolagem == null || rolagem.content == null)
            {
                return;
            }

            // Os botões acabaram de ser reconstruídos para o novo foco. Reconstrói apenas esta
            // subárvore de rolagem antes de normalizar; caso contrário, ScrollRect lê limites obsoletos e pode
            // reter uma posição fora da viewport depois que o grupo de layout se atualiza.
            if (rolagem.viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rolagem.viewport);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rolagem.content);
            rolagem.StopMovement();
            rolagem.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Alterna a lista de estruturas sem alterar a seleção atual.
        /// </summary>
        public void AlternarLista()
        {
            DefinirListaExpandida(!listaExpandida);
        }

        /// <summary>
        /// Mostra ou oculta apenas a lista, mantendo painel e botões principais ativos.
        /// </summary>
        public void DefinirListaExpandida(bool expandida)
        {
            listaExpandida = expandida;
            if (listaRaiz != null)
            {
                listaRaiz.SetActive(listaExpandida);
            }

            if (alternarListaTexto != null)
            {
                alternarListaTexto.text = listaExpandida ? "Ocultar lista" : "Mostrar lista";
            }

            AtualizarEstadosControles();
        }

        /// <summary>
        /// Cria um botão visual para uma estrutura anatômica da lista.
        /// </summary>
        private Button CriarBotao(string rotulo)
        {
            var botao = botaoPrefab != null ? Instantiate(botaoPrefab, conteinerLista) : CriarBotaoAlternativo(conteinerLista);
            if (botaoPrefab != null)
                AplicarContratoRaioInterface(botao);
            var texto = botao.GetComponentInChildren<Text>();
            if (texto != null)
            {
                texto.text = rotulo;
            }

            botao.gameObject.SetActive(true);
            return botao;
        }

        /// <summary>
        /// Cria um botão simples quando não há prefab definido na cena.
        /// </summary>
        private static Button CriarBotaoAlternativo(Transform pai)
        {
            var botaoObjeto = new GameObject("Navigation_Button");
            botaoObjeto.transform.SetParent(pai, false);
            // Itens compactos para que listas longas mostrem mais estruturas de uma vez.
            var retangulo = botaoObjeto.AddComponent<RectTransform>();
            retangulo.sizeDelta = new Vector2(260f, AlturaItemLista);
            var organizacao = botaoObjeto.AddComponent<LayoutElement>();
            organizacao.minHeight = AlturaItemLista;
            organizacao.preferredHeight = AlturaItemLista;
            var imagem = botaoObjeto.AddComponent<Image>();
            var sprite = TemaVRSNC.SpriteArredondado(pai);
            if (sprite != null)
            {
                imagem.sprite = sprite;
                imagem.type = Image.Type.Sliced;
            }

            imagem.raycastTarget = true;
            var botao = botaoObjeto.AddComponent<Button>();
            botao.targetGraphic = imagem;
            var textoObjeto = new GameObject("Text");
            textoObjeto.transform.SetParent(botaoObjeto.transform, false);
            var textoRetangulo = textoObjeto.AddComponent<RectTransform>();
            textoRetangulo.anchorMin = Vector2.zero;
            textoRetangulo.anchorMax = Vector2.one;
            textoRetangulo.offsetMin = new Vector2(10f, 2f);
            textoRetangulo.offsetMax = new Vector2(-10f, -2f);
            var texto = textoObjeto.AddComponent<Text>();
            texto.raycastTarget = false;
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.alignment = TextAnchor.MiddleLeft;
            texto.fontSize = FonteItemLista;
            texto.resizeTextForBestFit = true;
            texto.resizeTextMinSize = FonteMinimaItemLista;
            texto.resizeTextMaxSize = FonteItemLista;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Truncate;
            TemaVRSNC.AplicarBotao(botao);
            AplicarContratoRaioInterface(botao);
            return botao;
        }

        /// <summary>
        /// Mantém botões de navegação gerados e derivados de prefab na camada de UI do Canvas
        /// e garante que somente a Image-alvo receba o raio de UI XR.
        /// </summary>
        private static void AplicarContratoRaioInterface(Button botao)
        {
            if (botao == null)
                return;
            DefinirCamadaInterface(botao.transform, 5);
            var imagem = botao.targetGraphic as Image ?? botao.GetComponent<Image>() ?? botao.GetComponentInChildren<Image>(true);
            if (imagem == null)
                imagem = botao.gameObject.AddComponent<Image>();
            botao.targetGraphic = imagem;
            foreach (var elementoVisual in botao.GetComponentsInChildren<Graphic>(true))
                elementoVisual.raycastTarget = elementoVisual == imagem;
            imagem.raycastTarget = true;
        }

        private static void DefinirCamadaInterface(Transform raiz, int camada)
        {
            raiz.gameObject.layer = camada;
            for (var indice = 0; indice < raiz.childCount; indice++)
                DefinirCamadaInterface(raiz.GetChild(indice), camada);
        }

        /// <summary>
        /// Liga os botões do painel aos controladores existentes sem remover componentes.
        /// </summary>
        private void ConectarUtilitarioBotoes()
        {
            if (voltarBotao != null)
            {
                voltarBotao.onClick.RemoveAllListeners();
                voltarBotao.onClick.AddListener(() => gerenciadorSelecao?.VoltarAoNoPai());
            }

            if (entrarBotao != null)
            {
                entrarBotao.onClick.RemoveAllListeners();
                entrarBotao.onClick.AddListener(() => gerenciadorSelecao?.EntrarNoSelecionado());
            }

            if (botaoRestaurar != null)
            {
                botaoRestaurar.onClick.RemoveAllListeners();
                botaoRestaurar.onClick.AddListener(ConfirmarOuResetar);
            }

            if (botaoOcultarMostrar != null)
            {
                botaoOcultarMostrar.onClick.RemoveListener(AlternarOcultacaoDoPainel);
                botaoOcultarMostrar.onClick.AddListener(AlternarOcultacaoDoPainel);
            }

            if (botaoExpandir != null)
            {
                botaoExpandir.onClick.RemoveListener(AlternarVisaoExplodidaPeloPainel);
                botaoExpandir.onClick.AddListener(AlternarVisaoExplodidaPeloPainel);
            }

            if (botaoIsolar != null)
            {
                botaoIsolar.onClick.RemoveListener(AlternarIsolamentoDePainel);
                botaoIsolar.onClick.AddListener(AlternarIsolamentoDePainel);
            }

            if (botaoLimparSelecao != null)
            {
                botaoLimparSelecao.onClick.RemoveListener(LimparSelecaoDePainel);
                botaoLimparSelecao.onClick.AddListener(LimparSelecaoDePainel);
            }

            if (alternarListaBotao != null)
            {
                alternarListaBotao.onClick.RemoveAllListeners();
                alternarListaBotao.onClick.AddListener(AlternarLista);
            }
        }

        /// <summary>
        /// Remove os botões gerados anteriormente antes de reconstruir a lista do foco atual.
        /// </summary>
        private void LimparGeradoBotoes()
        {
            DestruirLinhaGerada(ref linhaFiltroLados);
            DestruirLinhaGerada(ref linhaMostrarOcultas);

            if (conteinerLista != null)
            {
                for (var indice = conteinerLista.childCount - 1; indice >= 0; indice--)
                {
                    var filho = conteinerLista.GetChild(indice);
                    if (filho == null)
                    {
                        continue;
                    }

                    var item = filho.gameObject;
                    if (botaoPrefab != null && item == botaoPrefab.gameObject)
                    {
                        continue;
                    }

                    // Content é dedicado aos itens de navegação. Filhos decorativos que não
                    // sejam Button são preservados.
                    if (item.GetComponent<Button>() == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        // Destroy só conclui no fim do frame. Desativar e retirar do container
                        // impede duplicação visual/layout mesmo com dois Refresh no mesmo frame.
                        item.SetActive(false);
                        filho.SetParent(null, false);
                        Destroy(item);
                    }
                    else
                    {
                        DestroyImmediate(item);
                    }
                }
            }

            botoesGerados.Clear();
        }

        /// <summary>Remove uma linha gerada (filtro ou "mostrar ocultas") sem esperar o fim do quadro.</summary>
        private static void DestruirLinhaGerada(ref GameObject linha)
        {
            if (linha == null)
                return;
            if (Application.isPlaying)
            {
                linha.SetActive(false);
                linha.transform.SetParent(null, false);
                Destroy(linha);
            }
            else
            {
                DestroyImmediate(linha);
            }

            linha = null;
        }

        /// <summary>
        /// Alterna a vista explodida e atualiza imediatamente rótulo e disponibilidade dos controles.
        /// </summary>
        private void AlternarVisaoExplodidaPeloPainel()
        {
            controladorVisaoExplodida?.AlternarExpansaoNivelAtual();
            AtualizarEstadosControles();
        }

        /// <summary>
        /// Resetar desfaz tudo; por isso o primeiro toque só pede confirmação e o segundo, dentro de 3 s, reseta.
        /// </summary>
        private void ConfirmarOuResetar()
        {
            if (confirmacaoResetAte > 0f && Time.unscaledTime <= confirmacaoResetAte)
            {
                CancelarConfirmacaoReset();
                gerenciadorRestauracao?.RestaurarTudo();
                return;
            }

            confirmacaoResetAte = Time.unscaledTime + JanelaConfirmacaoResetSegundos;
            // Pedido de confirmação bem visível: fundo vermelho-escuro e rótulo claro em negrito.
            if (botaoRestaurar != null && botaoRestaurar.targetGraphic is Image imagem)
                imagem.color = CorConfirmacaoReset;
            var rotulo = botaoRestaurar != null ? botaoRestaurar.GetComponentInChildren<Text>(true) : null;
            if (rotulo != null)
            {
                rotulo.color = TemaVRSNC.Texto;
                rotulo.fontStyle = FontStyle.Bold;
            }

            DefinirTextoBotao(botaoRestaurar, "Confirmar reset?");
        }

        private void CancelarConfirmacaoReset()
        {
            confirmacaoResetAte = -1f;
            // Volta ao estilo de perigo do tema: superfície normal com rótulo vermelho suave.
            TemaVRSNC.AplicarBotao(botaoRestaurar, TemaVRSNC.Estilo.Perigo);
            var rotulo = botaoRestaurar != null ? botaoRestaurar.GetComponentInChildren<Text>(true) : null;
            if (rotulo != null)
                rotulo.fontStyle = FontStyle.Normal;
            DefinirTextoBotao(botaoRestaurar, "Resetar SNC");
        }

        /// <summary>Estrutura sobre a qual Ocultar/Mostrar age: a selecionada ou, sem seleção, o grupo em foco.</summary>
        private NoInteracaoAnatomica ResolverAlvoOcultacao()
        {
            if (gerenciadorSelecao == null)
                return null;
            if (gerenciadorSelecao.NoSelecionado != null)
                return gerenciadorSelecao.NoSelecionado;
            var foco = gerenciadorSelecao.NoEmFoco;
            return foco != null && foco.NoPai != null ? foco : null;
        }

        private void AlternarOcultacaoDoPainel()
        {
            var alvo = ResolverAlvoOcultacao();
            if (alvo != null)
                controladorVisibilidade?.AlternarVisibilidadeNo(alvo);
            AtualizarEstadosControles();
        }

        /// <summary>
        /// Limpa a seleção e reflete a mudança imediatamente na disponibilidade dos controles.
        /// </summary>
        private void LimparSelecaoDePainel()
        {
            gerenciadorSelecao?.LimparSelecao();
            AtualizarEstadosControles();
        }

        /// <summary>
        /// Alterna o isolamento e atualiza imediatamente o estado e o rótulo do botão.
        /// </summary>
        private void AlternarIsolamentoDePainel()
        {
            gerenciadorSelecao?.AlternarIsolamento();
            AtualizarEstadosControles();
        }

        /// <summary>
        /// Mantém a disponibilidade dos botões coerente com foco, seleção e vista explodida.
        /// </summary>
        public void AtualizarEstadosControles()
        {
            var foco = gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null;
            var selecionado = gerenciadorSelecao != null ? gerenciadorSelecao.NoSelecionado : null;
            var ehIsolando = gerenciadorSelecao != null && gerenciadorSelecao.EhIsolando;
            if (voltarBotao != null)
            {
                // O botão diz para onde volta: o grupo que vai ficar selecionado (a concha atual).
                var destino = gerenciadorSelecao != null ? gerenciadorSelecao.DestinoVoltar() : string.Empty;
                voltarBotao.interactable = !string.IsNullOrEmpty(destino);
                DefinirTextoBotao(voltarBotao, string.IsNullOrEmpty(destino) ? "‹ Voltar" : "‹ " + EncurtarDestino(destino));
            }

            if (entrarBotao != null)
            {
                entrarBotao.interactable = selecionado != null && selecionado.NosFilhos.Count > 0;
            }

            if (botaoIsolar != null)
            {
                // Sem seleção, Isolar age sobre o grupo em foco (fora do nível inicial).
                botaoIsolar.interactable = selecionado != null || ehIsolando || (foco != null && foco.NoPai != null);
                DefinirTextoBotao(botaoIsolar, ehIsolando ? "Restaurar" : "Isolar");
            }

            if (botaoLimparSelecao != null)
            {
                botaoLimparSelecao.interactable = selecionado != null;
            }

            if (botaoExpandir != null)
            {
                botaoExpandir.interactable = foco != null && foco.NosFilhos.Count > 0;
                DefinirTextoBotao(botaoExpandir, controladorVisaoExplodida != null && controladorVisaoExplodida.EstaExpandido ? "Restaurar" : "Explodir");
            }

            if (botaoOcultarMostrar != null)
            {
                var alvoOcultacao = ResolverAlvoOcultacao();
                botaoOcultarMostrar.interactable = alvoOcultacao != null && controladorVisibilidade != null;
                DefinirTextoBotao(botaoOcultarMostrar, controladorVisibilidade != null && controladorVisibilidade.EstaOculto(alvoOcultacao) ? "Mostrar" : "Ocultar");
            }

            // Botões inativos ficam translúcidos para não parecerem a ação principal.
            foreach (var botao in new[] { voltarBotao, entrarBotao, botaoRestaurar, botaoExpandir, botaoIsolar, botaoLimparSelecao, alternarListaBotao, botaoOcultarMostrar })
                EstiloBotaoInterface.AtualizarAparencia(botao);
        }

        /// <summary>
        /// Escolhe o nome do título da lista conforme a regra do Navigation Panel.
        /// A lista mostra filhos diretos do foco atual; por isso o título deve acompanhar o foco, não a seleção.
        /// </summary>
        private string ResolverNoTituloNavegacao(NoInteracaoAnatomica focoNo)
        {
            var tituloNo = focoNo != null ? focoNo : gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null;
            return tituloNo != null ? tituloNo.NomeLegivel : "Sistema Nervoso Central";
        }

        /// <summary>
        /// Garante rótulos em português nos botões principais do painel.
        /// </summary>
        private static string EncurtarDestino(string nome)
        {
            if (string.IsNullOrEmpty(nome) || nome.Length <= CaracteresMaximosDestinoVoltar)
                return nome;
            return nome.Substring(0, CaracteresMaximosDestinoVoltar - 1).TrimEnd() + "…";
        }

        private void DefinirRotulosBotoesUtilitarios()
        {
            DefinirTextoBotao(voltarBotao, "‹ Voltar");
            DefinirTextoBotao(entrarBotao, "Entrar");
            DefinirTextoBotao(botaoRestaurar, "Resetar SNC");
            DefinirTextoBotao(botaoExpandir, controladorVisaoExplodida != null && controladorVisaoExplodida.EstaExpandido ? "Restaurar" : "Explodir");
            DefinirTextoBotao(botaoIsolar, gerenciadorSelecao != null && gerenciadorSelecao.EhIsolando ? "Restaurar" : "Isolar");
            DefinirTextoBotao(botaoLimparSelecao, "Limpar seleção");
            DefinirTextoBotao(alternarListaBotao, listaExpandida ? "Ocultar lista" : "Mostrar lista");
        }

        /// <summary>
        /// Atualiza o texto de um botão quando ele possui um componente Text em seus filhos.
        /// </summary>
        private static void DefinirTextoBotao(Button botao, string rotulo)
        {
            if (botao == null)
            {
                return;
            }

            var texto = botao.GetComponentInChildren<Text>(true);
            if (texto != null)
            {
                texto.text = rotulo;
            }
        }
    }
}