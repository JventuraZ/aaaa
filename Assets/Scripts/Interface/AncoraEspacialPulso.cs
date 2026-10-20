using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;

namespace VRSNC.Interface
{
    /// <summary>
    /// Possui a pose do menu de pulso copiado. O componente legado de acompanhamento HandMenu é desativado apenas
    /// nesta instância copiada, para somente este componente gravar sua pose enquanto acoplado.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.UI", "Assembly-CSharp", "BrainWristSpatialAnchor")]
    public sealed class AncoraEspacialPulso : MonoBehaviour
    {
        public enum EstadoPostura
        {
            Acoplado,
            Agarrado,
            FixadoNoMundo
        }

        [Header("Pose de Ancoragem (metros no mundo)")]
        [SerializeField, Range(0.12f, 0.20f)]
        [UnityEngine.Serialization.FormerlySerializedAs("dorsalOffsetMeters")]
        private float dorsalDeslocamentoMetros = 0.16f;
        [UnityEngine.Serialization.FormerlySerializedAs("posicaoSuavizarTempo")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("positionSmoothTime")]
        private float tempoSuavizacaoPosicao = 0.09f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximoLinearVelocidade")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("maximumLinearSpeed")]
        private float velocidadeLinearMaxima = 2.5f;
        [UnityEngine.Serialization.FormerlySerializedAs("angularSuavizacao")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("angularSmoothing")]
        private float suavizacaoAngular = 12f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximoAngularVelocidade")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("maximumAngularSpeed")]
        private float velocidadeAngularMaxima = 540f;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdapulso")]
        [Header("Referências do Rig Rastreado")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftWrist")]
        private Transform pulsoEsquerdo;
        [UnityEngine.Serialization.FormerlySerializedAs("direitapulso")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightWrist")]
        private Transform pulsoDireito;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdacontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftController")]
        private Transform controladorEsquerdo;
        [UnityEngine.Serialization.FormerlySerializedAs("direitacontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightController")]
        private Transform controladorDireito;
        [UnityEngine.Serialization.FormerlySerializedAs("hMD")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("hmd")]
        private Transform dispositivoVR;
        [UnityEngine.Serialization.FormerlySerializedAs("entradamodalidadegerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("inputModalityManager")]
        private XRInputModalityManager gerenciadorModalidadeEntrada;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("dockAnchor")]
        private Transform acoplarAncora;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("legacyPoseDriver")]
        private Behaviour legadoPosturaControlador;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("headerGrab")]
        private XRGrabInteractable cabecalhoAgarre;
        [Header("Visibilidade ao virar o pulso")]
        // Acoplado, o oráculo só aparece quando o pulso (ou o controle) se volta para o rosto, como um relógio.
        [SerializeField]
        private bool mostrarSoAoVirarPulso = true;
        // Mesma regra do Hand Menu do XRI: o eixo de referência e o ângulo vêm destes presets.
        [SerializeField]
        private FollowPresetDatum predefinicaoMao;
        [SerializeField]
        private FollowPresetDatum predefinicaoControle;
        // O pulso também precisa estar perto do centro do olhar.
        [SerializeField, Range(20f, 90f)]
        private float anguloOlharPulsoGraus = 50f;
        [SerializeField, Min(1f)]
        private float velocidadeTransicao = 8f;
        [SerializeField]
        private CanvasGroup grupoConteudo;
        private bool visivelAoVirarPulso;
        private float ultimoRequisitoAtendido = float.NegativeInfinity;
        private ZonaProximidadePainelInterface[] zonasProximidade = new ZonaProximidadePainelInterface[0];
        public bool EstaVisivel => grupoConteudo == null || grupoConteudo.alpha > 0.5f;
        private static readonly List<XRHandSubsystem> MaoSubsistemas = new();
        private XRHandSubsystem maoSubsistema;
        private float tempoProximaBuscaSubsistemaMao;
        private Vector3 velocidadePosicao;
        private EstadoPostura estado = EstadoPostura.Acoplado;
        private int quantidadeAgarres;
        public EstadoPostura Estado => estado;
        public bool EstaAgarrado => estado == EstadoPostura.Agarrado;
        /// <summary>
        /// Configura âncoras de mão rastreada. O fallback de controle permanece desativado até o gerenciador
        /// de modalidade e suas raízes de controle serem fornecidos pela adaptação da cena.
        /// </summary>
        public void Configurar(Transform esquerda, Transform direito, Transform dispositivoVR, Behaviour legadoControlador, XRGrabInteractable agarrar, XRInputModalityManager modalidade = null)
        {
            DesconectarAgarre();
            pulsoEsquerdo = esquerda;
            pulsoDireito = direito;
            this.dispositivoVR = dispositivoVR;
            legadoPosturaControlador = legadoControlador;
            cabecalhoAgarre = agarrar;
            gerenciadorModalidadeEntrada = modalidade != null ? modalidade : gerenciadorModalidadeEntrada;
            ResolverControladorRaizes();
            ResolverAcoplarAncora();
            DesabilitarLegadoControlador();
            ConectarAgarre();
            RestaurarEstadoAgarre();
        }

        private void Awake()
        {
            ResolverReferencias();
            DesabilitarLegadoControlador();
            ConectarAgarre();
            RestaurarEstadoAgarre();
            if (grupoConteudo == null)
                grupoConteudo = GetComponentInChildren<CanvasGroup>(true);
            zonasProximidade = GetComponentsInChildren<ZonaProximidadePainelInterface>(true);
        }

        private void OnEnable()
        {
            ResolverReferencias();
            DesabilitarLegadoControlador();
            ConectarAgarre();
            RestaurarEstadoAgarre();
        }

        private void OnDisable()
        {
            DesconectarAgarre();
            velocidadePosicao = Vector3.zero;
            quantidadeAgarres = 0;
        }

        private void LateUpdate()
        {
            AtualizarVisibilidade();
            if (estado != EstadoPostura.Acoplado || quantidadeAgarres > 0)
                return;
            if (!TentarObterRastreadoAcoplarAncora(out var rastreadoAncora, out var ehEsquerda))
                return; // Keep the last valid pose during tracking loss; never snap to an origin.
            var lateraisMetros = ehEsquerda ? -0.035f : 0.035f;
            // O deslocamento é apenas rotacionado, nunca escalado por TransformPoint. Sua magnitude permanece 0,12-0,20 m.
            var localDeslocamento = new Vector3(lateraisMetros, dorsalDeslocamentoMetros, 0f);
            var posicaoDesejada = rastreadoAncora.position + rastreadoAncora.rotation * localDeslocamento;
            transform.position = Vector3.SmoothDamp(transform.position, posicaoDesejada, ref velocidadePosicao, tempoSuavizacaoPosicao, velocidadeLinearMaxima, Time.deltaTime);
            var camera = dispositivoVR != null ? dispositivoVR : Camera.main != null ? Camera.main.transform : null;
            if (camera == null)
                return;
            var paraCamera = camera.position - transform.position;
            if (paraCamera.sqrMagnitude < 0.0001f)
                return;
            var direcaoCimaEstabilizada = Vector3.ProjectOnPlane(camera.up, paraCamera);
            if (direcaoCimaEstabilizada.sqrMagnitude < 0.0001f)
                direcaoCimaEstabilizada = Vector3.ProjectOnPlane(Vector3.up, paraCamera);
            if (direcaoCimaEstabilizada.sqrMagnitude < 0.0001f)
                return;
            // Este canvas renderiza sua face legível no -Z local; portanto, transform +Z aponta para longe
            // do HMD. A direção 3D preserva intencionalmente a legibilidade acima/abaixo da cabeça.
            var rotacaoDesejada = Quaternion.LookRotation(-paraCamera.normalized, direcaoCimaEstabilizada.normalized);
            var suavizacao = 1f - Mathf.Exp(-suavizacaoAngular * Time.deltaTime);
            var rotacaoSuavizada = Quaternion.Slerp(transform.rotation, rotacaoDesejada, suavizacao);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotacaoSuavizada, velocidadeAngularMaxima * Time.deltaTime);
        }

