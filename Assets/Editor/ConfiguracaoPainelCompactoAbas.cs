using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;
using VRSNC.Interacao;
using VRSNC.Interface;

/// <summary>
/// Painel principal compacto (400 × 550, cerca de 40 × 55 cm) com abas Ficha / Estruturas / EEG, o Voltar no cabeçalho
/// (mostrando para onde volta), barra de ações 3 × 2, escala tipográfica única e oráculo do pulso menor, que aparece ao
/// virar o pulso. No fim aplica o tema "Grafite suave" (<see cref="AplicacaoTemaGrafite"/>) e as legendas dos controles.
/// É idempotente, registra tudo no Desfazer e não salva sozinho; as ferramentas antigas de interface a chamam no fim
/// para nunca voltar ao layout anterior.
/// </summary>
public static class ConfiguracaoPainelCompactoAbas
{
    public const float Largura = 400f;
    public const float Altura = 550f;
    // Faixas verticais do painel (fração da altura, de baixo para cima).
    public const float InicioCabecalho = 0.835f;
    public const float InicioAbas = 0.775f;
    public const float InicioConteudo = 0.25f;
    // Escala tipográfica (unidades de UI; na escala 0,001 do painel, 1 unidade = 1 mm).
    public const int FonteTitulo = 26;
    public const int FonteCorpo = 20;
    public const int FonteSecao = 18;
    public const int FonteBotao = 18;
    public const int FonteCaminho = 16;
    public const int FonteSecundaria = 15;
    public const int FonteMinima = 14;
    public const string RotuloMinimizar = "Minimizar";
    private const float EscalaPulso = 0.00035f;
    private const string CaminhoCena = "Assets/Scenes/SampleScene.unity";
    private const string CaminhoImagemArredondada = "Assets/VRTemplateAssets/Sprites/UI/Round Radius 10.png";
    private const string PresetMaoCaminho = "Assets/VRTemplateAssets/UI/FollowPresets/OracleDorsalHandsFollowPreset.asset";
    private const string PresetControleCaminho = "Assets/VRTemplateAssets/UI/FollowPresets/OracleDorsalControllerFollowPreset.asset";
    private static readonly Color CorRotuloEscuro = new(0.10f, 0.12f, 0.16f, 1f);
    private static readonly Color CorBotao = new(0.96f, 0.97f, 0.98f, 1f);

