using UnityEngine;

namespace VRSNC.EEG
{
    /// <summary>
    /// As duas rampas do Protobrain (web/src/lib/colormap.js), interpoladas em OKLab:
    /// divergente (azul, cinza neutro, vermelho) para valores com polaridade, como o z e a voltagem,
    /// e sequencial (azul escuro a claro) para magnitudes. Nunca arco-íris.
    /// As tabelas estão em sRGB; quem desenha converte para linear quando precisa.
    /// </summary>
    public static class MapaCoresEEG
    {
        private const int Tamanho = 256;
        private static readonly string[] Divergente = { "#3987e5", "#383835", "#e66767" };
        private static readonly string[] Sequencial = { "#0d366b", "#184f95", "#256abf", "#3987e5", "#6da7ec", "#9ec5f4", "#cde2fb" };
        private static Color32[] tabelaDivergente;
        private static Color32[] tabelaSequencial;

        public static Color32[] Tabela(bool divergente)
        {
            if (divergente)
                return tabelaDivergente ??= Construir(Divergente);
            return tabelaSequencial ??= Construir(Sequencial);
        }

        /// <summary>Normaliza para 0..1 dentro da escala; o ganho aproxima os extremos do centro.</summary>
        public static float Normalizar(float valor, EscalaMapa escala, float ganho = 1f)
        {
            if (escala.divergente)
            {
                var limite = Mathf.Max(Mathf.Abs(escala.vmin), Mathf.Abs(escala.vmax)) / ganho;
                return Mathf.Clamp01((valor / (limite > 0f ? limite : 1f) + 1f) * 0.5f);
            }

            var amplitude = (escala.vmax - escala.vmin) / ganho;
            return Mathf.Clamp01((valor - escala.vmin) / (amplitude != 0f ? amplitude : 1f));
        }

        public static Color32 Cor(float valor, EscalaMapa escala, float ganho = 1f)
        {
            var tabela = Tabela(escala.divergente);
            return tabela[Mathf.Clamp(Mathf.RoundToInt(Normalizar(valor, escala, ganho) * (Tamanho - 1)), 0, Tamanho - 1)];
        }

        /// <summary>Extremos efetivos da escala, já com o ganho aplicado.</summary>
        public static Vector2 Limites(EscalaMapa escala, float ganho = 1f)
        {
            if (escala.divergente)
            {
                var limite = Mathf.Max(Mathf.Abs(escala.vmin), Mathf.Abs(escala.vmax)) / ganho;
                return new Vector2(-limite, limite);
            }

            return new Vector2(escala.vmin, escala.vmin + (escala.vmax - escala.vmin) / ganho);
        }

        private static Color32[] Construir(string[] paradas)
        {
            var lab = new Vector3[paradas.Length];
            for (var indice = 0; indice < paradas.Length; indice++)
            {
                ColorUtility.TryParseHtmlString(paradas[indice], out var cor);
                lab[indice] = ParaOklab(cor.r, cor.g, cor.b);
            }

            var segmentos = lab.Length - 1;
            var tabela = new Color32[Tamanho];
            for (var i = 0; i < Tamanho; i++)
            {
                var posicao = i / (float)(Tamanho - 1) * segmentos;
                var indice = Mathf.Min(segmentos - 1, Mathf.FloorToInt(posicao));
                var rgb = DeOklab(Vector3.LerpUnclamped(lab[indice], lab[indice + 1], posicao - indice));
                tabela[i] = new Color32((byte)Mathf.RoundToInt(rgb.x * 255f), (byte)Mathf.RoundToInt(rgb.y * 255f), (byte)Mathf.RoundToInt(rgb.z * 255f), 255);
            }

            return tabela;
        }

        private static float ParaLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float DeLinear(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;

        private static float Cbrt(float x) => x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);

        private static Vector3 ParaOklab(float r, float g, float b)
        {
            var lr = ParaLinear(r);
            var lg = ParaLinear(g);
            var lb = ParaLinear(b);
            var l = Cbrt(0.4122214708f * lr + 0.5363325363f * lg + 0.0514459929f * lb);
            var m = Cbrt(0.2119034982f * lr + 0.6806995451f * lg + 0.1073969566f * lb);
            var s = Cbrt(0.0883024619f * lr + 0.2817188376f * lg + 0.6299787005f * lb);
            return new Vector3(
                0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        private static Vector3 DeOklab(Vector3 lab)
        {
            var l = Mathf.Pow(lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z, 3f);
            var m = Mathf.Pow(lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z, 3f);
            var s = Mathf.Pow(lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z, 3f);
            var r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            var g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            var b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
            return new Vector3(Mathf.Clamp01(DeLinear(r)), Mathf.Clamp01(DeLinear(g)), Mathf.Clamp01(DeLinear(b)));
        }
    }
}
