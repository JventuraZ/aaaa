using System.Collections.Generic;
using UnityEngine;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Deixa translúcido tudo o que está fora do grupo em foco, para o alvo não ficar escondido atrás de peças que
    /// não podem ser escolhidas naquele nível. No nível inicial nada muda. Os materiais originais voltam a cada
    /// troca de foco e ao desligar; o destaque, a visibilidade e as posições não são tocados.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControladorEsmaecimentoForaDoFoco : MonoBehaviour
    {
        [SerializeField]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        // URP Unlit transparente compartilhado por todas as peças fora do foco.
        [SerializeField]
        private Material materialEsmaecido;
        private readonly Dictionary<Renderer, Material[]> materiaisOriginais = new();

        public bool EstaEsmaecendo => materiaisOriginais.Count > 0;

        private void Awake()
        {
            if (gerenciadorSelecao == null)
                gerenciadorSelecao = GetComponent<GerenciadorSelecaoHierarquica>();
        }

        private void OnEnable()
        {
            if (gerenciadorSelecao != null)
                gerenciadorSelecao.emFocoAlterado.AddListener(Aplicar);
        }

        private void OnDisable()
        {
            if (gerenciadorSelecao != null)
                gerenciadorSelecao.emFocoAlterado.RemoveListener(Aplicar);
            RestaurarTodos();
        }

        /// <summary>Esmaece o que está fora do foco informado; na raiz, apenas restaura.</summary>
        public void Aplicar(NoInteracaoAnatomica foco)
        {
            RestaurarTodos();
            if (!Application.isPlaying || foco == null || foco.NoPai == null || materialEsmaecido == null || gerenciadorSelecao == null || gerenciadorSelecao.NoRaiz == null)
                return;
            foreach (var renderizador in gerenciadorSelecao.NoRaiz.GetComponentsInChildren<Renderer>(true))
            {
                if (renderizador == null || renderizador.transform.IsChildOf(foco.transform))
                    continue;
                var originais = renderizador.sharedMaterials;
                materiaisOriginais[renderizador] = originais;
                var substitutos = new Material[originais.Length];
                for (var indice = 0; indice < substitutos.Length; indice++)
                    substitutos[indice] = materialEsmaecido;
                renderizador.sharedMaterials = substitutos;
            }
        }

        private void RestaurarTodos()
        {
            foreach (var par in materiaisOriginais)
            {
                if (par.Key != null)
                    par.Key.sharedMaterials = par.Value;
            }

            materiaisOriginais.Clear();
        }
    }
}
