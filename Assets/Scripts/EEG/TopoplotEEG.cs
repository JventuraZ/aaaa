using System.Collections.Generic;
using UnityEngine;

namespace VRSNC.EEG
{
    /// <summary>
    /// Topoplot 2D nas convenções do EEGLAB, como em web/src/lib/topo2d.js do Protobrain: projeção azimutal,
    /// cabeça no raio 0,5 com nariz para cima e esquerda do sujeito à esquerda, mapa interpolado até intrad
    /// (2% além do eletrodo mais externo) e 6 isolinhas igualmente espaçadas na escala de cor.
    /// Desenhado numa textura sRGB. Tudo o que não muda entre quadros (posição de cada pixel na grade,
    /// contorno da cabeça, nariz, orelhas e pontos dos eletrodos) é calculado uma vez no construtor,
    /// para o desenho caber no orçamento do Quest. <see cref="Calcular"/> não usa a API do Unity e pode rodar fora da
    /// thread principal; <see cref="Enviar"/> manda os pixels para a textura e precisa da thread principal.
    /// </summary>
    public sealed class TopoplotEEG
    {
        private const int Isolinhas = 6;
        private static readonly float[] OrelhaX = { 0.492f, 0.510f, 0.518f, 0.5299f, 0.5419f, 0.54f, 0.547f, 0.532f, 0.510f, 0.484f };
        private static readonly float[] OrelhaY = { 0.0555f, 0.0775f, 0.0783f, 0.0746f, 0.0555f, -0.0055f, -0.0932f, -0.1313f, -0.1384f, -0.1199f };
        private static readonly Vector2[] Nariz = { new(0.09f, 0.4954f), new(0.02f, 0.57f), new(0f, 0.575f), new(-0.02f, 0.57f), new(-0.09f, 0.4954f) };
        // Tinta clara: o topoplot fica sobre o fundo grafite da janela.
        private static readonly Color32 Contorno = new(0xae, 0xb6, 0xc2, 255);
        private static readonly Color32 Isolinha = new(0, 0, 0, 90);
        private static readonly Color32 PontoEletrodo = new(255, 255, 255, 255);
        private static readonly Color32 BordaEletrodo = new(0x20, 0x24, 0x2c, 255);
        private static readonly Color32 Selecionado = new(0xe6, 0x67, 0x67, 255);
        private static readonly Color32 Interpolado = new(0x89, 0x87, 0x81, 255);
        private static readonly Color32 Transparente = new(0, 0, 0, 0);

        private readonly DadosEEG dados;
        private readonly int lado;
        private readonly float alcance;
        private readonly float[] campoGrade;
        private readonly float[] campoPixel;
        private readonly Color32[] pixels;
        // Por pixel dentro de intrad: índice do canto da célula da grade e pesos da bilinear; −1 fora do mapa.
        private readonly int[] celula;
        private readonly float[] pesoColuna;
        private readonly float[] pesoLinha;
        // Contorno da cabeça, nariz e orelhas: pixels e cores, desenhados por cima a cada quadro.
        private readonly List<int> indicesContorno = new();
        private readonly List<Color32> coresContorno = new();

        public Texture2D Textura { get; }

