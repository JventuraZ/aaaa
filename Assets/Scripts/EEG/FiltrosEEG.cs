using System;
using System.Collections.Generic;

namespace VRSNC.EEG
{
    /// <summary>
    /// Filtros de leitura do traçado, como no aparelho (web/src/lib/browser/filters.js do Protobrain):
    /// Butterworth de 2ª ordem em biquads (Audio EQ Cookbook), aplicados ida e volta (fase zero, como o
    /// filtfilt) com as bordas estendidas por reflexão ímpar. O corte de projeto é deslocado para que o
    /// valor escolhido seja o −3 dB do resultado da ida e volta. Padrão IFCN/ILAE: 0,5–70 Hz, notch desligado.
    /// </summary>
    public static class FiltrosEEG
    {
        public static readonly float[] OpcoesPassaAlta = { 0.3f, 0.5f, 1f, 1.6f, 5f };
        // 0 = sem passa-baixa.
        public static readonly float[] OpcoesPassaBaixa = { 15f, 35f, 45f, 70f, 0f };

        [Serializable]
        public struct Configuracao : IEquatable<Configuracao>
        {
            public float passaAlta;
            public float passaBaixa;
            public bool notch;

            public bool Equals(Configuracao outra) => passaAlta == outra.passaAlta && passaBaixa == outra.passaBaixa && notch == outra.notch;
            public override bool Equals(object obj) => obj is Configuracao outra && Equals(outra);
            public override int GetHashCode() => HashCode.Combine(passaAlta, passaBaixa, notch);
        }

        public static Configuracao Padrao => new() { passaAlta = 0.5f, passaBaixa = 70f, notch = false };

        private static readonly double QButterworth = Math.Sqrt(0.5);
        // Ida e volta elevam a resposta ao quadrado; este fator leva |H|² = 1/√2 para o corte escolhido.
        private static readonly double DeslocamentoFaseZero = Math.Pow(Math.Sqrt(2.0) - 1.0, 0.25);
        // Notch estreito: cerca de 2 Hz de largura a 60 Hz.
        private const double QNotch = 30.0;

        /// <summary>Seções de 2ª ordem [b0, b1, b2, a1, a2] (já normalizadas por a0).</summary>
        public static List<double[]> Projetar(Configuracao configuracao, double fs, double rede)
        {
            var secoes = new List<double[]>();
            if (configuracao.passaAlta > 0f)
                secoes.Add(PassaAlta(CorteDeProjeto(configuracao.passaAlta, fs, true), fs, QButterworth));
            if (configuracao.passaBaixa > 0f)
                secoes.Add(PassaBaixa(CorteDeProjeto(configuracao.passaBaixa, fs, false), fs, QButterworth));
            if (configuracao.notch && rede > 0)
            {
                // A rede e os harmônicos que sobram abaixo do passa-baixa (ou de Nyquist).
                var teto = Math.Min(configuracao.passaBaixa > 0f ? configuracao.passaBaixa : double.PositiveInfinity, fs / 2 - 1);
                for (var harmonico = rede; harmonico < teto; harmonico += rede)
                    secoes.Add(Notch(harmonico, fs, QNotch));
            }

            return secoes;
        }

        /// <summary>
        /// Filtra os canais inteiros (channel-major) e devolve uma cópia; o original fica intacto
        /// para a próxima troca. Thread-safe: pode rodar fora da thread principal.
        /// </summary>
        public static float[] Aplicar(float[] valores, int canais, int comprimento, double fs, Configuracao configuracao, double rede)
        {
            var secoes = Projetar(configuracao, fs, rede);
            var saida = new float[valores.Length];
            if (secoes.Count == 0)
            {
                Array.Copy(valores, saida, valores.Length);
                return saida;
            }

            var margem = (int)Math.Min(comprimento - 1, Math.Round(10 * fs));
            var trabalho = new double[comprimento + 2 * margem];
            for (var canal = 0; canal < canais; canal++)
            {
                var base_ = canal * comprimento;
                double primeiro = valores[base_];
                double ultimo = valores[base_ + comprimento - 1];
                for (var i = 0; i < margem; i++)
                {
                    trabalho[i] = 2 * primeiro - valores[base_ + margem - i];
                    trabalho[margem + comprimento + i] = 2 * ultimo - valores[base_ + comprimento - 2 - i];
                }

                for (var i = 0; i < comprimento; i++)
                    trabalho[margem + i] = valores[base_ + i];
                foreach (var secao in secoes)
                    Executar(trabalho, secao);
                Array.Reverse(trabalho);
                foreach (var secao in secoes)
                    Executar(trabalho, secao);
                Array.Reverse(trabalho);
                for (var i = 0; i < comprimento; i++)
                    saida[base_ + i] = (float)trabalho[margem + i];
            }

            return saida;
        }

        /// <summary>Uma passada de biquad, no lugar (forma direta II transposta).</summary>
        private static void Executar(double[] buffer, double[] coeficientes)
        {
            double b0 = coeficientes[0], b1 = coeficientes[1], b2 = coeficientes[2], a1 = coeficientes[3], a2 = coeficientes[4];
            double z1 = 0, z2 = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                var x = buffer[i];
                var y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                buffer[i] = y;
            }
        }

        /// <summary>A resposta do biquad segue tan(πf/fs); o deslocamento é feito nessa escala.</summary>
        private static double CorteDeProjeto(double frequencia, double fs, bool passaAlta)
        {
            var dobrado = Math.Tan(Math.PI * frequencia / fs);
            var alvo = passaAlta ? dobrado * DeslocamentoFaseZero : dobrado / DeslocamentoFaseZero;
            return fs / Math.PI * Math.Atan(alvo);
        }

        private static double[] Normalizados(double b0, double b1, double b2, double a0, double a1, double a2)
            => new[] { b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0 };

        private static double[] PassaBaixa(double frequencia, double fs, double q)
        {
            var w0 = 2 * Math.PI * frequencia / fs;
            var cos = Math.Cos(w0);
            var alfa = Math.Sin(w0) / (2 * q);
            return Normalizados((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alfa, -2 * cos, 1 - alfa);
        }

        private static double[] PassaAlta(double frequencia, double fs, double q)
        {
            var w0 = 2 * Math.PI * frequencia / fs;
            var cos = Math.Cos(w0);
            var alfa = Math.Sin(w0) / (2 * q);
            return Normalizados((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alfa, -2 * cos, 1 - alfa);
        }

        private static double[] Notch(double frequencia, double fs, double q)
        {
            var w0 = 2 * Math.PI * frequencia / fs;
            var cos = Math.Cos(w0);
            var alfa = Math.Sin(w0) / (2 * q);
            return Normalizados(1, -2 * cos, 1, 1 + alfa, -2 * cos, 1 - alfa);
        }

        public static string Rotulo(float frequencia) => frequencia <= 0f ? "desligado" : frequencia.ToString(frequencia < 1f || frequencia % 1f != 0f ? "0.0" : "0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + " Hz";
    }
}
