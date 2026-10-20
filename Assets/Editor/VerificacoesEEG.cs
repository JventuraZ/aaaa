using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRSNC.EEG;

namespace VRSNC.FerramentasEditor
{
    /// <summary>
    /// Verificações determinísticas do EEG no Modo de Edição: dados importados íntegros, interpolação presa
    /// na faixa medida e exata em campo constante, filtros de leitura em sinais sintéticos, rampas de cor,
    /// montagens e a ligação de cada eletrodo a um giro do modelo na cena.
    /// </summary>
    public static class VerificacoesEEG
    {
        [MenuItem("Ferramentas/VR SNC/EEG/Validar EEG (Modo de Edição)", false, 310)]
        private static void ValidarPeloMenu()
        {
            Validar();
            Debug.Log("[EEG] APROVADO: dados, interpolação, filtros, cores, montagens e ligações aos giros.");
        }

        public static void Validar()
        {
            var dados = AssetDatabase.LoadAssetAtPath<DadosEEG>(InstaladorEEG.CaminhoDados);
            Exigir(dados != null, "DadosEEG.asset ausente: rode Ferramentas > VR SNC > EEG > Importar dados do Protobrain");
            var conjunto = ConjuntoEEG.Carregar(dados, out var erro);
            Exigir(conjunto != null, "a gravação não carrega: " + erro);
            Exigir(conjunto.Canais == 32, $"esperados 32 canais, há {conjunto.Canais}");
            Exigir(dados.escalas.Length == conjunto.Cubos.Length && dados.escalas.Length >= 1, "escalas e cubos em número diferente");
            Exigir(dados.canais.All(canal => !float.IsNaN(canal.posicao.x)), "posição de eletrodo inválida");

            // Interpolação: dentro da faixa medida e exata num campo constante (a spline reproduz constantes).
            var valores = new float[conjunto.Canais];
            var campo = new float[conjunto.VerticesEscalpo];
            foreach (var quadro in new[] { 0, conjunto.Quadros / 2, conjunto.Quadros - 1 })
            {
                conjunto.CopiarQuadro(0, quadro, dados.IndiceBanda("alpha"), valores);
                ConjuntoEEG.Interpolar(conjunto.PesosEscalpo, valores, campo);
                Exigir(campo.Min() >= valores.Min() - 1e-4f && campo.Max() <= valores.Max() + 1e-4f, $"o campo do quadro {quadro} saiu da faixa medida");
            }

            for (var canal = 0; canal < valores.Length; canal++)
                valores[canal] = 5f;
            var somaLinha = new float[conjunto.VerticesEscalpo];
            for (var v = 0; v < conjunto.VerticesEscalpo; v++)
            {
                var soma = 0f;
                for (var canal = 0; canal < conjunto.Canais; canal++)
                    soma += conjunto.PesosEscalpo[v * conjunto.Canais + canal];
                somaLinha[v] = soma;
            }

            Exigir(somaLinha.All(soma => Mathf.Abs(soma - 1f) < 0.02f), "os pesos da spline não somam 1 (o campo constante não seria reproduzido)");

            // Quadro e amostra nas pontas.
            Exigir(conjunto.QuadroEm(-5f) == 0 && conjunto.QuadroEm(1e6f) == conjunto.Quadros - 1, "quadro fora da gravação não foi preso às pontas");
            Exigir(conjunto.AmostraEm(0f) == 0 && conjunto.AmostraEm(conjunto.Duracao + 1f) == conjunto.Amostras - 1, "amostra fora da gravação não foi presa às pontas");

            VerificarFiltros();
            VerificarCores();
            VerificarReservaMao();
            VerificarToqueNoMapa(dados);

            // Montagens.
            var media = MontagensEEG.Derivar(false, conjunto.Sinais, dados, conjunto.Amostras);
            var banana = MontagensEEG.Derivar(true, conjunto.Sinais, dados, conjunto.Amostras);
            Exigir(media.faixas.Length == 32 && media.faixas[0].rotulo == "Fp1" && media.faixas[1].rotulo == "Fp2", "referência média fora da ordem de leitura");
            Exigir(banana.faixas.Length == 18 && banana.faixas[0].rotulo == "Fp1-F7", "dupla banana sem as 18 derivações");
            var fp1 = dados.IndiceCanal("Fp1");
            var f7 = dados.IndiceCanal("F7");
            Exigir(Mathf.Abs(banana.valores[100] - (conjunto.Sinais[fp1 * conjunto.Amostras + 100] - conjunto.Sinais[f7 * conjunto.Amostras + 100])) < 1e-4f, "Fp1-F7 não é a diferença dos dois canais");

            // Correspondência Desikan → modelo para os 32 eletrodos.
            foreach (var canal in dados.canais)
            {
                var regiao = dados.regioes[canal.regiao];
                Exigir(regiao.chave == "unknown" || CorrespondenciaGirosEEG.TentarObter(regiao.chave, out _, out _), "região sem equivalência no modelo: " + regiao.chave);
            }

            // Na cena: o controlador existe e cada eletrodo aponta um giro do modelo.
            var controlador = UnityEngine.Object.FindFirstObjectByType<ControladorEEG>(FindObjectsInactive.Include);
            Exigir(controlador != null, "Controlador_EEG ausente na cena: rode Ferramentas > VR SNC > EEG > Instalar EEG na cena");
            Exigir(controlador.Dados == dados, "o controlador da cena usa outra gravação");
            var ligados = Enumerable.Range(0, dados.canais.Length).Count(canal => controlador.LigacaoDe(canal)?.no != null);
            Exigir(ligados == dados.canais.Length, $"só {ligados}/{dados.canais.Length} eletrodos ligados a um giro");
            Exigir(controlador.LigacaoDe(fp1).no.NomeLegivel.StartsWith("Giro frontal médio", StringComparison.OrdinalIgnoreCase), "Fp1 deveria cair no giro frontal médio (Desikan: frontal médio rostral)");
            Exigir(controlador.MaterialEscalpo != null && controlador.MaterialEletrodo != null && controlador.MaterialLigacao != null, "materiais do EEG ausentes");
        }

