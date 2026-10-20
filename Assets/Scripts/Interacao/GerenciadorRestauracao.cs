using UnityEngine;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Gerenciador de Reset do SNC
    // Nome técnico do script: GerenciadorRestauracao
    // Função: restaura seleção, foco, visibilidade, vista explodida, grab e transformações para o estado inicial.
    // Relação com o protótipo: garante que o Brain original permaneça desativado e que a organização anatômica ativa volte
    // ao estado funcional sem recriar hierarquia, colliders ou componentes.
    // Objetos da cena: usado no Sistema_Nervoso_Central e acionado pelo botão Resetar SNC do painel e pelo botão Y do controle esquerdo.
    /// <summary>
    /// Centraliza o reset seguro do protótipo.
    /// O reset chama os controladores existentes e restaura o estado capturado, sem remover scripts ou objetos.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainResetManager")]
    public sealed class GerenciadorRestauracao : MonoBehaviour
    {
        // Modelo original importado, preservado desativado durante a experiência.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("originalBrain")]
        private GameObject original;
        // Raiz anatômica ativa do SNC.
        [UnityEngine.Serialization.FormerlySerializedAs("anatomicoraiz")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("anatomicalRoot")]
        private GameObject raizAnatomica;
        // Nó raiz usado para percorrer todos os nós filhos durante o reset.
        [UnityEngine.Serialization.FormerlySerializedAs("raizno")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rootNode")]
        private NoInteracaoAnatomica noRaiz;
        // Controladores restaurados em ordem para manter seleção, painel e visibilidade consistentes.
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [UnityEngine.Serialization.FormerlySerializedAs("modelomanipulador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("modelManipulator")]
        private ManipuladorModeloAnatomico manipuladorModelo;
        [UnityEngine.Serialization.FormerlySerializedAs("expandidoVisaoControlador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("explodedViewController")]
        private ControladorVisaoExplodida controladorVisaoExplodida;
        [UnityEngine.Serialization.FormerlySerializedAs("visibilidadecontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("visibilityController")]
        private ControladorVisibilidadeAnatomica controladorVisibilidade;
        [UnityEngine.Serialization.FormerlySerializedAs("raioselecaocontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("raySelectionController")]
        private ControladorSelecaoPorRaio controladorSelecaoPorRaio;
        [UnityEngine.Serialization.FormerlySerializedAs("selecionadoobjetomanipulador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectedObjectManipulator")]
        private ManipuladorObjetoSelecionado manipuladorObjetoSelecionado;
        /// <summary>
        /// Resolve referências locais e captura estado original dos nós anatômicos.
        /// </summary>
        private void Awake()
        {
            if (raizAnatomica == null)
            {
                raizAnatomica = gameObject;
            }

            if (noRaiz == null)
            {
                noRaiz = GetComponent<NoInteracaoAnatomica>();
            }

            gerenciadorSelecao ??= GetComponent<GerenciadorSelecaoHierarquica>();
            manipuladorModelo ??= GetComponent<ManipuladorModeloAnatomico>();
            controladorVisaoExplodida ??= GetComponent<ControladorVisaoExplodida>();
            controladorVisibilidade ??= GetComponent<ControladorVisibilidadeAnatomica>();
            controladorSelecaoPorRaio ??= GetComponent<ControladorSelecaoPorRaio>();
            manipuladorObjetoSelecionado ??= GetComponent<ManipuladorObjetoSelecionado>();
            foreach (var no in GetComponentsInChildren<NoInteracaoAnatomica>(true))
            {
                no.CapturarEstadoOriginal();
            }
        }

        /// <summary>
        /// Configura o reset a partir da ferramenta/editor sem trocar arquitetura da cena.
        /// </summary>
        public void Configurar(GameObject cerebro, GameObject raiz, NoInteracaoAnatomica interacaoRaiz, GerenciadorSelecaoHierarquica selecao, ManipuladorModeloAnatomico manipulador, ControladorVisaoExplodida expandido, ControladorVisibilidadeAnatomica visibilidade, ControladorSelecaoPorRaio raioControlador)
        {
            original = cerebro;
            raizAnatomica = raiz;
            noRaiz = interacaoRaiz;
            gerenciadorSelecao = selecao;
            manipuladorModelo = manipulador;
            controladorVisaoExplodida = expandido;
            controladorVisibilidade = visibilidade;
            controladorSelecaoPorRaio = raioControlador;
        }

        /// <summary>
        /// Executa reset completo do protótipo preservando objetos e componentes existentes.
        /// </summary>
        /// <summary>Avisados no início e no fim do reset geral; o retorno sensorial silencia os passos intermediários.</summary>
        public event System.Action emRestaurando;
        public event System.Action emRestaurado;

        public void RestaurarTudo()
        {
            emRestaurando?.Invoke();
            // Primeiro cancela os proprietários dos gestos: nenhuma atualização
            // tardia pode reaplicar a expansão ou o agarre depois do reset.
            GetComponent<PonteInteracaoMao>()?.Soltar();
            GetComponent<CoordenadorGestoAnatomico>()?.CancelarERestaurar();
            GetComponent<ControladorExpansaoGlobal>()?.CancelarERestaurar();
            GetComponent<MapeadorEntradaMetaQuest2>()?.EncerrarAgarreDireito();
            GetComponent<RoteadorEntradaXR>()?.EncerrarAgarrePorMao();
            // O Brain original deve continuar desativado para não concorrer com a organização anatômica ativa.
            if (original != null)
            {
                original.SetActive(false);
            }

            // A raiz anatômica organizada permanece ativa após qualquer reset.
            if (raizAnatomica != null)
            {
                raizAnatomica.SetActive(true);
            }

            // Antes de restaurar posições, encerra modos temporários como vista explodida e grab.
            controladorVisaoExplodida?.RestaurarVisaoExplodida();
            manipuladorObjetoSelecionado?.EncerrarAgarre();
            // Cada nó volta ao estado capturado, sem reconstruir a hierarquia anatômica.
            if (noRaiz != null)
            {
                foreach (var no in noRaiz.GetComponentsInChildren<NoInteracaoAnatomica>(true))
                {
                    no.RestaurarEstadoOriginal();
                }
            }

            // Reaplica estado visual e seleção limpa para que painel, hover e foco fiquem sincronizados.
            manipuladorModelo?.RedefinirTransformacao();
            controladorVisibilidade?.RestaurarVisibilidade();
            gerenciadorSelecao?.RedefinirRealces();
            gerenciadorSelecao?.LimparSelecao();
            gerenciadorSelecao?.RedefinirFoco();
            controladorSelecaoPorRaio?.LimparFoco();
            emRestaurado?.Invoke();
        }
    }
}
