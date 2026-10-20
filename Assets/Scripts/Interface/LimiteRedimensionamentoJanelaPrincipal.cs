using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;

namespace VRSNC.Interface
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.UI", "Assembly-CSharp", "MainSpatialWindowResizeEdge")]
    public sealed class LimiteRedimensionamentoJanelaPrincipal : MonoBehaviour
    {
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("window")]
        private JanelaEspacialPrincipal janela;
        [UnityEngine.Serialization.FormerlySerializedAs("bordadirecao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("edgeDirection")]
        private Vector2 direcaoBorda;
        private XRSimpleInteractable interagivel;
        private Image bordaImagem;
        private BoxCollider colisorBorda;
        private int quantidadeFocos;
        private readonly HashSet<IXRHoverInteractor> emFocoInteratores = new HashSet<IXRHoverInteractor>();
        private IXRSelectInteractor proprietarioRedimensionamento;
        private Vector3 ultimoColisorTamanho = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 ultimoColisorCentro = new Vector3(float.NaN, float.NaN, float.NaN);
        public void Configurar(JanelaEspacialPrincipal responsavel, Vector2 direcao)
        {
            janela = responsavel;
            direcaoBorda = direcao.normalized;
        }

        private void Awake()
        {
            interagivel = GetComponent<XRSimpleInteractable>();
            bordaImagem = GetComponent<Image>();
            colisorBorda = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            interagivel ??= GetComponent<XRSimpleInteractable>();
            if (interagivel == null)
                return;
            interagivel.selectEntered.AddListener(EmEntrou);
            interagivel.selectExited.AddListener(EmSair);
            interagivel.hoverEntered.AddListener(EmFocoEntrou);
            interagivel.hoverExited.AddListener(EmFocoSair);
            AtualizarColisorParaAtualRetangulo();
        }

        private void OnDisable()
        {
            if (interagivel != null)
            {
                interagivel.selectEntered.RemoveListener(EmEntrou);
                interagivel.selectExited.RemoveListener(EmSair);
                interagivel.hoverEntered.RemoveListener(EmFocoEntrou);
                interagivel.hoverExited.RemoveListener(EmFocoSair);
            }

            foreach (var interator in emFocoInteratores)
                DefinirFocoControladorPainel(interator, false);
            emFocoInteratores.Clear();
            janela?.CancelarRedimensionamento(proprietarioRedimensionamento);
            proprietarioRedimensionamento = null;
        }

        private void LateUpdate()
        {
            AtualizarColisorParaAtualRetangulo();
            if (interagivel == null || !interagivel.isSelected)
                return;
            foreach (var interator in interagivel.interactorsSelecting)
                janela?.AtualizarRedimensionamento(interator, direcaoBorda);
        }

        private void EmEntrou(SelectEnterEventArgs argumentos)
        {
            if (janela != null && janela.IniciarRedimensionamento(argumentos.interactorObject, interagivel))
                proprietarioRedimensionamento = argumentos.interactorObject;
        }

        private void EmSair(SelectExitEventArgs argumentos)
        {
            if (argumentos.interactorObject != proprietarioRedimensionamento)
                return;
            janela?.EncerrarRedimensionamento(argumentos.interactorObject);
            proprietarioRedimensionamento = null;
        }

        private void EmFocoEntrou(HoverEnterEventArgs argumentos)
        {
            if (argumentos.interactorObject != null && emFocoInteratores.Add(argumentos.interactorObject))
                DefinirFocoControladorPainel(argumentos.interactorObject, true);
            quantidadeFocos = emFocoInteratores.Count;
            DefinirFocoVisual();
        }

        private void EmFocoSair(HoverExitEventArgs argumentos)
        {
            if (argumentos.interactorObject != null && emFocoInteratores.Remove(argumentos.interactorObject))
                DefinirFocoControladorPainel(argumentos.interactorObject, false);
            quantidadeFocos = emFocoInteratores.Count;
            DefinirFocoVisual();
        }

        private void DefinirFocoVisual()
        {
            if (bordaImagem != null)
                bordaImagem.color = quantidadeFocos > 0 ? TemaVRSNC.BordaRedimensionarEmFoco : TemaVRSNC.BordaRedimensionar;
        }

        private void AtualizarColisorParaAtualRetangulo()
        {
            if (colisorBorda == null)
                colisorBorda = GetComponent<BoxCollider>();
            var retangulo = transform as RectTransform;
            if (colisorBorda == null || retangulo == null)
                return;
            var tamanho = new Vector3(Mathf.Max(24f, retangulo.rect.width), Mathf.Max(24f, retangulo.rect.height), 24f);
            var centro = retangulo.rect.center;
            var colisorCentro = new Vector3(centro.x, centro.y, 0f);
            if (tamanho == ultimoColisorTamanho && colisorCentro == ultimoColisorCentro)
                return;
            colisorBorda.size = tamanho;
            colisorBorda.center = colisorCentro;
            ultimoColisorTamanho = tamanho;
            ultimoColisorCentro = colisorCentro;
        }

        private void DefinirFocoControladorPainel(IXRHoverInteractor interator, bool emFoco)
        {
            if (interator is Component componente)
                componente.GetComponent<IntermediarioSelecaoControladorJanelaPrincipal>()?.DefinirFocoNoPainel(this, emFoco);
        }
    }
}