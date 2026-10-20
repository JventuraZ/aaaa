using System;
using System.Collections.Generic;
using UnityEngine;
using VRSNC.Anatomia;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Separa temporariamente cada representação anatômica física a partir de uma referência imutável
    /// de limites visuais. Nunca altera hierarquia anatômica, malhas, escala ou metadados.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "GlobalExplodeController")]
    public sealed class ControladorExpansaoGlobal : MonoBehaviour
    {
        public const int QuantidadeFolhasEsperada = 240;
        public const int QuantidadeGruposEsperada = 32;
        [UnityEngine.Serialization.FormerlySerializedAs("anatomiaraiz")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("anatomyRoot")]
        private Transform raizAnatomica;
        [UnityEngine.Serialization.FormerlySerializedAs("maximoRadialDistancia")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("maximumRadialDistance")]
        private float distanciaRadialMaxima = 0.22f;
        private readonly List<EstadoFolha> folhas = new();
        // Instantaneos de grupo permanecem apenas uma invariante de índice. A separação global nunca pode
        // gravar um transform de grupo: um grupo ou a raiz pode estar sendo segurado quando a segunda
        // mão inicia seu gesto.
        private readonly List<EstadoGrupo> grupos = new();
        private readonly HashSet<NoInteracaoAnatomica> nosIndexados = new();
        private bool indexado;
        private int quantidadeGruposIndexados;
        private bool gestoAtivo;
        private bool avisoIndiceEmitido;
        private bool avisoContagemEmitido;
        private float fator;
        private uint estadoVersao;
        public bool EstaPronto => indexado;
        public bool GestoAtivo => gestoAtivo;
        public bool EstaExpandido => fator > 0f;
        public float Fator => fator;
        public uint VersaoEstado => estadoVersao;
        public int QuantidadeFolhasIndexadas => folhas.Count;
        public int QuantidadeGruposIndexados => quantidadeGruposIndexados;
        public int QuantidadeTransformacoesGrupoIndexadas => grupos.Count;
        public int QuantidadeDirecoesDegeneradas
        {
            get; private set;
        }

        public event Action<float> FatorAlterado;
        private void Awake()
        {
            raizAnatomica ??= transform;
            GarantirIndiceAtualizado();
        }

        public void Configurar(Transform raiz)
        {
            // Nunca substitui uma referência montada conhecida pela pose que estiver
            // visível durante um gesto explodido.
            if (indexado)
                CancelarERestaurar();
            raizAnatomica = raiz != null ? raiz : transform;
            indexado = false;
            quantidadeGruposIndexados = 0;
            folhas.Clear();
            grupos.Clear();
            nosIndexados.Clear();
            QuantidadeDirecoesDegeneradas = 0;
            GarantirIndiceAtualizado();
        }

        /// <summary>Captura apenas um catálogo montado no fator zero. Uma contagem falha nunca move uma folha.</summary>
        public bool GarantirIndiceAtualizado()
        {
            if (indexado)
                return true;
            raizAnatomica ??= transform;
            var localExpandir = GetComponent<ControladorVisaoExplodida>();
            if (localExpandir != null && localExpandir.EstaExpandido)
                return false;
            var interagiveis = raizAnatomica.GetComponentsInChildren<ElementoInteragivelMao>(true);
            var vinculos = raizAnatomica.GetComponentsInChildren<VinculoAnatomico>(true);
            var quantidadeGrupos = 0;
            foreach (var vinculo in vinculos)
            {
                if (vinculo != null && vinculo.EhGrupo)
                    quantidadeGrupos++;
            }

            if (interagiveis.Length == 0 || quantidadeGrupos == 0)
            {
                AvisarIndiceIndisponivel($"nenhuma estrutura ou grupo anatômico encontrado sob {raizAnatomica.name}");
                return false;
            }

            if ((interagiveis.Length != QuantidadeFolhasEsperada || quantidadeGrupos != QuantidadeGruposEsperada) && !avisoContagemEmitido)
            {
                // O catálogo mudou: a expansão segue com o que existe na cena, mas a diferença fica registrada.
                avisoContagemEmitido = true;
                Debug.LogWarning($"[VR SNC] Expansão global indexando {interagiveis.Length} estruturas e {quantidadeGrupos} grupos; o catálogo de referência tem {QuantidadeFolhasEsperada} e {QuantidadeGruposEsperada}.", this);
            }

            folhas.Clear();
            grupos.Clear();
            nosIndexados.Clear();
            quantidadeGruposIndexados = quantidadeGrupos;
            var centroVisual = CalcularCentroVisual(interagiveis);
            var raizEhGrupo = false;
            foreach (var vinculo2 in vinculos)
            {
                if (vinculo2 == null || !vinculo2.EhGrupo)
                    continue;
                var noGrupo = vinculo2.GetComponent<NoInteracaoAnatomica>();
                if (noGrupo == null)
                {
                    LimparIndice();
                    AvisarIndiceIndisponivel($"o grupo {vinculo2.name} não tem nó de interação");
                    return false;
                }

                // A raiz também é um grupo do catálogo, mas deliberadamente
                // não grava pose: o fechamento global deve preservar a pose raiz do SNC do usuário.
                if (noGrupo.transform == raizAnatomica)
                {
                    raizEhGrupo = true;
                    continue;
                }

                if (noGrupo.transform.parent == null)
                {
                    LimparIndice();
                    AvisarIndiceIndisponivel($"o grupo {noGrupo.name} está fora da hierarquia anatômica");
                    return false;
                }

                grupos.Add(new EstadoGrupo(noGrupo.transform, noGrupo.transform.parent, noGrupo.transform.localPosition, noGrupo.transform.localRotation, noGrupo.transform.localScale));
            }

            grupos.Sort((primeiro, segundo) => ObterProfundidadeTransformacao(primeiro.Transformacao).CompareTo(ObterProfundidadeTransformacao(segundo.Transformacao)));
            foreach (var interagivel in interagiveis)
            {
                var no = interagivel != null ? interagivel.No : null;
                if (no == null || no.VinculoAnatomico == null || no.VinculoAnatomico.EhGrupo || !nosIndexados.Add(no) || no.transform.parent == null)
                {
                    LimparIndice();
                    AvisarIndiceIndisponivel($"a estrutura {(interagivel != null ? interagivel.name : "(nula)")} não tem um vínculo anatômico de estrutura único");
                    return false;
                }

                var limites = no.ObterLimitesRenderizaveis();
                var variacaoMundo = limites.center - raizAnatomica.TransformPoint(centroVisual);
                // A degeneração é avaliada em unidades de modelo/raiz. Na escala raiz 0,001
                // comum do SNC importado, testar aqui um vetor de mundo classificaria centros
                // distintos de folhas como coincidentes antes de normalizar sua direção.
                var variacaoLocal = raizAnatomica.InverseTransformPoint(limites.center) - centroVisual;
                // As direções retêm apenas a orientação raiz, nunca a escala raiz. Assim, o
                // deslocamento final permanece maximumRadialDistance em metros no mundo.
                var direcao = raizAnatomica.InverseTransformDirection(variacaoMundo);
                if (variacaoLocal.sqrMagnitude < 0.000001f && no.NoPai != null)
                {
                    var paiLimites = no.NoPai.ObterLimitesRenderizaveis();
                    variacaoMundo = paiLimites.center - raizAnatomica.TransformPoint(centroVisual);
                    variacaoLocal = raizAnatomica.InverseTransformPoint(paiLimites.center) - centroVisual;
                    direcao = raizAnatomica.InverseTransformDirection(variacaoMundo);
                }

                if (variacaoLocal.sqrMagnitude < 0.000001f)
                {
                    // Um centro visual coincidente não tem direção radial fiel. Mantém-no montado
                    // em vez de inventar um vetor aleatório ou anatomicamente enganoso.
                    QuantidadeDirecoesDegeneradas++;
                }

                folhas.Add(new EstadoFolha(no, no.transform.parent, no.transform.localPosition, variacaoLocal.sqrMagnitude >= 0.000001f ? direcao.normalized : Vector3.zero));
            }

            indexado = folhas.Count == interagiveis.Length && nosIndexados.Count == interagiveis.Length && grupos.Count == quantidadeGrupos - (raizEhGrupo ? 1 : 0);
            if (!indexado)
            {
                LimparIndice();
                AvisarIndiceIndisponivel("o índice de estruturas e grupos ficou incompleto");
            }

            return indexado;
        }

        private void AvisarIndiceIndisponivel(string motivo)
        {
            if (avisoIndiceEmitido)
                return;
            avisoIndiceEmitido = true;
            Debug.LogWarning("[VR SNC] Expansão global indisponível: " + motivo + ".", this);
        }

        public bool ContemFolha(NoInteracaoAnatomica no) => GarantirIndiceAtualizado() && no != null && nosIndexados.Contains(no);
        public bool IniciarGesto()
        {
            if (!GarantirIndiceAtualizado())
                return false;
            AplicarFator();
            gestoAtivo = true;
            AvancarEstado();
            return true;
        }

        public bool DefinirFator(float fatorSolicitado)
        {
            if (!GarantirIndiceAtualizado() || float.IsNaN(fatorSolicitado) || float.IsInfinity(fatorSolicitado))
                return false;
            fator = Mathf.Clamp01(fatorSolicitado);
            AplicarFator();
            FatorAlterado?.Invoke(fator);
            return true;
        }

        public bool ConcluirGesto()
        {
            if (!gestoAtivo)
                return false;
            gestoAtivo = false;
            AvancarEstado();
            return true;
        }

        /// <summary>Restaura a referência local precisa do fator zero, inclusive após perda de rastreamento ou redefinição.</summary>
        public void CancelarERestaurar()
        {
            gestoAtivo = false;
            fator = 0f;
            if (indexado)
                AplicarFator();
            FatorAlterado?.Invoke(fator);
            AvancarEstado();
        }

        private Vector3 CalcularCentroVisual(ElementoInteragivelMao[] interagiveis)
        {
            var possuiLimites = false;
            var limites = new Bounds(raizAnatomica.position, Vector3.zero);
            foreach (var interagivel in interagiveis)
            {
                var no = interagivel != null ? interagivel.No : null;
                if (no == null)
                    continue;
                var limitesNo = no.ObterLimitesRenderizaveis();
                if (!possuiLimites)
                {
                    limites = limitesNo;
                    possuiLimites = true;
                }
                else
                    limites.Encapsulate(limitesNo);
            }

            return raizAnatomica.InverseTransformPoint(limites.center);
        }

        private void AplicarFator()
        {
            foreach (var folha in folhas)
            {
                if (folha.No == null || folha.Pai == null)
                    continue;
                // Não repara parentalidade, rotações, grupos ou a raiz do SNC aqui.
                // Eles são limites de posse da anatomia/interação. Uma folha cujo pai
                // mudou externamente é deliberadamente ignorada, em vez de ser
                // silenciosamente reparentada para um snapshot antigo.
                if (folha.No.transform.parent != folha.Pai)
                    continue;
                if (fator <= 0f)
                {
                    folha.No.transform.localPosition = folha.PosicaoLocalOriginal;
                    continue;
                }

                // A referência local permanece relativa ao transform ancestral atual da folha.
                // Portanto, uma rotação/movimento do usuário em um ancestral anatômico ou na raiz do SNC não pode
                // fazer o fator zero e um fator positivo usarem sistemas de coordenadas diferentes.
                var referenciaMundo = folha.Pai.TransformPoint(folha.PosicaoLocalOriginal);
                var deslocamentoMundo = raizAnatomica.TransformDirection(folha.DirecaoRadial) * (distanciaRadialMaxima * fator);
                folha.No.transform.localPosition = folha.Pai.InverseTransformPoint(referenciaMundo + deslocamentoMundo);
            }
        }

        private void AvancarEstado()
        {
            unchecked
            {
                estadoVersao++;
            }
        }

        private void LimparIndice()
        {
            folhas.Clear();
            grupos.Clear();
            nosIndexados.Clear();
            indexado = false;
            quantidadeGruposIndexados = 0;
        }

        private static int ObterProfundidadeTransformacao(Transform transformacao)
        {
            var profundidade = 0;
            while (transformacao != null)
            {
                profundidade++;
                transformacao = transformacao.parent;
            }

            return profundidade;
        }

        private readonly struct EstadoFolha
        {
            public readonly NoInteracaoAnatomica No;
            public readonly Transform Pai;
            public readonly Vector3 PosicaoLocalOriginal;
            public readonly Vector3 DirecaoRadial;
            public EstadoFolha(NoInteracaoAnatomica no, Transform pai, Vector3 localPosicao, Vector3 direcaoRadial)
            {
                No = no;
                Pai = pai;
                PosicaoLocalOriginal = localPosicao;
                DirecaoRadial = direcaoRadial;
            }
        }

        private readonly struct EstadoGrupo
        {
            public readonly Transform Transformacao;
            public readonly Transform Pai;
            public readonly Vector3 PosicaoLocalOriginal;
            public readonly Quaternion RotacaoLocalOriginal;
            public readonly Vector3 EscalaLocalOriginal;
            public EstadoGrupo(Transform transformacao, Transform pai, Vector3 localPosicao, Quaternion localRotacao, Vector3 localEscala)
            {
                Transformacao = transformacao;
                Pai = pai;
                PosicaoLocalOriginal = localPosicao;
                RotacaoLocalOriginal = localRotacao;
                EscalaLocalOriginal = localEscala;
            }
        }
    }
}
