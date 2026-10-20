using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRSNC.Interface
{
    /// <summary>
    /// Alça inferior das janelas flutuantes criadas em execução (mapa 2D e traçados do EEG). Funciona como a alça do
    /// painel principal: pinça ou grip na barra e a janela acompanha a mão. O <see cref="XRGrabInteractable"/> fica na
    /// raiz da janela e só a barra tem colisor, então o resto da janela continua sendo interface comum.
    /// Enquanto a alça está segura, a mesma mão ou controle não move a anatomia (<see cref="ControlePainelMundo"/>).
    /// Ao soltar, a janela gira até ficar de frente para o rosto, como ao abrir: o pulso não deixa a leitura torta.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlcaJanelaFlutuante : MonoBehaviour
    {
        public const float DuracaoVoltarParaRosto = 0.2f;
        private XRGrabInteractable agarre;
        private Image barra;
        private int apontando;
        private Quaternion rotacaoAoSoltar;
        private float viradaComecouEm = -1f;

        /// <summary>Monta a alça: Rigidbody cinemático e captura na raiz, colisor só na <paramref name="barraAlca"/>.</summary>
        public static AlcaJanelaFlutuante Configurar(GameObject raizJanela, Image barraAlca, Vector3 tamanhoColisor)
        {
            var colisor = barraAlca.gameObject.AddComponent<BoxCollider>();
            colisor.size = tamanhoColisor;
            var corpo = raizJanela.GetComponent<Rigidbody>();
            if (corpo == null)
                corpo = raizJanela.AddComponent<Rigidbody>();
            corpo.useGravity = false;
            corpo.isKinematic = true;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
            var captura = raizJanela.AddComponent<XRGrabInteractable>();
            // Mesma configuração da alça do painel principal (ConfiguracaoInterfacePainelMundo).
            captura.throwOnDetach = false;
            captura.trackPosition = true;
            captura.trackRotation = true;
            captura.trackScale = false;
            captura.retainTransformParent = true;
            captura.useDynamicAttach = true;
            captura.matchAttachPosition = true;
            captura.matchAttachRotation = true;
            captura.reinitializeDynamicAttachEverySingleGrab = true;
            captura.colliders.Clear();
            captura.colliders.Add(colisor);
            var alca = raizJanela.AddComponent<AlcaJanelaFlutuante>();
            alca.agarre = captura;
            alca.barra = barraAlca;
            barraAlca.color = TemaVRSNC.Alca;
            return alca;
        }

        private void OnEnable()
        {
            if (agarre == null)
                agarre = GetComponent<XRGrabInteractable>();
            if (agarre == null)
                return;
            agarre.hoverEntered.AddListener(AoApontar);
            agarre.hoverExited.AddListener(AoDeixarDeApontar);
            agarre.selectEntered.AddListener(AoSegurar);
            agarre.selectExited.AddListener(AoSoltar);
        }

        private void OnDisable()
        {
            if (agarre != null)
            {
                agarre.hoverEntered.RemoveListener(AoApontar);
                agarre.hoverExited.RemoveListener(AoDeixarDeApontar);
                agarre.selectEntered.RemoveListener(AoSegurar);
                agarre.selectExited.RemoveListener(AoSoltar);
                // Janela fechada no meio de um agarre: nenhum interator pode ficar preso como "segurando painel".
                foreach (var interator in agarre.interactorsSelecting)
                    ControlePainelMundo.DefinirAgarreJanelaFlutuante(interator, false);
            }

            apontando = 0;
            viradaComecouEm = -1f;
            AtualizarCor();
        }

        private void Update()
        {
            if (viradaComecouEm < 0f)
                return;
            // Outra mão ainda segurando (ou um novo agarre) cancela a virada: quem manda é a mão.
            var camera = Camera.main;
            var paraJanela = camera != null ? transform.position - camera.transform.position : Vector3.zero;
            if ((agarre != null && agarre.isSelected) || paraJanela.sqrMagnitude < 1e-6f)
            {
                viradaComecouEm = -1f;
                return;
            }

            var progresso = Mathf.Clamp01((Time.unscaledTime - viradaComecouEm) / DuracaoVoltarParaRosto);
            transform.rotation = Quaternion.Slerp(rotacaoAoSoltar, Quaternion.LookRotation(paraJanela, Vector3.up), Mathf.SmoothStep(0f, 1f, progresso));
            if (progresso >= 1f)
                viradaComecouEm = -1f;
        }

        private void AoApontar(HoverEnterEventArgs _)
        {
            apontando++;
            AtualizarCor();
        }

        private void AoDeixarDeApontar(HoverExitEventArgs _)
        {
            apontando = Mathf.Max(0, apontando - 1);
            AtualizarCor();
        }

        private void AoSegurar(SelectEnterEventArgs argumentos) => ControlePainelMundo.DefinirAgarreJanelaFlutuante(argumentos.interactorObject, true);

        private void AoSoltar(SelectExitEventArgs argumentos)
        {
            ControlePainelMundo.DefinirAgarreJanelaFlutuante(argumentos.interactorObject, false);
            rotacaoAoSoltar = transform.rotation;
            viradaComecouEm = Time.unscaledTime;
        }

        private void AtualizarCor()
        {
            if (barra != null)
                barra.color = apontando > 0 || (agarre != null && agarre.isSelected) ? TemaVRSNC.AlcaEmFoco : TemaVRSNC.Alca;
        }
    }
}
