using UnityEngine;
using UnityEngine.UI;

namespace VRSNC.Interface
{
    /// <summary>
    /// Cartão pequeno preso a um controle com os comandos reais do VR SNC em português. Aparece quando o usuário olha
    /// para o controle por um instante (o mesmo critério dos balões do template, que descreviam comandos que este app não
    /// usa) e some ao desviar o olhar. Fica acima do controle, sempre voltado para o rosto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegendaControleVRSNC : MonoBehaviour
    {
        private const float Escala = 0.0005f;
        private const float LarguraCartao = 250f;
        private const float AlturaLinha = 24f;
        private const int TamanhoFonte = 18;

        [SerializeField]
        private bool esquerdo;
        [SerializeField]
        private string[] linhas;
        // Produto escalar entre o olhar e a direção do controle a partir do qual o usuário "está olhando" (0,85 ≈ 32°).
        [SerializeField, Range(0f, 1f)]
        private float limiarOlhar = 0.85f;
        [SerializeField, Min(0f)]
        private float esperaSegundos = 0.6f;
        [SerializeField, Min(0.1f)]
        private float distanciaMaxima = 0.9f;

        private RectTransform cartao;
        private CanvasGroup grupo;
        private float olhandoDesde = -1f;
        // Com os dois controles na frente do rosto, só a legenda do que está mais perto do centro do olhar aparece.
        private static readonly System.Collections.Generic.List<LegendaControleVRSNC> ativas = new();

        public bool Visivel => grupo != null && grupo.alpha > 0.5f;

        private void OnEnable() => ativas.Add(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RedefinirAtivas() => ativas.Clear();

        private float Alinhamento(Transform cabeca)
        {
            var paraControle = transform.position - cabeca.position;
            return paraControle.magnitude < distanciaMaxima ? Vector3.Dot(cabeca.forward, paraControle.normalized) : -1f;
        }

        public static string[] LinhasPadrao(bool controleEsquerdo) => controleEsquerdo
            ? new[] { "Gatilho · Isolar", "Grip · Ocultar ou mostrar", "X · Limpar seleção", "Y (segurar) · Resetar", "Analógico · Rolar o painel", "Clicar o analógico · Minimizar" }
            : new[] { "Gatilho · Selecionar", "Grip · Agarrar", "A · Voltar", "B · Entrar", "Analógico · Girar e aproximar", "Clicar o analógico · Explodir" };

        /// <summary>Usado pela ferramenta de Editor que instala as legendas nos dois controles.</summary>
        public void Configurar(bool controleEsquerdo, string[] textos)
        {
            esquerdo = controleEsquerdo;
            linhas = textos;
        }

        private void OnDisable()
        {
            ativas.Remove(this);
            olhandoDesde = -1f;
            if (grupo != null)
                grupo.alpha = 0f;
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            if (cartao == null)
                CriarCartao();
            var cabeca = camera.transform;
            var alinhamento = Alinhamento(cabeca);
            var olhando = alinhamento > limiarOlhar;
            foreach (var outra in ativas)
            {
                if (outra != this && outra != null && outra.Alinhamento(cabeca) > alinhamento)
                    olhando = false;
            }

            if (!olhando)
                olhandoDesde = -1f;
            else if (olhandoDesde < 0f)
                olhandoDesde = Time.unscaledTime;
            var visivel = olhando && Time.unscaledTime - olhandoDesde >= esperaSegundos;
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, visivel ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (grupo.alpha <= 0f)
                return;
            // Acima do controle e um pouco para o lado de dentro, para não cobrir os botões.
            var lado = esquerdo ? cabeca.right : -cabeca.right;
            var posicao = transform.position + Vector3.up * 0.075f + lado * 0.03f;
            cartao.SetPositionAndRotation(posicao, Quaternion.LookRotation(posicao - cabeca.position, Vector3.up));
        }

        private void CriarCartao()
        {
            var textos = linhas != null && linhas.Length > 0 ? linhas : LinhasPadrao(esquerdo);
            var objeto = new GameObject("Legenda_Controle_" + (esquerdo ? "Esquerdo" : "Direito"), typeof(RectTransform));
            // Fora da hierarquia do controle: o cartão segue a posição do controle mas nunca gira com ele.
            cartao = (RectTransform)objeto.transform;
            cartao.sizeDelta = new Vector2(LarguraCartao, textos.Length * AlturaLinha + 16f);
            cartao.pivot = new Vector2(0.5f, 0f);
            cartao.localScale = Vector3.one * Escala;
            var canvas = objeto.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 90;
            TemaVRSNC.PrepararCanvasMundo(objeto);
            grupo = objeto.AddComponent<CanvasGroup>();
            grupo.alpha = 0f;
            grupo.interactable = false;
            grupo.blocksRaycasts = false;
            var fundo = objeto.AddComponent<Image>();
            fundo.color = TemaVRSNC.FundoRotulo;
            fundo.raycastTarget = false;
            var fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (var indice = 0; indice < textos.Length; indice++)
            {
                var linha = new GameObject("Linha_" + indice, typeof(RectTransform));
                linha.transform.SetParent(cartao, false);
                var retangulo = (RectTransform)linha.transform;
                retangulo.anchorMin = new Vector2(0f, 1f);
                retangulo.anchorMax = new Vector2(1f, 1f);
                retangulo.pivot = new Vector2(0.5f, 1f);
                retangulo.offsetMin = new Vector2(12f, -8f - (indice + 1) * AlturaLinha);
                retangulo.offsetMax = new Vector2(-12f, -8f - indice * AlturaLinha);
                var texto = linha.AddComponent<Text>();
                texto.font = fonte;
                texto.fontSize = TamanhoFonte;
                texto.alignment = TextAnchor.MiddleLeft;
                texto.horizontalOverflow = HorizontalWrapMode.Overflow;
                texto.verticalOverflow = VerticalWrapMode.Overflow;
                texto.raycastTarget = false;
                // "Botão · ação": o botão em destaque azul, a ação no texto claro.
                var partes = textos[indice].Split('·');
                texto.supportRichText = true;
                texto.text = partes.Length == 2
                    ? "<color=#" + ColorUtility.ToHtmlStringRGB(TemaVRSNC.Destaque) + "><b>" + partes[0].Trim() + "</b></color>  " + partes[1].Trim()
                    : textos[indice];
                texto.color = TemaVRSNC.Texto;
            }
        }

        private void OnDestroy()
        {
            if (cartao != null)
                Destroy(cartao.gameObject);
        }
    }
}
