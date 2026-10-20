using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRSNC.Interacao
{
    /// <summary>Um alvo XRI por malha física. O nó lógico selecionado continua sendo o filho atual da hierarquia.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainHandInteractable")]
    public sealed class ElementoInteragivelMao : XRSimpleInteractable
    {
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("node")]
        private NoInteracaoAnatomica no;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("bridge")]
        private PonteInteracaoMao ponte;
        public NoInteracaoAnatomica No => no;
        protected override void Awake()
        {
            base.Awake();
            // Os 240 componentes existentes podem estar serializados como Single.
            // O bridge guarda a elegibilidade por interactor; Multiple só habilita
            // o segundo select do mesmo grupo anatômico, nunca de um leaf.
            selectMode = InteractableSelectMode.Multiple;
        }

        public void Configurar(NoInteracaoAnatomica folha, PonteInteracaoMao maoPonte, Collider colisorExato)
        {
            no = folha;
            ponte = maoPonte;
            colliders.Clear();
            colliders.Add(colisorExato);
            selectMode = InteractableSelectMode.Multiple;
        }

        public override bool IsHoverableBy(IXRHoverInteractor interator) => base.IsHoverableBy(interator) && ponte != null && ponte.EhInteratorDeMao(interator) && !PonteInteracaoMao.MaoReservada(interator) && !ponte.BloqueiaNovaSelecaoAnatomicaPara(interator) && ponte.Candidato(no) != null;
        public override bool IsSelectableBy(IXRSelectInteractor interator) => base.IsSelectableBy(interator) && ponte != null && ponte.PodeSelecionar(this, interator);
        protected override void OnHoverEntered(HoverEnterEventArgs args)
        {
            base.OnHoverEntered(args);
            ponte?.Foco(no);
        }

        protected override void OnHoverExited(HoverExitEventArgs args)
        {
            base.OnHoverExited(args);
            ponte?.LimparFoco(no);
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            ponte?.Iniciar(this, args.interactorObject);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            ponte?.Encerrar(this, args.interactorObject);
            base.OnSelectExited(args);
        }
    }
}