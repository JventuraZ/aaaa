using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRSNC.Anatomia;
using VRSNC.Interface;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Painel de Informações Anatômicas
    // Nome técnico do script: PainelInformacoesAnatomicas
    // Função: atualiza os textos visíveis do painel com foco, seleção, caminho anatômico e metadados do SNC.
    // Relação com o protótipo: recebe dados do GerenciadorSelecaoHierarquica e mostra ao usuário o que está em foco
    // ou selecionado durante a navegação anatômica por raycast/trigger.
    // Objetos da cena: usado no Painel_Anatomico_VR_SNC.
    /// <summary>
    /// Controla os textos e a expansão/recolhimento do painel de anatomia em VR.
    /// A classe só altera conteúdo visual; a seleção anatômica continua sendo controlada pelo gerenciador de seleção.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainInfoPanel")]
    public sealed class PainelInformacoesAnatomicas : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("painelraiz")]
        [Header("Painel")]
        // Raiz geral do painel. Em painel world-space, é o GameObject que contém Canvas e GraphicRaycaster.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("panelRoot")]
        private GameObject raizPainel;
        // Conteúdo completo do painel quando ele está aberto.
        [UnityEngine.Serialization.FormerlySerializedAs("expandidoRaiz")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("expandedRoot")]
        private GameObject raizExpandida;
        // Aba compacta usada para reabrir o painel quando ele está oculto.
        [UnityEngine.Serialization.FormerlySerializedAs("recolhidaAbaRaiz")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("collapsedTabRoot")]
        private GameObject raizAbaRecolhida;
        // Título principal exibido no cabeçalho.
        [UnityEngine.Serialization.FormerlySerializedAs("padraotitulo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("defaultTitle")]
        private string tituloPadrao = "VR SNC";
        // Mensagem padrão quando nenhuma estrutura anatômica foi selecionada.
        [UnityEngine.Serialization.FormerlySerializedAs("padraocorpo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("defaultBody")]
        private string corpoPadrao = "Nenhuma estrutura selecionada";
        [UnityEngine.Serialization.FormerlySerializedAs("titulotexto")]
        [Header("Texto")]
        // Campos TMP opcionais mantidos para compatibilidade com variações do painel.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("titleText")]
        private TMP_Text textoTitulo;
        [UnityEngine.Serialization.FormerlySerializedAs("corpotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("bodyText")]
        private TMP_Text textoCorpo;
        [UnityEngine.Serialization.FormerlySerializedAs("estadotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("statusText")]
        private TMP_Text textoEstado;
        [UnityEngine.Serialization.FormerlySerializedAs("caminhotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("pathText")]
        private TMP_Text textoCaminho;
        [UnityEngine.Serialization.FormerlySerializedAs("nonometexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("nodeNameText")]
        private TMP_Text textoNomeNo;
        [UnityEngine.Serialization.FormerlySerializedAs("exibicaonometexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("displayNameText")]
        private TMP_Text textoNomeExibicao;
        [UnityEngine.Serialization.FormerlySerializedAs("descricaotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("descriptionText")]
        private TMP_Text textoDescricao;
        [UnityEngine.Serialization.FormerlySerializedAs("divisaotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("divisionText")]
        private TMP_Text textoDivisao;
        [UnityEngine.Serialization.FormerlySerializedAs("niveltexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("levelText")]
        private TMP_Text textoNivel;
        // Campos UnityEngine.UI.Text usados pelo painel atual da cena.
        [UnityEngine.Serialization.FormerlySerializedAs("alternativotitulotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackTitleText")]
        private Text textoTituloAlternativo;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativocorpotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackBodyText")]
        private Text textoCorpoAlternativo;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativoestadotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackStatusText")]
        private Text textoEstadoAlternativo;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativocaminhotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackPathText")]
        private Text textoCaminhoAlternativo;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativononometexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackNodeNameText")]
        private Text textoNomeNoAlternativo;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativoexibicaonometexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackDisplayNameText")]
        private Text textoNomeExibicaoAlternativo;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativodescricaotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackDescriptionText")]
        private Text textoDescricaoAlternativa;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativodivisaotexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackDivisionText")]
        private Text textoDivisaoAlternativa;
        [UnityEngine.Serialization.FormerlySerializedAs("alternativoniveltexto")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("fallbackLevelText")]
        private Text textoNivelAlternativo;
        [UnityEngine.Serialization.FormerlySerializedAs("alternarpainelbotao")]
        [Header("Controles")]
        // Botão que oculta o painel expandido. A aba recolhida possui seu próprio botão na cena.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("togglePanelButton")]
        private Button botaoAlternarPainel;
        // Botão da aba compacta que reabre o painel.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("expandPanelButton")]
        private Button expandirPainelBotao;
        [UnityEngine.Serialization.FormerlySerializedAs("complementarTitulo")]
        [Header("Dados Futuros")]
        // Espaço reservado para conteúdo acadêmico complementar sem mudar a lógica de seleção.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("supplementaryTitle")]
        private string tituloComplementar;
        [UnityEngine.Serialization.FormerlySerializedAs("complementarCorpo")]
        [TextArea(2, 6)]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("supplementaryBody")]
        private string corpoComplementar;
        // Estado visual atual do painel: aberto ou recolhido.
        private bool painelVisivel = true;
        // Identidade já apresentada. Evita devolver a descrição ao topo quando o mesmo
        // nó é apenas redesenhado por uma atualização de foco ou de controles.
        private NoInteracaoAnatomica ultimoNoExibido;
        /// <summary>
        /// Liga o botão do painel e inicializa os textos em português.
        /// </summary>
        private void Awake()
        {
            ResolverReferenciasControles();
            painelVisivel = raizExpandida == null || raizExpandida.activeSelf;
            ConectarBotoes();
            DefinirVisibilidadePainel(painelVisivel);
            Limpar();
        }

        /// <summary>
        /// Configuração simples para versões antigas do painel que usam apenas título e corpo.
        /// </summary>
        public void Configurar(TMP_Text titulo, TMP_Text corpo, Text tituloAlternativo, Text corpoAlternativo)
        {
            textoTitulo = titulo;
            textoCorpo = corpo;
            textoTituloAlternativo = tituloAlternativo;
            textoCorpoAlternativo = corpoAlternativo;
            Limpar();
        }

        /// <summary>
        /// Recebe todas as referências do painel VR criado na cena e prepara os textos iniciais.
        /// </summary>
        public void ConfigurarPainelVR(GameObject raiz, GameObject expandida, GameObject recolhida, Text titulo, Text estado, Text caminho, Text nomeNo, Text nomeExibicao, Text descricao, Text divisao, Text nivel, Button alternarBotao, Button expandirBotao)
        {
            raizPainel = raiz;
            raizExpandida = expandida;
            raizAbaRecolhida = recolhida;
            textoTituloAlternativo = titulo;
            textoEstadoAlternativo = estado;
            textoCaminhoAlternativo = caminho;
            textoNomeNoAlternativo = nomeNo;
            textoNomeExibicaoAlternativo = nomeExibicao;
            textoDescricaoAlternativa = descricao;
            textoDivisaoAlternativa = divisao;
            textoNivelAlternativo = nivel;
            botaoAlternarPainel = alternarBotao;
            expandirPainelBotao = expandirBotao;
            ResolverReferenciasControles();
            ConectarBotoes();
            DefinirVisibilidadePainel(true);
            Limpar();
        }

        /// <summary>
        /// Mostra uma estrutura selecionada. O caminho é montado a partir da hierarquia do próprio nó;
        /// o parâmetro de caminho é mantido por compatibilidade com os chamadores existentes.
        /// </summary>
        public void ExibirNo(NoInteracaoAnatomica no, string caminhoAtual)
        {
            Exibir(no, false);
        }

        /// <summary>
        /// Mostra o foco atual quando não há estrutura selecionada.
        /// </summary>
        public void ExibirFoco(NoInteracaoAnatomica focoNo)
        {
            if (focoNo == null)
            {
                Limpar();
                return;
            }

            // No nível inicial nada foi escolhido ainda: a orientação de uso vem antes da ficha da raiz.
            Exibir(focoNo, focoNo.NoPai == null);
            DefinirTexto(textoEstado, textoEstadoAlternativo, "Em foco");
        }

        private void Exibir(NoInteracaoAnatomica no, bool comOrientacao)
        {
            if (no == null)
            {
                Limpar();
                return;
            }

            var noAlterado = ultimoNoExibido != no;
            var informacao = no.EstruturaInformacao != null ? no.EstruturaInformacao : no.GetComponent<InformacaoEstruturaAnatomica>();
            var vinculo = no.VinculoAnatomico;
            OraculoAnatomico.TentarObter(vinculo, out var oraculo);
            var ehGrupo = vinculo != null ? vinculo.EhGrupo : informacao == null;
            var caminho = MontarCaminhoCurto(no);
            var categoria = MontarCategoria(no, ehGrupo);
            var classificacao = MontarClassificacao(no, informacao);
            // O conteúdo didático vem primeiro; verificação, códigos e nomes de origem ficam no fim.
            // No painel compacto a linha própria da classificação fica oculta: ela passa para a linha discreta da ficha.
            var descricao = MontarConteudo(no, informacao, oraculo, ClassificacaoTemLinhaPropria ? string.Empty : classificacao);
            if (comOrientacao)
            {
                descricao = OrientacaoInicial + "\n\n" + descricao;
                categoria = "Como explorar";
            }

            DefinirTexto(textoTitulo, textoTituloAlternativo, no.NomeLegivelComParte);
            DefinirTexto(textoEstado, textoEstadoAlternativo, ehGrupo ? "Grupo selecionado" : "Estrutura selecionada");
            DefinirTexto(textoCaminho, textoCaminhoAlternativo, caminho);
            DefinirTexto(textoNomeNo, textoNomeNoAlternativo, string.Empty);
            DefinirTexto(textoNomeExibicao, textoNomeExibicaoAlternativo, categoria);
            DefinirTexto(textoDescricao, textoDescricaoAlternativa, descricao);
            DefinirTexto(textoDivisao, textoDivisaoAlternativa, classificacao);
            DefinirTexto(textoNivel, textoNivelAlternativo, $"Nível: {no.NivelNo}");
            DefinirTexto(textoCorpo, textoCorpoAlternativo, MontarCorpo(no, caminho, descricao, classificacao));
            if (noAlterado)
            {
                RedefinirRolagemDescricao();
            }

            ultimoNoExibido = no;
        }

        /// <summary>
        /// Guarda texto acadêmico complementar para ser exibido junto da descrição.
        /// </summary>
        public void DefinirDadosComplementares(string titulo, string corpo)
        {
            tituloComplementar = titulo;
            corpoComplementar = corpo;
        }

        /// <summary>
        /// Alterna entre painel aberto e aba compacta.
        /// </summary>
        public void AlternarPainel()
        {
            DefinirVisibilidadePainel(!painelVisivel);
        }

        /// <summary>
        /// Define a visibilidade do painel sem recriar objetos ou trocar componentes.
        /// </summary>
        public void DefinirVisibilidadePainel(bool visivel)
        {
            painelVisivel = visivel;
            if (raizExpandida != null)
            {
                raizExpandida.SetActive(visivel);
            }

            if (raizAbaRecolhida != null)
            {
                raizAbaRecolhida.SetActive(!visivel);
            }

            if (raizPainel != null && raizExpandida == null)
            {
                raizPainel.SetActive(visivel);
            }

            // "Minimizar", e não "Ocultar painel": o "Ocultar" da barra de ações já esconde estruturas.
            DefinirTextoBotao(botaoAlternarPainel, "Minimizar");
            DefinirTextoBotao(expandirPainelBotao, "Mostrar painel");
        }

        /// <summary>
        /// Limpa a seleção visual e deixa o painel com mensagens iniciais em português.
        /// </summary>
        public void Limpar()
        {
            DefinirTexto(textoTitulo, textoTituloAlternativo, tituloPadrao);
            DefinirTexto(textoEstado, textoEstadoAlternativo, "Nenhuma estrutura selecionada");
            DefinirTexto(textoCaminho, textoCaminhoAlternativo, "Nível inicial");
            DefinirTexto(textoNomeNo, textoNomeNoAlternativo, "Nenhuma estrutura selecionada");
            DefinirTexto(textoNomeExibicao, textoNomeExibicaoAlternativo, "Como explorar");
            DefinirTexto(textoDescricao, textoDescricaoAlternativa, OrientacaoInicial);
            DefinirTexto(textoDivisao, textoDivisaoAlternativa, string.Empty);
            DefinirTexto(textoNivel, textoNivelAlternativo, "Nível: -");
            DefinirTexto(textoCorpo, textoCorpoAlternativo, corpoPadrao);
            ultimoNoExibido = null;
            RedefinirRolagemDescricao();
        }

        /// <summary>
        /// Reposiciona a descrição apenas quando a identidade mostrada muda ou é limpa.
        /// Funciona tanto com a variante TMP quanto com o Text legado preservado na cena.
        /// </summary>
        private void RedefinirRolagemDescricao()
        {
            var descricaoTransformacao = textoDescricao != null ? textoDescricao.transform : textoDescricaoAlternativa != null ? textoDescricaoAlternativa.transform : null;
            var rolagem = descricaoTransformacao != null ? descricaoTransformacao.GetComponentInParent<ScrollRect>() : null;
            if (rolagem == null || rolagem.content == null)
            {
                return;
            }

            rolagem.StopMovement();
            var ancorado = rolagem.content.anchoredPosition;
            rolagem.content.anchoredPosition = new Vector2(ancorado.x, 0f);
            rolagem.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// Resolve o botão da aba compacta em cenas antigas que ainda não serializaram a referência.
        /// </summary>
        private void ResolverReferenciasControles()
        {
            if (expandirPainelBotao == null && raizAbaRecolhida != null)
            {
                expandirPainelBotao = raizAbaRecolhida.GetComponentInChildren<Button>(true);
            }
        }

        /// <summary>
        /// Liga os botões de ocultar e mostrar painel sem acumular listeners antigos.
        /// </summary>
        private void ConectarBotoes()
        {
            if (botaoAlternarPainel != null)
            {
                botaoAlternarPainel.onClick.RemoveListener(AlternarPainel);
                botaoAlternarPainel.onClick.AddListener(AlternarPainel);
            }

            if (expandirPainelBotao != null)
            {
                expandirPainelBotao.onClick.RemoveListener(AlternarPainel);
                expandirPainelBotao.onClick.AddListener(AlternarPainel);
            }
        }

        /// <summary>
        /// Monta a descrição textual da estrutura selecionada.
        /// </summary>
        private string MontarDescricao(NoInteracaoAnatomica no, InformacaoEstruturaAnatomica informacao)
        {
            var construtor = new StringBuilder();
            if (informacao != null && !string.IsNullOrWhiteSpace(informacao.DescricaoBreve))
            {
                construtor.AppendLine(informacao.DescricaoBreve);
            }
            else
            {
                var vinculo = no.VinculoAnatomico;
                if (informacao == null)
                    construtor.AppendLine($"{ContarRepresentacoesGrupo(no)} representações neste grupo. Explore os subgrupos e as estruturas na aba Estruturas.");
            }

            if (no.VinculoAnatomico != null || informacao == null)
                construtor.AppendLine("Conteúdo didático aguardando revisão bibliográfica.");
            if (!string.IsNullOrWhiteSpace(tituloComplementar) || !string.IsNullOrWhiteSpace(corpoComplementar))
            {
                construtor.AppendLine();
                construtor.AppendLine(tituloComplementar);
                construtor.AppendLine(corpoComplementar);
            }

            return construtor.ToString().Trim();
        }

        private static int ContarRepresentacoesGrupo(NoInteracaoAnatomica no)
        {
            return no.VinculoAnatomico != null ? no.VinculoAnatomico.MembroEstruturas.Count : no.GetComponentsInChildren<InformacaoEstruturaAnatomica>(true).Length;
        }

        /// <summary>
        /// Gera o corpo de texto detalhado para versões do painel que usam um campo único.
        /// </summary>
        private static string MontarCorpo(NoInteracaoAnatomica no, string caminho, string descricao, string divisao)
        {
            return $"Estrutura selecionada: {no.NomeLegivelComParte}\nCaminho anatômico: {caminho}\nNome técnico: {no.NomeNo}\nNível: {no.NivelNo}\n{divisao}\n\n{descricao}";
        }

        private const int NiveisVisiveisCaminho = 3;
        // Mostrada enquanto nada foi escolhido, para quem abre o protótipo pela primeira vez.
        private const string OrientacaoInicial = "Aponte o controle direito para uma estrutura e aperte o gatilho, ou escolha um item na aba Estruturas. Clicar num grupo já entra nele.\n\nO caminho no topo do painel volta a qualquer nível; Início volta ao começo. Isolar, Explodir e Ocultar ajudam a ver peças escondidas.\n\nPara recomeçar, segure Y por 1 segundo ou toque duas vezes em Resetar SNC. Vire o pulso esquerdo para o rosto para ver a ficha no pulso.";

        /// <summary>
        /// Mostra onde a estrutura está: seus ancestrais mais próximos, em uma linha que cabe no cabeçalho.
        /// </summary>
        private static string MontarCaminhoCurto(NoInteracaoAnatomica no)
        {
            var ancestrais = new List<string>();
            for (var atual = no.NoPai; atual != null; atual = atual.NoPai)
                ancestrais.Insert(0, atual.NomeLegivel);
            if (ancestrais.Count == 0)
                return "Nível inicial";
            if (ancestrais.Count <= NiveisVisiveisCaminho)
                return string.Join(" › ", ancestrais);
            return "… › " + string.Join(" › ", ancestrais.GetRange(ancestrais.Count - NiveisVisiveisCaminho, NiveisVisiveisCaminho));
        }

        private static string MontarCategoria(NoInteracaoAnatomica no, bool ehGrupo)
        {
            if (no.NaoPossuiModelo3D)
                return "Sem modelo 3D no acervo atual";
            if (!ehGrupo)
                return "Estrutura anatômica";
            var quantidade = ContarRepresentacoesGrupo(no);
            return quantidade == 1 ? "Grupo anatômico · 1 estrutura" : $"Grupo anatômico · {quantidade} estruturas";
        }

        /// <summary>
        /// Classificação traduzida da estrutura; para grupos, orienta a escolha de uma subestrutura.
        /// </summary>
        private static string MontarClassificacao(NoInteracaoAnatomica no, InformacaoEstruturaAnatomica informacao)
        {
            if (informacao == null)
                return no.NosFilhos.Count > 0 ? "Escolha uma subestrutura na aba Estruturas." : string.Empty;
            var partes = new List<string>();
            foreach (var parte in new[] { TextoEnumeracoesAnatomicas.Exibir(informacao.DivisaoPrincipal), TextoEnumeracoesAnatomicas.Exibir(informacao.Subdivisao), TextoEnumeracoesAnatomicas.Exibir(informacao.LoboCortical) })
            {
                if (!string.IsNullOrWhiteSpace(parte) && !partes.Contains(parte))
                    partes.Add(parte);
            }

            return string.Join(" › ", partes);
        }

        /// <summary>A classificação tem linha própria (Division) visível neste painel?</summary>
        private bool ClassificacaoTemLinhaPropria => (textoDivisao != null && textoDivisao.gameObject.activeSelf) || (textoDivisaoAlternativa != null && textoDivisaoAlternativa.gameObject.activeSelf);

        /// <summary>
        /// Resumo didático e descrição primeiro; estado de verificação, códigos e nomes de origem no fim.
        /// </summary>
        private string MontarConteudo(NoInteracaoAnatomica no, InformacaoEstruturaAnatomica informacao, RegistroOraculoAnatomico oraculo, string classificacao)
        {
            var construtor = new StringBuilder();
            // Um selo curto no topo substitui o aviso de revisão que antes pesava no meio de toda ficha.
            var linhaDiscreta = MontarSeloRevisao(oraculo);
            if (!string.IsNullOrWhiteSpace(classificacao))
                linhaDiscreta = classificacao + "\n" + linhaDiscreta;
            construtor.AppendLine(Discreto(linhaDiscreta)).AppendLine();
            if (oraculo != null)
            {
                construtor.AppendLine(oraculo.resumoDidatico);
                if (!string.IsNullOrWhiteSpace(oraculo.descricaoCientifica))
                    construtor.AppendLine().AppendLine(oraculo.descricaoCientifica);
            }
            else
            {
                construtor.AppendLine(MontarDescricao(no, informacao));
            }

            // Detalhes de verificação, códigos e nomes de origem ficam no fim, em letra menor.
            var detalhes = OraculoAnatomico.ObterEstadoVerificacao(oraculo);
            var identidade = MontarIdentidade(no, oraculo);
            if (!string.IsNullOrWhiteSpace(identidade))
                detalhes += "\n\n" + identidade;
            construtor.AppendLine().Append(Discreto(detalhes));
            return construtor.ToString().Trim();
        }

        private const int TamanhoTextoDiscreto = 15;

        /// <summary>Texto secundário: menor e em cinza, com marcação compatível com Text e TMP.</summary>
        private static string Discreto(string texto) => string.IsNullOrWhiteSpace(texto) ? string.Empty : $"<size={TamanhoTextoDiscreto}><color={TemaVRSNC.HexTextoSecundario}>{texto}</color></size>";

        /// <summary>Resumo de uma linha do estado da ficha: revisão docente e qualidade das fontes.</summary>
        private static string MontarSeloRevisao(RegistroOraculoAnatomico registro)
        {
            if (registro == null)
                return "Sem ficha científica no catálogo";
            var revisada = !string.IsNullOrWhiteSpace(registro.revisaoDocente) && registro.revisaoDocente.IndexOf("não", System.StringComparison.OrdinalIgnoreCase) < 0;
            var fontes = registro.estadoVerificacao switch
            {
                "source_checked" => "fontes conferidas",
                "limited_specificity" => "fontes de abrangência limitada",
                _ => "fontes não confirmadas",
            };
            return (revisada ? "Revisado por docente" : "Revisão docente pendente") + " · " + fontes;
        }

        private static string MontarIdentidade(NoInteracaoAnatomica no, RegistroOraculoAnatomico oraculo)
        {
            if (oraculo == null)
            {
                var vinculo = no.VinculoAnatomico;
                return vinculo != null && !vinculo.EhGrupo ? $"Código: {vinculo.ConceitoId}\nNome de origem (inglês): {vinculo.NomeCientificoExibido}" : string.Empty;
            }

            var linhas = new List<string>();
            if (!string.IsNullOrWhiteSpace(oraculo.idFma))
                linhas.Add($"Códigos: {oraculo.idArquivo} | {oraculo.idFma}");
            if (!string.IsNullOrWhiteSpace(oraculo.nomeIngles))
                linhas.Add($"Inglês: {oraculo.nomeIngles}");
            if (!string.IsNullOrWhiteSpace(oraculo.nomeLatim))
                linhas.Add($"Latim: {oraculo.nomeLatim}");
            return string.Join("\n", linhas);
        }

        /// <summary>
        /// Escreve o mesmo valor em TMP_Text e Text quando ambos existem.
        /// </summary>
        private static void DefinirTexto(TMP_Text alvo, Text alternativa, string valor)
        {
            if (alvo != null)
            {
                alvo.text = valor;
            }

            if (alternativa != null)
            {
                alternativa.text = valor;
            }
        }

        /// <summary>
        /// Atualiza o rótulo de um botão sem depender de uma classe específica de UI.
        /// </summary>
        private static void DefinirTextoBotao(Button botao, string valor)
        {
            if (botao == null)
            {
                return;
            }

            var texto = botao.GetComponentInChildren<Text>(true);
            if (texto != null)
            {
                texto.text = valor;
            }
        }
    }
}
