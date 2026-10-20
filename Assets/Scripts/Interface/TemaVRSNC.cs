using UnityEngine;
using UnityEngine.UI;

namespace VRSNC.Interface
{
    /// <summary>
    /// Tema "Grafite suave" do HUD: painéis escuros e foscos, texto quase branco, azul suave para destaque e
    /// âmbar para a seleção (o mesmo realce da anatomia). Brilha menos que um painel branco, o que cansa menos
    /// a vista nos óculos. As ferramentas de Editor (painel salvo na cena) e as interfaces montadas em execução
    /// (lista, caminho, aba EEG, janelas, rótulos) pintam tudo daqui.
    /// </summary>
    public static class TemaVRSNC
    {
        public static readonly Color Painel = Hex(0x1F242C, 0.94f);
        public static readonly Color Cabecalho = Hex(0x1A1E25, 0.97f);
        public static readonly Color Cartao = Hex(0x262C35);
        public static readonly Color Botao = Hex(0x343C48);
        public static readonly Color Ativo = Hex(0x2D4B6E);
        public static readonly Color TextoAtivo = Hex(0xCFE4FF);
        public static readonly Color Destaque = Hex(0x78B4FF);
        public static readonly Color Texto = Hex(0xE7E9EC);
        public static readonly Color TextoSecundario = Hex(0xA8B0BC);
        public static readonly Color TextoApagado = Hex(0x6E7785);
        public static readonly Color Selecao = Hex(0xF2C14E);
        public static readonly Color SelecaoSuperficie = Hex(0x4A3F22);
        public static readonly Color Perigo = Hex(0xE57373);
        public static readonly Color Contorno = Hex(0x3A4250);
        public static readonly Color FundoRotulo = Hex(0x12161C, 0.92f);
        public static readonly Color FundoJanela = Hex(0x171B21, 0.97f);
        public static readonly Color Alca = Hex(0x55606F);
        public static readonly Color AlcaEmFoco = Hex(0x78B4FF, 0.9f);
        public static readonly Color BordaRedimensionar = Hex(0x78B4FF, 0.06f);
        public static readonly Color BordaRedimensionarEmFoco = Hex(0x78B4FF, 0.35f);
        public const string HexTextoSecundario = "#A8B0BC";

        /// <summary>
        /// Cor de inativo do <see cref="ColorBlock"/>: com o multiplicador do tema vira branco a 40%, ou seja, a cor-base
        /// do botão translúcida.
        /// </summary>
        public static readonly Color CorDesativadoBloco = new(0.8f, 0.8f, 0.8f, 0.32f);

        // A cor-base fica na Image; o bloco de cores só multiplica. Com o multiplicador 1,25 o normal (0,8) devolve a
        // cor-base, o apontado (1,0) clareia 25% e o pressionado (0,68) escurece: num tema escuro, realçar escurecendo
        // sumiria.
        public const float MultiplicadorCor = 1.25f;
        private static readonly Color CorNormalBloco = new(0.8f, 0.8f, 0.8f, 1f);
        private static readonly Color CorApontadoBloco = Color.white;
        private static readonly Color CorPressionadoBloco = new(0.68f, 0.68f, 0.68f, 1f);

        // Texto legado rasterizado com mais resolução: com 1 (o padrão) fica borrado nos óculos.
        public const float PixelsDinamicosPorUnidade = 4f;

        private static Sprite spriteArredondado;

        public enum Estilo { Normal, Perigo, Transparente }

        public enum Papel { Titulo, Corpo, Secundario, Apagado, Destaque }

        public static Color Hex(int rgb, float alfa = 1f) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alfa);

        /// <summary>Bloco de cores do tema, preservando a navegação e os tempos do bloco atual.</summary>
        public static ColorBlock BlocoCores(ColorBlock atual)
        {
            atual.normalColor = CorNormalBloco;
            atual.highlightedColor = CorApontadoBloco;
            atual.selectedColor = CorNormalBloco;
            atual.pressedColor = CorPressionadoBloco;
            atual.disabledColor = CorDesativadoBloco;
            atual.colorMultiplier = MultiplicadorCor;
            return atual;
        }