        /// <summary>
        /// Aparece e some em fade. Escondido, não recebe cliques, não pode ser agarrado e não bloqueia o raio.
        /// Agarrado ou solto no mundo, fica sempre visível.
        /// </summary>
        private void AtualizarVisibilidade()
        {
            if (!Application.isPlaying || grupoConteudo == null)
                return;
            var mostrar = !mostrarSoAoVirarPulso || estado != EstadoPostura.Acoplado || quantidadeAgarres > 0 || PulsoVoltadoParaRosto();
            grupoConteudo.alpha = Mathf.MoveTowards(grupoConteudo.alpha, mostrar ? 1f : 0f, velocidadeTransicao * Time.unscaledDeltaTime);
            var interativo = grupoConteudo.alpha > 0.5f;
            if (grupoConteudo.interactable == interativo)
                return;
            grupoConteudo.interactable = interativo;
            grupoConteudo.blocksRaycasts = interativo;
            if (cabecalhoAgarre != null && quantidadeAgarres == 0)
                cabecalhoAgarre.enabled = interativo;
            foreach (var zona in zonasProximidade)
            {
                if (zona != null)
                    zona.enabled = interativo;
            }
        }

        private bool PulsoVoltadoParaRosto()
        {
            var camera = dispositivoVR != null ? dispositivoVR : Camera.main != null ? Camera.main.transform : null;
            if (camera == null || !TentarObterRastreadoAcoplarAncora(out var ancora, out var ehEsquerda))
            {
                visivelAoVirarPulso = false;
                return false;
            }

            var ehMao = ancora == pulsoEsquerdo || ancora == pulsoDireito;
            var datum = ehMao ? predefinicaoMao : predefinicaoControle;
            var predefinicao = datum != null ? datum.Value : null;
            var voltado = true;
            if (predefinicao != null && predefinicao.requirePalmFacingUser)
            {
                predefinicao.ComputeDotProductThresholds();
                var eixo = predefinicao.GetReferenceAxisForTrackingAnchor(ancora, !ehEsquerda);
                // Folga para esconder, como no Hand Menu: depois de aparecer, só some quando sai bem do ângulo.
                var limite = visivelAoVirarPulso ? predefinicao.palmFacingUserDotThreshold - (1f - predefinicao.palmFacingUserHideMenuDotThreshold) : predefinicao.palmFacingUserDotThreshold;
                voltado = Vector3.Dot(eixo, -camera.forward) > limite;
            }

            var olhando = Vector3.Angle(camera.forward, ancora.position - camera.position) <= anguloOlharPulsoGraus + (visivelAoVirarPulso ? 10f : 0f);
            if (voltado && olhando)
                ultimoRequisitoAtendido = Time.unscaledTime;
            var atraso = predefinicao != null ? predefinicao.hideDelaySeconds : 0.15f;
            visivelAoVirarPulso = Time.unscaledTime - ultimoRequisitoAtendido <= atraso;
            return visivelAoVirarPulso;
        }

