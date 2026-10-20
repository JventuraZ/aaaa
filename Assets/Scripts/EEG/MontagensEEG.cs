using System.Collections.Generic;

namespace VRSNC.EEG
{
    /// <summary>
    /// Montagens de leitura (ACNS Guideline 3), como em web/src/lib/browser/montages.js do Protobrain:
    /// referência média em ordem de leitura (de anterior para posterior, esquerda antes da direita) e
    /// dupla banana bipolar de 18 canais, com a esquerda acima da direita. O sinal já chega em referência
    /// média; numa derivação A−B a referência se cancela.
    /// </summary>
    public static class MontagensEEG
    {
        public sealed class Faixa
        {
            public string rotulo;
            // Índices dos eletrodos (no manifest) que compõem a faixa.
            public int[] eletrodos;
            public bool interpolada;
        }

        public sealed class Resultado
        {
            public float[] valores;
            public Faixa[] faixas;
            public int comprimento;
            public bool duplaBanana;
        }

        private static readonly string[] OrdemMedia =
        {
            "Fp1", "Fp2",
            "AF3", "AF4",
            "F7", "F3", "Fz", "F4", "F8",
            "FC5", "FC1", "FC2", "FC6",
            "T7", "C3", "Cz", "C4", "T8",
            "CP5", "CP1", "CP2", "CP6",
            "P7", "P3", "Pz", "P4", "P8",
            "PO3", "PO4",
            "O1", "Oz", "O2",
        };

        private static readonly string[][] DuplaBanana =
        {
            new[] { "Fp1", "F7" }, new[] { "F7", "T7" }, new[] { "T7", "P7" }, new[] { "P7", "O1" },
            new[] { "Fp1", "F3" }, new[] { "F3", "C3" }, new[] { "C3", "P3" }, new[] { "P3", "O1" },
            new[] { "Fz", "Cz" }, new[] { "Cz", "Pz" },
            new[] { "Fp2", "F4" }, new[] { "F4", "C4" }, new[] { "C4", "P4" }, new[] { "P4", "O2" },
            new[] { "Fp2", "F8" }, new[] { "F8", "T8" }, new[] { "T8", "P8" }, new[] { "P8", "O2" },
        };

        /// <summary>Monta as faixas do navegador a partir do sinal filtrado (canais em referência média, channel-major).</summary>
        public static Resultado Derivar(bool duplaBanana, float[] valores, DadosEEG dados, int comprimento)
        {
            var indice = new Dictionary<string, int>();
            for (var canal = 0; canal < dados.canais.Length; canal++)
                indice[dados.canais[canal].nome] = canal;
            var pares = new List<(int mais, int menos, string rotulo)>();
            if (duplaBanana)
            {
                foreach (var par in DuplaBanana)
                {
                    if (indice.TryGetValue(par[0], out var a) && indice.TryGetValue(par[1], out var b))
                        pares.Add((a, b, par[0] + "-" + par[1]));
                }
            }
            else
            {
                foreach (var nome in OrdemMedia)
                {
                    if (indice.TryGetValue(nome, out var a))
                        pares.Add((a, -1, nome));
                }

                // Canais fora da ordem conhecida vão para o fim, para nenhum sumir do traçado.
                for (var canal = 0; canal < dados.canais.Length; canal++)
                {
                    if (System.Array.IndexOf(OrdemMedia, dados.canais[canal].nome) < 0)
                        pares.Add((canal, -1, dados.canais[canal].nome));
                }
            }

            var dadosFaixas = new float[pares.Count * comprimento];
            var faixas = new Faixa[pares.Count];
            for (var faixa = 0; faixa < pares.Count; faixa++)
            {
                var (mais, menos, rotulo) = pares[faixa];
                var destino = faixa * comprimento;
                var origemA = mais * comprimento;
                if (menos < 0)
                {
                    System.Array.Copy(valores, origemA, dadosFaixas, destino, comprimento);
                }
                else
                {
                    var origemB = menos * comprimento;
                    for (var i = 0; i < comprimento; i++)
                        dadosFaixas[destino + i] = valores[origemA + i] - valores[origemB + i];
                }

                var eletrodos = menos < 0 ? new[] { mais } : new[] { mais, menos };
                var interpolada = false;
                foreach (var eletrodo in eletrodos)
                    interpolada |= dados.canais[eletrodo].interpolado;
                faixas[faixa] = new Faixa { rotulo = rotulo, eletrodos = eletrodos, interpolada = interpolada };
            }

            return new Resultado { valores = dadosFaixas, faixas = faixas, comprimento = comprimento, duplaBanana = duplaBanana };
        }
    }
}
