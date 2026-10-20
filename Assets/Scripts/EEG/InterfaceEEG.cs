using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRSNC.Interface;

namespace VRSNC.EEG
{
    /// <summary>
    /// Peças de UI montadas em execução para a aba EEG e as janelas do EEG, no tema "Grafite suave" do painel
    /// (<see cref="TemaVRSNC"/>): botão escuro arredondado sem contorno, realce que clareia ao apontar e rótulo que
    /// encolhe até 14 em vez de vazar.
    /// </summary>
    internal static class InterfaceEEG
    {
        public const int CamadaUI = 5;
        public const int FonteBotao = 16;
        public const int FonteMinima = 14;
        public static readonly Color CorTexto = TemaVRSNC.Texto;
        public static readonly Color CorTextoSecundario = TemaVRSNC.TextoSecundario;
        public static readonly Color CorTextoApagado = TemaVRSNC.TextoApagado;
        public static readonly Color CorBotao = TemaVRSNC.Botao;
        public static readonly Color CorAtivo = TemaVRSNC.Ativo;
        public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
        private static Font fonte;

        public static Font Fonte => fonte != null ? fonte : fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Retangulo(string nome, Transform pai)
        {
            var objeto = new GameObject(nome, typeof(RectTransform)) { layer = CamadaUI };
            objeto.transform.SetParent(pai, false);
            return (RectTransform)objeto.transform;
        }

        public static void Esticar(RectTransform alvo, float esquerda = 0f, float baixo = 0f, float direita = 0f, float cima = 0f)
        {
            alvo.anchorMin = Vector2.zero;
            alvo.anchorMax = Vector2.one;
            alvo.offsetMin = new Vector2(esquerda, baixo);
            alvo.offsetMax = new Vector2(-direita, -cima);
        }

        public static LayoutElement Tamanho(Component alvo, float altura = -1f, float largura = -1f, float larguraFlexivel = -1f, float alturaFlexivel = -1f)
        {
            var elemento = alvo.GetComponent<LayoutElement>();
            if (elemento == null)
                elemento = alvo.gameObject.AddComponent<LayoutElement>();
            if (altura >= 0f)
            {
                elemento.minHeight = altura;
                elemento.preferredHeight = altura;
            }

            if (largura >= 0f)
            {
                elemento.minWidth = largura;
                elemento.preferredWidth = largura;
            }

            elemento.flexibleWidth = larguraFlexivel;
            elemento.flexibleHeight = alturaFlexivel;
            return elemento;
        }

        public static HorizontalLayoutGroup Linha(Transform pai, string nome, float altura, float espaco = 6f)
        {
            var linha = Retangulo(nome, pai);
            var organizacao = linha.gameObject.AddComponent<HorizontalLayoutGroup>();
            organizacao.spacing = espaco;
            organizacao.childAlignment = TextAnchor.MiddleLeft;
            organizacao.childControlWidth = true;
            organizacao.childControlHeight = true;
            organizacao.childForceExpandWidth = false;
            organizacao.childForceExpandHeight = true;
            Tamanho(linha, altura);
            return organizacao;
        }

        public static VerticalLayoutGroup Coluna(Transform pai, string nome, float espaco = 3f)
        {
            var coluna = Retangulo(nome, pai);
            var organizacao = coluna.gameObject.AddComponent<VerticalLayoutGroup>();
            organizacao.spacing = espaco;
            organizacao.childControlWidth = true;
            organizacao.childControlHeight = true;
            organizacao.childForceExpandWidth = true;
            organizacao.childForceExpandHeight = false;
            return organizacao;
        }

        public static Text Texto(Transform pai, string nome, string conteudo, int tamanho, TextAnchor alinhamento, Color cor, FontStyle estilo = FontStyle.Normal, bool ajustar = false, int tamanhoMinimo = FonteMinima)
        {
            var retangulo = Retangulo(nome, pai);
            var texto = retangulo.gameObject.AddComponent<Text>();
            texto.font = Fonte;
            texto.text = conteudo;
            texto.fontSize = tamanho;
            texto.fontStyle = estilo;
            texto.alignment = alinhamento;
            texto.color = cor;
            texto.raycastTarget = false;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Truncate;
            texto.resizeTextForBestFit = ajustar;
            texto.resizeTextMinSize = Mathf.Min(tamanhoMinimo, tamanho);
            texto.resizeTextMaxSize = tamanho;
            return texto;
        }

