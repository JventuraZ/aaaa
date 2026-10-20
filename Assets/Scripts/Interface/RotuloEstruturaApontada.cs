using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VRSNC.Interacao;

namespace VRSNC.Interface
{
    /// <summary>
    /// Rótulo flutuante com o nome em português da estrutura apontada, antes de selecioná-la.
    /// Fica logo acima do ponto onde o raio toca a peça (ou do centro dela, quando é a mão que aponta),
    /// voltado para a câmera e com tamanho aparente constante. É desenhado por cima da anatomia para
    /// nunca ficar escondido dentro da malha.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RotuloEstruturaApontada : MonoBehaviour
    {
        // Altura do rótulo em unidades do canvas (texto + margens); a escala converte isso no tamanho angular.
        private const float AlturaRotuloUnidades = 44f;
        private const int TamanhoFonte = 28;

        [SerializeField]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [SerializeField]
        private ControladorSelecaoPorRaio controladorRaio;
        // Altura aparente do rótulo, em graus do campo de visão.
        [SerializeField, Range(0.8f, 3f)]
        private float alturaAngularGraus = 2.2f;
        private RectTransform raizVisual;
        private Text texto;
        private Material materialSempreVisivel;
        private readonly Dictionary<NoInteracaoAnatomica, Vector3> centrosLocais = new();
        // Rótulo pedido por outro sistema (ex.: eletrodo do EEG apontado), válido só no quadro em que foi pedido.
        private string textoExterno;
        private Vector3 pontoExterno;
        private int quadroExterno = -1;

        /// <summary>Mostra outro rótulo neste quadro, no lugar do da anatomia. Chamar a cada Update enquanto valer.</summary>
        public void DefinirRotuloExterno(string conteudo, Vector3 ponto)
        {
            textoExterno = conteudo;
            pontoExterno = ponto;
            quadroExterno = Time.frameCount;
        }

        private void Awake()
        {
            if (gerenciadorSelecao == null)
                gerenciadorSelecao = GetComponent<GerenciadorSelecaoHierarquica>();
            if (controladorRaio == null)
                controladorRaio = GetComponent<ControladorSelecaoPorRaio>();
        }

        private void OnDestroy()
        {
            if (raizVisual != null)
                Destroy(raizVisual.gameObject);
            if (materialSempreVisivel != null)
                Destroy(materialSempreVisivel);
        }

        private void LateUpdate()
        {
            var no = gerenciadorSelecao != null ? gerenciadorSelecao.EmFocoNo : null;
            var externo = quadroExterno == Time.frameCount && !string.IsNullOrEmpty(textoExterno);
            var camera = Camera.main;
            if (!Application.isPlaying || (no == null && !externo) || camera == null)
            {
                Mostrar(false);
                return;
            }

            if (raizVisual == null)
                CriarVisual();
            var ponto = externo ? pontoExterno : controladorRaio != null && controladorRaio.PontoAcertoAtual.HasValue ? controladorRaio.PontoAcertoAtual.Value : ObterCentro(no);
            var cabeca = camera.transform;
            var paraCamera = cabeca.position - ponto;
            var distancia = paraCamera.magnitude;
            // Poses inválidas (rastreamento perdido) nunca chegam ao transform do rótulo.
            if (float.IsNaN(distancia) || float.IsInfinity(distancia) || distancia < 0.05f)
            {
                Mostrar(false);
                return;
            }

            // Um pouco à frente e acima do ponto, para não cobrir o que está sendo apontado.
            var posicao = ponto + paraCamera / distancia * 0.02f + cabeca.up * (distancia * 0.03f);
            raizVisual.position = posicao;
            raizVisual.rotation = Quaternion.LookRotation(posicao - cabeca.position, cabeca.up);
            var alturaMundo = 2f * Vector3.Distance(cabeca.position, posicao) * Mathf.Tan(alturaAngularGraus * 0.5f * Mathf.Deg2Rad);
            raizVisual.localScale = Vector3.one * (alturaMundo / AlturaRotuloUnidades);
            var rotulo = externo ? textoExterno : no.NosFilhos.Count > 0 ? no.NomeLegivelComParte + "  ·  grupo" : no.NomeLegivelComParte;
            if (texto.text != rotulo)
                texto.text = rotulo;
            Mostrar(true);
        }

        private void Mostrar(bool visivel)
        {
            if (raizVisual != null && raizVisual.gameObject.activeSelf != visivel)
                raizVisual.gameObject.SetActive(visivel);
        }

        /// <summary>Centro visual da estrutura no espaço dela, para acompanhar peças movidas ou explodidas.</summary>
        private Vector3 ObterCentro(NoInteracaoAnatomica no)
        {
            if (!centrosLocais.TryGetValue(no, out var local))
            {
                var limites = new Bounds(no.transform.position, Vector3.zero);
                var primeiro = true;
                foreach (var renderizador in no.GetComponentsInChildren<Renderer>())
                {
                    if (primeiro)
                    {
                        limites = renderizador.bounds;
                        primeiro = false;
                    }
                    else
                    {
                        limites.Encapsulate(renderizador.bounds);
                    }
                }

                local = no.transform.InverseTransformPoint(limites.center);
                centrosLocais[no] = local;
            }

            return no.transform.TransformPoint(local);
        }

        /// <summary>Canvas pequeno em espaço de mundo, fora da hierarquia anatômica, sem raycast.</summary>
        private void CriarVisual()
        {
            var objeto = new GameObject("Rotulo_Estrutura_Apontada", typeof(RectTransform));
            raizVisual = (RectTransform)objeto.transform;
            raizVisual.pivot = new Vector2(0.5f, 0f);
            var canvas = objeto.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            TemaVRSNC.PrepararCanvasMundo(objeto);
            // UI/Default com ZTest Always: o rótulo nunca afunda dentro da malha anatômica.
            materialSempreVisivel = new Material(Shader.Find("UI/Default"));
            materialSempreVisivel.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            var fundo = objeto.AddComponent<Image>();
            fundo.color = TemaVRSNC.FundoRotulo;
            fundo.material = materialSempreVisivel;
            fundo.raycastTarget = false;
            var organizacao = objeto.AddComponent<HorizontalLayoutGroup>();
            organizacao.padding = new RectOffset(16, 16, 6, 6);
            organizacao.childControlWidth = true;
            organizacao.childControlHeight = true;
            var ajuste = objeto.AddComponent<ContentSizeFitter>();
            ajuste.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var textoObjeto = new GameObject("Texto", typeof(RectTransform));
            textoObjeto.transform.SetParent(raizVisual, false);
            texto = textoObjeto.AddComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = TamanhoFonte;
            texto.color = TemaVRSNC.Texto;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.horizontalOverflow = HorizontalWrapMode.Overflow;
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            texto.material = materialSempreVisivel;
            texto.raycastTarget = false;
            objeto.SetActive(false);
        }
    }
}
