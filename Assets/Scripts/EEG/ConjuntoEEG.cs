using System;
using UnityEngine;

namespace VRSNC.EEG
{
    /// <summary>
    /// Uma gravação carregada em arrays comuns, prontos para os laços de pintura e para os filtros
    /// (que rodam fora da thread principal). Os binários são little-endian, como em todas as
    /// plataformas do Unity, então basta copiar os bytes.
    /// </summary>
    public sealed class ConjuntoEEG
    {
        public DadosEEG Dados { get; private set; }
        public int Canais { get; private set; }
        public int Amostras { get; private set; }
        public int Quadros { get; private set; }
        public int Bandas { get; private set; }
        public int VerticesEscalpo { get; private set; }
        public float[][] Cubos { get; private set; }
        public float[] Sinais { get; private set; }
        public float[] PesosEscalpo { get; private set; }
        public float[] CoberturaEscalpo { get; private set; }
        public float[] PesosTopo { get; private set; }
        public float Duracao => Amostras / Dados.frequenciaAmostragem;

        public static ConjuntoEEG Carregar(DadosEEG dados, out string erro)
        {
            erro = null;
            if (dados == null)
            {
                erro = "Nenhum DadosEEG atribuído.";
                return null;
            }

            var conjunto = new ConjuntoEEG
            {
                Dados = dados,
                Canais = dados.canais.Length,
                Amostras = dados.amostras,
                Quadros = dados.quadros,
                Bandas = dados.bandas.Length,
                VerticesEscalpo = dados.escalpo != null ? dados.escalpo.vertexCount : 0,
            };

            try
            {
                conjunto.Cubos = new float[dados.cubos.Length][];
                for (var modo = 0; modo < dados.cubos.Length; modo++)
                    conjunto.Cubos[modo] = Ler(dados.cubos[modo], conjunto.Quadros * conjunto.Bandas * conjunto.Canais, "cubo " + modo);
                conjunto.Sinais = Ler(dados.sinais, conjunto.Canais * conjunto.Amostras, "sinais");
                conjunto.PesosEscalpo = Ler(dados.pesosEscalpo, conjunto.VerticesEscalpo * conjunto.Canais, "pesos do escalpo");
                conjunto.CoberturaEscalpo = Ler(dados.coberturaEscalpo, conjunto.VerticesEscalpo, "cobertura do escalpo");
                conjunto.PesosTopo = Ler(dados.pesosTopo, dados.gradeTopo * dados.gradeTopo * conjunto.Canais, "pesos do topoplot");
            }
            catch (Exception excecao)
            {
                erro = excecao.Message;
                return null;
            }

            return conjunto;
        }

        private static float[] Ler(TextAsset ativo, int esperado, string nome)
        {
            if (ativo == null)
                throw new InvalidOperationException($"EEG: binário ausente ({nome}).");
            var bytes = ativo.bytes;
            if (bytes.Length != esperado * sizeof(float))
                throw new InvalidOperationException($"EEG: {nome} tem {bytes.Length} bytes; esperado {esperado * sizeof(float)}.");
            var valores = new float[esperado];
            Buffer.BlockCopy(bytes, 0, valores, 0, bytes.Length);
            return valores;
        }

        /// <summary>Quadro do cubo que contém um instante. Os quadros são centrados em t0 + k·passo.</summary>
        public int QuadroEm(float tempo) => Mathf.Clamp(Mathf.RoundToInt((tempo - Dados.inicioQuadros) / Dados.passoQuadro), 0, Quadros - 1);

        /// <summary>Amostra do sinal que contém um instante (mapa de voltagem).</summary>
        public int AmostraEm(float tempo) => Mathf.Clamp(Mathf.RoundToInt(tempo * Dados.frequenciaAmostragem), 0, Amostras - 1);

        /// <summary>Os valores dos canais num quadro e numa banda (layout [quadro][banda][canal]).</summary>
        public void CopiarQuadro(int modo, int quadro, int banda, float[] destino)
        {
            var inicio = (quadro * Bandas + banda) * Canais;
            Array.Copy(Cubos[modo], inicio, destino, 0, Canais);
        }

        /// <summary>
        /// Campo interpolado: saída[i] = Σ pesos[i, c] · valores[c], preso entre o menor e o maior
        /// valor medido. A spline com λ = 0 passa exatamente por cada eletrodo, mas cria extremos entre
        /// eles que nenhum eletrodo mediu; preso, o mapa não mostra valor que não foi medido.
        /// </summary>
        public static void Interpolar(float[] pesos, float[] valores, float[] saida)
        {
            var canais = valores.Length;
            var minimo = float.PositiveInfinity;
            var maximo = float.NegativeInfinity;
            for (var c = 0; c < canais; c++)
            {
                if (valores[c] < minimo)
                    minimo = valores[c];
                if (valores[c] > maximo)
                    maximo = valores[c];
            }

            for (var i = 0; i < saida.Length; i++)
            {
                var base_ = i * canais;
                var soma = 0f;
                for (var c = 0; c < canais; c++)
                    soma += pesos[base_ + c] * valores[c];
                saida[i] = soma < minimo ? minimo : soma > maximo ? maximo : soma;
            }
        }
    }
}