        public void AcoplarAoPulso()
        {
            if (quantidadeAgarres > 0)
                return;
            acoplarAncora = pulsoEsquerdo != null ? pulsoEsquerdo : pulsoDireito;
            DefinirAcoplamento();
        }

        public void AcoplarAoPulso(Transform pulso)
        {
            if (quantidadeAgarres > 0 || pulso == null)
                return;
            acoplarAncora = pulso;
            DefinirAcoplamento();
        }

        public void Desacoplar()
        {
            if (quantidadeAgarres > 0)
                return;
            estado = EstadoPostura.FixadoNoMundo;
            velocidadePosicao = Vector3.zero;
        }

        private void ConectarAgarre()
        {
            if (cabecalhoAgarre == null)
                return;
            cabecalhoAgarre.selectEntered.RemoveListener(EmAgarrado);
            cabecalhoAgarre.selectExited.RemoveListener(EmSolto);
            cabecalhoAgarre.selectEntered.AddListener(EmAgarrado);
            cabecalhoAgarre.selectExited.AddListener(EmSolto);
        }

        private void DesconectarAgarre()
        {
            if (cabecalhoAgarre == null)
                return;
            cabecalhoAgarre.selectEntered.RemoveListener(EmAgarrado);
            cabecalhoAgarre.selectExited.RemoveListener(EmSolto);
        }

