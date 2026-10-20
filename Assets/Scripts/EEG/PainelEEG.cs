using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VRSNC.Interface;

namespace VRSNC.EEG
{
    /// <summary>
    /// Aba "EEG" do painel compacto, montada em execução dentro de <c>EEGPanel</c>: reprodução e linha do tempo,
    /// banda, normalização ou voltagem, o eletrodo escolhido com o córtex sob ele e os botões que abrem as duas
    /// janelas do EEG, o mapa 2D (<see cref="JanelaMapaEEG"/>) e os traçados (<see cref="JanelaTracadosEEG"/>).
    /// Abrir a aba ou qualquer janela mostra o escalpo e os eletrodos em volta do cérebro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PainelEEG : MonoBehaviour
    {
        // As três ressalvas precisam caber inteiras: espaço de sensores, giro sem ser a fonte, ensino sem ser diagnóstico.
        private const string AvisoLeitura = "Cor = medida dos sensores. O giro é correspondência anatômica, não a fonte do sinal. Ensino, não diagnóstico.";
        // No mapa de voltagem o valor muda a cada amostra: o texto do eletrodo acompanha 10 vezes por segundo.
        private const float IntervaloVoltagem = 0.1f;

        [SerializeField]
        private RectTransform conteudo;
        [SerializeField]
        private AbasPainelAnatomico abas;
        // Botão do painel usado como modelo de sprite (cantos arredondados).
        [SerializeField]
        private Button modeloBotao;

        private ControladorEEG controlador;
        private Sprite sprite;
        private bool construido;
        private Button botaoTocar;
        private Text textoTempo;
        private Slider linhaTempo;
        private Button[] botoesBandas;
        private Button[] botoesModos;
        private Button botaoVoltagem;
        private Text nomeEletrodo;
        private Text infoEletrodo;
        private Button botaoFicha;
        private Button botaoEscalpo;
        private Button botaoMapa;
        private Button botaoTracados;
        private float[] valores;
        private bool infoPendente = true;
        private float ultimaInfo = float.NegativeInfinity;
        private JanelaTracadosEEG janela;
        private JanelaMapaEEG janelaMapa;

        public ControladorEEG Controlador => controlador;
        public JanelaTracadosEEG Janela => janela;
        public JanelaMapaEEG JanelaMapa => janelaMapa;

        private void Awake()
        {
            if (abas == null)
                abas = GetComponent<AbasPainelAnatomico>();
            if (conteudo == null && abas != null && abas.ConteudoEEG != null)
                conteudo = (RectTransform)abas.ConteudoEEG.transform;
        }

        private void Start()
        {
            controlador = FindFirstObjectByType<ControladorEEG>();
            var disponivel = controlador != null && controlador.Disponivel && conteudo != null;
            abas?.DefinirAbaEEGDisponivel(disponivel);
            if (!disponivel)
                return;
            sprite = InterfaceEEG.SpriteDe(modeloBotao);
            if (sprite == null)
                sprite = TemaVRSNC.SpriteArredondado(transform);
            Construir();
            controlador.EstadoAlterado += AoMudarEstado;
            controlador.MapaAlterado += AoMudarMapa;
            controlador.TempoAlterado += AoMudarTempo;
            if (abas != null)
                abas.AbaAlterada += AoMudarAba;
            AoMudarEstado();
            AoMudarTempo();
        }

        private void OnDestroy()
        {
            if (controlador != null)
            {
                controlador.EstadoAlterado -= AoMudarEstado;
                controlador.MapaAlterado -= AoMudarMapa;
                controlador.TempoAlterado -= AoMudarTempo;
            }

            if (abas != null)
                abas.AbaAlterada -= AoMudarAba;
        }

        private void LateUpdate()
        {
            if (!construido || !conteudo.gameObject.activeInHierarchy)
                return;
            var podeAtualizar = controlador.Mapa != ControladorEEG.TipoMapa.Voltagem || Time.unscaledTime - ultimaInfo >= IntervaloVoltagem;
            if (infoPendente && podeAtualizar)
                AtualizarInfoEletrodo();
        }

        private void AoMudarAba(AbasPainelAnatomico.Aba aba) => AtualizarModoAtivo();

        /// <summary>O escalpo aparece com a aba EEG aberta ou com qualquer janela do EEG aberta.</summary>
        public void AtualizarModoAtivo()
        {
            if (controlador == null)
                return;
            var abaAberta = abas != null && abas.AbaAtual == AbasPainelAnatomico.Aba.EEG;
            var janelaAberta = (janela != null && janela.Aberta) || (janelaMapa != null && janelaMapa.Aberta);
            controlador.DefinirModoAtivo(abaAberta || janelaAberta);
            infoPendente = true;
            MarcarJanelas();
        }

        private void AoMudarMapa() => infoPendente = true;

        private void AoMudarTempo()
        {
            if (!construido)
                return;
            textoTempo.text = InterfaceEEG.Numero(controlador.Tempo) + " / " + InterfaceEEG.Numero(controlador.Duracao, "0") + " s";
            linhaTempo.SetValueWithoutNotify(controlador.Tempo);
        }

        private void AoMudarEstado()
        {
            if (!construido)
                return;
            InterfaceEEG.DefinirRotulo(botaoTocar, controlador.Tocando ? "Pausar" : "Tocar");
            var voltagem = controlador.Mapa == ControladorEEG.TipoMapa.Voltagem;
            for (var indice = 0; indice < botoesBandas.Length; indice++)
                InterfaceEEG.Marcar(botoesBandas[indice], !voltagem && indice == controlador.Banda);

            for (var indice = 0; indice < botoesModos.Length; indice++)
                InterfaceEEG.Marcar(botoesModos[indice], !voltagem && indice == controlador.Modo);
            InterfaceEEG.Marcar(botaoVoltagem, voltagem);
            InterfaceEEG.DefinirRotulo(botaoEscalpo, controlador.Escalpo switch
            {
                ControladorEEG.EstadoEscalpo.Opaco => "Opaco",
                ControladorEEG.EstadoEscalpo.Desligado => "Sem escalpo",
                _ => "Translúcido",
            });
            InterfaceEEG.Marcar(botaoEscalpo, controlador.Escalpo != ControladorEEG.EstadoEscalpo.Desligado);
            MarcarJanelas();
            AtualizarInfoEletrodo();
        }

        private void MarcarJanelas()
        {
            if (!construido)
                return;
            InterfaceEEG.Marcar(botaoMapa, janelaMapa != null && janelaMapa.Aberta);
            InterfaceEEG.Marcar(botaoTracados, janela != null && janela.Aberta);
        }

        private void AtualizarInfoEletrodo()
        {
            if (!construido)
                return;
            infoPendente = false;
            ultimaInfo = Time.unscaledTime;
            var dados = controlador.Dados;
            var canal = controlador.CanalSelecionado;
            botaoFicha.interactable = canal >= 0 && controlador.LigacaoDe(canal)?.no != null;
            EstiloBotaoInterface.AtualizarAparencia(botaoFicha);
            if (canal < 0)
            {
                nomeEletrodo.text = "Nenhum eletrodo";
                infoEletrodo.text = "Toque num eletrodo no escalpo, no mapa 2D, ou use ◀ ▶ para percorrer os 32.";
                return;
            }

            var info = dados.canais[canal];
            var regiao = dados.RegiaoDe(canal);
            // Nome e valor na mesma linha; o resto em três linhas curtas que cabem na coluna.
            var voltagem = controlador.Mapa == ControladorEEG.TipoMapa.Voltagem;
            controlador.ValoresMapa(valores);
            var unidade = voltagem ? "µV" : dados.escalas[controlador.Modo].unidade;
            nomeEletrodo.text = info.nome + (info.interpolado ? "*" : "") + " · " + InterfaceEEG.Numero(valores[canal], voltagem ? "0.0" : "0.00") + " " + unidade;
            var lado = info.linhaMedia ? " (LM)" : info.hemisferioEsquerdo ? " (E)" : " (D)";
            var texto = new StringBuilder();
            if (regiao != null && regiao.chave != "unknown")
                texto.Append(regiao.nome).Append(lado).AppendLine().Append(regiao.lobo);
            else
                texto.Append("Parede medial, sem giro").AppendLine();
            if (info.brodmann > 0)
                texto.Append(" · BA ").Append(info.brodmann);
            texto.Append(" · a ").Append(InterfaceEEG.Numero(info.distanciaCortexMm, "0")).AppendLine(" mm");
            var ligacao = controlador.LigacaoDe(canal);
            if (ligacao != null && ligacao.no != null)
                texto.Append("Modelo: ").Append(ligacao.no.NomeLegivelComParte).Append(ligacao.aproximada ? " (aprox.)" : "");
            infoEletrodo.text = texto.ToString();
        }

        private void Construir()
        {
            var dados = controlador.Dados;
            valores = new float[dados.canais.Length];
            var raiz = InterfaceEEG.Retangulo("Conteudo_EEG", conteudo);
            InterfaceEEG.Esticar(raiz, 8f, 6f, 8f, 6f);
            var coluna = raiz.gameObject.AddComponent<VerticalLayoutGroup>();
            coluna.spacing = 3f;
            coluna.childControlWidth = true;
            coluna.childControlHeight = true;
            coluna.childForceExpandWidth = true;
            coluna.childForceExpandHeight = false;

            // Reprodução.
            var reproducao = InterfaceEEG.Linha(raiz, "Linha_Reproducao", 28f);
            botaoTocar = InterfaceEEG.Botao(reproducao.transform, "Botao_Tocar", "Tocar", sprite, controlador.AlternarReproducao);
            InterfaceEEG.Tamanho(botaoTocar, -1f, 72f);
            textoTempo = InterfaceEEG.Texto(reproducao.transform, "Tempo", "", 16, TextAnchor.MiddleCenter, InterfaceEEG.CorTexto, FontStyle.Normal, true);
            InterfaceEEG.Tamanho(textoTempo, -1f, -1f, 1f);
            var voltar = InterfaceEEG.Botao(reproducao.transform, "Botao_Voltar5s", "−5 s", sprite, () => controlador.DefinirTempo(controlador.Tempo - 5f));
            InterfaceEEG.Tamanho(voltar, -1f, 56f);
            var avancar = InterfaceEEG.Botao(reproducao.transform, "Botao_Avancar5s", "+5 s", sprite, () => controlador.DefinirTempo(controlador.Tempo + 5f));
            InterfaceEEG.Tamanho(avancar, -1f, 56f);

            linhaTempo = InterfaceEEG.Deslizante(raiz, "Linha_Tempo", sprite, valor => controlador.DefinirTempo(valor));
            linhaTempo.minValue = 0f;
            linhaTempo.maxValue = controlador.Duracao;
            InterfaceEEG.Tamanho(linhaTempo, 16f);

            // Bandas (nomes curtos; a banda escolhida fica marcada).
            var bandas = InterfaceEEG.Linha(raiz, "Linha_Bandas", 26f, 4f);
            botoesBandas = new Button[dados.bandas.Length];
            for (var indice = 0; indice < dados.bandas.Length; indice++)
            {
                var banda = indice;
                var rotulo = dados.rotulosBandas[indice].Split(' ')[0];
                botoesBandas[indice] = InterfaceEEG.Botao(bandas.transform, "Banda_" + dados.bandas[indice], rotulo, sprite, () => controlador.DefinirBanda(banda));
                InterfaceEEG.Tamanho(botoesBandas[indice], -1f, -1f, 1f);
            }

            // Normalizações da potência e o mapa de voltagem.
            var modos = InterfaceEEG.Linha(raiz, "Linha_Modos", 26f, 4f);
            botoesModos = new Button[dados.escalas.Length];
            for (var indice = 0; indice < dados.escalas.Length; indice++)
            {
                var modo = indice;
                botoesModos[indice] = InterfaceEEG.Botao(modos.transform, "Modo_" + dados.escalas[indice].modo, dados.escalas[indice].rotulo, sprite, () => controlador.DefinirModo(modo));
                InterfaceEEG.Tamanho(botoesModos[indice], -1f, -1f, 1f);
            }

            botaoVoltagem = InterfaceEEG.Botao(modos.transform, "Modo_Voltagem", "Voltagem", sprite, () => controlador.DefinirTipoMapa(ControladorEEG.TipoMapa.Voltagem));
            InterfaceEEG.Tamanho(botaoVoltagem, -1f, -1f, 1f);

            // Eletrodo escolhido: navegação, valor e o córtex sob ele.
            var navegacao = InterfaceEEG.Linha(raiz, "Linha_Eletrodo", 28f, 4f);
            var anterior = InterfaceEEG.Botao(navegacao.transform, "Eletrodo_Anterior", "◀", sprite, () => controlador.SelecionarCanalVizinho(-1));
            InterfaceEEG.Tamanho(anterior, -1f, 40f);
            nomeEletrodo = InterfaceEEG.Texto(navegacao.transform, "Nome_Eletrodo", "", 17, TextAnchor.MiddleCenter, InterfaceEEG.CorTexto, FontStyle.Bold, true);
            InterfaceEEG.Tamanho(nomeEletrodo, -1f, -1f, 1f);
            var proximo = InterfaceEEG.Botao(navegacao.transform, "Eletrodo_Proximo", "▶", sprite, () => controlador.SelecionarCanalVizinho(1));
            InterfaceEEG.Tamanho(proximo, -1f, 40f);
            infoEletrodo = InterfaceEEG.Texto(raiz, "Info_Eletrodo", "", 14, TextAnchor.UpperLeft, InterfaceEEG.CorTextoSecundario);
            InterfaceEEG.Tamanho(infoEletrodo, 50f);

            // As duas janelas do EEG, a ficha do giro e o escalpo.
            var acoes = InterfaceEEG.Linha(raiz, "Linha_Acoes", 30f, 4f);
            botaoMapa = InterfaceEEG.Botao(acoes.transform, "Botao_Mapa2D", "Mapa 2D", sprite, AlternarMapa);
            InterfaceEEG.Tamanho(botaoMapa, -1f, -1f, 1f);
            botaoTracados = InterfaceEEG.Botao(acoes.transform, "Botao_Tracados", "Traçados", sprite, AlternarJanela);
            InterfaceEEG.Tamanho(botaoTracados, -1f, -1f, 1f);
            botaoFicha = InterfaceEEG.Botao(acoes.transform, "Botao_Ficha_Giro", "Ficha do giro", sprite, () => abas?.Mostrar(AbasPainelAnatomico.Aba.Ficha));
            InterfaceEEG.Tamanho(botaoFicha, -1f, -1f, 1f);
            botaoEscalpo = InterfaceEEG.Botao(acoes.transform, "Botao_Escalpo", "Translúcido", sprite, controlador.AlternarEscalpo);
            InterfaceEEG.Tamanho(botaoEscalpo, -1f, -1f, 1f);

            var aviso = InterfaceEEG.Texto(raiz, "Aviso_Leitura", AvisoLeitura, 14, TextAnchor.UpperLeft, InterfaceEEG.CorTextoSecundario);
            InterfaceEEG.Tamanho(aviso, 48f);
            construido = true;
        }

        /// <summary>Abre ou fecha os traçados (usado também pelos testes).</summary>
        public void AlternarJanela()
        {
            if (janela == null)
                janela = JanelaTracadosEEG.Criar(controlador, sprite, AtualizarModoAtivo);
            janela.DefinirAberta(!janela.Aberta);
            AtualizarModoAtivo();
            AoMudarEstado();
        }

        /// <summary>Abre ou fecha o mapa 2D (usado também pelos testes).</summary>
        public void AlternarMapa()
        {
            if (janelaMapa == null)
                janelaMapa = JanelaMapaEEG.Criar(controlador, sprite, AtualizarModoAtivo);
            janelaMapa.DefinirAberta(!janelaMapa.Aberta);
            AtualizarModoAtivo();
            AoMudarEstado();
        }
    }
}