        /// <summary>Sinal sintético a 512 Hz: DC + 10 Hz + 60 Hz. Os filtros de leitura devem tirar o DC, manter 10 Hz e o notch tirar a rede.</summary>
        private static void VerificarFiltros()
        {
            const int fs = 512;
            const int n = fs * 30;
            var sinal = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)fs;
                sinal[i] = 100f + 20f * Mathf.Sin(2f * Mathf.PI * 10f * t) + 10f * Mathf.Sin(2f * Mathf.PI * 60f * t);
            }

            var padrao = FiltrosEEG.Aplicar(sinal, 1, n, fs, FiltrosEEG.Padrao, 60);
            var comNotch = FiltrosEEG.Aplicar(sinal, 1, n, fs, new FiltrosEEG.Configuracao { passaAlta = 0.5f, passaBaixa = 70f, notch = true }, 60);
            var trecho = Enumerable.Range(n / 3, n / 3);
            var media = trecho.Average(i => padrao[i]);
            Exigir(Mathf.Abs((float)media) < 1f, $"o passa-alta deixou DC ({media:0.00})");
            var amplitude10 = Amplitude(padrao, trecho, 10f, fs);
            Exigir(amplitude10 > 19f && amplitude10 < 21f, $"10 Hz deveria passar intacto ({amplitude10:0.0} de 20)");
            var rede = Amplitude(comNotch, trecho, 60f, fs);
            Exigir(rede < 1f, $"o notch deixou {rede:0.0} de 10 em 60 Hz");
            var semPassaBaixa = FiltrosEEG.Aplicar(sinal, 1, n, fs, new FiltrosEEG.Configuracao { passaAlta = 0.5f, passaBaixa = 15f, notch = false }, 60);
            Exigir(Amplitude(semPassaBaixa, trecho, 60f, fs) < 1f, "o passa-baixa de 15 Hz deixou passar 60 Hz");
            // −3 dB exatamente no corte escolhido, como no aparelho (ida e volta).
            var corte = new float[n];
            for (var i = 0; i < n; i++)
                corte[i] = Mathf.Sin(2f * Mathf.PI * 70f * i / fs);
            var noCorte = Amplitude(FiltrosEEG.Aplicar(corte, 1, n, fs, new FiltrosEEG.Configuracao { passaAlta = 0f, passaBaixa = 70f, notch = false }, 60), trecho, 70f, fs);
            Exigir(Mathf.Abs(noCorte - 0.7071f) < 0.03f, $"o corte de 70 Hz não está em −3 dB ({noCorte:0.000})");
        }

        private static float Amplitude(float[] sinal, System.Collections.Generic.IEnumerable<int> trecho, float frequencia, int fs)
        {
            double seno = 0, cosseno = 0;
            var quantidade = 0;
            foreach (var i in trecho)
            {
                var fase = 2 * Math.PI * frequencia * i / fs;
                seno += sinal[i] * Math.Sin(fase);
                cosseno += sinal[i] * Math.Cos(fase);
                quantidade++;
            }

            return (float)(2 * Math.Sqrt(seno * seno + cosseno * cosseno) / quantidade);
        }

        private static void VerificarCores()
        {
            var divergente = MapaCoresEEG.Tabela(true);
            var meio = divergente[127];
            // Polo neutro da rampa divergente: #383835, nunca um matiz.
            Exigir(Mathf.Abs(meio.r - 0x38) <= 2 && Mathf.Abs(meio.g - 0x38) <= 2 && Mathf.Abs(meio.b - 0x35) <= 3, $"o meio da rampa divergente não é o cinza neutro ({meio})");
            var escala = new EscalaMapa { vmin = -2f, vmax = 2f, divergente = true };
            Exigir(Mathf.Approximately(MapaCoresEEG.Normalizar(0f, escala), 0.5f), "zero não cai no meio da rampa divergente");
            var sequencial = MapaCoresEEG.Tabela(false);
            Exigir(sequencial[0].b < sequencial[255].b && sequencial[0].r < sequencial[255].r, "a rampa sequencial não vai do escuro ao claro");
        }

        /// <summary>
        /// Com a mão apontando um eletrodo, a anatomia atrás não pode receber a pinça dessa mão: a reserva
        /// vale para a raiz da mão e seus filhos (os interatores), não para a outra mão, e é liberada ao sair.
        /// </summary>
        private static void VerificarReservaMao()
        {
            // HideAndDontSave: objetos de teste não sujam nem entram na cena.
            var esquerda = EditorUtility.CreateGameObjectWithHideFlags("Mao_Teste_Esquerda", HideFlags.HideAndDontSave);
            var direita = EditorUtility.CreateGameObjectWithHideFlags("Mao_Teste_Direita", HideFlags.HideAndDontSave);
            try
            {
                var interator = EditorUtility.CreateGameObjectWithHideFlags("NearFar_Teste", HideFlags.HideAndDontSave).transform;
                interator.SetParent(esquerda.transform, false);
                Interacao.PonteInteracaoMao.DefinirMaoReservada(esquerda.transform, true);
                Exigir(Interacao.PonteInteracaoMao.MaoReservada(interator), "o interator da mão reservada não ficou reservado");
                Exigir(!Interacao.PonteInteracaoMao.MaoReservada(direita.transform), "reservar uma mão reservou a outra");
                Interacao.PonteInteracaoMao.DefinirMaoReservada(esquerda.transform, false);
                Exigir(!Interacao.PonteInteracaoMao.MaoReservada(interator), "a reserva da mão não foi liberada");
                Exigir(!Interacao.PonteInteracaoMao.MaoReservada((Transform)null), "transform nulo não pode contar como mão reservada");
            }
            finally
            {
                Interacao.PonteInteracaoMao.DefinirMaoReservada(esquerda.transform, false);
                UnityEngine.Object.DestroyImmediate(esquerda);
                UnityEngine.Object.DestroyImmediate(direita);
            }
        }

        /// <summary>
        /// Mapa 2D na janela: tocar exatamente sobre cada eletrodo (posição normalizada na textura, convertida de volta)
        /// escolhe o próprio eletrodo, e tocar fora da cabeça não escolhe nenhum. A reserva da mão também conta pedidos.
        /// </summary>
        private static void VerificarToqueNoMapa(DadosEEG dados)
        {
            var mapa = new TopoplotEEG(dados, 64);
            try
            {
                for (var canal = 0; canal < dados.canais.Length; canal++)
                {
                    var normalizado = mapa.ParaNormalizado(dados.canais[canal].topo);
                    var escolhido = mapa.CanalMaisProximo(mapa.DeNormalizado(normalizado), 0.06f);
                    Exigir(escolhido == canal, $"tocar no mapa sobre {dados.canais[canal].nome} escolheu {(escolhido >= 0 ? dados.canais[escolhido].nome : "nada")}");
                }

                Exigir(mapa.CanalMaisProximo(mapa.DeNormalizado(new Vector2(0.02f, 0.02f)), 0.06f) < 0, "tocar no canto do mapa, fora da cabeça, não pode escolher eletrodo");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mapa.Textura);
            }

            var mao = EditorUtility.CreateGameObjectWithHideFlags("Mao_Teste_Contagem", HideFlags.HideAndDontSave);
            try
            {
                Interacao.PonteInteracaoMao.DefinirMaoReservada(mao.transform, true);
                Interacao.PonteInteracaoMao.DefinirMaoReservada(mao.transform, true);
                Interacao.PonteInteracaoMao.DefinirMaoReservada(mao.transform, false);
                Exigir(Interacao.PonteInteracaoMao.MaoReservada(mao.transform), "com dois pedidos de reserva, liberar um não pode liberar a mão");
                Interacao.PonteInteracaoMao.DefinirMaoReservada(mao.transform, false);
                Exigir(!Interacao.PonteInteracaoMao.MaoReservada(mao.transform), "liberados os dois pedidos, a mão deveria ficar livre");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mao);
            }
        }

        private static void Exigir(bool condicao, string mensagem)
        {
            if (!condicao)
                throw new InvalidDataException("EEG: " + mensagem + ".");
        }
    }
}
