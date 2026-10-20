using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VRSNC.Interacao;

namespace VRSNC.Interface
{
    /// <summary>
    /// Caminho de navegação clicável no cabeçalho do painel: "Início › … › últimos níveis".
    /// Cada nível volta direto àquele grupo sem resetar nada; o nível atual aparece em destaque, sem clique.
    /// Quando o caminho não cabe na largura, os níveis mais antigos viram "…" e, por último, o nome atual é encurtado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarraCaminhoAnatomico : MonoBehaviour
    {
        private const int NiveisVisiveis = 3;
        private const int CaracteresMaximosPorNivel = 24;
        private const int CaracteresMinimosNivelAtual = 6;
        private const float AlturaItem = 26f;
        private const int TamanhoFonte = 16;
        private const int PreenchimentoBotao = 8;
        private const float EspacamentoItens = 4f;
        private const int CamadaInterface = 5;
        private const string Separador = "›";
        private const string Reticencias = "…";

        [SerializeField]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        // Texto de caminho antigo do cabeçalho; fica oculto enquanto a barra clicável está ativa.
        [SerializeField]
        private Text textoCaminhoOriginal;
        private RectTransform conteiner;
        // Texto invisível, fora do layout, usado só para medir a largura dos rótulos antes de montar o caminho.
        private Text medidor;
        private readonly List<GameObject> itens = new();

        private void Awake()
        {
            if (gerenciadorSelecao == null)
                gerenciadorSelecao = FindFirstObjectByType<GerenciadorSelecaoHierarquica>();
            if (textoCaminhoOriginal == null)
            {
                var caminho = transform.Find("ExpandedRoot/Header/Path");
                textoCaminhoOriginal = caminho != null ? caminho.GetComponent<Text>() : null;
            }

            CriarConteiner();
        }

        private void OnEnable()
        {
            if (gerenciadorSelecao != null)
                gerenciadorSelecao.emFocoAlterado.AddListener(Atualizar);
        }

        private void OnDisable()
        {
            if (gerenciadorSelecao != null)
                gerenciadorSelecao.emFocoAlterado.RemoveListener(Atualizar);
        }

        private void Start()
        {
            Atualizar(gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null);
        }

        /// <summary>Remonta o caminho para o foco informado.</summary>
        public void Atualizar(NoInteracaoAnatomica foco)
        {
            if (conteiner == null)
                return;
            // Destroy só conclui no fim do quadro: retirar antes evita itens duplicados com duas trocas no mesmo quadro.
            foreach (var item in itens)
            {
                if (item == null)
                    continue;
                item.SetActive(false);
                item.transform.SetParent(null, false);
                Destroy(item);
            }

            itens.Clear();
            var niveis = new List<NoInteracaoAnatomica>();
            for (var atual = foco; atual != null; atual = atual.NoPai)
                niveis.Insert(0, atual);
            if (niveis.Count == 0)
                return;

            // niveis[0] é a raiz ("Início"); depois dela, só os últimos níveis que couberem na largura do cabeçalho.
            var visiveis = Mathf.Min(NiveisVisiveis, niveis.Count - 1);
            var nomeAtual = niveis.Count > 1 ? niveis[^1].NomeLegivel : "Início";
            var limiteAtual = CaracteresMaximosPorNivel;
            var rotuloAtual = Encurtar(nomeAtual, limiteAtual);
            var disponivel = conteiner.rect.width;
            if (disponivel > 0f)
            {
                while (visiveis > 1 && LarguraCaminho(niveis, visiveis, rotuloAtual) > disponivel)
                    visiveis--;
                while (limiteAtual > CaracteresMinimosNivelAtual && LarguraCaminho(niveis, visiveis, rotuloAtual) > disponivel)
                    rotuloAtual = Encurtar(nomeAtual, limiteAtual -= 2);
            }

            if (niveis.Count == 1)
            {
                CriarTexto(rotuloAtual, true);
                return;
            }

            CriarItem("Início", niveis[0]);
            var primeiroVisivel = niveis.Count - visiveis;
            if (primeiroVisivel > 1)
            {
                CriarSeparador();
                CriarTexto(Reticencias, false);
            }

            for (var indice = primeiroVisivel; indice < niveis.Count; indice++)
            {
                CriarSeparador();
                if (indice == niveis.Count - 1)
                    CriarTexto(rotuloAtual, true);
                else
                    CriarItem(Encurtar(niveis[indice].NomeLegivel), niveis[indice]);
            }
        }

        /// <summary>Largura que o caminho ocuparia com os últimos <paramref name="visiveis"/> níveis.</summary>
        private float LarguraCaminho(List<NoInteracaoAnatomica> niveis, int visiveis, string rotuloAtual)
        {
            if (niveis.Count == 1)
                return Medir(rotuloAtual, true);
            var largura = Medir("Início", false) + 2 * PreenchimentoBotao;
            var quantidade = 1;
            var primeiroVisivel = niveis.Count - visiveis;
            if (primeiroVisivel > 1)
            {
                largura += Medir(Separador, false) + Medir(Reticencias, false);
                quantidade += 2;
            }

            for (var indice = primeiroVisivel; indice < niveis.Count; indice++)
            {
                largura += Medir(Separador, false);
                largura += indice == niveis.Count - 1 ? Medir(rotuloAtual, true) : Medir(Encurtar(niveis[indice].NomeLegivel), false) + 2 * PreenchimentoBotao;
                quantidade += 2;
            }

            return largura + EspacamentoItens * (quantidade - 1);
        }

        private float Medir(string conteudo, bool destaque)
        {
            if (medidor == null)
                return 0f;
            medidor.fontStyle = destaque ? FontStyle.Bold : FontStyle.Normal;
            medidor.text = conteudo;
            return Mathf.Ceil(medidor.preferredWidth);
        }

        private void IrPara(NoInteracaoAnatomica alvo)
        {
            if (gerenciadorSelecao == null || alvo == null)
                return;
            gerenciadorSelecao.LimparSelecao();
            if (alvo.NoPai == null)
                gerenciadorSelecao.RedefinirFoco();
            else
                gerenciadorSelecao.EntrarNo(alvo);
        }

        /// <summary>Nível clicável; sem destino, é o nível atual e aparece em negrito, sem botão.</summary>
        private void CriarItem(string rotulo, NoInteracaoAnatomica destino)
        {
            if (destino == null)
            {
                CriarTexto(rotulo, true);
                return;
            }

            var objeto = new GameObject("Caminho_" + rotulo, typeof(RectTransform));
            objeto.transform.SetParent(conteiner, false);
            objeto.layer = CamadaInterface;
            var imagem = objeto.AddComponent<Image>();
            var sprite = TemaVRSNC.SpriteArredondado(transform);
            if (sprite != null)
            {
                imagem.sprite = sprite;
                imagem.type = Image.Type.Sliced;
            }

            imagem.color = TemaVRSNC.Botao;
            var botao = objeto.AddComponent<Button>();
            botao.targetGraphic = imagem;
            botao.colors = TemaVRSNC.BlocoCores(botao.colors);
            var organizacao = CriarOrganizacaoItem(objeto);
            organizacao.padding = new RectOffset(PreenchimentoBotao, PreenchimentoBotao, 2, 2);
            var elemento = objeto.AddComponent<LayoutElement>();
            elemento.minHeight = AlturaItem;
            elemento.preferredHeight = AlturaItem;
            // Largura mínima = rótulo inteiro: o layout nunca espreme um nível a ponto de cortar letras.
            elemento.minWidth = Medir(rotulo, false) + 2 * PreenchimentoBotao;
            var texto = CriarTextoFilho(objeto.transform, rotulo, false);
            texto.color = TemaVRSNC.Destaque;
            botao.onClick.AddListener(() => IrPara(destino));
            itens.Add(objeto);
        }

        private void CriarSeparador() => CriarTexto(Separador, false, TemaVRSNC.TextoApagado);

        private void CriarTexto(string conteudo, bool destaque, Color? cor = null)
        {
            var objeto = new GameObject("Caminho_Texto", typeof(RectTransform));
            objeto.transform.SetParent(conteiner, false);
            objeto.layer = CamadaInterface;
            var texto = CriarTextoFilho(objeto.transform, conteudo, destaque);
            if (cor.HasValue)
                texto.color = cor.Value;
            CriarOrganizacaoItem(objeto);
            var elemento = objeto.AddComponent<LayoutElement>();
            elemento.minHeight = AlturaItem;
            elemento.preferredHeight = AlturaItem;
            elemento.minWidth = Medir(conteudo, destaque);
            itens.Add(objeto);
        }

        /// <summary>
        /// Layout de um item: largura do próprio rótulo, altura toda. Sem desligar a expansão horizontal padrão, cada
        /// item vira flexível e o caminho se espalha pela linha (um "Início" sozinho fica no centro).
        /// </summary>
        private static HorizontalLayoutGroup CriarOrganizacaoItem(GameObject objeto)
        {
            var organizacao = objeto.AddComponent<HorizontalLayoutGroup>();
            organizacao.childControlWidth = true;
            organizacao.childControlHeight = true;
            organizacao.childForceExpandWidth = false;
            return organizacao;
        }

        private Text CriarTextoFilho(Transform pai, string conteudo, bool destaque)
        {
            var objeto = new GameObject("Texto", typeof(RectTransform));
            objeto.transform.SetParent(pai, false);
            objeto.layer = CamadaInterface;
            var texto = objeto.AddComponent<Text>();
            texto.text = conteudo;
            texto.font = textoCaminhoOriginal != null && textoCaminhoOriginal.font != null ? textoCaminhoOriginal.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = TamanhoFonte;
            texto.fontStyle = destaque ? FontStyle.Bold : FontStyle.Normal;
            texto.color = TemaVRSNC.Texto;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.horizontalOverflow = HorizontalWrapMode.Overflow;
            texto.verticalOverflow = VerticalWrapMode.Truncate;
            texto.raycastTarget = false;
            return texto;
        }

        /// <summary>Ocupa o lugar do texto de caminho antigo, com os mesmos limites, e o esconde.</summary>
        private void CriarConteiner()
        {
            if (textoCaminhoOriginal == null || conteiner != null)
                return;
            var objeto = new GameObject("Caminho_Navegavel", typeof(RectTransform));
            objeto.layer = CamadaInterface;
            conteiner = (RectTransform)objeto.transform;
            var origem = (RectTransform)textoCaminhoOriginal.transform;
            conteiner.SetParent(origem.parent, false);
            conteiner.anchorMin = origem.anchorMin;
            conteiner.anchorMax = origem.anchorMax;
            conteiner.pivot = origem.pivot;
            conteiner.offsetMin = origem.offsetMin;
            conteiner.offsetMax = origem.offsetMax;
            conteiner.SetSiblingIndex(origem.GetSiblingIndex() + 1);
            var linha = objeto.AddComponent<HorizontalLayoutGroup>();
            linha.spacing = EspacamentoItens;
            linha.childAlignment = TextAnchor.MiddleLeft;
            linha.childControlWidth = true;
            linha.childControlHeight = true;
            linha.childForceExpandWidth = false;
            linha.childForceExpandHeight = false;
            objeto.AddComponent<RectMask2D>();
            textoCaminhoOriginal.enabled = false;

            var objetoMedidor = new GameObject("Caminho_Medidor", typeof(RectTransform));
            objetoMedidor.layer = CamadaInterface;
            objetoMedidor.transform.SetParent(conteiner, false);
            objetoMedidor.AddComponent<LayoutElement>().ignoreLayout = true;
            medidor = CriarTextoFilho(objetoMedidor.transform, string.Empty, false);
            // Desligado: não desenha nada, mas continua medindo com a mesma fonte e o mesmo tamanho dos itens.
            medidor.enabled = false;
        }

        private static string Encurtar(string nome, int maximo = CaracteresMaximosPorNivel)
        {
            if (string.IsNullOrEmpty(nome) || nome.Length <= maximo)
                return nome;
            return nome.Substring(0, Mathf.Max(1, maximo - 1)).TrimEnd() + Reticencias;
        }
    }
}
