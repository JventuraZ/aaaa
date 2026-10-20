using System;
using UnityEngine;

namespace VRSNC.EEG
{
    /// <summary>
    /// Gravação de EEG gerada pelo pipeline do Protobrain (Python/MNE) e importada pelo menu
    /// Ferramentas > VR SNC > EEG. Guarda o manifest já tipado e os binários como TextAsset.
    /// Coordenadas no "referencial do sujeito", em metros: x = direita, y = superior, z = anterior.
    /// </summary>
    public sealed class DadosEEG : ScriptableObject
    {
        [Serializable]
        public sealed class Canal
        {
            public string nome;
            public Vector3 posicao;
            public Vector3 normal;
            public bool interpolado;
            // Posição no topoplot 2D do EEGLAB (cabeça de raio 0,5, nariz para cima).
            public Vector2 topo;
            public float desvioUv;
            // Córtex sob o eletrodo no fsaverage: região Desikan, área de Brodmann e lado.
            public int regiao;
            public int brodmann;
            public bool hemisferioEsquerdo;
            public bool linhaMedia;
            public float distanciaCortexMm;
        }

        [Serializable]
        public sealed class Regiao
        {
            public string chave;
            public string nome;
            public string lobo;
            public string funcao;
        }

        [Serializable]
        public sealed class AreaBrodmann
        {
            public int numero;
            public string funcao;
        }

        [Serializable]
        public sealed class Escala
        {
            public string modo;
            public string rotulo;
            public float vmin;
            public float vmax;
            public bool divergente;
            public string unidade;
            // Limites por banda (só na potência absoluta); vazio quando a escala é única.
            public float[] vminPorBanda = Array.Empty<float>();
            public float[] vmaxPorBanda = Array.Empty<float>();
        }

        [Serializable]
        public sealed class Evento
        {
            public float tempo;
            public int codigo;
        }

        [Header("Origem")]
        public string arquivoOrigem;
        public string gravadoEm;
        public float frequenciaAmostragem;
        public float duracaoSegundos;
        public string referencia;
        public Vector2 filtroAnalise;
        public string montagemEletrodos;
        public float frequenciaRede;
        public string resumoIca;
        public string[] canaisInterpolados = Array.Empty<string>();
        public string pastaImportada;
        public string importadoEmUtc;

        [Header("Canais e anatomia")]
        public Canal[] canais = Array.Empty<Canal>();
        public Regiao[] regioes = Array.Empty<Regiao>();
        public AreaBrodmann[] areasBrodmann = Array.Empty<AreaBrodmann>();

        [Header("Bandas e quadros")]
        public string[] bandas = Array.Empty<string>();
        public string[] rotulosBandas = Array.Empty<string>();
        public Vector2[] faixasBandas = Array.Empty<Vector2>();
        public int quadros;
        public float passoQuadro;
        public float janelaQuadro;
        public float inicioQuadros;
        // Um cubo [quadro][banda][canal] por normalização, na mesma ordem das escalas.
        public Escala[] escalas = Array.Empty<Escala>();
        public TextAsset[] cubos = Array.Empty<TextAsset>();

        [Header("Sinal de leitura")]
        // [canal][amostra], µV, referência média, só o passa-alta de 0,1 Hz.
        public TextAsset sinais;
        public int amostras;
        public Evento[] eventos = Array.Empty<Evento>();

        [Header("Escalpo e topoplot")]
        public Mesh escalpo;
        // Spline esférica: [vértice][canal].
        public TextAsset pesosEscalpo;
        public TextAsset coberturaEscalpo;
        public TextAsset pesosTopo;
        public int gradeTopo;
        public float extensaoTopo;
        public float raioCabecaTopo;
        public float intradTopo;

        [Header("Córtex do fsaverage (para o alinhamento)")]
        public Vector3 cortexMinimo;
        public Vector3 cortexMaximo;

        public int QuantidadeCanais => canais.Length;

        public int IndiceModo(string modo)
        {
            for (var indice = 0; indice < escalas.Length; indice++)
            {
                if (escalas[indice].modo == modo)
                    return indice;
            }

            return 0;
        }

        public int IndiceBanda(string banda)
        {
            var indice = Array.IndexOf(bandas, banda);
            return indice < 0 ? 0 : indice;
        }

        public int IndiceCanal(string nome)
        {
            for (var indice = 0; indice < canais.Length; indice++)
            {
                if (string.Equals(canais[indice].nome, nome, StringComparison.OrdinalIgnoreCase))
                    return indice;
            }

            return -1;
        }

        public Regiao RegiaoDe(int canal)
        {
            var indice = canal >= 0 && canal < canais.Length ? canais[canal].regiao : -1;
            return indice >= 0 && indice < regioes.Length ? regioes[indice] : null;
        }

        public string FuncaoBrodmann(int numero)
        {
            foreach (var area in areasBrodmann)
            {
                if (area.numero == numero)
                    return area.funcao;
            }

            return null;
        }

        /// <summary>Escala efetiva de uma normalização numa banda ("absoluta" tem limites por banda).</summary>
        public EscalaMapa EscalaPara(int modo, int banda)
        {
            var escala = escalas[Mathf.Clamp(modo, 0, escalas.Length - 1)];
            var resultado = new EscalaMapa { vmin = escala.vmin, vmax = escala.vmax, divergente = escala.divergente, unidade = escala.unidade };
            if (banda >= 0 && banda < escala.vminPorBanda.Length && banda < escala.vmaxPorBanda.Length)
            {
                resultado.vmin = escala.vminPorBanda[banda];
                resultado.vmax = escala.vmaxPorBanda[banda];
            }

            return resultado;
        }
    }

    /// <summary>Faixa de cores de um mapa: limites, polaridade e unidade.</summary>
    public struct EscalaMapa
    {
        public float vmin;
        public float vmax;
        public bool divergente;
        public string unidade;

        public static EscalaMapa Voltagem(float faixaUv) => new() { vmin = -faixaUv, vmax = faixaUv, divergente = true, unidade = "µV" };
    }
}