        /// <summary>Botão do tema. <paramref name="transparente"/> deixa só o rótulo clicável (nomes das faixas dos traçados).</summary>
        public static Button Botao(Transform pai, string nome, string rotulo, Sprite sprite, UnityAction acao, int tamanho = FonteBotao, bool transparente = false)
        {
            var retangulo = Retangulo(nome, pai);
            var imagem = retangulo.gameObject.AddComponent<Image>();
            imagem.sprite = sprite;
            imagem.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            var botao = retangulo.gameObject.AddComponent<Button>();
            botao.targetGraphic = imagem;
            if (acao != null)
                botao.onClick.AddListener(acao);
            var texto = Texto(retangulo, "Text", rotulo, tamanho, TextAnchor.MiddleCenter, CorTexto, FontStyle.Normal, true);
            Esticar(texto.rectTransform, 3f, 1f, 3f, 1f);
            TemaVRSNC.AplicarBotao(botao, transparente ? TemaVRSNC.Estilo.Transparente : TemaVRSNC.Estilo.Normal);
            return botao;
        }

        public static void DefinirRotulo(Button botao, string rotulo)
        {
            var texto = botao != null ? botao.GetComponentInChildren<Text>(true) : null;
            if (texto != null && texto.text != rotulo)
                texto.text = rotulo;
        }

        /// <summary>Opção escolhida de um grupo: superfície azulada e negrito, como as abas.</summary>
        public static void Marcar(Button botao, bool ativo) => TemaVRSNC.Marcar(botao, ativo);

        public static Slider Deslizante(Transform pai, string nome, Sprite sprite, UnityAction<float> acao)
        {
            var raiz = Retangulo(nome, pai);
            var fundo = Retangulo("Fundo", raiz);
            Esticar(fundo, 0f, 6f, 0f, 6f);
            var imagemFundo = fundo.gameObject.AddComponent<Image>();
            imagemFundo.sprite = sprite;
            imagemFundo.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            imagemFundo.color = TemaVRSNC.Botao;
            var areaPreenchimento = Retangulo("Area_Preenchimento", raiz);
            Esticar(areaPreenchimento, 0f, 6f, 0f, 6f);
            var preenchimento = Retangulo("Preenchimento", areaPreenchimento);
            Esticar(preenchimento);
            var imagemPreenchimento = preenchimento.gameObject.AddComponent<Image>();
            imagemPreenchimento.sprite = sprite;
            imagemPreenchimento.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            imagemPreenchimento.color = TemaVRSNC.Destaque;
            imagemPreenchimento.raycastTarget = false;
            var areaPegador = Retangulo("Area_Pegador", raiz);
            Esticar(areaPegador, 7f, 0f, 7f, 0f);
            var pegador = Retangulo("Pegador", areaPegador);
            pegador.sizeDelta = new Vector2(14f, 0f);
            pegador.anchorMin = new Vector2(0f, 0f);
            pegador.anchorMax = new Vector2(0f, 1f);
            var imagemPegador = pegador.gameObject.AddComponent<Image>();
            imagemPegador.sprite = sprite;
            imagemPegador.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            imagemPegador.color = TemaVRSNC.Texto;
            var deslizante = raiz.gameObject.AddComponent<Slider>();
            deslizante.colors = TemaVRSNC.BlocoCores(deslizante.colors);
            deslizante.fillRect = preenchimento;
            deslizante.handleRect = pegador;
            deslizante.targetGraphic = imagemPegador;
            deslizante.direction = Slider.Direction.LeftToRight;
            if (acao != null)
                deslizante.onValueChanged.AddListener(acao);
            return deslizante;
        }

        public static string Numero(float valor, string formato = "0.0") => valor.ToString(formato, Cultura);

        public static string Lado(DadosEEG.Canal canal) => canal.linhaMedia ? "linha média" : canal.hemisferioEsquerdo ? "esquerdo" : "direito";

        public static string FormatarTempo(float segundos) => Numero(segundos) + " s";

        public static Sprite SpriteDe(Button modelo)
        {
            return modelo != null && modelo.targetGraphic is Image imagem ? imagem.sprite : null;
        }

        public static void Destruir(UnityEngine.Object objeto)
        {
            if (objeto == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(objeto);
            else
                UnityEngine.Object.DestroyImmediate(objeto);
        }

        public static T Exigir<T>(T valor, string nome) where T : class
        {
            if (valor == null)
                throw new InvalidOperationException("EEG: " + nome + " ausente.");
            return valor;
        }
    }
}
