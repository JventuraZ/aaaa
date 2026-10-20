using UnityEngine;
using UnityEngine.UI;

namespace VRSNC.EEG
{
    /// <summary>
    /// Uma faixa do traçado: um segmento vertical (mínimo a máximo) por coluna, a envoltória real de cada
    /// intervalo. Sem isso, a página de 10 s teria ~8 amostras por coluna e o traçado dependeria de qual
    /// amostra caiu ali (aliasing). Coordenadas em unidades do canvas (1 unidade = 1 mm na janela).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GraficoFaixaEEG : MaskableGraphic
    {
        private const float LarguraColuna = 1.1f;
        private const float AlturaMinima = 0.9f;

        private float[] minimos = System.Array.Empty<float>();
        private float[] maximos = System.Array.Empty<float>();
        private int colunas;
        private float inicioX;
        private float passoX;

        /// <summary>Recebe os extremos já em coordenadas Y do canvas (o sinal invertido pela convenção "negativo para cima").</summary>
        public void Definir(float[] yMinimos, float[] yMaximos, int quantidade, float xInicial, float passo)
        {
            minimos = yMinimos;
            maximos = yMaximos;
            colunas = quantidade;
            inicioX = xInicial;
            passoX = passo;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var cor = (Color32)color;
            for (var coluna = 0; coluna < colunas; coluna++)
            {
                var x = inicioX + (coluna + 0.5f) * passoX;
                var baixo = Mathf.Min(minimos[coluna], maximos[coluna]);
                var cima = Mathf.Max(minimos[coluna], maximos[coluna]);
                if (cima - baixo < AlturaMinima)
                {
                    var meio = (cima + baixo) * 0.5f;
                    baixo = meio - AlturaMinima * 0.5f;
                    cima = meio + AlturaMinima * 0.5f;
                }

                var indice = vh.currentVertCount;
                var meiaLargura = LarguraColuna * 0.5f;
                vh.AddVert(new Vector3(x - meiaLargura, baixo), cor, Vector4.zero);
                vh.AddVert(new Vector3(x - meiaLargura, cima), cor, Vector4.zero);
                vh.AddVert(new Vector3(x + meiaLargura, cima), cor, Vector4.zero);
                vh.AddVert(new Vector3(x + meiaLargura, baixo), cor, Vector4.zero);
                vh.AddTriangle(indice, indice + 1, indice + 2);
                vh.AddTriangle(indice + 2, indice + 3, indice);
            }
        }
    }
}
