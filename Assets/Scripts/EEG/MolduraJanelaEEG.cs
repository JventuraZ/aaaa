using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRSNC.Interface;

namespace VRSNC.EEG
{
    /// <summary>
    /// Moldura comum das janelas do EEG (mapa 2D e traçados): canvas de mundo com texto nítido, fundo do tema, barra de
    /// título com o nome e "Fechar", o corpo onde cada janela monta o seu conteúdo e, embaixo, a alça que move a janela
    /// como a do painel principal. Com a mão ou o controle perto da janela, ou o raio sobre ela, a anatomia não reage.
    /// Na escala da janela, 1 unidade do canvas = 1 mm.
    /// </summary>
    internal sealed class MolduraJanelaEEG
    {
        public const float MargemLateral = 10f;
        public const float AlturaTitulo = 32f;
        public const float MargemTopo = 8f;
        public const float EspacoTituloCorpo = 6f;
        public const float AlturaRodapeAlca = 30f;
        private const float LarguraAlca = 180f;
        private const float AlturaAlca = 10f;
        // Colisor da alça: mais largo e alto que a barra visível, como no painel principal (224 × 30 × 24 mm).
        private static readonly Vector3 TamanhoColisorAlca = new(220f, 30f, 24f);

        public RectTransform Raiz { get; private set; }
        public RectTransform Corpo { get; private set; }
        public HorizontalLayoutGroup LinhaTitulo { get; private set; }
        public Text Titulo { get; private set; }
        public Button Fechar { get; private set; }

        /// <summary>Monta a moldura em <paramref name="objeto"/> (que deve estar inativo) com o tamanho em mm.</summary>
        public static MolduraJanelaEEG Construir(GameObject objeto, Vector2 tamanho, Sprite sprite, string titulo, UnityAction aoFechar)
        {
            var moldura = new MolduraJanelaEEG { Raiz = (RectTransform)objeto.transform };
            var raiz = moldura.Raiz;
            raiz.sizeDelta = tamanho;
            raiz.localScale = Vector3.one * 0.001f;
            var canvas = objeto.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            TemaVRSNC.PrepararCanvasMundo(objeto);
            objeto.AddComponent<TrackedDeviceGraphicRaycaster>();
            var fundo = objeto.AddComponent<Image>();
            fundo.sprite = sprite;
            fundo.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            fundo.color = TemaVRSNC.FundoJanela;
            // Mesma proteção do painel principal: com o controle perto ou o raio sobre a janela, a anatomia não reage.
            objeto.AddComponent<ZonaProximidadePainelInterface>();

            moldura.LinhaTitulo = InterfaceEEG.Linha(raiz, "Linha_Titulo", AlturaTitulo, 6f);
            Prender((RectTransform)moldura.LinhaTitulo.transform, MargemTopo, AlturaTitulo);
            moldura.Titulo = InterfaceEEG.Texto(moldura.LinhaTitulo.transform, "Titulo", titulo, 17, TextAnchor.MiddleLeft, TemaVRSNC.Texto, FontStyle.Bold, true);
            InterfaceEEG.Tamanho(moldura.Titulo, -1f, -1f, 1f);
            moldura.Fechar = InterfaceEEG.Botao(moldura.LinhaTitulo.transform, "Fechar", "Fechar", sprite, aoFechar);
            InterfaceEEG.Tamanho(moldura.Fechar, -1f, 76f);

            moldura.Corpo = InterfaceEEG.Retangulo("Corpo", raiz);
            InterfaceEEG.Esticar(moldura.Corpo, MargemLateral, AlturaRodapeAlca, MargemLateral, MargemTopo + AlturaTitulo + EspacoTituloCorpo);

            var alca = InterfaceEEG.Retangulo("Alca_Janela", raiz);
            alca.anchorMin = alca.anchorMax = new Vector2(0.5f, 0f);
            alca.pivot = new Vector2(0.5f, 0.5f);
            alca.sizeDelta = new Vector2(LarguraAlca, AlturaAlca);
            alca.anchoredPosition = new Vector2(0f, AlturaRodapeAlca * 0.5f);
            var imagemAlca = alca.gameObject.AddComponent<Image>();
            imagemAlca.sprite = sprite;
            imagemAlca.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            imagemAlca.raycastTarget = false;
            AlcaJanelaFlutuante.Configurar(objeto, imagemAlca, TamanhoColisorAlca);
            return moldura;
        }

        /// <summary>
        /// Abre a janela no ângulo dado em relação ao olhar (negativo = à esquerda), na distância e na altura em relação
        /// aos olhos, voltada para o rosto.
        /// </summary>
        public void Posicionar(float anguloGraus, float distancia, float alturaRelativaOlhos)
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            var cabeca = camera.transform;
            var frente = Vector3.ProjectOnPlane(cabeca.forward, Vector3.up);
            if (frente.sqrMagnitude < 0.0001f)
                frente = Vector3.forward;
            frente = Quaternion.AngleAxis(anguloGraus, Vector3.up) * frente.normalized;
            var posicao = cabeca.position + frente * distancia + Vector3.up * alturaRelativaOlhos;
            Raiz.SetPositionAndRotation(posicao, Quaternion.LookRotation(posicao - cabeca.position, Vector3.up));
        }

        /// <summary>
        /// Na primeira abertura, ou se a janela ficou fora da vista (mais de 70° do olhar ou longe demais), volta à posição
        /// padrão; senão fica onde o usuário a deixou com a alça.
        /// </summary>
        public void PosicionarSeForaDaVista(float anguloGraus, float distancia, float alturaRelativaOlhos)
        {
            var camera = Camera.main;
            if (camera != null && posicionada)
            {
                var paraJanela = Raiz.position - camera.transform.position;
                if (Vector3.Angle(camera.transform.forward, paraJanela) < 70f && paraJanela.magnitude < 3f)
                    return;
            }

            Posicionar(anguloGraus, distancia, alturaRelativaOlhos);
            posicionada = true;
        }

        private bool posicionada;

        /// <summary>Prende uma linha no topo do pai, a <paramref name="distanciaTopo"/> mm da borda, com margens laterais.</summary>
        public static void Prender(RectTransform linha, float distanciaTopo, float altura, float margemLateral = MargemLateral)
        {
            linha.anchorMin = new Vector2(0f, 1f);
            linha.anchorMax = new Vector2(1f, 1f);
            linha.pivot = new Vector2(0.5f, 1f);
            linha.offsetMin = new Vector2(margemLateral, -distanciaTopo - altura);
            linha.offsetMax = new Vector2(-margemLateral, -distanciaTopo);
        }
    }
}
