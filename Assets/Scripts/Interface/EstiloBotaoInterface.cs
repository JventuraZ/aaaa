using UnityEngine;
using UnityEngine.UI;

namespace VRSNC.Interface
{
    /// <summary>
    /// Aparência comum dos botões dos painéis: um botão inativo fica translúcido, com o rótulo
    /// esmaecido, para não parecer a ação principal da tela. A cor de inativo é a do tema (com o multiplicador do
    /// bloco de cores vira a cor-base a 40%).
    /// </summary>
    public static class EstiloBotaoInterface
    {
        public static readonly Color CorDesativado = TemaVRSNC.CorDesativadoBloco;
        private const float OpacidadeRotuloDesativado = 0.4f;

        /// <summary>Aplica a cor de inativo e ajusta a opacidade dos rótulos ao estado atual do botão.</summary>
        public static void AtualizarAparencia(Button botao)
        {
            if (botao == null)
                return;
            var cores = botao.colors;
            if (cores.disabledColor != CorDesativado)
            {
                cores.disabledColor = CorDesativado;
                botao.colors = cores;
            }

            DefinirOpacidadeRotulos(botao, botao.interactable ? 1f : OpacidadeRotuloDesativado);
        }

        /// <summary>Esmaece os rótulos de um botão que continua clicável, mas indica conteúdo indisponível.</summary>
        public static void EsmaecerRotulos(Button botao)
        {
            DefinirOpacidadeRotulos(botao, OpacidadeRotuloDesativado + 0.15f);
        }

        private static void DefinirOpacidadeRotulos(Button botao, float opacidade)
        {
            if (botao == null)
                return;
            foreach (var rotulo in botao.GetComponentsInChildren<Graphic>(true))
            {
                if (rotulo == botao.targetGraphic)
                    continue;
                var cor = rotulo.color;
                cor.a = opacidade;
                rotulo.color = cor;
            }
        }
    }
}
