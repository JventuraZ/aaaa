using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRSNC.Interface;

namespace VRSNC.EEG
{
    /// <summary>
    /// Mapa 2D do EEG (topoplot) numa janela própria, como os traçados: mapa grande com o nome de cada eletrodo, barra de
    /// cores com limites e unidade, tocar/pausar com o tempo e a escolha da banda. Tocar num ponto do mapa escolhe o
    /// eletrodo mais próximo e mostra o giro equivalente, como tocar o eletrodo no escalpo.
    /// O mapa (256 px) é calculado numa Task; só o envio da textura fica na thread principal, para não pesar no quadro do Quest.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JanelaMapaEEG : MonoBehaviour, IPointerClickHandler
    {
        private const float Largura = 430f;
        private const float Altura = 520f;
        private const int LadoTextura = 256;
        private const float LadoMapa = 300f;
        private const float AlturaLinha = 30f;
        private const float AlturaBandas = 26f;
        private const float EspacoLinhas = 6f;
        private const float TopoMapa = AlturaLinha + EspacoLinhas + AlturaBandas + 8f;
        private const float LarguraBarraCores = 14f;
        // À esquerda do cérebro (longe o bastante para a cabeça do EEG não cobrir o mapa), na altura dos olhos; os
        // traçados ficam abaixo do cérebro.
        private const float AnguloPadrao = -44f;
        private const float DistanciaPadrao = 0.85f;
        private const float AlturaPadrao = 0.02f;
        // No mapa de voltagem o valor muda a cada amostra: 10 desenhos por segundo bastam para acompanhar.
        private const float IntervaloVoltagem = 0.1f;
        private const string Ressalva = "Espaço de sensores: não é a fonte do sinal nem diagnóstico.";
        private static readonly Color CorRotuloSelecionado = TemaVRSNC.Hex(0xFF9A9A);

        private ControladorEEG controlador;
        private Sprite sprite;
        private Action aoMudarAbertura;
        private MolduraJanelaEEG moldura;
        private TopoplotEEG topoplot;
        private RawImage imagemMapa;
        private RawImage barraCores;
        private Texture2D texturaBarra;
        private bool barraDivergente;
        private bool barraCriada;
        private Text textoMaximo;
        private Text textoMinimo;
        private Text textoUnidade;
        private Text textoTempo;
        private Button botaoTocar;
        private Button botaoNomes;
        private Button[] botoesBandas;
        private Text nomeEletrodo;
        private Text infoEletrodo;
        private Text[] rotulosEletrodos;
        private bool aberta;
        private bool nomesVisiveis = true;
        private bool pendente = true;
        private float ultimoDesenho = float.NegativeInfinity;
        private float[] valores;
        private float[] valoresCalculo;
        private EscalaMapa escalaCalculo;
        private Task tarefa;

        public bool Aberta => aberta;
        public bool NomesVisiveis => nomesVisiveis;
        /// <summary>Retângulo do mapa na janela (para testes e para converter o ponto tocado).</summary>
        public RectTransform AreaMapa => imagemMapa != null ? imagemMapa.rectTransform : null;
        public bool Desenhando => tarefa != null;

        public static JanelaMapaEEG Criar(ControladorEEG controlador, Sprite sprite, Action aoMudarAbertura)
        {
            var objeto = new GameObject("Janela_Mapa_EEG", typeof(RectTransform)) { layer = InterfaceEEG.CamadaUI };
            objeto.SetActive(false);
            var janela = objeto.AddComponent<JanelaMapaEEG>();
            janela.controlador = controlador;
            janela.sprite = sprite;
            janela.aoMudarAbertura = aoMudarAbertura;
            janela.Construir();
            return janela;
        }

        public void DefinirAberta(bool valor)
        {
            aberta = valor;
            if (valor)
                moldura.PosicionarSeForaDaVista(AnguloPadrao, DistanciaPadrao, AlturaPadrao);
            gameObject.SetActive(valor);
            pendente = true;
            aoMudarAbertura?.Invoke();
        }

        /// <summary>
        /// Escolhe o eletrodo mais próximo do ponto tocado, em coordenadas normalizadas do mapa (0..1, origem embaixo à
        /// esquerda). Devolve o canal escolhido, ou −1 quando o toque caiu longe de qualquer eletrodo.
        /// </summary>
        public int TocarNoMapa(Vector2 normalizado)
        {
            if (controlador == null || topoplot == null)
                return -1;
            var canal = topoplot.CanalMaisProximo(topoplot.DeNormalizado(normalizado), 0.06f);
            if (canal >= 0)
                controlador.SelecionarCanal(canal, true);
            return canal;
        }

        /// <summary>Clique (gatilho ou pinça) sobre o mapa: só vale quando o raio atingiu a imagem do mapa.</summary>
        public void OnPointerClick(PointerEventData dados)
        {
            if (imagemMapa == null || dados.pointerPressRaycast.gameObject != imagemMapa.gameObject)
                return;
            var retangulo = imagemMapa.rectTransform;
            var local = (Vector2)retangulo.InverseTransformPoint(dados.pointerPressRaycast.worldPosition);
            var area = retangulo.rect;
            TocarNoMapa(new Vector2((local.x - area.xMin) / area.width, (local.y - area.yMin) / area.height));
        }

        private void OnEnable()
        {
            if (controlador == null)
                return;
            controlador.MapaAlterado += AoMudarMapa;
            controlador.EstadoAlterado += AoMudarEstado;
            controlador.TempoAlterado += AoMudarTempo;
            AoMudarEstado();
            AoMudarTempo();
        }

        private void OnDisable()
        {
            if (controlador == null)
                return;
            controlador.MapaAlterado -= AoMudarMapa;
            controlador.EstadoAlterado -= AoMudarEstado;
            controlador.TempoAlterado -= AoMudarTempo;
        }

        private void OnDestroy()
        {
            if (topoplot != null)
                Destroy(topoplot.Textura);
            if (texturaBarra != null)
                Destroy(texturaBarra);
        }

        private void AoMudarMapa() => pendente = true;

        private void AoMudarTempo()
        {
            if (textoTempo != null)
                textoTempo.text = InterfaceEEG.Numero(controlador.Tempo) + " / " + InterfaceEEG.Numero(controlador.Duracao, "0") + " s";
        }

        private void AoMudarEstado()
        {
            if (botaoTocar == null)
                return;
            InterfaceEEG.DefinirRotulo(botaoTocar, controlador.Tocando ? "Pausar" : "Tocar");
            var voltagem = controlador.Mapa == ControladorEEG.TipoMapa.Voltagem;
            for (var indice = 0; indice < botoesBandas.Length; indice++)
                InterfaceEEG.Marcar(botoesBandas[indice], !voltagem && indice == controlador.Banda);
            InterfaceEEG.Marcar(botaoNomes, nomesVisiveis);
            var dados = controlador.Dados;
            var modo = voltagem ? "Voltagem" : dados.rotulosBandas[controlador.Banda].Split(' ')[0] + " · " + dados.escalas[controlador.Modo].rotulo;
            moldura.Titulo.text = "Mapa 2D · " + modo;
            pendente = true;
        }

        private void LateUpdate()
        {
            if (!aberta || controlador == null)
                return;
            if (tarefa != null)
            {
                if (!tarefa.IsCompleted)
                    return;
                var erro = tarefa.Exception;
                tarefa = null;
                if (erro != null)
                    Debug.LogError("[EEG] Falha ao desenhar o mapa 2D: " + erro.GetBaseException().Message);
                else
                {
                    topoplot.Enviar();
                    AtualizarBarra(escalaCalculo);
                }
            }

            var podeDesenhar = controlador.Mapa != ControladorEEG.TipoMapa.Voltagem || Time.unscaledTime - ultimoDesenho >= IntervaloVoltagem;
            if (!pendente || !podeDesenhar)
                return;
            pendente = false;
            ultimoDesenho = Time.unscaledTime;
            escalaCalculo = controlador.ValoresMapa(valores);
            Array.Copy(valores, valoresCalculo, valores.Length);
            var pesos = controlador.Conjunto.PesosTopo;
            var escala = escalaCalculo;
            var canal = controlador.CanalSelecionado;
            var copia = valoresCalculo;
            var mapa = topoplot;
            tarefa = Task.Run(() => mapa.Calcular(pesos, copia, escala, canal));
            AtualizarEletrodo();
        }

        private void AtualizarBarra(EscalaMapa escala)
        {
            if (!barraCriada || barraDivergente != escala.divergente)
            {
                barraDivergente = escala.divergente;
                barraCriada = true;
                // Barra vertical: o menor valor embaixo, o maior em cima.
                texturaBarra.SetPixels32(MapaCoresEEG.Tabela(escala.divergente));
                texturaBarra.Apply(false);
            }

            var limites = MapaCoresEEG.Limites(escala);
            var formato = Mathf.Abs(limites.y - limites.x) >= 10f ? "0" : "0.0";
            textoMinimo.text = InterfaceEEG.Numero(limites.x, formato);
            textoMaximo.text = InterfaceEEG.Numero(limites.y, formato);
            textoUnidade.text = escala.unidade;
        }

        private void AtualizarEletrodo()
        {
            var dados = controlador.Dados;
            var selecionado = controlador.CanalSelecionado;
            for (var canal = 0; canal < rotulosEletrodos.Length; canal++)
            {
                var rotulo = rotulosEletrodos[canal];
                var escolhido = canal == selecionado;
                rotulo.fontStyle = escolhido ? FontStyle.Bold : FontStyle.Normal;
                rotulo.color = escolhido ? CorRotuloSelecionado : dados.canais[canal].interpolado ? TemaVRSNC.TextoSecundario : TemaVRSNC.Texto;
                rotulo.gameObject.SetActive(nomesVisiveis || escolhido);
            }

            if (selecionado < 0)
            {
                nomeEletrodo.text = "Nenhum eletrodo";
                infoEletrodo.text = "Toque num ponto do mapa ou num eletrodo do escalpo.";
                return;
            }

            var info = dados.canais[selecionado];
            var regiao = dados.RegiaoDe(selecionado);
            var voltagem = controlador.Mapa == ControladorEEG.TipoMapa.Voltagem;
            var unidade = voltagem ? "µV" : dados.escalas[controlador.Modo].unidade;
            nomeEletrodo.text = info.nome + (info.interpolado ? "*" : "") + " · " + InterfaceEEG.Numero(valores[selecionado], voltagem ? "0.0" : "0.00") + " " + unidade;
            var texto = new StringBuilder();
            if (regiao != null && regiao.chave != "unknown")
                texto.Append(regiao.nome).Append(info.linhaMedia ? " (LM)" : info.hemisferioEsquerdo ? " (E)" : " (D)").Append(" · ").Append(regiao.lobo);
            else
                texto.Append("Parede medial, sem giro");
            if (info.brodmann > 0)
                texto.Append(" · BA ").Append(info.brodmann);
            infoEletrodo.text = texto.ToString();
        }

        private void AlternarNomes()
        {
            nomesVisiveis = !nomesVisiveis;
            InterfaceEEG.Marcar(botaoNomes, nomesVisiveis);
            AtualizarEletrodo();
        }

        private void Construir()
        {
            var dados = controlador.Dados;
            valores = new float[dados.canais.Length];
            valoresCalculo = new float[dados.canais.Length];
            topoplot = new TopoplotEEG(dados, LadoTextura);
            moldura = MolduraJanelaEEG.Construir(gameObject, new Vector2(Largura, Altura), sprite, "Mapa 2D", () => DefinirAberta(false));
            var corpo = moldura.Corpo;

            // Reprodução: tocar/pausar, tempo e o botão dos nomes dos eletrodos.
            var reproducao = InterfaceEEG.Linha(corpo, "Linha_Reproducao", AlturaLinha, 6f);
            MolduraJanelaEEG.Prender((RectTransform)reproducao.transform, 0f, AlturaLinha, 0f);
            botaoTocar = InterfaceEEG.Botao(reproducao.transform, "Botao_Tocar", "Tocar", sprite, controlador.AlternarReproducao);
            InterfaceEEG.Tamanho(botaoTocar, -1f, 80f);
            textoTempo = InterfaceEEG.Texto(reproducao.transform, "Tempo", "", 16, TextAnchor.MiddleCenter, TemaVRSNC.Texto, FontStyle.Normal, true);
            InterfaceEEG.Tamanho(textoTempo, -1f, -1f, 1f);
            botaoNomes = InterfaceEEG.Botao(reproducao.transform, "Botao_Nomes", "Nomes", sprite, AlternarNomes);
            InterfaceEEG.Tamanho(botaoNomes, -1f, 80f);

            // Bandas (nomes curtos).
            var bandas = InterfaceEEG.Linha(corpo, "Linha_Bandas", AlturaBandas, 4f);
            MolduraJanelaEEG.Prender((RectTransform)bandas.transform, AlturaLinha + EspacoLinhas, AlturaBandas, 0f);
            botoesBandas = new Button[dados.bandas.Length];
            for (var indice = 0; indice < dados.bandas.Length; indice++)
            {
                var banda = indice;
                botoesBandas[indice] = InterfaceEEG.Botao(bandas.transform, "Banda_" + dados.bandas[indice], dados.rotulosBandas[indice].Split(' ')[0], sprite, () => controlador.DefinirBanda(banda));
                InterfaceEEG.Tamanho(botoesBandas[indice], -1f, -1f, 1f);
            }

            // Mapa: quadrado de 300 mm, com o nome de cada eletrodo por cima.
            var quadro = InterfaceEEG.Retangulo("Mapa", corpo);
            quadro.anchorMin = quadro.anchorMax = new Vector2(0f, 1f);
            quadro.pivot = new Vector2(0f, 1f);
            quadro.sizeDelta = new Vector2(LadoMapa, LadoMapa);
            quadro.anchoredPosition = new Vector2(0f, -TopoMapa);
            imagemMapa = quadro.gameObject.AddComponent<RawImage>();
            imagemMapa.texture = topoplot.Textura;
            // O raio de interface precisa acertar o mapa para o toque escolher o eletrodo.
            imagemMapa.raycastTarget = true;
            rotulosEletrodos = new Text[dados.canais.Length];
            for (var canal = 0; canal < dados.canais.Length; canal++)
            {
                var info = dados.canais[canal];
                var rotulo = InterfaceEEG.Texto(quadro, "Rotulo_" + info.nome, info.nome + (info.interpolado ? "*" : ""), 12, TextAnchor.LowerCenter, TemaVRSNC.Texto);
                rotulo.horizontalOverflow = HorizontalWrapMode.Overflow;
                rotulo.verticalOverflow = VerticalWrapMode.Overflow;
                var sombra = rotulo.gameObject.AddComponent<Shadow>();
                sombra.effectColor = new Color(0f, 0f, 0f, 0.75f);
                sombra.effectDistance = new Vector2(1f, -1f);
                var retangulo = rotulo.rectTransform;
                retangulo.anchorMin = retangulo.anchorMax = topoplot.ParaNormalizado(info.topo);
                retangulo.pivot = new Vector2(0.5f, 0f);
                retangulo.sizeDelta = new Vector2(40f, 14f);
                retangulo.anchoredPosition = new Vector2(0f, 4f);
                rotulosEletrodos[canal] = rotulo;
            }

            // Barra de cores vertical à direita do mapa, com máximo em cima, mínimo embaixo e a unidade no meio.
            var colunaBarra = LadoMapa + 12f;
            var barra = InterfaceEEG.Retangulo("Barra_Cores", corpo);
            barra.anchorMin = barra.anchorMax = new Vector2(0f, 1f);
            barra.pivot = new Vector2(0f, 1f);
            barra.sizeDelta = new Vector2(LarguraBarraCores, LadoMapa);
            barra.anchoredPosition = new Vector2(colunaBarra, -TopoMapa);
            barraCores = barra.gameObject.AddComponent<RawImage>();
            barraCores.raycastTarget = false;
            texturaBarra = new Texture2D(1, 256, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, name = "Barra_Cores_Mapa_EEG" };
            barraCores.texture = texturaBarra;
            textoMaximo = TextoLateral(corpo, "Maximo", colunaBarra + LarguraBarraCores + 6f, -TopoMapa, TextAnchor.UpperLeft);
            textoUnidade = TextoLateral(corpo, "Unidade", colunaBarra + LarguraBarraCores + 6f, -TopoMapa - LadoMapa * 0.5f + 9f, TextAnchor.MiddleLeft);
            textoMinimo = TextoLateral(corpo, "Minimo", colunaBarra + LarguraBarraCores + 6f, -TopoMapa - LadoMapa + 18f, TextAnchor.LowerLeft);

            // Eletrodo escolhido e a ressalva de leitura, embaixo do mapa.
            var baseMapa = TopoMapa + LadoMapa + 8f;
            nomeEletrodo = TextoLinha(corpo, "Nome_Eletrodo", baseMapa, 22f, 17, TemaVRSNC.Texto, FontStyle.Bold);
            infoEletrodo = TextoLinha(corpo, "Info_Eletrodo", baseMapa + 24f, 20f, 14, TemaVRSNC.TextoSecundario, FontStyle.Normal);
            TextoLinha(corpo, "Ressalva", baseMapa + 46f, 18f, 13, TemaVRSNC.TextoApagado, FontStyle.Normal).text = Ressalva;
        }

        private static Text TextoLateral(RectTransform pai, string nome, float x, float y, TextAnchor alinhamento)
        {
            var texto = InterfaceEEG.Texto(pai, nome, "", 14, alinhamento, TemaVRSNC.TextoSecundario);
            texto.horizontalOverflow = HorizontalWrapMode.Overflow;
            var retangulo = texto.rectTransform;
            retangulo.anchorMin = retangulo.anchorMax = new Vector2(0f, 1f);
            retangulo.pivot = new Vector2(0f, 1f);
            retangulo.sizeDelta = new Vector2(64f, 18f);
            retangulo.anchoredPosition = new Vector2(x, y);
            return texto;
        }

        private static Text TextoLinha(RectTransform pai, string nome, float topo, float altura, int tamanho, Color cor, FontStyle estilo)
        {
            var texto = InterfaceEEG.Texto(pai, nome, "", tamanho, TextAnchor.MiddleLeft, cor, estilo, true);
            MolduraJanelaEEG.Prender(texto.rectTransform, topo, altura, 0f);
            return texto;
        }
    }
}
