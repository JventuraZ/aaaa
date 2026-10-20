using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>Polimento idempotente apenas de apresentação para a UI existente no mundo e no pulso.</summary>
internal static class AcabamentoInterfaceEspacial
{
    private const string CaminhoCena = "Assets/Scenes/SampleScene.unity";
    private const string CaminhoImagemArredondada = "Assets/VRTemplateAssets/Sprites/UI/Round Radius 10.png";
    private static readonly Color PainelCinza = new(0.25f, 0.27f, 0.30f, 0.90f);
    private static readonly Color JanelaFlutuanteCinza = new(0.22f, 0.24f, 0.28f, 0.84f);
    private static readonly Color BrancoBotao = new(0.96f, 0.97f, 0.98f, 1f);
    private static readonly Color EscuroTexto = new(0.08f, 0.09f, 0.11f, 1f);
    [MenuItem("Ferramentas/VR SNC/Interface/Aplicar Ajustes da Interface Espacial", false, 240)]
    private static void Aplicar()
    {
        var cena = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || cena.path != CaminhoCena || cena.isDirty)
            throw new InvalidOperationException("Use um Editor ocioso com uma SampleScene salva no Modo de Edição.");
        var raiz = GameObject.Find("Painel_Anatomico_VR_SNC");
        if (raiz == null)
            throw new InvalidDataException("Painel_Anatomico_VR_SNC nao foi encontrado.");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoImagemArredondada);
        if (sprite == null)
            throw new FileNotFoundException("O sprite de UI arredondado não foi encontrado.", CaminhoImagemArredondada);
        Undo.IncrementCurrentGroup();
        var grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Aplicar o polimento da UI anatômica espacial");
        try
        {
            EstiloPainel(raiz.transform, sprite, false);
            var pulso = GameObject.Find("Oraculo_Anatomico_Punho");
            if (pulso != null)
                EstiloPainel(pulso.transform, sprite, true);
            // O layout final é sempre o painel compacto com abas; reaplicar esta ferramenta não volta ao anterior.
            ConfiguracaoPainelCompactoAbas.Aplicar(raiz, pulso);
            Validar(raiz.transform, pulso != null ? pulso.transform : null);
            EditorSceneManager.MarkSceneDirty(cena);
            if (!EditorSceneManager.SaveScene(cena))
                throw new IOException("Não foi possível salvar o polimento da UI espacial.");
            Undo.CollapseUndoOperations(grupo);
        }
        catch
        {
            Undo.RevertAllDownToGroup(grupo);
            throw;
        }
    }

    private static void EstiloPainel(Transform raiz, Sprite sprite, bool janelaFlutuante)
    {
        if (janelaFlutuante)
        {
            var raizCanvas = raiz.GetComponentInChildren<Canvas>(true);
            if (raizCanvas != null)
            {
                Undo.RecordObject(raizCanvas, "Definir a ordem de classificação do Canvas do pop-up do oráculo");
                // Um canvas raiz já possui seu contexto de ordenação; o Unity normaliza
                // overrideSorting para falso nele. Apenas canvases aninhados precisam disso.
                if (!raizCanvas.isRootCanvas)
                    raizCanvas.overrideSorting = true;
                raizCanvas.sortingOrder = Math.Max(raizCanvas.sortingOrder, 120);
            }
        }

        EstiloNomeadosSuperficie(raiz, "Background", sprite, janelaFlutuante ? JanelaFlutuanteCinza : PainelCinza);
        EstiloNomeadosSuperficie(raiz, "Header", sprite, janelaFlutuante ? JanelaFlutuanteCinza : PainelCinza);
        EstiloNomeadosSuperficie(raiz, "InfoPanel", sprite, janelaFlutuante ? JanelaFlutuanteCinza : PainelCinza);
        EstiloNomeadosSuperficie(raiz, "NavigationPanel", sprite, janelaFlutuante ? JanelaFlutuanteCinza : PainelCinza);
        if (janelaFlutuante)
            EstiloNomeadosSuperficie(raiz, "Panel", sprite, JanelaFlutuanteCinza);
        if (!janelaFlutuante)
        {
            ConfigurarDensidadeCanvasPrincipal(raiz);
            EstiloCabecalhoTitulo(raiz);
            ReorganizarInformacaoPainel(raiz);
            ReorganizarNavegacaoPainel(raiz);
        }

        foreach (var botao in raiz.GetComponentsInChildren<Button>(true))
            EstiloBotao(botao, sprite, janelaFlutuante);
        foreach (var texto in raiz.GetComponentsInChildren<Text>(true))
        {
            if (texto.GetComponentInParent<Button>() != null)
                continue;
            Undo.RecordObject(texto, "Polish spatial UI text");
            texto.color = Color.white;
        }

        foreach (var texto2 in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (texto2.GetComponentInParent<Button>() != null)
                continue;
            Undo.RecordObject(texto2, "Polish spatial UI TMP text");
            texto2.color = Color.white;
        }
    }

    private static void EstiloNomeadosSuperficie(Transform raiz, string nome, Sprite sprite, Color cor)
    {
        foreach (var imagem in raiz.GetComponentsInChildren<Image>(true))
        {
            if (imagem.name != nome || imagem.GetComponent<Mask>() != null || imagem.GetComponent<RectMask2D>() != null)
                continue;
            Undo.RecordObject(imagem, "Polish known spatial UI surface");
            imagem.sprite = sprite;
            imagem.type = Image.Type.Sliced;
            imagem.color = cor;
        }
    }

    private static void EstiloCabecalhoTitulo(Transform raiz)
    {
        RectTransform cabecalho = null;
        foreach (var retangulo in raiz.GetComponentsInChildren<RectTransform>(true))
            if (retangulo.name == "Header")
            {
                cabecalho = retangulo;
                break;
            }

        if (cabecalho == null)
            return;
        // O cabeçalho tem apenas 72 unidades de UI. Mantém o título completo em português em uma linha
        // dedicada de largura total e reserva duas linhas compactas independentes para categoria/status e caminho/código.
        ConfigurarCabecalhoLinha(cabecalho.Find("Title") as RectTransform, new Vector2(0.04f, 0.48f), new Vector2(0.96f, 0.96f), 26);
        ConfigurarCabecalhoLinha(cabecalho.Find("Status") as RectTransform, new Vector2(0.04f, 0.12f), new Vector2(0.56f, 0.46f), 14);
        ConfigurarCabecalhoLinha(cabecalho.Find("Path") as RectTransform, new Vector2(0.58f, 0.12f), new Vector2(0.96f, 0.46f), 12);
    }

    private static void ConfigurarCabecalhoLinha(RectTransform linha, Vector2 ancoraMinimo, Vector2 ancoraMaximo, int tamanhoFonte)
    {
        if (linha == null)
            return;
        Undo.RecordObject(linha, "Organizar a linha de cabeçalho da anatomia");
        linha.anchorMin = ancoraMinimo;
        linha.anchorMax = ancoraMaximo;
        linha.offsetMin = Vector2.zero;
        linha.offsetMax = Vector2.zero;
        foreach (var texto in linha.GetComponentsInChildren<Text>(true))
        {
            Undo.RecordObject(texto, "Destacar o título da anatomia");
            texto.fontSize = tamanhoFonte;
            texto.resizeTextForBestFit = false;
        }

        foreach (var texto2 in linha.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(texto2, "Destacar o título TMP da anatomia");
            texto2.enableAutoSizing = false;
            texto2.fontSize = tamanhoFonte;
        }
    }

    private static void ConfigurarDensidadeCanvasPrincipal(Transform raiz)
    {
        var escalonador = raiz.GetComponentInChildren<CanvasScaler>(true);
        if (escalonador == null)
            return;
        Undo.RecordObject(escalonador, "Definir densidade legível do Canvas do painel no mundo");
        escalonador.dynamicPixelsPerUnit = 4f;
    }

    private static void ReorganizarInformacaoPainel(Transform raiz)
    {
        var informacao = raiz.GetComponentsInChildren<RectTransform>(true);
        RectTransform painel = null;
        foreach (var candidato in informacao)
        {
            if (candidato.name == "InfoPanel")
            {
                painel = candidato;
                break;
            }
        }

        if (painel == null)
            return;
        // A identidade fica destacada, a categoria continua legível e o corpo científico é
        // recortado por um ScrollRect real em vez de invadir a área de ações.
        var exibicao = painel.Find("DisplayName") as RectTransform;
        var divisao = painel.Find("Division") as RectTransform;
        var descricao = (painel.Find("Description") ?? painel.Find("DescriptionViewport/Description")) as RectTransform;
        // Categoria e classificação ocupam uma linha cada; a descrição rolável fica com a maior parte da altura.
        ConfigurarInformacaoLinha(exibicao, new Vector2(0.04f, 0.85f), new Vector2(0.96f, 1f));
        ConfigurarInformacaoLinha(divisao, new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.85f));
        DefinirTextoLegivel(exibicao, 18, FontStyle.Bold);
        DefinirTextoLegivel(divisao, 18, FontStyle.Normal);
        GarantirDescricaoRolagem(painel, descricao);
        var nivel = painel.Find("Level");
        if (nivel != null && nivel.gameObject.activeSelf)
        {
            Undo.RecordObject(nivel.gameObject, "Ocultar o nível de anatomia de depuração do painel espacial");
            nivel.gameObject.SetActive(false);
        }

        var nomeNo = painel.Find("NodeName");
        if (nomeNo != null && nomeNo.gameObject.activeSelf)
        {
            // BrainInfoPanel escreve a identidade selecionada em DisplayName/Division; NodeName
            // caso contrário duplica o título de status ou seu texto de reserva anterior à seleção.
            Undo.RecordObject(nomeNo.gameObject, "Ocultar o rótulo duplicado do nó anatômico");
            nomeNo.gameObject.SetActive(false);
        }
    }

    private static void ReorganizarNavegacaoPainel(Transform raiz)
    {
        var painel = raiz.Find("ExpandedRoot/NavigationPanel") as RectTransform;
        if (painel == null)
            return;
        var titulo = painel.Find("NavigationTitle") as RectTransform;
        ConfigurarInformacaoLinha(titulo, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f));
        DefinirTextoLegivel(titulo, 18, FontStyle.Bold);
        var areaVisivel = painel.Find("Viewport") as RectTransform;
        if (areaVisivel != null)
        {
            ConfigurarInformacaoLinha(areaVisivel, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.84f));
            var rolagem = painel.GetComponent<ScrollRect>();
            if (rolagem != null)
            {
                Undo.RecordObject(rolagem, "Keep navigation list scrollable");
                rolagem.vertical = true;
                rolagem.horizontal = false;
                rolagem.movementType = ScrollRect.MovementType.Clamped;
            }
        }
    }

    private static void GarantirDescricaoRolagem(RectTransform painel, RectTransform descricao)
    {
        if (descricao == null)
            return;
        var areaVisivel = painel.Find("DescriptionViewport") as RectTransform;
        if (areaVisivel == null)
        {
            var criado = new GameObject("DescriptionViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            Undo.RegisterCreatedObjectUndo(criado, "Criar área rolável de descrição científica");
            criado.layer = painel.gameObject.layer;
            criado.transform.SetParent(painel, false);
            areaVisivel = criado.GetComponent<RectTransform>();
            var imagem = criado.GetComponent<Image>();
            imagem.color = new Color(0f, 0f, 0f, 0f);
            imagem.raycastTarget = true;
        }

        // A ferramenta pode ser reaplicada a cenas migradas. Repare também uma viewport
        // existente incompleta, em vez de assumir que seus componentes ainda existem.
        var areaVisivelImagem = areaVisivel.GetComponent<Image>() ?? Undo.AddComponent<Image>(areaVisivel.gameObject);
        var areaVisivelMascara = areaVisivel.GetComponent<RectMask2D>() ?? Undo.AddComponent<RectMask2D>(areaVisivel.gameObject);
        var rolagem = areaVisivel.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(areaVisivel.gameObject);
        Undo.RecordObject(areaVisivelImagem, "Keep scientific description viewport interactive");
        areaVisivelImagem.color = new Color(0f, 0f, 0f, 0f);
        areaVisivelImagem.raycastTarget = true;
        Undo.RecordObject(areaVisivelMascara, "Keep scientific description viewport clipped");
        ConfigurarInformacaoLinha(areaVisivel, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.68f));
        if (descricao.parent != areaVisivel)
        {
            Undo.SetTransformParent(descricao, areaVisivel, "Posicionar a descrição científica na área de rolagem");
        }

        Undo.RecordObject(descricao, "Lay out scrollable scientific description");
        descricao.anchorMin = new Vector2(0f, 1f);
        descricao.anchorMax = new Vector2(1f, 1f);
        descricao.pivot = new Vector2(0.5f, 1f);
        descricao.anchoredPosition = Vector2.zero;
        descricao.sizeDelta = new Vector2(0f, 0f);
        var ajustador = descricao.GetComponent<ContentSizeFitter>() ?? Undo.AddComponent<ContentSizeFitter>(descricao.gameObject);
        Undo.RecordObject(ajustador, "Ajustar a descrição científica ao conteúdo rolável");
        ajustador.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        ajustador.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        DefinirTextoLegivel(descricao, 18, FontStyle.Normal);
        foreach (var texto in descricao.GetComponentsInChildren<Text>(true))
        {
            texto.verticalOverflow = VerticalWrapMode.Overflow;
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        Undo.RecordObject(rolagem, "Configurar a rolagem da descrição científica");
        rolagem.viewport = areaVisivel;
        rolagem.content = descricao;
        rolagem.horizontal = false;
        rolagem.vertical = true;
        rolagem.movementType = ScrollRect.MovementType.Clamped;
    }

    private static void DefinirTextoLegivel(RectTransform linha, int tamanhoFonte, FontStyle estilo)
    {
        if (linha == null)
            return;
        foreach (var texto in linha.GetComponentsInChildren<Text>(true))
        {
            Undo.RecordObject(texto, "Definir texto anatômico legível");
            texto.resizeTextForBestFit = false;
            texto.fontSize = tamanhoFonte;
            texto.fontStyle = estilo;
        }

        foreach (var texto2 in linha.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(texto2, "Definir texto TMP anatômico legível");
            texto2.enableAutoSizing = false;
            texto2.fontSize = tamanhoFonte;
            texto2.fontStyle = estilo == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private static void ConfigurarInformacaoLinha(RectTransform linha, Vector2 ancoraMinimo, Vector2 ancoraMaximo)
    {
        if (linha == null)
            return;
        Undo.RecordObject(linha, "Reorganizar o painel de informações anatômicas");
        linha.anchorMin = ancoraMinimo;
        linha.anchorMax = ancoraMaximo;
        linha.offsetMin = Vector2.zero;
        linha.offsetMax = Vector2.zero;
    }

    private static bool TextoEstaVazio(Transform linha)
    {
        var legado = linha.GetComponent<Text>();
        if (legado != null)
            return string.IsNullOrWhiteSpace(legado.text);
        var textoTMP = linha.GetComponent<TMP_Text>();
        return textoTMP != null && string.IsNullOrWhiteSpace(textoTMP.text);
    }

    private static void EstiloBotao(Button botao, Sprite sprite, bool janelaFlutuante)
    {
        var imagem = botao.targetGraphic as Image ?? botao.GetComponent<Image>();
        if (imagem != null)
        {
            Undo.RecordObject(imagem, "Aprimorar o fundo do botão da UI espacial");
            imagem.sprite = sprite;
            imagem.type = Image.Type.Sliced;
            imagem.color = BrancoBotao;
            botao.targetGraphic = imagem;
        }

        Undo.RecordObject(botao, "Aprimorar as cores do botão da UI espacial");
        var cores = botao.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(0.82f, 0.86f, 0.90f, 1f);
        cores.pressedColor = new Color(0.66f, 0.72f, 0.79f, 1f);
        // Inativo fica translúcido; um cinza opaco sobre o painel escuro parecia a ação principal.
        cores.disabledColor = VRSNC.Interface.EstiloBotaoInterface.CorDesativado;
        botao.colors = cores;
        var rotulo = botao.gameObject.name;
        var primario = rotulo.IndexOf("Entrar", StringComparison.OrdinalIgnoreCase) >= 0 || rotulo.IndexOf("Isolar", StringComparison.OrdinalIgnoreCase) >= 0 || rotulo.IndexOf("Enter", StringComparison.OrdinalIgnoreCase) >= 0 || rotulo.IndexOf("Isolate", StringComparison.OrdinalIgnoreCase) >= 0;
        var secundario = rotulo.IndexOf("Fonte", StringComparison.OrdinalIgnoreCase) >= 0 || rotulo.IndexOf("Voltar", StringComparison.OrdinalIgnoreCase) >= 0 || rotulo.IndexOf("Source", StringComparison.OrdinalIgnoreCase) >= 0 || rotulo.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0;
        var retangulo = botao.transform as RectTransform;
        if (retangulo != null && (primario || secundario))
        {
            var alturaAlvo = primario ? 50f : 45f;
            var organizacao = botao.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(botao.gameObject);
            Undo.RecordObject(organizacao, "Size spatial UI action hit area");
            organizacao.minHeight = Mathf.Max(organizacao.minHeight, alturaAlvo);
            organizacao.preferredHeight = Mathf.Max(organizacao.preferredHeight, alturaAlvo);
        }

        var tamanhoRotulo = primario ? 18 : secundario ? 16 : 14;
        foreach (var texto in botao.GetComponentsInChildren<Text>(true))
        {
            Undo.RecordObject(texto, "Aprimorar o rótulo do botão da UI espacial");
            texto.color = EscuroTexto;
            texto.raycastTarget = false;
            texto.resizeTextForBestFit = false;
            texto.fontSize = tamanhoRotulo;
            texto.fontStyle = primario ? FontStyle.Bold : FontStyle.Normal;
        }

        foreach (var texto2 in botao.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(texto2, "Aprimorar o rótulo TMP do botão da UI espacial");
            texto2.color = EscuroTexto;
            texto2.raycastTarget = false;
            texto2.enableAutoSizing = false;
            texto2.fontSize = tamanhoRotulo;
            texto2.fontStyle = primario ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private static void Validar(Transform principal, Transform janelaFlutuante)
    {
        var avisos = new List<string>();
        var botoes = 0;
        foreach (var botao in principal.GetComponentsInChildren<Button>(true))
        {
            botoes++;
            var retangulo = botao.transform as RectTransform;
            if (retangulo == null || retangulo.rect.width <= 0f || retangulo.rect.height <= 0f)
                avisos.Add("retângulo do botão inválido " + botao.name);
        }

        var canvas = principal.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            avisos.Add("Canvas principal ausente");
        if (janelaFlutuante != null)
        {
            var janelaFlutuanteCanvas = janelaFlutuante.GetComponentInChildren<Canvas>(true);
            if (janelaFlutuanteCanvas != null && !janelaFlutuanteCanvas.isRootCanvas && !janelaFlutuanteCanvas.overrideSorting)
                avisos.Add("nested popup canvas lacks independent sorting " + janelaFlutuanteCanvas.name);
            if (janelaFlutuanteCanvas != null && janelaFlutuanteCanvas.isRootCanvas && janelaFlutuanteCanvas.sortingOrder < 120)
                avisos.Add("ordem de classificação do Canvas raiz do pop-up abaixo de 120 " + janelaFlutuanteCanvas.name);
            foreach (var aninhado in janelaFlutuante.GetComponentsInChildren<Canvas>(true))
                if (aninhado != janelaFlutuanteCanvas && aninhado.overrideSorting)
                    avisos.Add("nested popup canvas overrides sorting " + aninhado.name);
        }

        Debug.Log("[Polimento da UI espacial] validado " + botoes + " buttons; " + (avisos.Count == 0 ? "verificações de geometria e texto do Canvas aprovadas." : string.Join("; ", avisos)));
    }
}