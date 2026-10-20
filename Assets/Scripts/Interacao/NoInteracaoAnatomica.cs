using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using VRSNC.Anatomia;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Nó de Interação Anatômica
    // Nome técnico do script: NoInteracaoAnatomica
    // Função: guarda os metadados de navegação, seleção e restauração de cada nó anatômico do SNC.
    // Relação com o protótipo: cada grupo anatômico usa este componente para participar da seleção em camadas,
    // do painel de informações, da visibilidade, da vista explodida e do reset.
    // Objetos da cena: usado no objeto principal Sistema_Nervoso_Central e nos nós anatômicos organizados abaixo dele.
    /// <summary>
    /// Representa uma unidade selecionável da hierarquia anatômica.
    /// O componente não cria hierarquia nova em tempo de execução; ele apenas registra pai, filhos,
    /// nível anatômico e estado original para que os outros controladores possam consultar o nó com segurança.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainInteractionNode")]
    public sealed class NoInteracaoAnatomica : MonoBehaviour
    {
        // Nome técnico do nó, normalmente derivado do GameObject ou da organização anatômica já montada.
        [UnityEngine.Serialization.FormerlySerializedAs("noNome")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("nodeName")]
        private string nomeNo;
        // Nome mostrado ao usuário no painel. Quando estiver vazio, o script gera um nome amigável a partir de nodeName.
        [UnityEngine.Serialization.FormerlySerializedAs("exibicaoNome")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("displayName")]
        private string nomeExibicao;
        // Profundidade na seleção em camadas: raiz = 0, divisões principais = 1 e assim por diante.
        [UnityEngine.Serialization.FormerlySerializedAs("noNivel")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("nodeLevel")]
        private int nivelNo;
        // Referência direta para o pai anatômico, usada para voltar um nível e validar seleção de filhos diretos.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("parentNode")]
        private NoInteracaoAnatomica paiNo;
        // Lista dos filhos imediatos. A seleção atual só aceita estes filhos quando este nó está em foco.
        [UnityEngine.Serialization.FormerlySerializedAs("filhoNos")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("childNodes")]
        private List<NoInteracaoAnatomica> nosFilhos = new();
        // Define se o nó pode ser selecionado por raycast/trigger.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("isSelectable")]
        private bool ehSelecionavel = true;
        // Define se o nó participa da vista explodida do nível atual.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("isExplodable")]
        private bool ehExpansivel = true;
        // Indica se este nó é o foco atual da navegação anatômica.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("isCurrentFocus")]
        private bool ehAtualFoco;
        // Marca o nó raiz da organização do SNC.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("isRootNode")]
        private bool ehRaizNo;
        // Estado original capturado para reset sem reconstruir a cena.
        [UnityEngine.Serialization.FormerlySerializedAs("originalpai")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("originalParent")]
        private Transform paiOriginal;
        [UnityEngine.Serialization.FormerlySerializedAs("originallocalposicao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("originalLocalPosition")]
        private Vector3 posicaoLocalOriginal;
        [UnityEngine.Serialization.FormerlySerializedAs("originallocalrotacao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("originalLocalRotation")]
        private Quaternion rotacaoLocalOriginal = Quaternion.identity;
        [UnityEngine.Serialization.FormerlySerializedAs("originallocalescala")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("originalLocalScale")]
        private Vector3 escalaLocalOriginal = Vector3.one;
        [UnityEngine.Serialization.FormerlySerializedAs("originalativoestado")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("originalActiveState")]
        private bool estadoAtivoOriginal = true;
        // Metadados acadêmicos/anatômicos opcionais exibidos pelo painel de informações.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("structureInfo")]
        private InformacaoEstruturaAnatomica estruturaInformacao;
        [UnityEngine.Serialization.FormerlySerializedAs("anatomiaVinculo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("anatomyBinding")]
        private VinculoAnatomico vinculoAnatomico;
        private static readonly Dictionary<string, string> NomesExibicaoAmigaveis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {{"Brain_AnatomicalOrganization", "Sistema Nervoso Central"}, {"Sistema_Nervoso_Central", "Sistema Nervoso Central"}, {"Cerebro", "Cérebro"}, {"Telencefalo", "Telencéfalo"}, {"Diencefalo", "Diencéfalo"}, {"Cortex", "Córtex"}, {"Lobo_Frontal", "Lobo frontal"}, {"Lobo Frontal", "Lobo frontal"}, {"Lobo_Parietal", "Lobo parietal"}, {"Lobo Parietal", "Lobo parietal"}, {"Lobo_Temporal", "Lobo temporal"}, {"Lobo Temporal", "Lobo temporal"}, {"Lobo_Occipital", "Lobo occipital"}, {"Lobo Occipital", "Lobo occipital"}, {"Insula", "Ínsula"}, {"Cerebelo", "Cerebelo"}, {"Tronco_Encefalico", "Tronco encefálico"}, {"Tronco Encefalico", "Tronco encefálico"}, {"TroncoEncefalico", "Tronco encefálico"}, {"Sistema_Ventricular", "Sistema ventricular"}, {"Sistema Ventricular", "Sistema ventricular"}, {"SistemaVentricular", "Sistema ventricular"}, {"Talamo", "Tálamo"}, {"Hipotalamo", "Hipotálamo"}, {"Epitalamo", "Epitálamo"}, {"Subtalamo", "Subtálamo"}, {"Mesencefalo", "Mesencéfalo"}, {"Ganglios_da_Base", "Gânglios da base"}, {"Ganglios da Base", "Gânglios da base"}, {"Sistema_Limbico", "Sistema límbico"}, {"Sistema Limbico", "Sistema límbico"}, {"Sistema_Olfatorio", "Sistema olfatório"}, {"Sistema Olfatorio", "Sistema olfatório"}, {"Aqueduto_Cerebral", "Aqueduto cerebral"}, {"Aqueduto Cerebral", "Aqueduto cerebral"}, {"Quarto_Ventriculo", "Quarto ventrículo"}, {"Quarto Ventriculo", "Quarto ventrículo"}, {"Plexo_Coroide", "Plexo coroide"}, {"Plexo Coroide", "Plexo coroide"}, };
        public string NomeNo => string.IsNullOrWhiteSpace(nomeNo) ? gameObject.name : nomeNo;
        public string NomeExibicao => ConverterParaNomeExibidoEmPortugues(string.IsNullOrWhiteSpace(nomeExibicao) ? NomeNo : nomeExibicao);
        public int NivelNo => nivelNo;
        public NoInteracaoAnatomica NoPai => paiNo;
        public IReadOnlyList<NoInteracaoAnatomica> NosFilhos => nosFilhos;
        public bool EhSelecionavel => ehSelecionavel;
        public bool EhExpansivel => ehExpansivel;
        public bool EstaNoFocoAtual => ehAtualFoco;
        public bool EhNoRaiz => ehRaizNo;
        public InformacaoEstruturaAnatomica EstruturaInformacao => estruturaInformacao;
        public VinculoAnatomico VinculoAnatomico => vinculoAnatomico;
        /// <summary>
        /// Nome para leitura na interface: o nome em português do catálogo científico quando existe,
        /// sem o código de arquivo entre colchetes e com a inicial maiúscula.
        /// </summary>
        public string NomeLegivel
        {
            get
            {
                var nome = OraculoAnatomico.TentarObter(vinculoAnatomico, out var registro) && !string.IsNullOrWhiteSpace(registro.nomePortugues) ? registro.nomePortugues : RemoverCodigoArquivo(NomeExibicao);
                return CapitalizarInicial(nome);
            }
        }

        /// <summary>
        /// Nome legível com "(parte N)" quando a mesma estrutura está dividida em várias malhas irmãs de mesmo nome.
        /// A numeração segue o código do arquivo de origem, então é estável entre execuções.
        /// </summary>
        public string NomeLegivelComParte
        {
            get
            {
                var nome = NomeLegivel;
                if (paiNo == null)
                    return nome;
                var partes = new List<NoInteracaoAnatomica>();
                foreach (var irmao in paiNo.NosFilhos)
                {
                    if (irmao != null && string.Equals(irmao.NomeLegivel, nome, StringComparison.OrdinalIgnoreCase))
                        partes.Add(irmao);
                }

                if (partes.Count < 2)
                    return nome;
                partes.Sort((a, b) => string.CompareOrdinal(ObterCodigoArquivo(a.NomeExibicao), ObterCodigoArquivo(b.NomeExibicao)));
                return $"{nome} (parte {partes.IndexOf(this) + 1})";
            }
        }

        /// <summary>Indica um nó sem filhos e sem nenhuma malha, como um grupo preservado sem modelo 3D.</summary>
        public bool NaoPossuiModelo3D => nosFilhos.Count == 0 && GetComponentInChildren<Renderer>(true) == null;
        /// <summary>Atribui os dados do próprio objeto; grupos nunca usam a identidade de um descendente.</summary>
        public void VincularAnatomia(InformacaoEstruturaAnatomica estruturaPropria, VinculoAnatomico vinculo)
        {
            if (estruturaPropria != null && estruturaPropria.gameObject != gameObject)
                throw new ArgumentException("Um nó anatômico deve vincular sua própria estrutura.", nameof(estruturaPropria));
            estruturaInformacao = estruturaPropria;
            vinculoAnatomico = vinculo;
        }

        /// <summary>
        /// Garante que o nó tenha nome técnico e referência de metadados quando a cena inicia.
        /// </summary>
        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(nomeNo))
            {
                nomeNo = gameObject.name;
            }

            if (estruturaInformacao == null)
            {
                estruturaInformacao = GetComponent<InformacaoEstruturaAnatomica>();
            }

            if (paiOriginal == null)
            {
                CapturarEstadoOriginal();
            }
        }

        /// <summary>
        /// Configura o nó a partir de uma ferramenta de montagem da hierarquia anatômica.
        /// Mantém a estrutura pai-filhos já calculada e captura o estado original para reset futuro.
        /// </summary>
        public void Configurar(string configuradosNoNome, string configuradosExibicaoNome, int configuradosNivel, NoInteracaoAnatomica configuradosPai, IEnumerable<NoInteracaoAnatomica> configuradosFilhos, bool selecionavel, bool expansivel, bool raizNo)
        {
            nomeNo = string.IsNullOrWhiteSpace(configuradosNoNome) ? gameObject.name : configuradosNoNome;
            nomeExibicao = string.IsNullOrWhiteSpace(configuradosExibicaoNome) ? nomeNo : configuradosExibicaoNome;
            nivelNo = configuradosNivel;
            paiNo = configuradosPai;
            ehSelecionavel = selecionavel;
            ehExpansivel = expansivel;
            ehRaizNo = raizNo;
            estruturaInformacao = GetComponent<InformacaoEstruturaAnatomica>();
            nosFilhos.Clear();
            if (configuradosFilhos != null)
            {
                foreach (var filho in configuradosFilhos)
                {
                    if (filho != null && filho != this && !nosFilhos.Contains(filho))
                    {
                        nosFilhos.Add(filho);
                    }
                }
            }

            CapturarEstadoOriginal();
        }

        /// <summary>
        /// Marca se este nó é o foco atual da navegação em camadas.
        /// </summary>
        public void DefinirFocoAtual(bool valor)
        {
            ehAtualFoco = valor;
        }

        /// <summary>
        /// Captura posição, rotação, escala, pai e estado ativo originais para o botão de reset.
        /// </summary>
        public void CapturarEstadoOriginal()
        {
            paiOriginal = transform.parent;
            posicaoLocalOriginal = transform.localPosition;
            rotacaoLocalOriginal = transform.localRotation;
            escalaLocalOriginal = transform.localScale;
            estadoAtivoOriginal = gameObject.activeSelf;
        }

        /// <summary>
        /// Restaura o nó ao estado inicial sem remover componentes, colliders ou referências serializadas.
        /// </summary>
        public void RestaurarEstadoOriginal()
        {
            if (paiOriginal != null && transform.parent != paiOriginal)
            {
                transform.SetParent(paiOriginal, false);
            }

            transform.localPosition = posicaoLocalOriginal;
            transform.localRotation = rotacaoLocalOriginal;
            transform.localScale = escalaLocalOriginal;
            gameObject.SetActive(estadoAtivoOriginal);
            ehAtualFoco = false;
        }

        /// <summary>
        /// Calcula os limites renderizáveis do nó e dos seus filhos para vista explodida e posicionamento.
        /// Objetos inativos, como auxiliares ocultos, não deslocam o centro anatômico.
        /// </summary>
        public Bounds ObterLimitesRenderizaveis()
        {
            var renderizadores = GetComponentsInChildren<Renderer>(true);
            var possuiLimites = false;
            var limites = new Bounds(transform.position, Vector3.zero);
            foreach (var renderizador in renderizadores)
            {
                if (renderizador == null || !renderizador.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!possuiLimites)
                {
                    limites = renderizador.bounds;
                    possuiLimites = true;
                }
                else
                {
                    limites.Encapsulate(renderizador.bounds);
                }
            }

            return limites;
        }

        /// <summary>
        /// Indica se este nó pertence à subárvore de outro nó anatômico.
        /// Essa regra é usada para isolar uma estrutura junto com seus ancestrais e descendentes.
        /// </summary>
        public bool EhDescendenteDe(NoInteracaoAnatomica outro)
        {
            if (outro == null)
            {
                return false;
            }

            var atual = paiNo;
            while (atual != null)
            {
                if (atual == outro)
                {
                    return true;
                }

                atual = atual.paiNo;
            }

            return false;
        }

        /// <summary>
        /// Confirma se o nó é filho direto do foco atual, preservando a seleção anatômica por camadas.
        /// </summary>
        public bool EhFilhoDiretoDe(NoInteracaoAnatomica outro)
        {
            return outro != null && paiNo == outro;
        }

        /// <summary>
        /// Converte nomes técnicos ou sem acento para nomes mais claros em português no painel.
        /// O GameObject real não é renomeado aqui; a tradução afeta apenas texto visível.
        /// </summary>
        public static string ConverterParaNomeExibidoEmPortugues(string nomeBruto)
        {
            if (string.IsNullOrWhiteSpace(nomeBruto))
            {
                return "Sistema Nervoso Central";
            }

            var nomeLimpo = VinculoAnatomico.CorrigirGrafiaExibida(nomeBruto.Trim());
            if (NomesExibicaoAmigaveis.TryGetValue(nomeLimpo, out var nomeAmigavel))
            {
                return nomeAmigavel;
            }

            var semSublinhados = nomeLimpo.Replace("_", " ");
            return NomesExibicaoAmigaveis.TryGetValue(semSublinhados, out nomeAmigavel) ? nomeAmigavel : semSublinhados;
        }

        private static readonly Regex CodigoArquivoFinal = new(@"\s*\[[A-Z]{2}\d+[A-Z]*\]\s*$");
        /// <summary>Remove do fim de um nome exibido o código de arquivo do BodyParts3D, como "[FJ7465]".</summary>
        public static string RemoverCodigoArquivo(string nome) => string.IsNullOrEmpty(nome) ? nome : CodigoArquivoFinal.Replace(nome, string.Empty);
        /// <summary>Código de arquivo do BodyParts3D no fim de um nome exibido, como "FJ7465"; vazio quando não há.</summary>
        public static string ObterCodigoArquivo(string nome) => string.IsNullOrEmpty(nome) ? string.Empty : CodigoArquivoFinal.Match(nome).Value.Trim().Trim('[', ']');
        /// <summary>Coloca a primeira letra em maiúscula sem alterar o restante do texto.</summary>
        public static string CapitalizarInicial(string texto) => string.IsNullOrEmpty(texto) ? texto : char.ToUpperInvariant(texto[0]) + texto.Substring(1);
    }
}