        public TopoplotEEG(DadosEEG dados, int lado = 160)
        {
            this.dados = dados;
            this.lado = lado;
            // Cabe a cabeça inteira (o nariz chega a 0,575), com folga.
            alcance = Mathf.Max(dados.extensaoTopo, 0.58f) * 1.08f;
            campoGrade = new float[dados.gradeTopo * dados.gradeTopo];
            campoPixel = new float[lado * lado];
            pixels = new Color32[lado * lado];
            celula = new int[lado * lado];
            pesoColuna = new float[lado * lado];
            pesoLinha = new float[lado * lado];
            Textura = new Texture2D(lado, lado, TextureFormat.RGBA32, false, false)
            {
                name = "Topoplot_EEG",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            // As tabelas de cor são montadas na primeira chamada: aqui, na thread principal, antes de qualquer cálculo em segundo plano.
            MapaCoresEEG.Tabela(true);
            MapaCoresEEG.Tabela(false);
            PreCalcularGrade();
            PreCalcularContorno();
        }

        /// <summary>Pixel → coordenadas do topoplot (x à direita, y para cima, cabeça de raio 0,5).</summary>
        public Vector2 ParaTopo(int px, int py) => new(((px + 0.5f) / lado * 2f - 1f) * alcance, ((py + 0.5f) / lado * 2f - 1f) * alcance);

        /// <summary>Coordenadas do topoplot → posição normalizada 0..1 na textura (para pôr rótulos por cima).</summary>
        public Vector2 ParaNormalizado(Vector2 topo) => new((topo.x / alcance + 1f) * 0.5f, (topo.y / alcance + 1f) * 0.5f);

        /// <summary>Posição normalizada 0..1 na textura → coordenadas do topoplot (o inverso de <see cref="ParaNormalizado"/>).</summary>
        public Vector2 DeNormalizado(Vector2 normalizado) => new((normalizado.x * 2f - 1f) * alcance, (normalizado.y * 2f - 1f) * alcance);

        /// <summary>Eletrodo mais próximo de um ponto do topoplot, até <paramref name="raioMaximo"/> (na escala da cabeça de raio 0,5); −1 se nenhum.</summary>
        public int CanalMaisProximo(Vector2 topo, float raioMaximo = 0.05f)
        {
            var melhor = -1;
            var menor = raioMaximo * raioMaximo;
            for (var canal = 0; canal < dados.canais.Length; canal++)
            {
                var distancia = (dados.canais[canal].topo - topo).sqrMagnitude;
                if (distancia <= menor)
                {
                    menor = distancia;
                    melhor = canal;
                }
            }

            return melhor;
        }

        private void PreCalcularGrade()
        {
            var grade = dados.gradeTopo;
            var extensao = dados.extensaoTopo;
            var passo = 2f * extensao / (grade - 1);
            for (var py = 0; py < lado; py++)
            {
                for (var px = 0; px < lado; px++)
                {
                    var indice = py * lado + px;
                    var p = ParaTopo(px, py);
                    if (p.magnitude > dados.intradTopo)
                    {
                        celula[indice] = -1;
                        continue;
                    }

                    // Linha 0 da grade no topo (y = extensão), coluna 0 à esquerda (x = −extensão).
                    var fc = Mathf.Clamp((p.x + extensao) / passo, 0f, grade - 1.001f);
                    var fl = Mathf.Clamp((extensao - p.y) / passo, 0f, grade - 1.001f);
                    var c0 = (int)fc;
                    var l0 = (int)fl;
                    celula[indice] = l0 * grade + c0;
                    pesoColuna[indice] = fc - c0;
                    pesoLinha[indice] = fl - l0;
                }
            }
        }

        private void PreCalcularContorno()
        {
            var camada = new Color32[lado * lado];
            Circulo(camada, dados.raioCabecaTopo, Contorno, 1.6f);
            Polilinha(camada, Nariz, Contorno);
            var orelhaEsquerda = new Vector2[OrelhaX.Length];
            var orelhaDireita = new Vector2[OrelhaX.Length];
            for (var i = 0; i < OrelhaX.Length; i++)
            {
                orelhaDireita[i] = new Vector2(OrelhaX[i], OrelhaY[i]);
                orelhaEsquerda[i] = new Vector2(-OrelhaX[i], OrelhaY[i]);
            }

            Polilinha(camada, orelhaEsquerda, Contorno);
            Polilinha(camada, orelhaDireita, Contorno);
            for (var indice = 0; indice < camada.Length; indice++)
            {
                if (camada[indice].a == 0)
                    continue;
                indicesContorno.Add(indice);
                coresContorno.Add(camada[indice]);
            }
        }

        /// <summary>Calcula e já envia para a textura (thread principal).</summary>
        public void Desenhar(float[] pesos, float[] valores, EscalaMapa escala, int canalSelecionado)
        {
            Calcular(pesos, valores, escala, canalSelecionado);
            Enviar();
        }

        /// <summary>Envia para a textura os pixels do último <see cref="Calcular"/>. Só na thread principal.</summary>
        public void Enviar()
        {
            Textura.SetPixels32(pixels);
            Textura.Apply(false);
        }

        /// <summary>
        /// Interpola o campo, pinta os pixels, as isolinhas, o contorno e os eletrodos no buffer interno. Não toca a API
        /// do Unity: pode rodar numa Task, desde que nada mais use esta instância até o fim.
        /// </summary>
        public void Calcular(float[] pesos, float[] valores, EscalaMapa escala, int canalSelecionado)
        {
            ConjuntoEEG.Interpolar(pesos, valores, campoGrade);
            var grade = dados.gradeTopo;
            var tabela = MapaCoresEEG.Tabela(escala.divergente);
            var limites = MapaCoresEEG.Limites(escala);
            var degrau = (limites.y - limites.x) / (Isolinhas + 1);
            for (var indice = 0; indice < pixels.Length; indice++)
            {
                var canto = celula[indice];
                if (canto < 0)
                {
                    campoPixel[indice] = float.NaN;
                    pixels[indice] = Transparente;
                    continue;
                }

                var tc = pesoColuna[indice];
                var tl = pesoLinha[indice];
                var cima = campoGrade[canto] + (campoGrade[canto + 1] - campoGrade[canto]) * tc;
                var baixo = campoGrade[canto + grade] + (campoGrade[canto + grade + 1] - campoGrade[canto + grade]) * tc;
                var valor = cima + (baixo - cima) * tl;
                campoPixel[indice] = valor;
                pixels[indice] = tabela[Mathf.Clamp((int)(MapaCoresEEG.Normalizar(valor, escala) * 255f + 0.5f), 0, 255)];
            }

            // Isolinhas: onde a faixa de nível muda entre um pixel e o vizinho da esquerda ou de baixo.
            if (degrau > 0f)
            {
                var inverso = 1f / degrau;
                for (var py = 1; py < lado; py++)
                {
                    for (var px = 1; px < lado; px++)
                    {
                        var indice = py * lado + px;
                        var v = campoPixel[indice];
                        var esquerda = campoPixel[indice - 1];
                        var abaixo = campoPixel[indice - lado];
                        if (float.IsNaN(v) || float.IsNaN(esquerda) || float.IsNaN(abaixo))
                            continue;
                        var nivel = Mathf.FloorToInt((v - limites.x) * inverso);
                        if (nivel != Mathf.FloorToInt((esquerda - limites.x) * inverso) || nivel != Mathf.FloorToInt((abaixo - limites.x) * inverso))
                            pixels[indice] = Misturar(pixels[indice], Isolinha);
                    }
                }
            }

            for (var i = 0; i < indicesContorno.Count; i++)
                pixels[indicesContorno[i]] = Misturar(pixels[indicesContorno[i]], coresContorno[i]);

            // Eletrodos: ponto branco com borda; o escolhido ganha anel vermelho.
            for (var canal = 0; canal < dados.canais.Length; canal++)
            {
                var info = dados.canais[canal];
                var escolhido = canal == canalSelecionado;
                var escala2 = lado / 256f;
                Disco(pixels, info.topo, (escolhido ? 5f : 3.2f) * escala2, BordaEletrodo);
                Disco(pixels, info.topo, (escolhido ? 3.6f : 2.2f) * escala2, info.interpolado ? Interpolado : PontoEletrodo);
                if (escolhido)
                    Anel(pixels, info.topo, 6.5f * escala2, 1.4f, Selecionado);
            }
        }

        private static Color32 Misturar(Color32 fundo, Color32 tinta)
        {
            if (tinta.a == 255)
                return tinta;
            var a = tinta.a / 255f;
            return new Color32((byte)(fundo.r + (tinta.r - fundo.r) * a), (byte)(fundo.g + (tinta.g - fundo.g) * a), (byte)(fundo.b + (tinta.b - fundo.b) * a), (byte)Mathf.Max(fundo.a, tinta.a));
        }

        private Vector2 ParaPixel(Vector2 topo) => new((topo.x / alcance + 1f) * 0.5f * lado - 0.5f, (topo.y / alcance + 1f) * 0.5f * lado - 0.5f);

        private void Pintar(Color32[] destino, int px, int py, Color32 cor)
        {
            if (px < 0 || py < 0 || px >= lado || py >= lado)
                return;
            destino[py * lado + px] = Misturar(destino[py * lado + px], cor);
        }

        private void Disco(Color32[] destino, Vector2 topo, float raioPixels, Color32 cor)
        {
            var centro = ParaPixel(topo);
            var r = Mathf.CeilToInt(raioPixels);
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    var px = Mathf.RoundToInt(centro.x) + dx;
                    var py = Mathf.RoundToInt(centro.y) + dy;
                    if (new Vector2(px - centro.x, py - centro.y).sqrMagnitude <= raioPixels * raioPixels)
                        Pintar(destino, px, py, cor);
                }
            }
        }

