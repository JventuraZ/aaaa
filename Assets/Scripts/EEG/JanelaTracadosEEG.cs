using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRSNC.Interface;

namespace VRSNC.EEG
{
    /// <summary>
    /// Navegador de traçados do EEG numa janela própria, nas convenções da leitura clínica (IFCN/ILAE, ACNS),
    /// como o TraceBrowser do Protobrain: negativo para cima, sensibilidade única em µV/mm para todas as faixas
    /// (7 µV/mm no adulto), filtros de fase zero 0,5–70 Hz, referência média ou dupla banana, página de 10 s.
    /// O cursor segue o mesmo tempo do escalpo. Tocar o nome de uma faixa escolhe o eletrodo.
    /// Na janela, 1 unidade do canvas = 1 mm, então µV/mm vira unidades por µV direto. A moldura
    /// (<see cref="MolduraJanelaEEG"/>) dá o título, o Fechar e a alça para mover a janela.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JanelaTracadosEEG : MonoBehaviour
    {
        private const float Largura = 760f;
        private const float Altura = 540f;
        private const float Calha = 72f;
        private const float AlturaComandos = 30f;
        private const float AlturaFiltros = 28f;
        private const float EspacoLinhas = 6f;
        private const float MargemTopo = AlturaComandos + EspacoLinhas + AlturaFiltros + 10f;
        private const float MargemBase = 26f;
        private const float MargemDireita = 12f;
        private const float SegundosPagina = 10f;
        private const int FaixasMaximas = 32;
        // Abaixo do cérebro e um pouco à esquerda, inclinada para os olhos, como uma tela de mesa.
        private const float AnguloPadrao = -5f;
        private const float DistanciaPadrao = 1.0f;
        private const float AlturaPadrao = -0.52f;
        private static readonly float[] Sensibilidades = { 1f, 2f, 3f, 5f, 7f, 10f, 15f, 20f, 30f, 50f, 70f, 100f };
        private static readonly Color Tinta = TemaVRSNC.Texto;
        private static readonly Color Apagado = TemaVRSNC.TextoSecundario;
        private static readonly Color Traco = new(0.62f, 0.773f, 0.957f, 0.8f);
        private static readonly Color TracoSelecionado = new(0.902f, 0.404f, 0.404f, 1f);
        private static readonly Color Grade = new(1f, 1f, 1f, 0.07f);
        private static readonly Color CorEvento = new(0.878f, 0.698f, 0.322f, 1f);

        private ControladorEEG controlador;
        private Sprite sprite;
        private Action aoMudarAbertura;
        private MolduraJanelaEEG moldura;
        private RectTransform area;
        private readonly GraficoFaixaEEG[] faixas = new GraficoFaixaEEG[FaixasMaximas];
        private readonly Button[] rotulos = new Button[FaixasMaximas];
        private readonly float[][] baixos = new float[FaixasMaximas][];
        private readonly float[][] altos = new float[FaixasMaximas][];
        private RectTransform cursor;
        private RectTransform barraCalibracao;
        private Text textoCalibracao;
        private readonly Text[] marcasTempo = new Text[11];
        private readonly RectTransform[] linhasGrade = new RectTransform[11];
        private RectTransform marcadorEvento;
        private Text textoEvento;
        private Button botaoMedia;
        private Button botaoBanana;
        private Text textoSensibilidade;
        private Button botaoPassaAlta;
        private Button botaoPassaBaixa;
        private Button botaoNotch;
        private Text textoSituacao;
        private Text titulo;
        private float inicioPagina = -1f;
        private bool aberta;
        private int colunas;

        public bool Aberta => aberta;
        public float InicioPagina => inicioPagina;
        public int FaixasVisiveis => controlador != null && controlador.Faixas != null ? controlador.Faixas.faixas.Length : 0;

        public static JanelaTracadosEEG Criar(ControladorEEG controlador, Sprite sprite, Action aoMudarAbertura)
        {
            var objeto = new GameObject("Janela_Tracados_EEG", typeof(RectTransform)) { layer = InterfaceEEG.CamadaUI };
            objeto.SetActive(false);
            var janela = objeto.AddComponent<JanelaTracadosEEG>();
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
            if (valor)
                Reconstruir();
            aoMudarAbertura?.Invoke();
        }

        private void OnEnable()
        {
            if (controlador == null)
                return;
            controlador.TempoAlterado += AoMudarTempo;
            controlador.SinaisAlterados += Reconstruir;
            controlador.EstadoAlterado += AoMudarEstado;
        }

        private void OnDisable()
        {
            if (controlador == null)
                return;
            controlador.TempoAlterado -= AoMudarTempo;
            controlador.SinaisAlterados -= Reconstruir;
            controlador.EstadoAlterado -= AoMudarEstado;
        }

        private void AoMudarTempo()
        {
            var tempo = controlador.Tempo;
            if (tempo < inicioPagina || tempo >= inicioPagina + SegundosPagina)
                Reconstruir();
            else
                AtualizarCursor();
        }

        private void AoMudarEstado()
        {
            AtualizarControles();
            AtualizarCores();
        }

        // ---------------------------------------------------------------- desenho

        private void Reconstruir()
        {
            if (!aberta || controlador == null || controlador.Faixas == null)
                return;
            var resultado = controlador.Faixas;
            var quantidade = Mathf.Min(FaixasMaximas, resultado.faixas.Length);
            var fs = controlador.Dados.frequenciaAmostragem;
            var duracao = controlador.Duracao;
            inicioPagina = Mathf.Clamp(Mathf.Floor(controlador.Tempo / SegundosPagina) * SegundosPagina, 0f, Mathf.Max(0f, duracao - SegundosPagina));
            var largura = area.rect.width;
            var altura = area.rect.height;
            var alturaFaixa = altura / quantidade;
            var unidadesPorUv = 1f / controlador.Sensibilidade;
            var amostrasPorColuna = SegundosPagina * fs / colunas;
            var amostraInicial = inicioPagina * fs;
            var comprimento = resultado.comprimento;
            var passo = largura / colunas;
            for (var faixa = 0; faixa < FaixasMaximas; faixa++)
            {
                var ativa = faixa < quantidade;
                faixas[faixa].gameObject.SetActive(ativa);
                rotulos[faixa].gameObject.SetActive(ativa);
                if (!ativa)
                    continue;
                var centro = altura - (faixa + 0.5f) * alturaFaixa;
                var limite = alturaFaixa * 3f;
                var base_ = faixa * comprimento;
                for (var coluna = 0; coluna < colunas; coluna++)
                {
                    var de = Mathf.Clamp(Mathf.FloorToInt(amostraInicial + coluna * amostrasPorColuna), 0, comprimento - 1);
                    var ate = Mathf.Clamp(Mathf.FloorToInt(amostraInicial + (coluna + 1) * amostrasPorColuna), de + 1, comprimento);
                    var minimo = float.PositiveInfinity;
                    var maximo = float.NegativeInfinity;
                    for (var i = de; i < ate; i++)
                    {
                        var valor = resultado.valores[base_ + i];
                        if (valor < minimo)
                            minimo = valor;
                        if (valor > maximo)
                            maximo = valor;
                    }

                    // Negativo para cima: o Y do canvas cresce para cima, então o valor entra com sinal trocado.
                    baixos[faixa][coluna] = Mathf.Clamp(centro - maximo * unidadesPorUv, centro - limite, centro + limite);
                    altos[faixa][coluna] = Mathf.Clamp(centro - minimo * unidadesPorUv, centro - limite, centro + limite);
                }

                faixas[faixa].Definir(baixos[faixa], altos[faixa], colunas, 0f, passo);
                var rotulo = (RectTransform)rotulos[faixa].transform;
                rotulo.anchoredPosition = new Vector2(-Calha + 2f, centro);
                rotulo.sizeDelta = new Vector2(Calha - 6f, Mathf.Max(10f, alturaFaixa - 1f));
                var info = resultado.faixas[faixa];
                InterfaceEEG.DefinirRotulo(rotulos[faixa], info.rotulo + (info.interpolada ? "*" : ""));
            }

            for (var marca = 0; marca < marcasTempo.Length; marca++)
            {
                var x = marca / (float)(marcasTempo.Length - 1) * largura;
                marcasTempo[marca].text = InterfaceEEG.Numero(inicioPagina + marca * SegundosPagina / (marcasTempo.Length - 1), "0") + " s";
                ((RectTransform)marcasTempo[marca].transform).anchoredPosition = new Vector2(x, -12f);
                linhasGrade[marca].anchoredPosition = new Vector2(x, 0f);
            }

            var evento = Array.Find(controlador.Dados.eventos, e => e.tempo >= inicioPagina && e.tempo < inicioPagina + SegundosPagina);
            marcadorEvento.gameObject.SetActive(evento != null);
            if (evento != null)
            {
                marcadorEvento.anchoredPosition = new Vector2((evento.tempo - inicioPagina) / SegundosPagina * largura, 0f);
                textoEvento.text = "evento " + evento.codigo;
            }

            barraCalibracao.sizeDelta = new Vector2(2f, 50f * unidadesPorUv);
            textoCalibracao.text = "50 µV · " + InterfaceEEG.Numero(controlador.Sensibilidade, "0") + " µV/mm · neg. ↑";
            AtualizarCores();
            AtualizarControles();
            AtualizarCursor();
        }

        private void AtualizarCursor()
        {
            if (cursor == null)
                return;
            var largura = area.rect.width;
            cursor.anchoredPosition = new Vector2(Mathf.Clamp01((controlador.Tempo - inicioPagina) / SegundosPagina) * largura, 0f);
        }

        /// <summary>As faixas que contêm o eletrodo escolhido ficam em vermelho, como no Protobrain.</summary>
        private void AtualizarCores()
        {
            if (!aberta || controlador.Faixas == null)
                return;
            var selecionado = controlador.CanalSelecionado;
            var info = controlador.Faixas.faixas;
            for (var faixa = 0; faixa < Mathf.Min(FaixasMaximas, info.Length); faixa++)
            {
                var destacada = selecionado >= 0 && Array.IndexOf(info[faixa].eletrodos, selecionado) >= 0;
                faixas[faixa].color = destacada ? TracoSelecionado : Traco;
                var texto = rotulos[faixa].GetComponentInChildren<Text>();
                texto.color = destacada ? TracoSelecionado : info[faixa].interpolada ? Apagado : Tinta;
                texto.fontStyle = destacada ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void AtualizarControles()
        {
            if (controlador == null || botaoMedia == null)
                return;
            InterfaceEEG.Marcar(botaoMedia, !controlador.DuplaBanana);
            InterfaceEEG.Marcar(botaoBanana, controlador.DuplaBanana);
            textoSensibilidade.text = InterfaceEEG.Numero(controlador.Sensibilidade, "0") + " µV/mm";
            var filtros = controlador.Filtros;
            InterfaceEEG.DefinirRotulo(botaoPassaAlta, "FBF " + FiltrosEEG.Rotulo(filtros.passaAlta));
            InterfaceEEG.DefinirRotulo(botaoPassaBaixa, "FAF " + FiltrosEEG.Rotulo(filtros.passaBaixa));
            InterfaceEEG.DefinirRotulo(botaoNotch, filtros.notch ? "Notch " + InterfaceEEG.Numero(controlador.Dados.frequenciaRede, "0") + " Hz" : "Notch: não");
            InterfaceEEG.Marcar(botaoNotch, filtros.notch);
            textoSituacao.text = controlador.Filtrando ? "Filtrando…" : "Referência " + (controlador.DuplaBanana ? "bipolar (dupla banana)" : "média") + " · página de 10 s";
            var canal = controlador.CanalSelecionado;
            titulo.text = canal >= 0 ? "Traçados · " + controlador.Dados.canais[canal].nome : "Traçados EEG";
        }

        // ---------------------------------------------------------------- ações

        private void TrocarSensibilidade(int passo)
        {
            var atual = Array.IndexOf(Sensibilidades, controlador.Sensibilidade);
            if (atual < 0)
                atual = Array.IndexOf(Sensibilidades, 7f);
            controlador.DefinirSensibilidade(Sensibilidades[Mathf.Clamp(atual + passo, 0, Sensibilidades.Length - 1)]);
        }

        private void TrocarPassaAlta()
        {
            var filtros = controlador.Filtros;
            filtros.passaAlta = Proximo(FiltrosEEG.OpcoesPassaAlta, filtros.passaAlta);
            controlador.DefinirFiltros(filtros);
        }

        private void TrocarPassaBaixa()
        {
            var filtros = controlador.Filtros;
            filtros.passaBaixa = Proximo(FiltrosEEG.OpcoesPassaBaixa, filtros.passaBaixa);
            controlador.DefinirFiltros(filtros);
        }

        private void AlternarNotch()
        {
            var filtros = controlador.Filtros;
            filtros.notch = !filtros.notch;
            controlador.DefinirFiltros(filtros);
        }

        private static float Proximo(float[] opcoes, float atual)
        {
            var indice = Array.IndexOf(opcoes, atual);
            return opcoes[(indice + 1) % opcoes.Length];
        }

        private void MudarPagina(int passo)
        {
            controlador.DefinirTempo(Mathf.Max(0f, inicioPagina + passo * SegundosPagina));
            Reconstruir();
        }

        // ---------------------------------------------------------------- construção

        private void Construir()
        {
            moldura = MolduraJanelaEEG.Construir(gameObject, new Vector2(Largura, Altura), sprite, "Traçados EEG", () => DefinirAberta(false));
            titulo = moldura.Titulo;
            var corpo = moldura.Corpo;

            var linhaA = InterfaceEEG.Linha(corpo, "Linha_Comandos", AlturaComandos, 6f);
            MolduraJanelaEEG.Prender((RectTransform)linhaA.transform, 0f, AlturaComandos, 0f);
            botaoMedia = InterfaceEEG.Botao(linhaA.transform, "Montagem_Media", "Média", sprite, () => controlador.DefinirDuplaBanana(false));
            InterfaceEEG.Tamanho(botaoMedia, -1f, 70f);
            botaoBanana = InterfaceEEG.Botao(linhaA.transform, "Montagem_Banana", "Banana", sprite, () => controlador.DefinirDuplaBanana(true));
            InterfaceEEG.Tamanho(botaoBanana, -1f, 76f);
            var menos = InterfaceEEG.Botao(linhaA.transform, "Sensibilidade_Menos", "−", sprite, () => TrocarSensibilidade(-1));
            InterfaceEEG.Tamanho(menos, -1f, 32f);
            textoSensibilidade = InterfaceEEG.Texto(linhaA.transform, "Sensibilidade", "", 15, TextAnchor.MiddleCenter, Tinta);
            InterfaceEEG.Tamanho(textoSensibilidade, -1f, 84f);
            var mais = InterfaceEEG.Botao(linhaA.transform, "Sensibilidade_Mais", "+", sprite, () => TrocarSensibilidade(1));
            InterfaceEEG.Tamanho(mais, -1f, 32f);
            var espaco = InterfaceEEG.Retangulo("Espaco", linhaA.transform);
            InterfaceEEG.Tamanho(espaco, -1f, -1f, 1f);
            var anterior = InterfaceEEG.Botao(linhaA.transform, "Pagina_Anterior", "◀ 10 s", sprite, () => MudarPagina(-1));
            InterfaceEEG.Tamanho(anterior, -1f, 72f);
            var proxima = InterfaceEEG.Botao(linhaA.transform, "Pagina_Proxima", "10 s ▶", sprite, () => MudarPagina(1));
            InterfaceEEG.Tamanho(proxima, -1f, 72f);

            var linhaB = InterfaceEEG.Linha(corpo, "Linha_Filtros", AlturaFiltros, 6f);
            MolduraJanelaEEG.Prender((RectTransform)linhaB.transform, AlturaComandos + EspacoLinhas, AlturaFiltros, 0f);
            botaoPassaAlta = InterfaceEEG.Botao(linhaB.transform, "Filtro_PassaAlta", "FBF", sprite, TrocarPassaAlta, 15);
            InterfaceEEG.Tamanho(botaoPassaAlta, -1f, 120f);
            botaoPassaBaixa = InterfaceEEG.Botao(linhaB.transform, "Filtro_PassaBaixa", "FAF", sprite, TrocarPassaBaixa, 15);
            InterfaceEEG.Tamanho(botaoPassaBaixa, -1f, 120f);
            botaoNotch = InterfaceEEG.Botao(linhaB.transform, "Filtro_Notch", "Notch", sprite, AlternarNotch, 15);
            InterfaceEEG.Tamanho(botaoNotch, -1f, 116f);
            textoSituacao = InterfaceEEG.Texto(linhaB.transform, "Situacao", "", 14, TextAnchor.MiddleLeft, Apagado);
            InterfaceEEG.Tamanho(textoSituacao, -1f, -1f, 1f);

            area = InterfaceEEG.Retangulo("Area_Tracados", corpo);
            InterfaceEEG.Esticar(area, Calha, MargemBase, MargemDireita, MargemTopo);
            area.pivot = Vector2.zero;
            area.gameObject.AddComponent<RectMask2D>().padding = new Vector4(-Calha, -MargemBase, 0f, 0f);
            colunas = Mathf.RoundToInt(Largura - 2f * MolduraJanelaEEG.MargemLateral - Calha - MargemDireita);

            for (var marca = 0; marca < linhasGrade.Length; marca++)
            {
                var linha = InterfaceEEG.Retangulo("Grade_" + marca, area);
                linha.anchorMin = new Vector2(0f, 0f);
                linha.anchorMax = new Vector2(0f, 1f);
                linha.pivot = new Vector2(0.5f, 0.5f);
                linha.sizeDelta = new Vector2(1f, 0f);
                var imagem = linha.gameObject.AddComponent<Image>();
                imagem.color = Grade;
                imagem.raycastTarget = false;
                linhasGrade[marca] = linha;
                var texto = InterfaceEEG.Texto(area, "Tempo_" + marca, "", 12, TextAnchor.MiddleCenter, Apagado, FontStyle.Normal, false, 12);
                var retangulo = texto.rectTransform;
                retangulo.anchorMin = retangulo.anchorMax = Vector2.zero;
                retangulo.sizeDelta = new Vector2(44f, 16f);
                marcasTempo[marca] = texto;
            }

            for (var faixa = 0; faixa < FaixasMaximas; faixa++)
            {
                var objeto = InterfaceEEG.Retangulo("Faixa_" + faixa, area);
                InterfaceEEG.Esticar(objeto);
                objeto.pivot = Vector2.zero;
                var grafico = objeto.gameObject.AddComponent<GraficoFaixaEEG>();
                grafico.raycastTarget = false;
                grafico.color = Traco;
                faixas[faixa] = grafico;
                baixos[faixa] = new float[colunas];
                altos[faixa] = new float[colunas];
                var indice = faixa;
                var rotulo = InterfaceEEG.Botao(area, "Rotulo_" + faixa, "", null, () => SelecionarFaixa(indice), 10, true);
                var texto = rotulo.GetComponentInChildren<Text>();
                texto.alignment = TextAnchor.MiddleRight;
                // Faixas de ~11 mm: sem corte vertical (o Unity apagaria a linha inteira) e sem ajuste automático.
                texto.resizeTextForBestFit = false;
                texto.fontSize = 10;
                texto.verticalOverflow = VerticalWrapMode.Overflow;
                texto.horizontalOverflow = HorizontalWrapMode.Overflow;
                texto.color = Tinta;
                var retangulo = (RectTransform)rotulo.transform;
                retangulo.anchorMin = retangulo.anchorMax = Vector2.zero;
                retangulo.pivot = new Vector2(0f, 0.5f);
                rotulos[faixa] = rotulo;
            }

            marcadorEvento = InterfaceEEG.Retangulo("Evento", area);
            marcadorEvento.anchorMin = new Vector2(0f, 0f);
            marcadorEvento.anchorMax = new Vector2(0f, 1f);
            marcadorEvento.sizeDelta = new Vector2(1.5f, 0f);
            var imagemEvento = marcadorEvento.gameObject.AddComponent<Image>();
            imagemEvento.color = new Color(CorEvento.r, CorEvento.g, CorEvento.b, 0.7f);
            imagemEvento.raycastTarget = false;
            textoEvento = InterfaceEEG.Texto(marcadorEvento, "Texto", "", 12, TextAnchor.UpperLeft, CorEvento, FontStyle.Normal, false, 12);
            textoEvento.rectTransform.anchorMin = textoEvento.rectTransform.anchorMax = new Vector2(0f, 1f);
            textoEvento.rectTransform.pivot = new Vector2(0f, 1f);
            textoEvento.rectTransform.sizeDelta = new Vector2(80f, 16f);
            textoEvento.rectTransform.anchoredPosition = new Vector2(4f, 0f);

            cursor = InterfaceEEG.Retangulo("Cursor", area);
            cursor.anchorMin = new Vector2(0f, 0f);
            cursor.anchorMax = new Vector2(0f, 1f);
            cursor.sizeDelta = new Vector2(2f, 0f);
            var imagemCursor = cursor.gameObject.AddComponent<Image>();
            imagemCursor.color = CorEvento;
            imagemCursor.raycastTarget = false;

            barraCalibracao = InterfaceEEG.Retangulo("Calibracao", area);
            barraCalibracao.anchorMin = barraCalibracao.anchorMax = Vector2.zero;
            barraCalibracao.pivot = Vector2.zero;
            barraCalibracao.anchoredPosition = new Vector2(8f, 6f);
            var imagemCalibracao = barraCalibracao.gameObject.AddComponent<Image>();
            imagemCalibracao.color = Tinta;
            imagemCalibracao.raycastTarget = false;
            textoCalibracao = InterfaceEEG.Texto(barraCalibracao, "Texto", "", 12, TextAnchor.LowerLeft, Tinta, FontStyle.Normal, false, 12);
            var retanguloCalibracao = textoCalibracao.rectTransform;
            retanguloCalibracao.anchorMin = retanguloCalibracao.anchorMax = new Vector2(1f, 0f);
            retanguloCalibracao.pivot = Vector2.zero;
            retanguloCalibracao.sizeDelta = new Vector2(220f, 16f);
            retanguloCalibracao.anchoredPosition = new Vector2(6f, 0f);
        }

        private void SelecionarFaixa(int faixa)
        {
            var info = controlador.Faixas?.faixas;
            if (info == null || faixa >= info.Length)
                return;
            controlador.SelecionarCanal(info[faixa].eletrodos[0], true);
        }
    }
}