        /// <summary>Pinta um botão: cor-base na Image, bloco de cores do tema e rótulos na cor do texto.</summary>
        public static void AplicarBotao(Button botao, Estilo estilo = Estilo.Normal)
        {
            if (botao == null)
                return;
            botao.colors = BlocoCores(botao.colors);
            if (botao.targetGraphic is Image imagem)
                imagem.color = estilo == Estilo.Transparente ? new Color(1f, 1f, 1f, 0f) : Botao;
            var cor = estilo == Estilo.Perigo ? Perigo : Texto;
            foreach (var rotulo in botao.GetComponentsInChildren<Text>(true))
                rotulo.color = new Color(cor.r, cor.g, cor.b, rotulo.color.a);
        }

        /// <summary>Opção escolhida de um grupo (aba, banda, modo): superfície azulada, texto claro e negrito.</summary>
        public static void Marcar(Button botao, bool ativo)
        {
            if (botao == null)
                return;
            if (botao.targetGraphic is Image imagem)
                imagem.color = ativo ? Ativo : Botao;
            foreach (var rotulo in botao.GetComponentsInChildren<Text>(true))
            {
                var cor = ativo ? TextoAtivo : Texto;
                rotulo.color = new Color(cor.r, cor.g, cor.b, rotulo.color.a);
                rotulo.fontStyle = ativo ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        /// <summary>Item que é a seleção atual (lista de estruturas): superfície âmbar escura e texto âmbar.</summary>
        public static void MarcarSelecao(Button botao, bool selecionado)
        {
            if (botao == null)
                return;
            if (botao.targetGraphic is Image imagem)
                imagem.color = selecionado ? SelecaoSuperficie : Botao;
            foreach (var rotulo in botao.GetComponentsInChildren<Text>(true))
            {
                var cor = selecionado ? Selecao : Texto;
                rotulo.color = new Color(cor.r, cor.g, cor.b, rotulo.color.a);
                rotulo.fontStyle = selecionado ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        public static Color CorDoPapel(Papel papel) => papel switch
        {
            Papel.Secundario => TextoSecundario,
            Papel.Apagado => TextoApagado,
            Papel.Destaque => Destaque,
            _ => Texto,
        };

        public static void AplicarTexto(Text texto, Papel papel)
        {
            if (texto == null)
                return;
            texto.color = CorDoPapel(papel);
            if (papel == Papel.Titulo)
                texto.fontStyle = FontStyle.Bold;
        }

        /// <summary>
        /// Canvas de mundo criado em execução: garante o <see cref="CanvasScaler"/> com mais pixels por unidade, para
        /// o texto legado não ficar borrado.
        /// </summary>
        public static void PrepararCanvasMundo(GameObject objetoCanvas)
        {
            if (objetoCanvas == null)
                return;
            var escalador = objetoCanvas.GetComponent<CanvasScaler>();
            if (escalador == null)
                escalador = objetoCanvas.AddComponent<CanvasScaler>();
            escalador.dynamicPixelsPerUnit = PixelsDinamicosPorUnidade;
            escalador.referencePixelsPerUnit = 100f;
        }

        /// <summary>
        /// Sprite de cantos arredondados do painel (Round Radius 10), achado na primeira Image com sprite fatiado do
        /// canvas de <paramref name="referencia"/>. Em execução não dá para carregar o asset pelo caminho.
        /// </summary>
        public static Sprite SpriteArredondado(Transform referencia)
        {
            if (spriteArredondado != null)
                return spriteArredondado;
            var canvas = referencia != null ? referencia.GetComponentInParent<Canvas>() : null;
            var raiz = canvas != null ? canvas.rootCanvas.transform : referencia;
            if (raiz == null)
                return null;
            foreach (var botao in raiz.GetComponentsInChildren<Button>(true))
            {
                if (botao.targetGraphic is Image imagem && imagem.sprite != null && imagem.type == Image.Type.Sliced)
                    return spriteArredondado = imagem.sprite;
            }

            return null;
        }

        /// <summary>Registra o sprite do painel para quem monta interface sem acesso ao canvas principal.</summary>
        public static void RegistrarSpriteArredondado(Sprite sprite)
        {
            if (sprite != null)
                spriteArredondado = sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Redefinir() => spriteArredondado = null;
    }
}