        private void Anel(Color32[] destino, Vector2 topo, float raioPixels, float espessura, Color32 cor)
        {
            var centro = ParaPixel(topo);
            var r = Mathf.CeilToInt(raioPixels + espessura);
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    var px = Mathf.RoundToInt(centro.x) + dx;
                    var py = Mathf.RoundToInt(centro.y) + dy;
                    var distancia = new Vector2(px - centro.x, py - centro.y).magnitude;
                    if (Mathf.Abs(distancia - raioPixels) <= espessura * 0.5f)
                        Pintar(destino, px, py, cor);
                }
            }
        }

        private void Circulo(Color32[] destino, float raio, Color32 cor, float espessura)
        {
            var raioPixels = raio / alcance * 0.5f * lado;
            for (var py = 0; py < lado; py++)
            {
                for (var px = 0; px < lado; px++)
                {
                    var dx = px + 0.5f - lado * 0.5f;
                    var dy = py + 0.5f - lado * 0.5f;
                    if (Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - raioPixels) <= espessura * 0.5f)
                        Pintar(destino, px, py, cor);
                }
            }
        }

        private void Polilinha(Color32[] destino, Vector2[] pontos, Color32 cor)
        {
            for (var i = 1; i < pontos.Length; i++)
            {
                var a = ParaPixel(pontos[i - 1]);
                var b = ParaPixel(pontos[i]);
                var passos = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f) + 1;
                for (var k = 0; k <= passos; k++)
                {
                    var p = Vector2.Lerp(a, b, k / (float)passos);
                    Pintar(destino, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), cor);
                }
            }
        }
    }
}
