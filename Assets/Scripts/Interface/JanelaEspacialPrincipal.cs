using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRSNC.Interface
{
    /// <summary>Possui as affordances leves da janela espacial do painel principal; nunca possui anatomia nem poses de popup.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.UI", "Assembly-CSharp", "MainSpatialWindow")]
    public sealed class JanelaEspacialPrincipal : MonoBehaviour
    {
        [UnityEngine.Serialization.FormerlySerializedAs("janelaRetangulo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("windowRect")]
        private RectTransform retanguloJanela;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("follow")]
        private AcompanhamentoCameraPainelInterface acompanhar;
        [UnityEngine.Serialization.FormerlySerializedAs("painelcontrole")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("panelHandle")]
        private ControlePainelMundo controlePainel;
        [UnityEngine.Serialization.FormerlySerializedAs("moveragarre")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("moveGrab")]
        private XRGrabInteractable agarreMovimento;
        [UnityEngine.Serialization.FormerlySerializedAs("inferiorManipular")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("bottomHandle")]
        private RectTransform alcaInferior;
        [UnityEngine.Serialization.FormerlySerializedAs("inferiorManipularImagem")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("bottomHandleImage")]
        private Image imagemAlcaInferior;
        [UnityEngine.Serialization.FormerlySerializedAs("minimolargura")]
        [SerializeField, Min(260f)]
        [UnityEngine.Serialization.FormerlySerializedAs("minimumWidth")]
        private float larguraMinima = 420f;
        [UnityEngine.Serialization.FormerlySerializedAs("minimoaltura")]
        [SerializeField, Min(260f)]
        [UnityEngine.Serialization.FormerlySerializedAs("minimumHeight")]
        private float alturaMinima = 420f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximolargura")]
        [SerializeField, Min(300f)]
        [UnityEngine.Serialization.FormerlySerializedAs("maximumWidth")]
        private float larguraMaxima = 980f;
        [UnityEngine.Serialization.FormerlySerializedAs("maximoaltura")]
        [SerializeField, Min(300f)]
        [UnityEngine.Serialization.FormerlySerializedAs("maximumHeight")]
        private float alturaMaxima = 980f;
        [UnityEngine.Serialization.FormerlySerializedAs("duploCliquesSegundos")]
        [SerializeField, Min(0.1f)]
        [UnityEngine.Serialization.FormerlySerializedAs("doubleClickSeconds")]
        private float intervaloCliqueDuploSegundos = 0.34f;
        [UnityEngine.Serialization.FormerlySerializedAs("cliquesMoverToleranciaMetros")]
        [SerializeField, Min(0.001f)]
        [UnityEngine.Serialization.FormerlySerializedAs("clickMoveToleranceMeters")]
        private float toleranciaMovimentoCliqueMetros = 0.012f;
        private IXRSelectInteractor redimensionandoInterator;
        private IXRInteractable fonteRedimensionamento;
        private bool responsabilidadeRedimensionamentoExterno;
        private Vector2 tamanhoInicialRedimensionamento;
        private Vector3 pontoLocalInicialRedimensionamento;
        private Vector3 posicaoInicialRedimensionamento;
        private Quaternion rotacaoInicialRedimensionamento;
        private Vector3 escalaInicialRedimensionamento;
        private Vector3 redimensionarPlanoPonto;
        private Vector3 redimensionarPlanoNormal;
        private float tempoUltimoClique = float.NegativeInfinity;
        private IXRSelectInteractor interatorUltimoClique;
        private readonly HashSet<IXRSelectInteractor> moverSelecionandoInteratores = new HashSet<IXRSelectInteractor>();
        private readonly HashSet<IXRHoverInteractor> manipularFocoInteratores = new HashSet<IXRHoverInteractor>();
        private float moverSelecaoIniciadoEm;
        private Vector3 posicaoInicialMovimento;
        private Quaternion rotacaoInicialMovimento;
        private Vector3 escalaInicialMovimento;
        private bool movidoDuranteSelecao;
        private bool maximizado;
        private Vector2 tamanhoAntesMaximizar;
        private Vector3 posicaoAntesMaximizar;
        private Quaternion rotacaoAntesMaximizar;
        private Vector3 escalaAntesMaximizar;
        public bool EstaRedimensionando => redimensionandoInterator != null;
        public bool EstaMaximizado => maximizado;
        public Vector2 TamanhoAtual => retanguloJanela != null ? retanguloJanela.sizeDelta : Vector2.zero;
        public void Configurar(RectTransform retangulo, AcompanhamentoCameraPainelInterface painelSeguir, XRGrabInteractable agarrar, RectTransform alca, Image alcaImagem)
        {
            retanguloJanela = retangulo;
            acompanhar = painelSeguir;
            controlePainel ??= GetComponent<ControlePainelMundo>();
            agarreMovimento = agarrar;
            alcaInferior = alca;
            imagemAlcaInferior = alcaImagem;
        }

        private void Awake()
        {
            retanguloJanela ??= transform as RectTransform;
            acompanhar ??= GetComponent<AcompanhamentoCameraPainelInterface>();
            controlePainel ??= GetComponent<ControlePainelMundo>();
            agarreMovimento ??= GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (agarreMovimento == null)
                return;
            agarreMovimento.hoverEntered.AddListener(EmMoverFocoEntrou);
            agarreMovimento.hoverExited.AddListener(EmMoverFocoSair);
            agarreMovimento.selectEntered.AddListener(EmMoverSelecionarEntrou);
            agarreMovimento.selectExited.AddListener(EmMoverSelecionarSair);
        }

        private void OnDisable()
        {
            if (agarreMovimento != null)
            {
                agarreMovimento.hoverEntered.RemoveListener(EmMoverFocoEntrou);
                agarreMovimento.hoverExited.RemoveListener(EmMoverFocoSair);
                agarreMovimento.selectEntered.RemoveListener(EmMoverSelecionarEntrou);
                agarreMovimento.selectExited.RemoveListener(EmMoverSelecionarSair);
            }

            moverSelecionandoInteratores.Clear();
            foreach (var interator in manipularFocoInteratores)
                DefinirFocoControladorPainel(interator, false);
            manipularFocoInteratores.Clear();
            interatorUltimoClique = null;
            quantidadeFocosMovimento = 0;
            DefinirFocoControle(false);
            CancelarRedimensionamento();
        }

        private void LateUpdate()
        {
            if (moverSelecionandoInteratores.Count > 0)
            {
                movidoDuranteSelecao |= Vector3.Distance(transform.position, posicaoInicialMovimento) > toleranciaMovimentoCliqueMetros || Quaternion.Angle(transform.rotation, rotacaoInicialMovimento) > 1f || Vector3.Distance(transform.lossyScale, escalaInicialMovimento) > toleranciaMovimentoCliqueMetros;
            }

            if (redimensionandoInterator == null)
                return;
            if (redimensionandoInterator.transform == null || !redimensionandoInterator.transform.gameObject.activeInHierarchy)
                CancelarRedimensionamento();
        }

        public bool IniciarRedimensionamento(IXRSelectInteractor interator)
        {
            return IniciarRedimensionamento(interator, null);
        }

        public bool IniciarRedimensionamento(IXRSelectInteractor interator, IXRInteractable fonte)
        {
            if (interator == null || redimensionandoInterator != null || agarreMovimento != null && agarreMovimento.isSelected || retanguloJanela == null)
                return false;
            controlePainel ??= GetComponent<ControlePainelMundo>();
            // Cada redimensionamento é um gesto real do painel para a consulta de arbitragem anatômica do mapeador.
            if (controlePainel == null || !controlePainel.TentarIniciarGestoExterno(interator))
                return false;
            redimensionandoInterator = interator;
            fonteRedimensionamento = fonte;
            responsabilidadeRedimensionamentoExterno = true;
            tamanhoInicialRedimensionamento = retanguloJanela.sizeDelta;
            posicaoInicialRedimensionamento = transform.position;
            rotacaoInicialRedimensionamento = transform.rotation;
            escalaInicialRedimensionamento = transform.localScale;
            // O plano do gesto é fixado na aquisição. Raios distantes continuam projetando nesse
            // plano após deixar o collider fino da borda, em vez de saltar para a anatomia atrás dele.
            redimensionarPlanoPonto = retanguloJanela.TransformPoint(retanguloJanela.rect.center);
            redimensionarPlanoNormal = retanguloJanela.forward;
            pontoLocalInicialRedimensionamento = transform.InverseTransformPoint(ObterPontoGestoNoMundo(interator));
            return true;
        }

        public void AtualizarRedimensionamento(IXRSelectInteractor interator, Vector2 bordaDirecao)
        {
            if (interator == null || interator != redimensionandoInterator || retanguloJanela == null)
                return;
            var pontoLocalAtual = transform.InverseTransformPoint(ObterPontoGestoNoMundo(interator));
            var variacao = new Vector2(pontoLocalAtual.x - pontoLocalInicialRedimensionamento.x, pontoLocalAtual.y - pontoLocalInicialRedimensionamento.y);
            var proximo = tamanhoInicialRedimensionamento + new Vector2(Mathf.Abs(bordaDirecao.x) * variacao.x * Mathf.Sign(bordaDirecao.x), Mathf.Abs(bordaDirecao.y) * variacao.y * Mathf.Sign(bordaDirecao.y));
            retanguloJanela.sizeDelta = new Vector2(Mathf.Clamp(proximo.x, larguraMinima, larguraMaxima), Mathf.Clamp(proximo.y, alturaMinima, alturaMaxima));
            // Um gesto de borda pode redimensionar o conteúdo, mas nunca deve deixar uma pose de mundo movida/rotacionada.
            transform.SetPositionAndRotation(posicaoInicialRedimensionamento, rotacaoInicialRedimensionamento);
            transform.localScale = escalaInicialRedimensionamento;
        }

        public void EncerrarRedimensionamento(IXRSelectInteractor interator)
        {
            if (interator == null || interator != redimensionandoInterator)
                return;
            SoltarResponsabilidadeRedimensionamento();
        }

        public void CancelarRedimensionamento()
        {
            if (redimensionandoInterator == null)
                return;
            transform.SetPositionAndRotation(posicaoInicialRedimensionamento, rotacaoInicialRedimensionamento);
            transform.localScale = escalaInicialRedimensionamento;
            SoltarResponsabilidadeRedimensionamento();
        }

        /// <summary>O cancelamento do ciclo de vida de uma borda só pode liberar o proprietário atual dessa borda.</summary>
        public void CancelarRedimensionamento(IXRSelectInteractor interator)
        {
            if (interator != null && interator == redimensionandoInterator)
                CancelarRedimensionamento();
        }

        public void AlternarMaximizacao()
        {
            if (retanguloJanela == null)
                return;
            maximizado = !maximizado;
            if (maximizado)
            {
                tamanhoAntesMaximizar = retanguloJanela.sizeDelta;
                posicaoAntesMaximizar = transform.position;
                rotacaoAntesMaximizar = transform.rotation;
                escalaAntesMaximizar = transform.localScale;
                retanguloJanela.sizeDelta = new Vector2(larguraMaxima, alturaMaxima);
                acompanhar?.PosicionarJanelaConfortavelmenteDianteDaCamera();
            }
            else
            {
                retanguloJanela.sizeDelta = tamanhoAntesMaximizar;
                transform.SetPositionAndRotation(posicaoAntesMaximizar, rotacaoAntesMaximizar);
                transform.localScale = escalaAntesMaximizar;
            }

            acompanhar?.DefinirAcompanhamentoAtivo(false);
            // Também pela alça do painel, para o estado Seguir/Fixar continuar coerente com a pose.
            controlePainel?.DefinirAcoplamento(false);
        }

        private int quantidadeFocosMovimento;
        private void EmMoverFocoEntrou(HoverEnterEventArgs argumentos)
        {
            if (argumentos.interactorObject != null && manipularFocoInteratores.Add(argumentos.interactorObject))
                DefinirFocoControladorPainel(argumentos.interactorObject, true);
            quantidadeFocosMovimento = manipularFocoInteratores.Count;
            DefinirFocoControle(quantidadeFocosMovimento > 0);
        }

        private void EmMoverFocoSair(HoverExitEventArgs argumentos)
        {
            if (argumentos.interactorObject != null && manipularFocoInteratores.Remove(argumentos.interactorObject))
                DefinirFocoControladorPainel(argumentos.interactorObject, false);
            quantidadeFocosMovimento = manipularFocoInteratores.Count;
            DefinirFocoControle(quantidadeFocosMovimento > 0);
        }

        private void EmMoverSelecionarEntrou(SelectEnterEventArgs argumentos)
        {
            if (argumentos.interactorObject == null || !moverSelecionandoInteratores.Add(argumentos.interactorObject))
                return;
            if (moverSelecionandoInteratores.Count > 1)
            {
                // Seleções XRI simultâneas são uma manipulação, nunca o segundo clique de um clique duplo.
                movidoDuranteSelecao = true;
                interatorUltimoClique = null;
                tempoUltimoClique = float.NegativeInfinity;
                return;
            }

            posicaoInicialMovimento = transform.position;
            rotacaoInicialMovimento = transform.rotation;
            escalaInicialMovimento = transform.lossyScale;
            moverSelecaoIniciadoEm = Time.unscaledTime;
            movidoDuranteSelecao = false;
            acompanhar?.DefinirAcompanhamentoAtivo(false);
        }

        private void EmMoverSelecionarSair(SelectExitEventArgs argumentos)
        {
            if (argumentos.interactorObject == null || !moverSelecionandoInteratores.Remove(argumentos.interactorObject))
                return;
            if (moverSelecionandoInteratores.Count > 0)
                return;
            movidoDuranteSelecao |= Vector3.Distance(transform.position, posicaoInicialMovimento) > toleranciaMovimentoCliqueMetros || Quaternion.Angle(transform.rotation, rotacaoInicialMovimento) > 1f || Vector3.Distance(transform.lossyScale, escalaInicialMovimento) > toleranciaMovimentoCliqueMetros;
            if (argumentos.isCanceled || movidoDuranteSelecao || Time.unscaledTime - moverSelecaoIniciadoEm > intervaloCliqueDuploSegundos)
            {
                interatorUltimoClique = null;
                tempoUltimoClique = float.NegativeInfinity;
                return;
            }

            var agora = Time.unscaledTime;
            if (interatorUltimoClique == argumentos.interactorObject && agora - tempoUltimoClique <= intervaloCliqueDuploSegundos)
            {
                interatorUltimoClique = null;
                tempoUltimoClique = float.NegativeInfinity;
                AlternarMaximizacao();
            }
            else
            {
                interatorUltimoClique = argumentos.interactorObject;
                tempoUltimoClique = agora;
            }
        }

        private void DefinirFocoControle(bool foco)
        {
            if (alcaInferior == null || imagemAlcaInferior == null)
                return;
            imagemAlcaInferior.color = foco ? TemaVRSNC.AlcaEmFoco : TemaVRSNC.Alca;
            alcaInferior.anchorMin = new Vector2(.5f, 0f);
            alcaInferior.anchorMax = new Vector2(.5f, 0f);
            alcaInferior.anchoredPosition = new Vector2(0f, -42f);
            alcaInferior.sizeDelta = foco ? new Vector2(224f, 16f) : new Vector2(30f, 18f);
        }

        private Vector3 ObterPontoGestoNoMundo(IXRSelectInteractor interator)
        {
            if (TentarObterPontoRaioPlanoRedimensionamento(interator, out var raioPonto))
                return raioPonto;
            var associar = interator.GetAttachTransform(fonteRedimensionamento);
            return associar != null ? associar.position : interator.transform.position;
        }

        private bool TentarObterPontoRaioPlanoRedimensionamento(IXRSelectInteractor interator, out Vector3 ponto)
        {
            ponto = default;
            if (redimensionarPlanoNormal.sqrMagnitude < 0.0001f)
                return false;
            Transform origem = null;
            Vector3 direcao = default;
            if (interator is NearFarInteractor proximoDistante)
            {
                // Seleções próximas mantêm seu ponto físico de anexação. Seleções distantes usam a
                // origem estabilizada do caster e o primeiro segmento de raio da curva.
                if (proximoDistante.selectionRegion.Value == NearFarInteractor.Region.Near)
                    return false;
                var emissor = proximoDistante.farInteractionCaster;
                origem = emissor != null ? emissor.effectiveCastOrigin : null;
                if (emissor != null && emissor.samplePoints.IsCreated && emissor.samplePoints.Length > 1)
                    direcao = emissor.samplePoints[1] - emissor.samplePoints[0];
            }
            else if (interator is XRRayInteractor raio)
            {
                origem = raio.rayOriginTransform != null ? raio.rayOriginTransform : raio.transform;
            }
            else
                return false;
            if (origem == null)
                return false;
            if (direcao.sqrMagnitude < 0.000001f)
                direcao = origem.forward;
            var denominador = Vector3.Dot(redimensionarPlanoNormal, direcao);
            if (Mathf.Abs(denominador) < 0.0001f)
                return false;
            var distancia = Vector3.Dot(redimensionarPlanoPonto - origem.position, redimensionarPlanoNormal) / denominador;
            if (distancia < 0f)
                return false;
            ponto = origem.position + direcao * distancia;
            return true;
        }

        private void SoltarResponsabilidadeRedimensionamento()
        {
            if (responsabilidadeRedimensionamentoExterno)
                controlePainel?.EncerrarGestoExterno(redimensionandoInterator);
            responsabilidadeRedimensionamentoExterno = false;
            redimensionandoInterator = null;
            fonteRedimensionamento = null;
        }

        private void DefinirFocoControladorPainel(IXRHoverInteractor interator, bool emFoco)
        {
            if (interator is Component componente)
                componente.GetComponent<IntermediarioSelecaoControladorJanelaPrincipal>()?.DefinirFocoNoPainel(this, emFoco);
        }
    }
}