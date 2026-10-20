using System.Collections.Generic;
using UnityEngine;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Controlador de Vista Explodida Anatômica
    // Nome técnico do script: ControladorVisaoExplodida
    // Função: afasta temporariamente as folhas descendentes do foco atual para facilitar a visualização das estruturas.
    // Relação com o protótipo: usa a hierarquia anatômica já existente e guarda posições originais para restaurar,
    // sem reclassificar objetos ou alterar colliders.
    // Objetos da cena: usado no Sistema_Nervoso_Central e acionado pelo painel.
    /// <summary>
    /// Controla a separação visual temporária dos filhos do foco atual.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainExplodedViewController")]
    public sealed class ControladorVisaoExplodida : MonoBehaviour
    {
        // Gerenciador que fornece o foco anatômico atual.
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        // Distância em metros/unidades de mundo aplicada aos filhos do foco.
        [UnityEngine.Serialization.FormerlySerializedAs("expandirdistancia")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("explodeDistance")]
        private float distanciaExpansao = 0.18f;
        // Campo reservado para compatibilidade com variações futuras de renderers inativos.
        [UnityEngine.Serialization.FormerlySerializedAs("incluirInativoRenderizadores")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("includeInactiveRenderers")]
        private bool incluirRenderizadoresInativos;
        // Posições locais originais das folhas deslocadas pela vista explodida.
        private readonly Dictionary<NoInteracaoAnatomica, Vector3> posicoesLocaisOriginais = new();
        private readonly Dictionary<NoInteracaoAnatomica, Vector3> separacaoLocalDeslocamentos = new();
        // Foco que está explodido atualmente; nulo indica que a cena está remontada.
        private NoInteracaoAnatomica noFocoExpandido;
        private NoInteracaoAnatomica noFocoGesto;
        private uint separacaoEstadoVersao;
        private ControladorExpansaoGlobal controladorExpansaoGlobal;
        public bool EstaExpandido => noFocoExpandido != null || noFocoGesto != null;
        public bool GestoEstaSeparando => noFocoGesto != null;
        public uint SeparacaoEstadoVersao => separacaoEstadoVersao;
        public float ProgressoInicialGesto
        {
            get; private set;
        }

        public float ProgressoAtualSeparacao => ObterProgressoAtualSeparacao();
        /// <summary>
        /// Resolve o gerenciador local quando a cena não preencheu a referência.
        /// </summary>
        private void Awake()
        {
            if (gerenciadorSelecao == null)
            {
                gerenciadorSelecao = GetComponent<GerenciadorSelecaoHierarquica>();
            }

            controladorExpansaoGlobal = GetComponent<ControladorExpansaoGlobal>();
        }

        /// <summary>
        /// Configura o gerenciador de seleção usado como fonte do foco.
        /// </summary>
        public void Configurar(GerenciadorSelecaoHierarquica gerenciador)
        {
            gerenciadorSelecao = gerenciador;
        }

        /// <summary>
        /// Afasta as folhas físicas descendentes do foco atual, usando os centros dos bounds como referência.
        /// </summary>
        public void ExpandirNivelAtual()
        {
            CancelarExpansaoGlobalAntesAlterarLocal();
            var foco = gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null;
            if (foco == null || foco.NosFilhos.Count == 0)
            {
                return;
            }

            ReagruparNivelAtual();
            if (!CapturarSeparacaoDeslocamentos(foco))
            {
                return;
            }

            AplicarSeparacao(1f);
            noFocoExpandido = foco;
            AvancarSeparacaoEstado();
        }

        /// <summary>
        /// Devolve todos os filhos deslocados às posições locais originais.
        /// </summary>
        public void ReagruparNivelAtual()
        {
            foreach (var par in posicoesLocaisOriginais)
            {
                if (par.Key != null)
                {
                    par.Key.transform.localPosition = par.Value;
                }
            }

            posicoesLocaisOriginais.Clear();
            separacaoLocalDeslocamentos.Clear();
            noFocoExpandido = null;
            noFocoGesto = null;
            ProgressoInicialGesto = 0f;
            AvancarSeparacaoEstado();
        }

        /// <summary>
        /// Prepara uma separação contínua entre todas as folhas físicas descendentes de
        /// um grupo selecionado. O transform do próprio grupo, seus ancestrais, a malha
        /// e a escala não são alterados.
        /// </summary>
        public bool IniciarSeparacaoPorGesto(NoInteracaoAnatomica grupo)
        {
            // Uma vista explodida global retida possui todas as folhas do catálogo, inclusive folhas
            // fora deste grupo. Restaurá-la aqui alteraria esses elementos externos antes
            // a bounded gesture has even begun, so refuse rather than silently reset.
            if (controladorExpansaoGlobal != null && (controladorExpansaoGlobal.GestoAtivo || controladorExpansaoGlobal.EstaExpandido))
            {
                return false;
            }

            if (grupo == null || grupo.NosFilhos.Count == 0)
            {
                return false;
            }

            // Retomar duas mãos no mesmo grupo já explodido preserva o snapshot e
            // o deslocamento atual; assim o primeiro frame do gesto não dá salto.
            if (noFocoExpandido == grupo && posicoesLocaisOriginais.Count > 0 && separacaoLocalDeslocamentos.Count > 0)
            {
                noFocoExpandido = null;
                noFocoGesto = grupo;
                ProgressoInicialGesto = ObterProgressoAtualSeparacao();
                AvancarSeparacaoEstado();
                return true;
            }

            ReagruparNivelAtual();
            if (!CapturarSeparacaoDeslocamentos(grupo))
            {
                return false;
            }

            noFocoGesto = grupo;
            ProgressoInicialGesto = 0f;
            AvancarSeparacaoEstado();
            return true;
        }

        /// <summary>
        /// Atualiza os offsets de folhas preparados para o gesto de duas mãos.
        /// </summary>
        public bool DefinirSeparacaoPorGesto(NoInteracaoAnatomica grupo, float progressoNormalizado)
        {
            if (grupo == null || grupo != noFocoGesto)
            {
                return false;
            }

            AplicarSeparacao(Mathf.Clamp01(progressoNormalizado));
            return true;
        }

        /// <summary>
        /// Finaliza a fase de duas mãos, mantendo a separação resultante para inspeção.
        /// Reset, reassemble e perda de tracking ainda restauram o snapshot original.
        /// </summary>
        public bool ConcluirSeparacaoPorGesto()
        {
            if (noFocoGesto == null)
            {
                return false;
            }

            noFocoExpandido = noFocoGesto;
            noFocoGesto = null;
            // Mantém offsets junto ao snapshot para uma retomada contínua do mesmo
            // grupo; ReassembleCurrentLevel continua sendo o único ponto de limpeza.
            ProgressoInicialGesto = 0f;
            AvancarSeparacaoEstado();
            return true;
        }

        /// <summary>
        /// Cancela uma separação em andamento e restaura as posições locais capturadas.
        /// </summary>
        public void CancelarSeparacaoPorGesto()
        {
            ReagruparNivelAtual();
        }

        /// <summary>Avisado quando o usuário explode ou reagrupa o nível atual (botão, controle ou atalho).</summary>
        public event System.Action emExpansaoAlternada;

        /// <summary>
        /// Alterna entre vista explodida e restaurada para o nível atual.
        /// </summary>
        public void AlternarExpansaoNivelAtual()
        {
            if (EstaExpandido)
            {
                ReagruparNivelAtual();
            }
            else
            {
                ExpandirNivelAtual();
            }

            emExpansaoAlternada?.Invoke();
        }

        /// <summary>
        /// Entrada chamada pelo reset geral para garantir que nada fique deslocado.
        /// </summary>
        public void RestaurarVisaoExplodida()
        {
            ReagruparNivelAtual();
            controladorExpansaoGlobal?.CancelarERestaurar();
        }

        // A explosão local de folhas descendentes e offsets globais de folhas nunca podem gravar os mesmos
        // transforms aninhados simultaneamente. Invocar a vista manual restaura explicitamente a
        // global baseline first; root-level model movement remains independent.
        private void CancelarExpansaoGlobalAntesAlterarLocal()
        {
            if (controladorExpansaoGlobal != null && (controladorExpansaoGlobal.GestoAtivo || controladorExpansaoGlobal.EstaExpandido))
            {
                controladorExpansaoGlobal.CancelarERestaurar();
            }
        }

        private void AvancarSeparacaoEstado()
        {
            unchecked
            {
                separacaoEstadoVersao++;
            }
        }

        private bool CapturarSeparacaoDeslocamentos(NoInteracaoAnatomica foco)
        {
            var focoLimites = foco.ObterLimitesRenderizaveis();
            var centro = focoLimites.center;
            var indice = 0;
            // A separação usa apenas representações físicas já existentes. Ela não cria
            // nós, não move grupos e não reclassifica a anatomia; apenas percorre as
            // folhas descendentes registradas pelos BrainHandInteractable existentes.
            var folhasDescendentes = foco.GetComponentsInChildren<ElementoInteragivelMao>(true);
            foreach (var interagivel in folhasDescendentes)
            {
                var filho = interagivel != null ? interagivel.No : null;
                if (filho == null || filho == foco || !filho.EhExpansivel || !filho.EhDescendenteDe(foco) || filho.VinculoAnatomico == null || filho.VinculoAnatomico.EhGrupo || posicoesLocaisOriginais.ContainsKey(filho))
                {
                    continue;
                }

                posicoesLocaisOriginais[filho] = filho.transform.localPosition;
                var filhoLimites = filho.ObterLimitesRenderizaveis();
                var direcao = filhoLimites.size == Vector3.zero ? Vector3.zero : filhoLimites.center - centro;
                if (direcao.sqrMagnitude < 0.0001f)
                {
                    var angulo = (Mathf.PI * 2f / Mathf.Max(1, folhasDescendentes.Length)) * indice;
                    direcao = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo));
                }

                var deslocamentoMundo = direcao.normalized * distanciaExpansao;
                separacaoLocalDeslocamentos[filho] = filho.transform.parent != null ? filho.transform.parent.InverseTransformVector(deslocamentoMundo) : deslocamentoMundo;
                indice++;
            }

            return posicoesLocaisOriginais.Count > 0;
        }

        private void AplicarSeparacao(float progressoNormalizado)
        {
            foreach (var par in posicoesLocaisOriginais)
            {
                if (par.Key != null && separacaoLocalDeslocamentos.TryGetValue(par.Key, out var deslocamento))
                {
                    par.Key.transform.localPosition = par.Value + deslocamento * progressoNormalizado;
                }
            }
        }

        private float ObterProgressoAtualSeparacao()
        {
            foreach (var par in posicoesLocaisOriginais)
            {
                if (par.Key == null || !separacaoLocalDeslocamentos.TryGetValue(par.Key, out var deslocamento) || deslocamento.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                var atualDeslocamento = par.Key.transform.localPosition - par.Value;
                return Mathf.Clamp01(Vector3.Dot(atualDeslocamento, deslocamento) / deslocamento.sqrMagnitude);
            }

            return 0f;
        }
    }
}