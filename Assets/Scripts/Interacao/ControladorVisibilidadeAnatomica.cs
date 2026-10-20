using System.Collections.Generic;
using UnityEngine;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Controlador de Visibilidade Anatômica
    // Nome técnico do script: ControladorVisibilidadeAnatomica
    // Função: oculta, mostra, isola e restaura nós anatômicos preservando renderers e estados originais.
    // Relação com o protótipo: permite isolar uma estrutura selecionada pelo painel ou controle sem remover objetos,
    // componentes ou nós anatômicos da cena; colliders acompanham a visibilidade para não aceitar raycasts invisíveis.
    // Objetos da cena: usado no Sistema_Nervoso_Central.
    /// <summary>
    /// Gerencia visibilidade por renderer ou por GameObject.
    /// O modo padrão usa Renderer.enabled e Collider.enabled para manter visual e interação física coerentes.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainVisibilityController")]
    public sealed class ControladorVisibilidadeAnatomica : MonoBehaviour
    {
        // Raiz anatômica percorrida para capturar e restaurar estados.
        [UnityEngine.Serialization.FormerlySerializedAs("raizno")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rootNode")]
        private NoInteracaoAnatomica noRaiz;
        // Quando verdadeiro, altera Renderer.enabled e Collider.enabled em vez de desativar o GameObject.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("useRendererMode")]
        private bool usarRenderizadorModo = true;
        // Estados originais usados pelo reset e pela restauração de isolamento.
        private readonly Dictionary<Renderer, bool> estadosRenderizadoresOriginais = new();
        private readonly Dictionary<Collider, bool> estadosColisoresOriginais = new();
        private readonly Dictionary<GameObject, bool> estadosAtivosOriginais = new();
        // Estruturas que o usuário ocultou (Ocultar), separadas do que o isolamento esconde temporariamente.
        private readonly HashSet<NoInteracaoAnatomica> ocultosPeloUsuario = new();
        public bool EhIsolando
        {
            get; private set;
        }

        /// <summary>Avisado depois de qualquer mudança de visibilidade (ocultar, mostrar, isolar ou restaurar).</summary>
        public event System.Action emVisibilidadeAlterada;
        public int QuantidadeOcultas => ocultosPeloUsuario.Count;
        public bool EstaOculto(NoInteracaoAnatomica no) => no != null && ocultosPeloUsuario.Contains(no);

        /// <summary>
        /// Resolve a raiz e captura a visibilidade inicial da cena.
        /// </summary>
        private void Awake()
        {
            if (noRaiz == null)
            {
                noRaiz = GetComponent<NoInteracaoAnatomica>();
            }

            CapturarVisibilidadeOriginal();
        }

        /// <summary>
        /// Configura a raiz anatômica e recaptura os estados visuais.
        /// </summary>
        public void Configurar(NoInteracaoAnatomica raiz)
        {
            noRaiz = raiz;
            CapturarVisibilidadeOriginal();
        }

        /// <summary>
        /// Captura quais renderers e nós estavam ativos no início, para reset sem reconstrução.
        /// </summary>
        public void CapturarVisibilidadeOriginal()
        {
            estadosRenderizadoresOriginais.Clear();
            estadosColisoresOriginais.Clear();
            estadosAtivosOriginais.Clear();
            var raiz = noRaiz != null ? noRaiz.transform : transform;
            foreach (var renderizador in raiz.GetComponentsInChildren<Renderer>(true))
            {
                estadosRenderizadoresOriginais[renderizador] = renderizador.enabled;
            }

            foreach (var colisor in raiz.GetComponentsInChildren<Collider>(true))
            {
                estadosColisoresOriginais[colisor] = colisor.enabled;
            }

            foreach (var no in raiz.GetComponentsInChildren<NoInteracaoAnatomica>(true))
            {
                estadosAtivosOriginais[no.gameObject] = no.gameObject.activeSelf;
            }
        }

        /// <summary>
        /// Mostra um nó específico.
        /// </summary>
        public void ExibirNo(NoInteracaoAnatomica no)
        {
            DefinirNoVisivel(no, true);
            ocultosPeloUsuario.Remove(no);
            // Mostrar um grupo não revela as peças dele que continuam ocultas pelo usuário.
            ReaplicarOcultasPeloUsuario();
            emVisibilidadeAlterada?.Invoke();
        }

        /// <summary>
        /// Oculta um nó específico.
        /// </summary>
        public void OcultarNo(NoInteracaoAnatomica no)
        {
            if (no == null)
                return;
            DefinirNoVisivel(no, false);
            ocultosPeloUsuario.Add(no);
            emVisibilidadeAlterada?.Invoke();
        }

        /// <summary>
        /// Alterna entre ocultar e mostrar um nó; se nenhum nó vier informado, restaura tudo.
        /// </summary>
        public void AlternarVisibilidadeNo(NoInteracaoAnatomica no)
        {
            if (no == null)
            {
                RestaurarVisibilidade();
                return;
            }

            if (EstaOculto(no))
                ExibirNo(no);
            else
                OcultarNo(no);
        }

        /// <summary>Mostra de novo tudo o que o usuário ocultou, sem mexer no isolamento.</summary>
        public void MostrarOcultas()
        {
            foreach (var no in new List<NoInteracaoAnatomica>(ocultosPeloUsuario))
                DefinirNoVisivel(no, true);
            ocultosPeloUsuario.Clear();
            emVisibilidadeAlterada?.Invoke();
        }

        /// <summary>Volta a esconder o que o usuário ocultou depois de uma operação que mostra peças em massa.</summary>
        private void ReaplicarOcultasPeloUsuario()
        {
            foreach (var no in ocultosPeloUsuario)
                DefinirNoVisivel(no, false);
        }

        /// <summary>
        /// Entrada usada por controles: alterna nó selecionado ou restaura a cena quando não há seleção.
        /// </summary>
        public void AlternarNoOuRestaurar(NoInteracaoAnatomica no)
        {
            if (no == null)
            {
                RestaurarVisibilidade();
                return;
            }

            AlternarVisibilidadeNo(no);
        }

        /// <summary>
        /// Isola um nó mantendo visíveis seus descendentes e ancestrais, para preservar contexto anatômico.
        /// </summary>
        public void ExibirSomenteNo(NoInteracaoAnatomica no)
        {
            if (noRaiz == null || no == null)
            {
                return;
            }

            foreach (var filho in noRaiz.GetComponentsInChildren<NoInteracaoAnatomica>(true))
            {
                var devemMostrar = filho == no || filho.EhDescendenteDe(no) || no.EhDescendenteDe(filho);
                DefinirNoVisivel(filho, devemMostrar);
            }

            ReaplicarOcultasPeloUsuario();
            EhIsolando = true;
            emVisibilidadeAlterada?.Invoke();
        }

        /// <summary>
        /// Mostra todos os renderers e nós sob a raiz, saindo do modo de isolamento.
        /// </summary>
        public void ExibirTudo()
        {
            var raiz = noRaiz != null ? noRaiz.transform : transform;
            foreach (var renderizador in raiz.GetComponentsInChildren<Renderer>(true))
            {
                renderizador.enabled = true;
            }

            foreach (var colisor in raiz.GetComponentsInChildren<Collider>(true))
            {
                RestaurarEstadoColisor(colisor);
            }

            foreach (var no in raiz.GetComponentsInChildren<NoInteracaoAnatomica>(true))
            {
                no.gameObject.SetActive(true);
            }

            // Sair do isolamento não desfaz o que o usuário ocultou de propósito.
            ReaplicarOcultasPeloUsuario();
            EhIsolando = false;
            emVisibilidadeAlterada?.Invoke();
        }

        /// <summary>
        /// Restaura exatamente os estados capturados no início da execução.
        /// </summary>
        public void RestaurarVisibilidade()
        {
            foreach (var par in estadosRenderizadoresOriginais)
            {
                if (par.Key != null)
                {
                    par.Key.enabled = par.Value;
                }
            }

            foreach (var par2 in estadosColisoresOriginais)
            {
                if (par2.Key != null)
                {
                    par2.Key.enabled = par2.Value;
                }
            }

            foreach (var par3 in estadosAtivosOriginais)
            {
                if (par3.Key != null)
                {
                    par3.Key.SetActive(par3.Value);
                }
            }

            ocultosPeloUsuario.Clear();
            EhIsolando = false;
            emVisibilidadeAlterada?.Invoke();
        }

        /// <summary>
        /// Aplica visibilidade a um nó sem alterar sua classificação anatômica.
        /// </summary>
        private void DefinirNoVisivel(NoInteracaoAnatomica no, bool visivel)
        {
            if (no == null)
            {
                return;
            }

            if (usarRenderizadorModo)
            {
                foreach (var renderizador in no.GetComponentsInChildren<Renderer>(true))
                {
                    renderizador.enabled = visivel;
                }

                foreach (var colisor in no.GetComponentsInChildren<Collider>(true))
                {
                    CapturarColisorEstadoSeNecessario(colisor);
                    if (visivel)
                    {
                        RestaurarEstadoColisor(colisor);
                    }
                    else
                    {
                        colisor.enabled = false;
                    }
                }
            }
            else
            {
                no.gameObject.SetActive(visivel);
            }
        }

        /// <summary>
        /// Registra preguiçosamente colliders adicionados depois de Awake antes de alterar seu estado.
        /// </summary>
        private void CapturarColisorEstadoSeNecessario(Collider colisor)
        {
            if (colisor != null && !estadosColisoresOriginais.ContainsKey(colisor))
            {
                estadosColisoresOriginais[colisor] = colisor.enabled;
            }
        }

        /// <summary>
        /// Reativa somente colliders que estavam habilitados quando a visibilidade original foi capturada.
        /// Colliders criados depois de Awake são registrados antes da primeira alteração.
        /// </summary>
        private void RestaurarEstadoColisor(Collider colisor)
        {
            if (colisor == null)
            {
                return;
            }

            CapturarColisorEstadoSeNecessario(colisor);
            colisor.enabled = estadosColisoresOriginais[colisor];
        }
    }
}