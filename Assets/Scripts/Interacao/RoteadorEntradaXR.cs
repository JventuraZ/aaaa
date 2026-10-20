using UnityEngine;
using UnityEngine.InputSystem;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Roteador de Entrada XR do SNC
    // Nome técnico do script: RoteadorEntradaXR
    // Função: conecta ações do Input System a seleção, grab, reset, visibilidade, isolamento, rotação e zoom.
    // Relação com o protótipo: preserva a lógica atual de trigger, grip e botões sem trocar XR Ray Interactor
    // ou XR Grab Interactable; também fornece fallbacks de teclado no editor.
    // Objetos da cena: usado no Sistema_Nervoso_Central.
    /// <summary>
    /// Roteia ações XR genéricas para os controladores do SNC.
    /// O script não decide regras anatômicas; ele apenas chama os componentes responsáveis.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainXRInputRouter")]
    public sealed class RoteadorEntradaXR : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("selecionarAcao")]
        [Header("Ações de Entrada")]
        // Ações do Input System ligadas no Inspector quando este roteador é usado diretamente.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectAction")]
        private InputActionReference acaoSelecionar;
        [UnityEngine.Serialization.FormerlySerializedAs("pincaSelecionarAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("pinchSelectAction")]
        private InputActionReference acaoSelecionarPorPinca;
        [UnityEngine.Serialization.FormerlySerializedAs("agarreAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("grabAction")]
        private InputActionReference acaoAgarrar;
        [UnityEngine.Serialization.FormerlySerializedAs("redefinirAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("resetAction")]
        private InputActionReference acaoRestaurar;
        [UnityEngine.Serialization.FormerlySerializedAs("voltarAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("backAction")]
        private InputActionReference acaoVoltar;
        [UnityEngine.Serialization.FormerlySerializedAs("limparSelecaoAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("clearSelectionAction")]
        private InputActionReference acaoLimparSelecao;
        [UnityEngine.Serialization.FormerlySerializedAs("expandirAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("explodeAction")]
        private InputActionReference acaoExpandir;
        [UnityEngine.Serialization.FormerlySerializedAs("reagruparAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("reassembleAction")]
        private InputActionReference acaoReagrupar;
        [UnityEngine.Serialization.FormerlySerializedAs("girarAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rotateAction")]
        private InputActionReference acaoGirar;
        [UnityEngine.Serialization.FormerlySerializedAs("zoomAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("zoomAction")]
        private InputActionReference acaoAproximar;
        [UnityEngine.Serialization.FormerlySerializedAs("alternarIsolamentoAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("toggleIsolationAction")]
        private InputActionReference acaoAlternarIsolamento;
        [UnityEngine.Serialization.FormerlySerializedAs("alternarVisibilidadeAcao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("toggleVisibilityAction")]
        private InputActionReference acaoAlternarVisibilidade;
        [Header("Alvos")]
        // Transform usado como referência física para grab.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("grabber")]
        private Transform agarrador;
        // Controladores chamados pelas ações.
        [UnityEngine.Serialization.FormerlySerializedAs("raioselecaocontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("raySelectionController")]
        private ControladorSelecaoPorRaio controladorSelecaoPorRaio;
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [UnityEngine.Serialization.FormerlySerializedAs("modelomanipulador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("modelManipulator")]
        private ManipuladorModeloAnatomico manipuladorModelo;
        [UnityEngine.Serialization.FormerlySerializedAs("redefinirgerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("resetManager")]
        private GerenciadorRestauracao gerenciadorRestauracao;
        [UnityEngine.Serialization.FormerlySerializedAs("expandidoVisaoControlador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("explodedViewController")]
        private ControladorVisaoExplodida controladorVisaoExplodida;
        [UnityEngine.Serialization.FormerlySerializedAs("visibilidadecontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("visibilityController")]
        private ControladorVisibilidadeAnatomica controladorVisibilidade;
        [Header("Alternativas do Editor")]
        // Permite testar no editor com teclado/mouse sem headset.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("enableEditorKeyboardFallbacks")]
        private bool habilitarEditorTecladoAlternativas = true;
        // Indica se o grip/gesto de grab está ativo.
        private bool agarrando;
        /// <summary>
        /// Resolve referências locais quando não estão preenchidas no Inspector.
        /// </summary>
        private void Awake()
        {
            controladorSelecaoPorRaio ??= GetComponent<ControladorSelecaoPorRaio>();
            gerenciadorSelecao ??= GetComponent<GerenciadorSelecaoHierarquica>();
            manipuladorModelo ??= GetComponent<ManipuladorModeloAnatomico>();
            gerenciadorRestauracao ??= GetComponent<GerenciadorRestauracao>();
            controladorVisaoExplodida ??= GetComponent<ControladorVisaoExplodida>();
            controladorVisibilidade ??= GetComponent<ControladorVisibilidadeAnatomica>();
        }

        /// <summary>
        /// Habilita ações e registra callbacks.
        /// </summary>
        private void OnEnable()
        {
            HabilitarAcao(acaoSelecionar, EmSelecionarExecutado);
            HabilitarAcao(acaoSelecionarPorPinca, EmPincaSelecionarExecutado);
            HabilitarAcao(acaoRestaurar, EmRedefinirExecutado);
            HabilitarAcao(acaoVoltar, EmVoltarExecutado);
            HabilitarAcao(acaoLimparSelecao, EmLimparSelecaoExecutado);
            HabilitarAcao(acaoExpandir, EmExpandirExecutado);
            HabilitarAcao(acaoReagrupar, EmReagruparExecutado);
            HabilitarAcao(acaoAlternarIsolamento, EmAlternarIsolamentoExecutado);
            HabilitarAcao(acaoAlternarVisibilidade, EmAlternarVisibilidadeExecutado);
            if (acaoAgarrar != null && acaoAgarrar.action != null)
            {
                acaoAgarrar.action.Enable();
                acaoAgarrar.action.performed += EmAgarrarExecutado;
                acaoAgarrar.action.canceled += EmAgarrarCancelado;
            }

            acaoGirar?.action?.Enable();
            acaoAproximar?.action?.Enable();
        }

        /// <summary>
        /// Remove callbacks para evitar chamadas duplicadas ao reabilitar o objeto.
        /// </summary>
        private void OnDisable()
        {
            DesabilitarAcao(acaoSelecionar, EmSelecionarExecutado);
            DesabilitarAcao(acaoSelecionarPorPinca, EmPincaSelecionarExecutado);
            DesabilitarAcao(acaoRestaurar, EmRedefinirExecutado);
            DesabilitarAcao(acaoVoltar, EmVoltarExecutado);
            DesabilitarAcao(acaoLimparSelecao, EmLimparSelecaoExecutado);
            DesabilitarAcao(acaoExpandir, EmExpandirExecutado);
            DesabilitarAcao(acaoReagrupar, EmReagruparExecutado);
            DesabilitarAcao(acaoAlternarIsolamento, EmAlternarIsolamentoExecutado);
            DesabilitarAcao(acaoAlternarVisibilidade, EmAlternarVisibilidadeExecutado);
            if (acaoAgarrar != null && acaoAgarrar.action != null)
            {
                acaoAgarrar.action.performed -= EmAgarrarExecutado;
                acaoAgarrar.action.canceled -= EmAgarrarCancelado;
            }
        }

        /// <summary>
        /// Lê entradas contínuas de rotação/zoom e atualiza grab em andamento.
        /// </summary>
        private void Update()
        {
            var girarEntrada = LerVetor2(acaoGirar);
            if (girarEntrada.sqrMagnitude > 0.0001f)
            {
                manipuladorModelo?.Girar(girarEntrada);
            }

            var entradaAproximacao = LerAproximacao(acaoAproximar);
            if (Mathf.Abs(entradaAproximacao) > 0.0001f)
            {
                manipuladorModelo?.AproximarOuAfastar(entradaAproximacao);
            }

            if (agarrando && agarrador != null)
            {
                manipuladorModelo?.AtualizarAgarre(agarrador);
            }

            ManipularEditorAlternativas();
        }

        /// <summary>
        /// Configura todas as referências usadas pelo roteador.
        /// </summary>
        public void Configurar(ControladorSelecaoPorRaio raio, GerenciadorSelecaoHierarquica selecao, ManipuladorModeloAnatomico manipulador, GerenciadorRestauracao redefinir, ControladorVisaoExplodida expandido, ControladorVisibilidadeAnatomica visibilidade, Transform padraoAgarrador)
        {
            controladorSelecaoPorRaio = raio;
            gerenciadorSelecao = selecao;
            manipuladorModelo = manipulador;
            gerenciadorRestauracao = redefinir;
            controladorVisaoExplodida = expandido;
            controladorVisibilidade = visibilidade;
            agarrador = padraoAgarrador;
        }

        /// <summary>
        /// Seleção por pinça/trigger: confirma a estrutura atualmente apontada.
        /// </summary>
        public void SelecionarPorPinca()
        {
            controladorSelecaoPorRaio?.SelecionarFocoAtual();
        }

        /// <summary>
        /// Reset por gesto de mão ou ação equivalente.
        /// </summary>
        public void RestaurarPorGestoDaPalma()
        {
            gerenciadorRestauracao?.RestaurarTudo();
        }

        /// <summary>
        /// Inicia grab por rastreamento de mão.
        /// </summary>
        public void IniciarAgarrePorMao(Transform mao)
        {
            agarrador = mao;
            agarrando = true;
            manipuladorModelo?.IniciarAgarre(agarrador);
        }

        /// <summary>
        /// Finaliza grab por rastreamento de mão.
        /// </summary>
        public void EncerrarAgarrePorMao()
        {
            agarrando = false;
            manipuladorModelo?.EncerrarAgarre();
        }

        // Callbacks das ações discretas. Cada um só encaminha para o controlador responsável.
        private void EmSelecionarExecutado(InputAction.CallbackContext _) => controladorSelecaoPorRaio?.SelecionarFocoAtual();
        private void EmPincaSelecionarExecutado(InputAction.CallbackContext _) => SelecionarPorPinca();
        private void EmRedefinirExecutado(InputAction.CallbackContext _) => gerenciadorRestauracao?.RestaurarTudo();
        private void EmVoltarExecutado(InputAction.CallbackContext _) => gerenciadorSelecao?.VoltarAoNoPai();
        private void EmLimparSelecaoExecutado(InputAction.CallbackContext _) => gerenciadorSelecao?.LimparSelecao();
        private void EmExpandirExecutado(InputAction.CallbackContext _) => controladorVisaoExplodida?.AlternarExpansaoNivelAtual();
        private void EmReagruparExecutado(InputAction.CallbackContext _) => controladorVisaoExplodida?.ReagruparNivelAtual();
        private void EmAlternarIsolamentoExecutado(InputAction.CallbackContext _) => gerenciadorSelecao?.AlternarIsolamento();
        private void EmAlternarVisibilidadeExecutado(InputAction.CallbackContext _) => controladorVisibilidade?.AlternarVisibilidadeNo(gerenciadorSelecao?.NoSelecionado);
        /// <summary>
        /// Inicia grab pelo grip, usando o grabber configurado ou o próprio objeto como fallback.
        /// </summary>
        private void EmAgarrarExecutado(InputAction.CallbackContext _)
        {
            agarrando = true;
            manipuladorModelo?.IniciarAgarre(agarrador != null ? agarrador : transform);
        }

        /// <summary>
        /// Encerra grab quando o grip é solto.
        /// </summary>
        private void EmAgarrarCancelado(InputAction.CallbackContext _)
        {
            agarrando = false;
            manipuladorModelo?.EncerrarAgarre();
        }

        /// <summary>
        /// Habilita uma ação de botão e registra callback.
        /// </summary>
        private static void HabilitarAcao(InputActionReference referenciaAcao, System.Action<InputAction.CallbackContext> aoExecutar)
        {
            if (referenciaAcao == null || referenciaAcao.action == null)
            {
                return;
            }

            referenciaAcao.action.Enable();
            referenciaAcao.action.performed += aoExecutar;
        }

        /// <summary>
        /// Remove callback de uma ação de botão.
        /// </summary>
        private static void DesabilitarAcao(InputActionReference referenciaAcao, System.Action<InputAction.CallbackContext> aoExecutar)
        {
            if (referenciaAcao == null || referenciaAcao.action == null)
            {
                return;
            }

            referenciaAcao.action.performed -= aoExecutar;
        }

        /// <summary>
        /// Lê um Vector2 de forma defensiva para suportar ações não configuradas no editor.
        /// </summary>
        private static Vector2 LerVetor2(InputActionReference referenciaAcao)
        {
            if (referenciaAcao == null || referenciaAcao.action == null)
            {
                return Vector2.zero;
            }

            try
            {
                return referenciaAcao.action.ReadValue<Vector2>();
            }
            catch
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Lê zoom como Vector2.y ou float, mantendo compatibilidade com diferentes bindings.
        /// </summary>
        private static float LerAproximacao(InputActionReference referenciaAcao)
        {
            if (referenciaAcao == null || referenciaAcao.action == null)
            {
                return 0f;
            }

            try
            {
                var vetor = referenciaAcao.action.ReadValue<Vector2>();
                return vetor.y;
            }
            catch
            {
                try
                {
                    return referenciaAcao.action.ReadValue<float>();
                }
                catch
                {
                    return 0f;
                }
            }
        }

        /// <summary>
        /// Atalhos de editor para testar reset, navegação, seleção e visibilidade sem headset.
        /// </summary>
        private void ManipularEditorAlternativas()
        {
#if UNITY_EDITOR
            // O mapeador dedicado tem prioridade. Se for desativado, o roteador
            // genérico assume os atalhos sem alterar a configuração da cena.
            var mapeador = GetComponent<MapeadorEntradaMetaQuest2>();
            if (!DeveProcessarAtalhos(habilitarEditorTecladoAlternativas, mapeador != null && mapeador.ProcessaAtalhosDoEditor) || Keyboard.current == null || MapeadorEntradaMetaQuest2.SimuladorXRAtivo)
            {
                return;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
                gerenciadorRestauracao?.RestaurarTudo();
            if (Keyboard.current.qKey.wasPressedThisFrame)
                gerenciadorSelecao?.VoltarAoNoPai();
            if (Keyboard.current.eKey.wasPressedThisFrame)
                controladorVisaoExplodida?.AlternarExpansaoNivelAtual();
            if (Keyboard.current.cKey.wasPressedThisFrame)
                gerenciadorSelecao?.LimparSelecao();
            if (Keyboard.current.iKey.wasPressedThisFrame)
                gerenciadorSelecao?.AlternarIsolamento();
            if (Keyboard.current.vKey.wasPressedThisFrame)
                controladorVisibilidade?.AlternarNoOuRestaurar(gerenciadorSelecao?.NoSelecionado);
            if (Keyboard.current.enterKey.wasPressedThisFrame)
                controladorSelecaoPorRaio?.SelecionarFocoAtual();
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                controladorSelecaoPorRaio?.SelecionarFocoAtual();
            }
#endif
        }

        public static bool DeveProcessarAtalhos(bool roteadorHabilitado, bool mapeadorProcessando)
            => roteadorHabilitado && !mapeadorProcessando;
    }
}