        private void EmAgarrado(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
        {
            RestaurarEstadoAgarre();
            if (quantidadeAgarres == 0)
                quantidadeAgarres = 1;
            estado = EstadoPostura.Agarrado;
            velocidadePosicao = Vector3.zero;
            DesabilitarLegadoControlador();
        }

        private void EmSolto(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs _)
        {
            RestaurarEstadoAgarre();
            if (quantidadeAgarres == 0)
            {
                estado = EstadoPostura.FixadoNoMundo;
                velocidadePosicao = Vector3.zero;
            }
        }

        private void RestaurarEstadoAgarre()
        {
            quantidadeAgarres = cabecalhoAgarre != null ? cabecalhoAgarre.interactorsSelecting.Count : 0;
            if (quantidadeAgarres > 0)
            {
                estado = EstadoPostura.Agarrado;
                velocidadePosicao = Vector3.zero;
            }
            else if (estado == EstadoPostura.Agarrado)
            {
                // Uma desativação/reativação pode ocorrer após o XRI liberar sua lista de seleção. Não
                // deixa a âncora permanentemente em Grabbed, mas preserva um estado Docked intencional.
                estado = EstadoPostura.FixadoNoMundo;
                velocidadePosicao = Vector3.zero;
            }
        }

        private bool TentarObterRastreadoAcoplarAncora(out Transform rastreadoAncora, out bool ehEsquerda)
        {
            ehEsquerda = acoplarAncora == null || acoplarAncora == pulsoEsquerdo || acoplarAncora == controladorEsquerdo;
            if (ehEsquerda)
            {
                if (EstaMaoRastreada(true) && pulsoEsquerdo != null)
                {
                    rastreadoAncora = pulsoEsquerdo;
                    return true;
                }

                if (ModoControladorAtivo(true) && controladorEsquerdo != null)
                {
                    rastreadoAncora = controladorEsquerdo;
                    return true;
                }
            }
            else
            {
                if (EstaMaoRastreada(false) && pulsoDireito != null)
                {
                    rastreadoAncora = pulsoDireito;
                    return true;
                }

                if (ModoControladorAtivo(false) && controladorDireito != null)
                {
                    rastreadoAncora = controladorDireito;
                    return true;
                }
            }

            rastreadoAncora = null;
            return false;
        }

        private bool EstaMaoRastreada(bool ehEsquerda)
        {
            ResolverSubsistemaMao();
            return maoSubsistema != null && maoSubsistema.running && (ehEsquerda ? maoSubsistema.leftHand.isTracked : maoSubsistema.rightHand.isTracked);
        }

        private bool ModoControladorAtivo(bool ehEsquerda)
        {
            if (gerenciadorModalidadeEntrada == null)
                return false;
            // XRInputModalityManager possui estes grupos: um grupo de controle ativo significa que sua
            // modalidade MotionController foi selecionada, não apenas que existe um GameObject de pose.
            var controladorGrupo = ehEsquerda ? gerenciadorModalidadeEntrada.leftController : gerenciadorModalidadeEntrada.rightController;
            return controladorGrupo != null && controladorGrupo.activeInHierarchy;
        }

        private void ResolverSubsistemaMao()
        {
            if (maoSubsistema != null && maoSubsistema.running)
                return;
            if (Time.unscaledTime < tempoProximaBuscaSubsistemaMao)
                return;
            tempoProximaBuscaSubsistemaMao = Time.unscaledTime + 1f;
            MaoSubsistemas.Clear();
            SubsystemManager.GetSubsystems(MaoSubsistemas);
            maoSubsistema = null;
            for (var i = 0; i < MaoSubsistemas.Count; i++)
            {
                if (MaoSubsistemas[i] != null && MaoSubsistemas[i].running)
                {
                    maoSubsistema = MaoSubsistemas[i];
                    return;
                }
            }
        }

        private void ResolverReferencias()
        {
            if (gerenciadorModalidadeEntrada == null)
                gerenciadorModalidadeEntrada = FindFirstObjectByType<XRInputModalityManager>();
            ResolverControladorRaizes();
            ResolverAcoplarAncora();
        }

        private void ResolverControladorRaizes()
        {
            if (gerenciadorModalidadeEntrada == null)
                return;
            if (controladorEsquerdo == null && gerenciadorModalidadeEntrada.leftController != null)
                controladorEsquerdo = gerenciadorModalidadeEntrada.leftController.transform;
            if (controladorDireito == null && gerenciadorModalidadeEntrada.rightController != null)
                controladorDireito = gerenciadorModalidadeEntrada.rightController.transform;
        }

        private void DesabilitarLegadoControlador()
        {
            if (legadoPosturaControlador != null && legadoPosturaControlador != this)
                legadoPosturaControlador.enabled = false;
        }

        private void ResolverAcoplarAncora()
        {
            if (acoplarAncora == null)
                acoplarAncora = pulsoEsquerdo != null ? pulsoEsquerdo : pulsoDireito;
        }

        private void DefinirAcoplamento()
        {
            if (acoplarAncora == null)
                return;
            estado = EstadoPostura.Acoplado;
            velocidadePosicao = Vector3.zero;
            DesabilitarLegadoControlador();
        }
    }
}