    [MenuItem("Ferramentas/VR SNC/Interface/Aplicar Painel Compacto com Abas", false, 245)]
    private static void AplicarPeloMenu()
    {
        var cena = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || cena.path != CaminhoCena || cena.isDirty)
            throw new InvalidOperationException("Use um Editor ocioso com uma SampleScene salva no Modo de Edição.");
        var painel = GameObject.Find("Painel_Anatomico_VR_SNC");
        if (painel == null)
            throw new InvalidDataException("Painel_Anatomico_VR_SNC não foi encontrado.");
        Undo.IncrementCurrentGroup();
        var grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Aplicar o painel compacto com abas");
        try
        {
            Aplicar(painel, GameObject.Find("Oraculo_Anatomico_Punho"));
            EditorSceneManager.MarkSceneDirty(cena);
            if (!EditorSceneManager.SaveScene(cena))
                throw new IOException("Não foi possível salvar a cena com o painel compacto.");
            Undo.CollapseUndoOperations(grupo);
            Debug.Log("[Painel compacto] Aplicado: 400 × 550 com abas Ficha / Estruturas, barra de ações única e oráculo do pulso ao virar o pulso.", painel);
        }
        catch
        {
            Undo.RevertAllDownToGroup(grupo);
            throw;
        }
    }

    /// <summary>Aplica o layout compacto ao painel principal e, se houver, ao oráculo do pulso.</summary>
    public static void Aplicar(GameObject painel, GameObject pulso)
    {
        var expandida = painel.transform.Find("ExpandedRoot") as RectTransform;
        if (expandida == null)
            throw new InvalidDataException("ExpandedRoot ausente no painel principal.");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CaminhoImagemArredondada);
        var retanguloPainel = (RectTransform)painel.transform;
        Undo.RecordObject(retanguloPainel, "Definir o tamanho do painel compacto");
        retanguloPainel.sizeDelta = new Vector2(Largura, Altura);
        DisporCabecalho(expandida);
        var botaoFicha = GarantirAba(expandida, sprite, out var botaoEstruturas, out var botaoEEG);
        DisporConteudo(expandida);
        var conteudoEEG = GarantirConteudoEEG(expandida);
        var botaoOcultar = DisporAcoes(expandida, sprite);
        DisporLinhaEspacial(expandida);
        var instrucao = expandida.Find("Barra_Manipulacao_Mundo/Instrucao_Manipulacao_Mundo")?.GetComponent<Text>();
        if (instrucao != null)
        {
            Undo.RecordObject(instrucao, "Instrução da barra legível");
            instrucao.fontSize = 14;
        }

        LigarComponentes(painel, expandida, botaoFicha, botaoEstruturas, botaoOcultar);
        LigarEEG(painel, expandida, botaoEEG, conteudoEEG);
        if (pulso != null)
            AjustarPulso(pulso);
        AjustarRotuloApontado();
        // Por último: o tema repinta tudo o que as etapas acima (e as ferramentas antigas) criaram em tons claros.
        AplicacaoTemaGrafite.Aplicar(painel, pulso);
    }

    // Cabeçalho em três faixas que não se sobrepõem: em cima o Voltar (com o destino) e o estado, no meio o título em
    // uma linha e embaixo o caminho clicável.
    private static void DisporCabecalho(RectTransform expandida)
    {
        var cabecalho = expandida.Find("Header") as RectTransform;
        if (cabecalho == null)
            return;
        Ancorar(cabecalho, new Vector2(0f, InicioCabecalho), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var voltar = expandida.Find("Controls/Button_Back") as RectTransform;
        if (voltar == null)
            voltar = cabecalho.Find("Button_Back") as RectTransform;
        if (voltar != null)
        {
            if (voltar.parent != cabecalho)
                Undo.SetTransformParent(voltar, cabecalho, "Levar o Voltar para o cabeçalho");
            Ancorar(voltar, new Vector2(0.03f, 0.68f), new Vector2(0.52f, 0.97f), Vector2.zero, Vector2.zero);
            // As ferramentas antigas desligam o raio dos gráficos do cabeçalho; o Voltar precisa recebê-lo.
            var imagem = voltar.GetComponent<Image>();
            if (imagem != null)
            {
                Undo.RecordObject(imagem, "Voltar clicável no cabeçalho");
                imagem.raycastTarget = true;
            }

            foreach (var texto in voltar.GetComponentsInChildren<Text>(true))
            {
                DefinirFonte(texto, FonteCaminho, FontStyle.Bold, true);
                texto.alignment = TextAnchor.MiddleLeft;
                var retangulo = (RectTransform)texto.transform;
                Undo.RecordObject(retangulo, "Margem do rótulo do Voltar");
                retangulo.anchorMin = Vector2.zero;
                retangulo.anchorMax = Vector2.one;
                retangulo.offsetMin = new Vector2(10f, 1f);
                retangulo.offsetMax = new Vector2(-6f, -1f);
            }
        }

        DisporTexto(cabecalho.Find("Status") as RectTransform, new Vector2(0.54f, 0.68f), new Vector2(0.97f, 0.97f), FonteSecundaria, FontStyle.Normal, TextAnchor.MiddleRight, true);
        DisporTexto(cabecalho.Find("Title") as RectTransform, new Vector2(0.03f, 0.35f), new Vector2(0.97f, 0.67f), FonteTitulo, FontStyle.Bold, TextAnchor.MiddleLeft, true);
        DisporTexto(cabecalho.Find("Path") as RectTransform, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.35f), FonteCaminho, FontStyle.Normal, TextAnchor.MiddleLeft, false);
    }

    private static Button GarantirAba(RectTransform expandida, Sprite sprite, out Button botaoEstruturas, out Button botaoEEG)
    {
        var abas = GarantirFilho(expandida, "Abas_Painel");
        Ancorar(abas, new Vector2(0f, InicioAbas), new Vector2(1f, InicioCabecalho), new Vector2(12f, 3f), new Vector2(-12f, -3f));
        // GetComponent pode devolver um nulo falso no Editor: comparar com == null em vez de usar ??.
        var linha = abas.GetComponent<HorizontalLayoutGroup>();
        if (linha == null)
            linha = Undo.AddComponent<HorizontalLayoutGroup>(abas.gameObject);
        Undo.RecordObject(linha, "Organizar as abas do painel");
        linha.spacing = 8f;
        linha.childControlWidth = true;
        linha.childControlHeight = true;
        linha.childForceExpandWidth = true;
        linha.childForceExpandHeight = true;
        var cabecalho = expandida.Find("Header");
        Undo.SetSiblingIndex(abas, cabecalho != null ? cabecalho.GetSiblingIndex() + 1 : 0, "Posicionar as abas abaixo do cabeçalho");
        var ficha = GarantirBotao(abas, "Aba_Ficha", "Ficha", sprite, FonteSecao);
        botaoEstruturas = GarantirBotao(abas, "Aba_Estruturas", "Estruturas", sprite, FonteSecao);
        // A aba EEG só aparece em execução se a cena tiver uma gravação instalada (PainelEEG decide).
        botaoEEG = GarantirBotao(abas, "Aba_EEG", "EEG", sprite, FonteSecao);
        return ficha;
    }

    /// <summary>Área da aba EEG: mesma faixa da ficha e da lista; o conteúdo é montado em execução pelo PainelEEG.</summary>
    private static RectTransform GarantirConteudoEEG(RectTransform expandida)
    {
        var existente = expandida.Find("EEGPanel") as RectTransform;
        var conteudo = existente != null ? existente : GarantirFilho(expandida, "EEGPanel");
        Ancorar(conteudo, new Vector2(0f, InicioConteudo), new Vector2(1f, InicioAbas), new Vector2(12f, 0f), new Vector2(-12f, -2f));
        var informacao = expandida.Find("InfoPanel");
        if (informacao != null)
            Undo.SetSiblingIndex(conteudo, informacao.GetSiblingIndex() + 1, "Posicionar a aba EEG");
        var modelo = informacao != null ? informacao.GetComponent<Image>() : null;
        var fundo = conteudo.GetComponent<Image>();
        if (fundo == null)
            fundo = Undo.AddComponent<Image>(conteudo.gameObject);
        Undo.RecordObject(fundo, "Fundo da aba EEG");
        if (modelo != null)
        {
            fundo.sprite = modelo.sprite;
            fundo.type = modelo.type;
            fundo.color = modelo.color;
        }
        else
        {
            fundo.color = new Color(1f, 1f, 1f, 0.92f);
        }

        fundo.raycastTarget = true;
        if (existente == null || conteudo.gameObject.activeSelf)
        {
            Undo.RecordObject(conteudo.gameObject, "Aba EEG começa fechada");
            conteudo.gameObject.SetActive(false);
        }

        return conteudo;
    }

    private static void LigarEEG(GameObject painel, RectTransform expandida, Button botaoEEG, RectTransform conteudoEEG)
    {
        var abas = painel.GetComponent<AbasPainelAnatomico>();
        if (abas != null)
        {
            var so = new SerializedObject(abas);
            so.FindProperty("botaoEEG").objectReferenceValue = botaoEEG;
            so.FindProperty("conteudoEEG").objectReferenceValue = conteudoEEG != null ? conteudoEEG.gameObject : null;
            so.ApplyModifiedProperties();
        }

        var painelEEG = painel.GetComponent<VRSNC.EEG.PainelEEG>();
        if (painelEEG == null)
            painelEEG = Undo.AddComponent<VRSNC.EEG.PainelEEG>(painel);
        var so2 = new SerializedObject(painelEEG);
        so2.FindProperty("conteudo").objectReferenceValue = conteudoEEG;
        so2.FindProperty("abas").objectReferenceValue = abas;
        // Só como fonte do sprite arredondado: qualquer botão da barra serve.
        var modelo = expandida.Find("Controls/Button_Isolate");
        so2.FindProperty("modeloBotao").objectReferenceValue = modelo != null ? modelo.GetComponent<Button>() : null;
        so2.ApplyModifiedProperties();
    }

    // Ficha e lista ocupam a mesma faixa, na altura toda; as abas mostram uma por vez. Cada aba tem só uma linha
    // de rótulo no topo: a classificação, que repetia o caminho do cabeçalho, vai para a linha discreta da ficha.
    private static void DisporConteudo(RectTransform expandida)
    {
        var informacao = expandida.Find("InfoPanel") as RectTransform;
        if (informacao != null)
        {
            Ancorar(informacao, new Vector2(0f, InicioConteudo), new Vector2(1f, InicioAbas), new Vector2(12f, 0f), new Vector2(-12f, -2f));
            DisporTexto(informacao.Find("DisplayName") as RectTransform, new Vector2(0.03f, 0.91f), new Vector2(0.97f, 1f), FonteCaminho, FontStyle.Bold, null, true);
            var divisao = informacao.Find("Division");
            if (divisao != null && divisao.gameObject.activeSelf)
            {
                Undo.RecordObject(divisao.gameObject, "Levar a classificação para a ficha");
                divisao.gameObject.SetActive(false);
            }

            var areaDescricao = informacao.Find("DescriptionViewport") as RectTransform;
            if (areaDescricao != null)
            {
                Ancorar(areaDescricao, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.90f), Vector2.zero, Vector2.zero);
                foreach (var texto in areaDescricao.GetComponentsInChildren<Text>(true))
                    DefinirFonte(texto, FonteCorpo, FontStyle.Normal, false);
            }
        }

        var navegacao = expandida.Find("NavigationPanel") as RectTransform;
        if (navegacao != null)
        {
            Ancorar(navegacao, new Vector2(0f, InicioConteudo), new Vector2(1f, InicioAbas), new Vector2(12f, 0f), new Vector2(-12f, -2f));
            DisporTexto(navegacao.Find("NavigationTitle") as RectTransform, new Vector2(0.03f, 0.91f), new Vector2(0.97f, 1f), FonteCaminho, FontStyle.Bold, null, true);
            var areaLista = navegacao.Find("Viewport") as RectTransform;
            if (areaLista != null)
                Ancorar(areaLista, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.90f), Vector2.zero, Vector2.zero);
            // Pivô no topo, como a descrição: com o pivô no centro, o ScrollRect centralizava na vertical uma lista curta.
            var conteudoLista = navegacao.GetComponent<ScrollRect>()?.content;
            if (conteudoLista != null && conteudoLista.pivot != new Vector2(0.5f, 1f))
            {
                Undo.RecordObject(conteudoLista, "Encostar a lista de estruturas no topo");
                conteudoLista.pivot = new Vector2(0.5f, 1f);
                conteudoLista.anchoredPosition = Vector2.zero;
            }
        }
    }

    // Barra de ações única, 3 × 2: Isolar, Ocultar, Explodir / Limpar seleção, Entrar, Resetar. O Voltar fica no
    // cabeçalho; o Entrar volta à barra porque complementa o Voltar (a concha fica selecionada, Entrar abre de novo).
    private static Button DisporAcoes(RectTransform expandida, Sprite sprite)
    {
        var controles = expandida.Find("Controls") as RectTransform;
        if (controles == null)
            return null;
        Ancorar(controles, new Vector2(0f, 0.12f), new Vector2(1f, 0.245f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
        var grade = controles.GetComponent<GridLayoutGroup>();
        if (grade == null)
            grade = Undo.AddComponent<GridLayoutGroup>(controles.gameObject);
        Undo.RecordObject(grade, "Organizar a barra de ações do painel compacto");
        grade.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grade.constraintCount = 3;
        grade.spacing = new Vector2(8f, 6f);
        grade.cellSize = new Vector2((Largura - 24f - 2f * 8f) / 3f, 30f);
        grade.childAlignment = TextAnchor.MiddleCenter;
        grade.padding = new RectOffset(0, 0, 2, 2);
        var ocultar = controles.Find("Button_HideShow") as RectTransform;
        if (ocultar == null)
        {
            var botao = GarantirBotao(controles, "Button_HideShow", "Ocultar", sprite, FonteBotao);
            ocultar = (RectTransform)botao.transform;
        }

        var lista = controles.Find("Button_ToggleList");
        if (lista != null && lista.gameObject.activeSelf)
        {
            Undo.RecordObject(lista.gameObject, "Remover botão substituído pelas abas");
            lista.gameObject.SetActive(false);
        }

        var entrar = controles.Find("Button_Enter");
        if (entrar != null && !entrar.gameObject.activeSelf)
        {
            Undo.RecordObject(entrar.gameObject, "Entrar de volta à barra: complementa o Voltar com concha selecionada");
            entrar.gameObject.SetActive(true);
        }

        var ordem = new[] { "Button_Isolate", "Button_HideShow", "Button_Explode", "Button_Clear", "Button_Enter", "Button_Reset" };
        for (var indice = 0; indice < ordem.Length; indice++)
        {
            var item = controles.Find(ordem[indice]);
            if (item == null)
                continue;
            Undo.SetSiblingIndex(item, indice, "Ordenar a barra de ações");
            foreach (var texto in item.GetComponentsInChildren<Text>(true))
            {
                DefinirFonte(texto, FonteBotao, FontStyle.Normal, true);
                AjustarMargemRotulo(texto);
            }
        }

        return ocultar.GetComponent<Button>();
    }

    // SEGUIR, RECENTRAR, FIXAR e MINIMIZAR (o painel) na mesma linha, abaixo das ações. "Minimizar" evita dois
    // botões "Ocultar" no mesmo painel: o da barra de ações esconde a estrutura, este recolhe a janela.
    private static void DisporLinhaEspacial(RectTransform expandida)
    {
        var linha = expandida.Find("Controles_Espaciais_Painel") as RectTransform;
        if (linha == null)
            return;
        Ancorar(linha, new Vector2(0.03f, 0.035f), new Vector2(0.97f, 0.105f), Vector2.zero, Vector2.zero);
        var recolher = expandida.Find("Controls/Button_CollapsePanel") as RectTransform ?? linha.Find("Button_CollapsePanel") as RectTransform;
        if (recolher != null && recolher.parent != linha)
            Undo.SetTransformParent(recolher, linha, "Levar Minimizar para a linha espacial");
        if (recolher != null)
        {
            foreach (var texto in recolher.GetComponentsInChildren<Text>(true))
            {
                Undo.RecordObject(texto, "Renomear Ocultar painel para Minimizar");
                texto.text = RotuloMinimizar;
            }
        }
        // RECENTRAR é o rótulo mais longo: recebe a faixa mais larga para não ser cortado nem cair abaixo de 14.
        var posicoes = new (string nome, float minimo, float maximo)[]
        {
            ("Button_SeguirPainel", 0f, 0.21f),
            ("Button_RecentrarPainel", 0.223f, 0.513f),
            ("Button_FixarPainel", 0.526f, 0.736f),
            ("Button_CollapsePanel", 0.749f, 1f),
        };
        foreach (var (nome, minimo, maximo) in posicoes)
        {
            var item = linha.Find(nome) as RectTransform;
            if (item == null)
                continue;
            Ancorar(item, new Vector2(minimo, 0.08f), new Vector2(maximo, 0.92f), Vector2.zero, Vector2.zero);
            foreach (var texto in item.GetComponentsInChildren<Text>(true))
            {
                DefinirFonte(texto, FonteCaminho, FontStyle.Normal, true);
                AjustarMargemRotulo(texto);
            }
        }
    }

    private static void LigarComponentes(GameObject painel, RectTransform expandida, Button botaoFicha, Button botaoEstruturas, Button botaoOcultar)
    {
        var navegacao = painel.GetComponent<PainelNavegacaoAnatomica>();
        if (navegacao != null && botaoOcultar != null)
        {
            var so = new SerializedObject(navegacao);
            so.FindProperty("botaoOcultarMostrar").objectReferenceValue = botaoOcultar;
            so.ApplyModifiedProperties();
        }

        var abas = painel.GetComponent<AbasPainelAnatomico>();
        if (abas == null)
            abas = Undo.AddComponent<AbasPainelAnatomico>(painel);
        var snc = GameObject.Find("Sistema_Nervoso_Central");
        var selecao = snc != null ? snc.GetComponent<GerenciadorSelecaoHierarquica>() : null;
        var so2 = new SerializedObject(abas);
        so2.FindProperty("gerenciadorSelecao").objectReferenceValue = selecao;
        so2.FindProperty("painelNavegacao").objectReferenceValue = navegacao;
        so2.FindProperty("botaoFicha").objectReferenceValue = botaoFicha;
        so2.FindProperty("botaoEstruturas").objectReferenceValue = botaoEstruturas;
        so2.FindProperty("conteudoFicha").objectReferenceValue = expandida.Find("InfoPanel")?.gameObject;
        so2.FindProperty("conteudoEstruturas").objectReferenceValue = expandida.Find("NavigationPanel")?.gameObject;
        so2.FindProperty("rolagemFicha").objectReferenceValue = expandida.Find("InfoPanel/DescriptionViewport")?.GetComponent<ScrollRect>();
        so2.ApplyModifiedProperties();
    }

    // Oráculo do pulso: cerca de 14 cm de largura, rótulos iguais aos do painel e visível só ao virar o pulso.
    private static void AjustarPulso(GameObject pulso)
    {
        var conteudo = Encontrar(pulso.transform, "Hand Scroll View");
        if (conteudo != null)
        {
            Undo.RecordObject(conteudo, "Reduzir o oráculo do pulso");
            conteudo.localScale = Vector3.one * EscalaPulso;
            if (conteudo.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(conteudo.gameObject);
        }

        foreach (var botao in pulso.GetComponentsInChildren<Button>(true))
        {
            var rotulo = botao.GetComponentInChildren<TMP_Text>(true);
            if (rotulo == null)
                continue;
            Undo.RecordObject(rotulo, "Rótulos do oráculo iguais aos do painel");
            switch (rotulo.text.Trim())
            {
                case "Entrar na estrutura":
                case "Entrar":
                    Undo.RecordObject(botao.gameObject, "Remover Entrar do pulso: clicar num grupo já entra nele");
                    botao.gameObject.SetActive(false);
                    break;
                case "Voltar ao grupo":
                    rotulo.text = "Voltar";
                    break;
                case "Isolar / restaurar":
                    rotulo.text = "Isolar";
                    break;
                case "Exibir fonte":
                    rotulo.text = "Fonte";
                    break;
            }

            rotulo.enableAutoSizing = false;
            rotulo.fontSize = FonteBotao;
        }

        foreach (var texto in pulso.GetComponentsInChildren<TMP_Text>(true))
        {
            if (texto.GetComponentInParent<Button>() != null)
                continue;
            Undo.RecordObject(texto, "Escala tipográfica do oráculo do pulso");
            if (texto.name == "Header")
            {
                texto.enableAutoSizing = true;
                texto.fontSizeMin = FonteSecao;
                texto.fontSizeMax = 24f;
            }
            else
            {
                texto.enableAutoSizing = false;
                texto.fontSize = texto.name == "Explicacao_Didatica" ? FonteCorpo : Mathf.Max(17f, texto.fontSize);
            }
        }

        var ancora = pulso.GetComponentInChildren<AncoraEspacialPulso>(true);
        if (ancora != null)
        {
            var so = new SerializedObject(ancora);
            so.FindProperty("mostrarSoAoVirarPulso").boolValue = true;
            so.FindProperty("predefinicaoMao").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FollowPresetDatum>(PresetMaoCaminho);
            so.FindProperty("predefinicaoControle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FollowPresetDatum>(PresetControleCaminho);
            so.FindProperty("grupoConteudo").objectReferenceValue = conteudo != null ? conteudo.GetComponent<CanvasGroup>() : null;
            so.ApplyModifiedProperties();
        }

        // Mesma regra do Hand Menu do XRI ("Under Watch"): controle pelo eixo Right; mão pelo dorso (Up).
        AjustarPreset(PresetControleCaminho, null, 54f);
        AjustarPreset(PresetMaoCaminho, FollowReferenceAxis.Up, 60f);
    }

    private static void AjustarPreset(string caminho, FollowReferenceAxis? eixo, float angulo)
    {
        var datum = AssetDatabase.LoadAssetAtPath<FollowPresetDatum>(caminho);
        if (datum == null)
            return;
        var so = new SerializedObject(datum);
        var valor = so.FindProperty("m_Value");
        if (valor == null)
            return;
        if (eixo.HasValue)
            valor.FindPropertyRelative("palmReferenceAxis").enumValueIndex = (int)eixo.Value;
        valor.FindPropertyRelative("requirePalmFacingUser").boolValue = true;
        valor.FindPropertyRelative("palmFacingUserDegreeAngleThreshold").floatValue = angulo;
        valor.FindPropertyRelative("hideDelaySeconds").floatValue = 0.15f;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(datum);
    }

    private static void AjustarRotuloApontado()
    {
        var rotulo = GameObject.Find("Sistema_Nervoso_Central")?.GetComponent<RotuloEstruturaApontada>();
        if (rotulo == null)
            return;
        var so = new SerializedObject(rotulo);
        so.FindProperty("alturaAngularGraus").floatValue = 2.2f;
        so.ApplyModifiedProperties();
    }

    private static void DisporTexto(RectTransform linha, Vector2 minimo, Vector2 maximo, int tamanho, FontStyle estilo, TextAnchor? alinhamento, bool ajustarAoEspaco)
    {
        if (linha == null)
            return;
        Ancorar(linha, minimo, maximo, Vector2.zero, Vector2.zero);
        foreach (var texto in linha.GetComponentsInChildren<Text>(true))
        {
            DefinirFonte(texto, tamanho, estilo, ajustarAoEspaco);
            if (alinhamento.HasValue)
                texto.alignment = alinhamento.Value;
        }
    }

    /// <summary>Tamanho da escala tipográfica; com ajuste ao espaço, rótulos longos encolhem até 14 em uma linha em vez de vazar.</summary>
    private static void DefinirFonte(Text texto, int tamanho, FontStyle estilo, bool ajustarAoEspaco)
    {
        Undo.RecordObject(texto, "Aplicar a escala tipográfica do painel");
        texto.fontSize = tamanho;
        texto.fontStyle = estilo;
        texto.resizeTextForBestFit = ajustarAoEspaco;
        texto.resizeTextMinSize = FonteMinima;
        texto.resizeTextMaxSize = tamanho;
        if (ajustarAoEspaco)
        {
            // Com estouro vertical o ajuste ignora a altura e quebra o rótulo em duas linhas grandes que vazam do botão.
            texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            texto.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }

    /// <summary>Margem curta e igual em todos os botões, para o rótulo usar quase toda a largura antes de encolher.</summary>
    private static void AjustarMargemRotulo(Text texto)
    {
        var retangulo = (RectTransform)texto.transform;
        Undo.RecordObject(retangulo, "Margem dos rótulos dos botões");
        retangulo.anchorMin = Vector2.zero;
        retangulo.anchorMax = Vector2.one;
        retangulo.offsetMin = new Vector2(4f, 1f);
        retangulo.offsetMax = new Vector2(-4f, -1f);
    }

    private static Button GarantirBotao(RectTransform pai, string nome, string rotulo, Sprite sprite, int tamanhoFonte)
    {
        var existente = pai.Find(nome);
        if (existente != null && existente.GetComponent<Button>() != null)
            return existente.GetComponent<Button>();
        var objeto = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(objeto, "Criar botão do painel compacto");
        objeto.layer = pai.gameObject.layer;
        objeto.transform.SetParent(pai, false);
        var imagem = objeto.GetComponent<Image>();
        imagem.sprite = sprite;
        imagem.type = Image.Type.Sliced;
        imagem.color = CorBotao;
        imagem.raycastTarget = true;
        var botao = objeto.GetComponent<Button>();
        botao.targetGraphic = imagem;
        var cores = botao.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(0.82f, 0.86f, 0.90f, 1f);
        cores.pressedColor = new Color(0.66f, 0.72f, 0.79f, 1f);
        cores.disabledColor = EstiloBotaoInterface.CorDesativado;
        botao.colors = cores;
        var textoObjeto = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textoObjeto.layer = objeto.layer;
        textoObjeto.transform.SetParent(objeto.transform, false);
        var retangulo = (RectTransform)textoObjeto.transform;
        retangulo.anchorMin = Vector2.zero;
        retangulo.anchorMax = Vector2.one;
        retangulo.offsetMin = new Vector2(4f, 1f);
        retangulo.offsetMax = new Vector2(-4f, -1f);
        var texto = textoObjeto.GetComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.text = rotulo;
        texto.color = CorRotuloEscuro;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.raycastTarget = false;
        texto.fontSize = tamanhoFonte;
        texto.resizeTextForBestFit = true;
        texto.resizeTextMinSize = FonteMinima;
        texto.resizeTextMaxSize = tamanhoFonte;
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;
        texto.verticalOverflow = VerticalWrapMode.Truncate;
        return botao;
    }

    private static RectTransform GarantirFilho(RectTransform pai, string nome)
    {
        var existente = pai.Find(nome) as RectTransform;
        if (existente != null)
            return existente;
        var objeto = new GameObject(nome, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(objeto, "Criar " + nome);
        objeto.layer = pai.gameObject.layer;
        objeto.transform.SetParent(pai, false);
        return (RectTransform)objeto.transform;
    }

    private static void Ancorar(RectTransform alvo, Vector2 minimo, Vector2 maximo, Vector2 deslocamentoMinimo, Vector2 deslocamentoMaximo)
    {
        if (alvo == null)
            return;
        Undo.RecordObject(alvo, "Organizar o painel compacto");
        alvo.anchorMin = minimo;
        alvo.anchorMax = maximo;
        alvo.offsetMin = deslocamentoMinimo;
        alvo.offsetMax = deslocamentoMaximo;
    }

    private static Transform Encontrar(Transform raiz, string nome)
    {
        foreach (var filho in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (filho.name == nome)
                return filho;
        }

        return null;
    }
}
