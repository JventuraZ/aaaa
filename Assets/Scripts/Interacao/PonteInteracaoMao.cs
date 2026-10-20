using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using VRSNC.Interface;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Conecta o rig de mãos XRI existente à seleção anatômica hierárquica, à captura com uma mão
    /// e ao coordenador opcional de separação de grupos com duas mãos.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainHandInteractionBridge")]
    public sealed class PonteInteracaoMao : MonoBehaviour
    {
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selection")]
        private GerenciadorSelecaoHierarquica selecao;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("manipulator")]
        private ManipuladorObjetoSelecionado manipulador;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdamao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftHand")]
        private Transform maoEsquerda;
        [UnityEngine.Serialization.FormerlySerializedAs("direitamao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightHand")]
        private Transform maoDireita;
        [UnityEngine.Serialization.FormerlySerializedAs("gestocoordenador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("gestureCoordinator")]
        private CoordenadorGestoAnatomico coordenadorGesto;
        [Header("Entrada com Mão Aberta")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("openHandDwellSeconds")]
        private float abrirMaoEsperaSegundos = 0.35f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximoAbrirDedoCurvatura")]
        [SerializeField, Range(0f, 1f)]
        [UnityEngine.Serialization.FormerlySerializedAs("maximumOpenFingerCurl")]
        private float curvaturaMaximaDedoAberto = 0.20f;
        [UnityEngine.Serialization.FormerlySerializedAs("estavelPosicaoTolerancia")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("stablePositionTolerance")]
        private float toleranciaPosicaoEstavel = 0.012f;
        [UnityEngine.Serialization.FormerlySerializedAs("estavelRotacaoTolerancia")]
        [SerializeField, Range(0f, 180f)]
        [UnityEngine.Serialization.FormerlySerializedAs("stableRotationTolerance")]
        private float toleranciaRotacaoEstavel = 5f;
        private ElementoInteragivelMao proprietario;
        private ElementoInteragivelMao secundarioResponsavel;
        private IXRSelectInteractor interatorAgarrando;
        private IXRSelectInteractor secundarioInterator;
        private IXRSelectInteractor aguardandoAbrirInterator;
        private Transform pegada;
        private Transform secundarioAgarre;
        private NoInteracaoAnatomica noAgarrado;
        private NoInteracaoAnatomica focoAgarre;
        private NoInteracaoAnatomica aguardandoAbrirNo;
        private uint versaoAgarre;
        private uint versaoProprietario;
        private uint secundarioResponsavelVersao;
        private Vector3 posicaoInicialPegada;
        private Quaternion rotacaoInicialPegada;
        private bool primarioMovido;
        // O movimento sozinho não deve impedir uma entrada deliberada de mão aberta após um
        // movimento com uma mão. Esta flag mantém a proteção separada após ação bimanual.
        private bool primarioUsadoNoAgarreSecundario;
        // Quando reset/foco/UI encerra a manipulação antes de o XRI emitir
        // SelectExited, mantemos o dono lógico para bloquear uma nova captura
        // até o mesmo pinch efetivamente sair.
        private bool soltarNecessario;
        private float abrirMaoIniciadoEm = -1f;
        private XRHandSubsystem maoSubsistema;
        private static readonly XRHandFingerID[] AbrirMaoDedos = { XRHandFingerID.Thumb, XRHandFingerID.Index, XRHandFingerID.Middle, XRHandFingerID.Ring, XRHandFingerID.Little, };
        // Mãos cujo raio está ocupado com algo que não é anatomia: um eletrodo do EEG apontado ou um gesto no ar em
        // curso. Enquanto durar a reserva, a anatomia não recebe foco nem pinça dessa mão. A reserva conta pedidos:
        // cada sistema libera só o que reservou.
        private static readonly Dictionary<Transform, int> maosReservadas = new();
        private static readonly List<Transform> raizesDescartadas = new();
        public bool EstaAgarrando => proprietario != null;

        /// <summary>Reserva (ou libera) o raio de uma mão rastreada para um alvo que não é anatomia.</summary>
        public static void DefinirMaoReservada(Transform raizMao, bool reservada)
        {
            if (raizMao == null)
                return;
            maosReservadas.TryGetValue(raizMao, out var pedidos);
            pedidos += reservada ? 1 : -1;
            if (pedidos > 0)
                maosReservadas[raizMao] = pedidos;
            else
                maosReservadas.Remove(raizMao);
        }

        public static bool MaoReservada(IXRInteractor interator) => interator != null && MaoReservada(interator.transform);

        public static bool MaoReservada(Transform transformacao)
        {
            if (transformacao == null)
                return false;
            var reservada = false;
            raizesDescartadas.Clear();
            foreach (var raiz in maosReservadas.Keys)
            {
                if (raiz == null)
                {
                    raizesDescartadas.Add(raiz);
                    continue;
                }

                if (transformacao == raiz || transformacao.IsChildOf(raiz))
                    reservada = true;
            }

            foreach (var raiz in raizesDescartadas)
                maosReservadas.Remove(raiz);
            return reservada;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RedefinirMaosReservadas() => maosReservadas.Clear();
        public bool EstaSeparandoComDuasMaos => secundarioResponsavel != null && coordenadorGesto != null && coordenadorGesto.EstaSeparando;
        public void Configurar(GerenciadorSelecaoHierarquica gerenciador, ManipuladorObjetoSelecionado manipuladorAlvo, Transform esquerda, Transform direito)
        {
            selecao = gerenciador;
            manipulador = manipuladorAlvo;
            maoEsquerda = esquerda;
            maoDireita = direito;
            coordenadorGesto ??= GetComponent<CoordenadorGestoAnatomico>();
        }

        private void Awake()
        {
            coordenadorGesto ??= GetComponent<CoordenadorGestoAnatomico>();
        }

        public bool EhInteratorDeMao(IXRInteractor interator)
        {
            if (interator == null || interator.transform == null || !interator.transform.gameObject.activeInHierarchy)
                return false;
            var transformacao = interator.transform;
            return (maoEsquerda != null && maoEsquerda.gameObject.activeInHierarchy && transformacao.IsChildOf(maoEsquerda)) || (maoDireita != null && maoDireita.gameObject.activeInHierarchy && transformacao.IsChildOf(maoDireita));
        }

        public NoInteracaoAnatomica Candidato(NoInteracaoAnatomica folha) => selecao != null ? selecao.ResolverCandidato(folha) : null;
        public bool PodeSelecionar(ElementoInteragivelMao fonte, IXRSelectInteractor interator)
        {
            if (!EstaInteratorMaoRastreado(interator) || selecao == null || manipulador == null)
                return false;
            // Owners retidos seguem válidos até o SelectExited físico. Isso evita
            // que apontar para UI durante retenção force um exited sintético antes
            // de o pinch realmente terminar; o LateUpdate já cancelou a manipulação.
            if (EhPrimario(fonte, interator) || EhSecundario(fonte, interator))
                return true;
            if (MaoReservada(interator))
                return false;
            if (EhInterfaceInteracaoAtivo(interator))
                return false;
            if (soltarNecessario)
                return false;
            if (BloqueiaNovaSelecaoAnatomicaPara(interator))
                return false;
            var candidato = Candidato(fonte != null ? fonte.No : null);
            if (proprietario == null)
                return !manipulador.EstaAgarrando && candidato != null;
            // Uma segunda mão rastreada oposta pode iniciar a separação global a partir de uma
            // folha anatômica exata diferente; as juntas físicas do pinch, não o anexo do raio, a conduzem.
            return secundarioResponsavel == null && interator != interatorAgarrando && EhMaoOposta(interator, interatorAgarrando) && candidato != null && coordenadorGesto != null;
        }

        public void Foco(NoInteracaoAnatomica folha)
        {
            if (proprietario == null && aguardandoAbrirNo == null)
                selecao?.DefinirNoSobFoco(Candidato(folha), folha);
        }

        public void LimparFoco(NoInteracaoAnatomica folha)
        {
            if (proprietario == null && aguardandoAbrirNo == null)
                selecao?.LimparNoSobFoco(folha);
        }

        public void Iniciar(ElementoInteragivelMao fonte, IXRSelectInteractor interator)
        {
            if (!PodeSelecionar(fonte, interator))
                return;
            var no = Candidato(fonte != null ? fonte.No : null);
            var associar = interator != null ? interator.GetAttachTransform(fonte) : null;
            if (no == null || associar == null)
                return;
            if (EhPrimario(fonte, interator) || EhSecundario(fonte, interator))
                return;
            if (proprietario != null)
            {
                IniciarSecundario(fonte, interator, no, associar);
                return;
            }

            CancelarAguardandoAbrirEntrada();
            selecao.SelecionarNo(no);
            proprietario = fonte;
            interatorAgarrando = interator;
            noAgarrado = no;
            focoAgarre = selecao.NoEmFoco;
            pegada = associar;
            posicaoInicialPegada = pegada.position;
            rotacaoInicialPegada = pegada.rotation;
            primarioMovido = false;
            primarioUsadoNoAgarreSecundario = false;
            unchecked
            {
                versaoProprietario++;
            }

            manipulador.IniciarAgarre(pegada, true);
            versaoAgarre = manipulador.VersaoAgarre;
        }

        private void IniciarSecundario(ElementoInteragivelMao fonte, IXRSelectInteractor interator, NoInteracaoAnatomica no, Transform associar)
        {
            if (coordenadorGesto == null || !TentarObterPosicaoFisicaPinca(interatorAgarrando, out var primeiroPinca) || !TentarObterPosicaoFisicaPinca(interator, out var segundoPinca) || !coordenadorGesto.TentarIniciar(proprietario != null ? proprietario.No : null, fonte != null ? fonte.No : null, primeiroPinca, segundoPinca))
            {
                return;
            }

            secundarioResponsavel = fonte;
            secundarioInterator = interator;
            secundarioAgarre = associar;
            secundarioResponsavelVersao = versaoProprietario;
            primarioMovido = true;
            primarioUsadoNoAgarreSecundario = true;
            if (ControlaManipulacao())
            {
                manipulador.EncerrarAgarre();
            }

            versaoAgarre = 0;
        }

        private void LateUpdate()
        {
            if (proprietario == null)
            {
                AtualizarAguardandoAbrirEntrada();
                return;
            }

            if (!EstaInteratorMaoRastreado(interatorAgarrando))
            {
                SoltarERestaurarAposPerdaRastreamento();
                return;
            }

            if (EhInterfaceInteracaoAtivo(interatorAgarrando) || noAgarrado == null || !noAgarrado.gameObject.activeInHierarchy || selecao == null || selecao.NoSelecionado != noAgarrado || selecao.NoEmFoco != focoAgarre)
            {
                ManterAteEncerrarSelecao();
                return;
            }

            if (secundarioResponsavel != null)
            {
                if (!EstaInteratorMaoRastreado(secundarioInterator))
                {
                    SoltarERestaurarAposPerdaRastreamento();
                }
                else if (EhInterfaceInteracaoAtivo(secundarioInterator) || secundarioAgarre == null || secundarioResponsavelVersao != versaoProprietario || coordenadorGesto == null || !TentarObterPosicaoFisicaPinca(interatorAgarrando, out var primeiroPinca) || !TentarObterPosicaoFisicaPinca(secundarioInterator, out var segundoPinca) || !coordenadorGesto.TentarAtualizar(primeiroPinca, segundoPinca))
                {
                    ManterAteEncerrarSelecao();
                }

                return;
            }

            if (pegada == null || !ControlaManipulacao())
            {
                // Reset ou foco/navegação interrompeu a captura. Exige liberação antes de nova interação.
                ManterAteEncerrarSelecao();
                return;
            }

            MarcarMovidoSeNecessario();
            manipulador.AtualizarAgarre(pegada);
        }

        public void Encerrar(ElementoInteragivelMao fonte, IXRSelectInteractor interator)
        {
            if (soltarNecessario)
            {
                if (EhPrimario(fonte, interator))
                {
                    LimparPrimario();
                }
                else if (EhSecundario(fonte, interator))
                {
                    LimparSecundario();
                }

                soltarNecessario = proprietario != null || secundarioResponsavel != null;
                if (!soltarNecessario)
                {
                    CancelarAguardandoAbrirEntrada();
                }

                return;
            }

            if (EhSecundario(fonte, interator) && secundarioResponsavelVersao == versaoProprietario)
            {
                ConcluirGestoDuasMaosERetomarPrimario();
                return;
            }

            if (!EhPrimario(fonte, interator))
                return;
            if (secundarioResponsavel != null)
            {
                ConcluirGestoDuasMaosEPromoverSecundario();
                return;
            }

            EncerrarPrimarioEPossivelmenteArmarAbrirEntrada(interator);
        }

        private void ConcluirGestoDuasMaosERetomarPrimario()
        {
            coordenadorGesto?.Concluir();
            LimparSecundario();
            primarioMovido = true;
            RetomarAgarrePrimario();
        }

        private void ConcluirGestoDuasMaosEPromoverSecundario()
        {
            coordenadorGesto?.Concluir();
            proprietario = secundarioResponsavel;
            interatorAgarrando = secundarioInterator;
            pegada = secundarioAgarre;
            noAgarrado = Candidato(secundarioResponsavel != null ? secundarioResponsavel.No : null);
            LimparSecundario();
            unchecked
            {
                versaoProprietario++;
            }

            primarioMovido = true;
            if (noAgarrado != null && selecao != null)
            {
                selecao.SelecionarNo(noAgarrado);
                focoAgarre = selecao.NoEmFoco;
            }

            RetomarAgarrePrimario();
        }

        private void RetomarAgarrePrimario()
        {
            if (proprietario == null || pegada == null || manipulador == null || selecao == null || selecao.NoSelecionado != noAgarrado || selecao.NoEmFoco != focoAgarre)
            {
                SoltarERestaurarAposPerdaRastreamento();
                return;
            }

            posicaoInicialPegada = pegada.position;
            rotacaoInicialPegada = pegada.rotation;
            manipulador.IniciarAgarre(pegada, true);
            versaoAgarre = manipulador.VersaoAgarre;
        }

        private void EncerrarPrimarioEPossivelmenteArmarAbrirEntrada(IXRSelectInteractor interatorSolto)
        {
            var podeArmarAbrirEntrada = !primarioUsadoNoAgarreSecundario && noAgarrado != null && noAgarrado.NosFilhos.Count > 0 && selecao != null && selecao.NoSelecionado == noAgarrado && selecao.NoEmFoco == focoAgarre;
            var no = noAgarrado;
            if (ControlaManipulacao())
            {
                manipulador.EncerrarAgarre();
            }

            LimparPrimario();
            if (podeArmarAbrirEntrada)
            {
                ArmarAbrirEntrada(no, interatorSolto);
            }
            else
            {
                CancelarAguardandoAbrirEntrada();
            }
        }

        private void AtualizarAguardandoAbrirEntrada()
        {
            if (aguardandoAbrirNo == null)
            {
                return;
            }

            if (selecao == null || selecao.NoSelecionado != aguardandoAbrirNo || !selecao.EhCandidatoValido(aguardandoAbrirNo) || aguardandoAbrirInterator == null || !EstaInteratorMaoRastreado(aguardandoAbrirInterator) || EhInterfaceInteracaoAtivo(aguardandoAbrirInterator) || aguardandoAbrirInterator.hasSelection)
            {
                CancelarAguardandoAbrirEntrada();
                return;
            }

            if (!EstaMaoAberta(aguardandoAbrirInterator))
            {
                abrirMaoIniciadoEm = -1f;
                return;
            }

            if (abrirMaoIniciadoEm < 0f)
            {
                abrirMaoIniciadoEm = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - abrirMaoIniciadoEm < abrirMaoEsperaSegundos)
            {
                return;
            }

            // EnterSelectedNode continua sendo a única autoridade para mudanças de foco.
            selecao.EntrarNoSelecionado();
            CancelarAguardandoAbrirEntrada();
        }

        private void ArmarAbrirEntrada(NoInteracaoAnatomica no, IXRSelectInteractor interatorSolto)
        {
            aguardandoAbrirNo = no;
            aguardandoAbrirInterator = interatorSolto;
            abrirMaoIniciadoEm = -1f;
        }

        private void CancelarAguardandoAbrirEntrada()
        {
            aguardandoAbrirNo = null;
            aguardandoAbrirInterator = null;
            abrirMaoIniciadoEm = -1f;
        }

        private bool EstaMaoAberta(IXRSelectInteractor interator)
        {
            if (!TentarObterMaoRastreada(interator, out var mao))
            {
                return false;
            }

            foreach (var dedo in AbrirMaoDedos)
            {
                var forma = mao.CalculateFingerShape(dedo, XRFingerShapeTypes.FullCurl);
                if (!forma.TryGetFullCurl(out var curvatura) || curvatura > curvaturaMaximaDedoAberto)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TentarObterMaoRastreada(IXRSelectInteractor interator, out XRHand mao)
        {
            mao = default;
            if (interator == null || interator.transform == null || !TentarObterSubsistemaMao(out var subsistema))
            {
                return false;
            }

            var transformacao = interator.transform;
            if (maoEsquerda != null && (transformacao == maoEsquerda || transformacao.IsChildOf(maoEsquerda)))
            {
                mao = subsistema.leftHand;
            }
            else if (maoDireita != null && (transformacao == maoDireita || transformacao.IsChildOf(maoDireita)))
            {
                mao = subsistema.rightHand;
            }
            else
            {
                return false;
            }

            return mao.isTracked;
        }

        private bool EstaInteratorMaoRastreado(IXRSelectInteractor interator) => EhInteratorDeMao(interator) && TentarObterMaoRastreada(interator, out _);
        private bool EhMaoOposta(IXRInteractor primeiro, IXRInteractor segundo)
        {
            if (!TentarObterLadoMao(primeiro, out var primeiroEhEsquerda) || !TentarObterLadoMao(segundo, out var segundoEhEsquerda))
            {
                return false;
            }

            return primeiroEhEsquerda != segundoEhEsquerda;
        }

        private bool TentarObterLadoMao(IXRInteractor interator, out bool ehEsquerda)
        {
            ehEsquerda = false;
            if (interator == null || interator.transform == null)
            {
                return false;
            }

            var transformacao = interator.transform;
            if (maoEsquerda != null && (transformacao == maoEsquerda || transformacao.IsChildOf(maoEsquerda)))
            {
                ehEsquerda = true;
                return true;
            }

            return maoDireita != null && (transformacao == maoDireita || transformacao.IsChildOf(maoDireita));
        }

        private bool EhInterfaceInteracaoAtivo(IXRInteractor interator)
        {
            if (interator == null)
            {
                return true;
            }

            if (TentarObterLadoMao(interator, out var ehEsquerda) && ControlePainelMundo.AgarrePainelAtivoParaMao(ehEsquerda ? maoEsquerda : maoDireita))
                return true;
            if (ControlePainelMundo.AgarrePainelAtivoPara(interator) || ZonaProximidadePainelInterface.InteratorEstaProximoDoPainel(interator))
                return true;
            // NearFar arbitra internamente UI contra casts 3D. Seu modelo de UI pode
            // reter um raycast de fundo válido mesmo depois de vencer um acerto anatômico;
            // apenas o ponto final resolvido da curva pode bloquear a anatomia.
            if (interator is NearFarInteractor proximoDistante && proximoDistante.TryGetCurveEndPoint(out _) == EndPointType.UI)
            {
                return true;
            }

            // Um alvo UGUI real no raio independente da mão vence uma nova seleção
            // anatômica. Ele nunca deve invalidar uma seleção anatômica já pertencente
            // a este mesmo pinch físico (o modelo de UI pode estar obsoleto por um frame).
            if (interator is NearFarInteractor maoProximoDistante && interator is IXRSelectInteractor interatorResponsavelInterface && !EhPrimario(proprietario, interatorResponsavelInterface) && !EhSecundario(proprietario, interatorResponsavelInterface) && PonteRaioInterfaceMao.RaioEstaAlvejandoInterface(maoProximoDistante))
            {
                return true;
            }

            // Interactors de UI de raio e direto/poke não expõem o ponto final resolvido do NearFar.
            // Para eles, o resultado atual de UI ainda é o sinal local disponível,
            // após as verificações explícitas acima da zona do painel para a mesma mão.
            if (interator is IUIInteractor interatorInterface && interatorInterface.TryGetUIModel(out var interfaceModelo) && interfaceModelo.currentRaycast.isValid)
            {
                return true;
            }

            // Abrange UI XRI ou outros alvos não anatômicos selecionados/em foco por mãos near/poke.
            if (interator is IXRSelectInteractor selecionarInterator)
            {
                foreach (var selecionado in selecionarInterator.interactablesSelected)
                {
                    if (selecionado is not ElementoInteragivelMao)
                    {
                        return true;
                    }
                }
            }

            if (interator is IXRHoverInteractor focoInterator)
            {
                foreach (var foco in focoInterator.interactablesHovered)
                {
                    if (foco is not ElementoInteragivelMao)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Bloqueia somente alvos anatômicos concorrentes da mão já em um gesto global.</summary>
        public bool BloqueiaNovaSelecaoAnatomicaPara(IXRInteractor interator)
        {
            return coordenadorGesto != null && coordenadorGesto.EstaAtivo && (EhMesmaMao(interator, interatorAgarrando) || EhMesmaMao(interator, secundarioInterator));
        }

        private bool EhMesmaMao(IXRInteractor primeiro, IXRInteractor segundo)
        {
            return TentarObterLadoMao(primeiro, out var primeiroEsquerda) && TentarObterLadoMao(segundo, out var segundoEsquerda) && primeiroEsquerda == segundoEsquerda;
        }

        /// <summary>Usa dados das juntas de polegar/indicador do XR Hands no espaço de mundo XROrigin, nunca uma pose de anexo de objeto distante.</summary>
        private bool TentarObterPosicaoFisicaPinca(IXRSelectInteractor interator, out Vector3 posicao)
        {
            posicao = default;
            if (!TentarObterMaoRastreada(interator, out var mao) || interator == null || interator.transform == null)
                return false;
            var origem = interator.transform.GetComponentInParent<XROrigin>();
            return origem != null && PincaMao.TentarObterPonto(mao, origem.transform, out posicao);
        }

        private bool TentarObterSubsistemaMao(out XRHandSubsystem subsistema) => PincaMao.TentarObterSubsistema(ref maoSubsistema, out subsistema);

        private void MarcarMovidoSeNecessario()
        {
            if (primarioMovido || pegada == null)
            {
                return;
            }

            primarioMovido = Vector3.Distance(pegada.position, posicaoInicialPegada) > toleranciaPosicaoEstavel || Quaternion.Angle(pegada.rotation, rotacaoInicialPegada) > toleranciaRotacaoEstavel;
        }

        private bool EhPrimario(ElementoInteragivelMao fonte, IXRSelectInteractor interator) => proprietario == fonte && interatorAgarrando == interator;
        private bool EhSecundario(ElementoInteragivelMao fonte, IXRSelectInteractor interator) => secundarioResponsavel == fonte && secundarioInterator == interator;
        private bool ControlaManipulacao() => proprietario != null && manipulador != null && manipulador.EstaAgarrando && manipulador.VersaoAgarre == versaoAgarre;
        private void LimparSecundario()
        {
            secundarioResponsavel = null;
            secundarioInterator = null;
            secundarioAgarre = null;
            secundarioResponsavelVersao = 0;
        }

        private void LimparPrimario()
        {
            proprietario = null;
            interatorAgarrando = null;
            pegada = null;
            noAgarrado = null;
            focoAgarre = null;
            versaoAgarre = 0;
            primarioMovido = false;
            primarioUsadoNoAgarreSecundario = false;
        }

        private void ManterAteEncerrarSelecao()
        {
            coordenadorGesto?.CancelarERestaurar();
            if (ControlaManipulacao())
            {
                manipulador.EncerrarAgarre();
            }

            pegada = null;
            versaoAgarre = 0;
            primarioMovido = true;
            soltarNecessario = proprietario != null || secundarioResponsavel != null;
            CancelarAguardandoAbrirEntrada();
        }

        private void SoltarERestaurarAposPerdaRastreamento()
        {
            coordenadorGesto?.CancelarERestaurar();
            if (ControlaManipulacao())
            {
                manipulador.EncerrarAgarre();
            }

            LimparSecundario();
            LimparPrimario();
            soltarNecessario = false;
            CancelarAguardandoAbrirEntrada();
        }

        private void OnDisable()
        {
            SoltarERestaurarAposPerdaRastreamento();
        }

        public void Soltar()
        {
            SoltarERestaurarAposPerdaRastreamento();
        }
    }
}