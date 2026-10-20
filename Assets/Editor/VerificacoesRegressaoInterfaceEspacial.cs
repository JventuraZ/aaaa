using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRSNC.Anatomia;
using VRSNC.Interacao;
using VRSNC.Interface;

/// <summary>Verificações de configuração e estado sintético voltadas ao operador; nunca alegam rastreamento físico da mão.</summary>
internal static class VerificacoesRegressaoInterfaceEspacial
{
    private static CenarioAgarreDuasMaos cenarioTestePendente;
    private static List<string> falhasExecucaoPendente;
    private static int quadrosCenarioTestePendente;
    [MenuItem("Ferramentas/VR SNC/Interface/Validar Configuração da Interface Espacial", false, 241)]
    private static void ValidarConfiguracao()
    {
        var falhas = new List<string>();
        try
        {
            var pulso = GameObject.Find("Oraculo_Anatomico_Punho");
            Exigir(pulso != null, "a raiz de pulso do oraculo existe", falhas);
            var ancora = pulso != null ? pulso.GetComponentInChildren<AncoraEspacialPulso>(true) : null;
            Exigir(ancora != null, "a ancora espacial de pulso existe", falhas);
            if (ancora != null)
            {
                Exigir(ObterCampo<Transform>(ancora, "pulsoEsquerdo") != null && ObterCampo<Transform>(ancora, "pulsoDireito") != null && ObterCampo<Transform>(ancora, "dispositivoVR") != null, "as referencias esquerda/direita/HMD da ancora estao atribuidas", falhas);
                Exigir(ObterCampo<Behaviour>(ancora, "legadoPosturaControlador") == null || !ObterCampo<Behaviour>(ancora, "legadoPosturaControlador").enabled, "o controlador legado de pose do HandMenu copiado esta desativado", falhas);
                var agarrar = ObterCampo<XRGrabInteractable>(ancora, "cabecalhoAgarre");
                Exigir(agarrar != null && agarrar.transform.IsChildOf(ancora.transform), "o grab do cabecalho e descendente da raiz de pose da ancora", falhas);
                Exigir(ancora.transform == pulso.transform || ancora.transform.IsChildOf(pulso.transform), "a ancora possui a raiz de pose de pulso copiada", falhas);
                if (agarrar != null)
                {
                    Exigir(agarrar.selectMode == InteractableSelectMode.Multiple, "o grab do cabecalho suporta selecoes multiplas", falhas);
                    var colisor = agarrar.colliders.Count > 0 ? agarrar.colliders[0] as BoxCollider : null;
                    var localProfundidade = colisor != null ? colisor.size.z * Mathf.Abs(colisor.transform.lossyScale.z) : 0f;
                    Exigir(colisor != null && colisor.transform.IsChildOf(agarrar.transform) && localProfundidade >= 0.01f && localProfundidade <= 0.04f, "o BoxCollider do cabecalho fica abaixo do grab e tem profundidade de mundo normal-local de 10-40 mm", falhas);
                }
            }

            var principal = GameObject.Find("Painel_Anatomico_VR_SNC");
            var alca = principal != null ? principal.GetComponentInChildren<ControlePainelMundo>(true) : null;
            Exigir(alca != null && alca.GetComponent<XRGrabInteractable>() != null, "a alca/grab do painel em mundo existe", falhas);
            if (alca != null)
            {
                var agarrar2 = alca.GetComponent<XRGrabInteractable>();
                Exigir(agarrar2.selectMode == InteractableSelectMode.Multiple, "a alca do painel em mundo suporta duas selecoes", falhas);
                var colisor2 = agarrar2.colliders.Count > 0 ? agarrar2.colliders[0] : null;
                Exigir(colisor2 != null && colisor2.transform.IsChildOf(principal.transform), "o collider da alca em mundo esta dentro do painel", falhas);
            }

            var zonas = UnityEngine.Object.FindObjectsByType<ZonaProximidadePainelInterface>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Exigir(zonas.Length >= 2, "existem duas zonas de proximidade do painel", falhas);
            var expandida = principal != null ? principal.transform.Find("ExpandedRoot") : null;
            var zonaExpandida = false;
            foreach (var zona in zonas)
                if (expandida != null && zona.transform.IsChildOf(expandida))
                    zonaExpandida = true;
            Exigir(zonaExpandida, "a zona principal de proximidade pertence a ExpandedRoot e desativa com o painel", falhas);
            VerificarPersistenteRecentralizar(principal, falhas);
            VerificarPersistenteAcoplar(pulso, ancora, falhas);
        }
        catch (Exception excecao)
        {
            falhas.Add("excecao inesperada de configuracao: " + excecao);
        }

        Relatorio("Configuracao da interface espacial", falhas, "Somente assercoes serializadas/de configuracao do Modo de Edicao.");
    }

    [MenuItem("Ferramentas/VR SNC/Interface/Validar Estados da Interface Espacial em Execução (Modo de Jogo)", false, 242)]
    private static void ValidarExecucaoEstados()
    {
        var falhas = new List<string>();
        if (!EditorApplication.isPlaying)
        {
            falhas.Add("entre no Modo de Jogo antes da validacao sintetica de estado em execucao");
            Relatorio("Execucao da interface espacial", falhas, "Nenhum hardware/rastreamento de maos XR e simulado.");
            return;
        }

        if (cenarioTestePendente != null)
        {
            falhas.Add("uma fixture sintetica de grab duplo ja aguarda seus quadros de ciclo de vida XRI");
            Relatorio("Execucao da interface espacial", falhas, "Nenhum hardware/rastreamento de maos XR e simulado.");
            return;
        }

        try
        {
            var principal = GameObject.Find("Painel_Anatomico_VR_SNC");
            var alca = principal != null ? principal.GetComponentInChildren<ControlePainelMundo>(true) : null;
            var pulso = GameObject.Find("Oraculo_Anatomico_Punho");
            var ancora = pulso != null ? pulso.GetComponentInChildren<AncoraEspacialPulso>(true) : null;
            Exigir(alca != null && ancora != null, "a alca e a ancora de execucao existem", falhas);
            if (alca != null)
            {
                alca.DefinirAcoplamento(false);
                Exigir(alca.EstadoPostura == ControlePainelMundo.EstadoPosturaPainel.FixadoNoMundo, "estado explicito de fixacao no mundo", falhas);
                alca.DefinirAcoplamento(true);
                Exigir(alca.EstadoPostura == ControlePainelMundo.EstadoPosturaPainel.Acoplado, "estado explicito de acoplamento", falhas);
            }

            if (ancora != null)
            {
                ancora.Desacoplar();
                Exigir(ancora.Estado == AncoraEspacialPulso.EstadoPostura.FixadoNoMundo, "estado de fixacao no mundo da ancora", falhas);
                ancora.AcoplarAoPulso();
                Exigir(ancora.Estado == AncoraEspacialPulso.EstadoPostura.Acoplado, "estado explicito de acoplamento da ancora", falhas);
            }

            VerificarCondicionalmenteRedefinirRolagens(falhas);
            IniciarCenarioAgarreDuasMaos(falhas);
        }
        catch (Exception excecao)
        {
            falhas.Add("excecao inesperada de execucao sintetica: " + excecao);
        }

        if (cenarioTestePendente == null)
            Relatorio("Execucao da interface espacial", falhas, "Somente verificacoes sinteticas da API de estado; nenhum rastreamento XR, pose do headset, conforto ou grab fisico e afirmado.");
    }

    // Usa as APIs do painel de produção e texto real do Oracle. É deliberadamente uma
    // verificação pequena de estado: a apresentação repetida de A mantém a posição de leitura,
    // enquanto a transição A -> B leva ambas as rolagens relevantes à primeira linha.
    private static void VerificarCondicionalmenteRedefinirRolagens(List<string> falhas)
    {
        var principal = GameObject.Find("Painel_Anatomico_VR_SNC");
        var raiz = GameObject.Find("Sistema_Nervoso_Central");
        var informacao = principal != null ? principal.GetComponentInChildren<PainelInformacoesAnatomicas>(true) : null;
        var navegacao = principal != null ? principal.GetComponentInChildren<PainelNavegacaoAnatomica>(true) : null;
        var selecao = raiz != null ? raiz.GetComponent<GerenciadorSelecaoHierarquica>() : null;
        Exigir(informacao != null && navegacao != null && selecao != null, "os paineis de informacao, navegacao e selecao existem para a regressao de rolagem", falhas);
        if (informacao == null || navegacao == null || selecao == null)
            return;
        NoInteracaoAnatomica maisLongo = null;
        NoInteracaoAnatomica alternativo = null;
        var comprimentoMaisLongo = -1;
        foreach (var no in UnityEngine.Object.FindObjectsByType<NoInteracaoAnatomica>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (no == null || !no.EhSelecionavel)
                continue;
            if (alternativo == null)
                alternativo = no;
            if (OraculoAnatomico.TentarObter(no.VinculoAnatomico, out var registro) && registro != null && !string.IsNullOrWhiteSpace(registro.descricaoCientifica) && registro.descricaoCientifica.Length > comprimentoMaisLongo)
            {
                maisLongo = no;
                comprimentoMaisLongo = registro.descricaoCientifica.Length;
            }
        }

        if (alternativo == maisLongo)
        {
            foreach (var no2 in UnityEngine.Object.FindObjectsByType<NoInteracaoAnatomica>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (no2 != null && no2.EhSelecionavel && no2 != maisLongo)
                {
                    alternativo = no2;
                    break;
                }
        }

        Exigir(maisLongo != null && alternativo != null && maisLongo != alternativo, "dois nos anatomicos reais selecionaveis estao disponiveis para a regressao de rolagem A-para-B", falhas);
        if (maisLongo == null || alternativo == null || maisLongo == alternativo)
            return;
        // No painel em abas, só a aba visível tem layout: cada rolagem é medida com a sua aba aberta.
        var abas = principal.GetComponent<AbasPainelAnatomico>();
        var abaAnterior = abas != null ? abas.AbaAtual : AbasPainelAnatomico.Aba.Ficha;
        if (abas != null)
            abas.Mostrar(AbasPainelAnatomico.Aba.Ficha);
        informacao.ExibirNo(maisLongo, maisLongo.NomeExibicao);
        Canvas.ForceUpdateCanvases();
        var descricaoRolagem = EncontrarRolagemDescricao(informacao);
        Exigir(descricaoRolagem != null && descricaoRolagem.content != null && descricaoRolagem.viewport != null, "o ScrollRect de descricao possui conteudo e viewport", falhas);
        if (descricaoRolagem != null && descricaoRolagem.content != null && descricaoRolagem.viewport != null)
        {
            Exigir(descricaoRolagem.content.rect.height > descricaoRolagem.viewport.rect.height, "a descricao longa real do Oraculo excede seu viewport e pode rolar", falhas);
            var testeY = Mathf.Min(24f, Mathf.Max(1f, descricaoRolagem.content.rect.height - descricaoRolagem.viewport.rect.height));
            var ancorado = descricaoRolagem.content.anchoredPosition;
            descricaoRolagem.content.anchoredPosition = new Vector2(ancorado.x, testeY);
            informacao.ExibirNo(maisLongo, maisLongo.NomeExibicao);
            Exigir(Mathf.Approximately(descricaoRolagem.content.anchoredPosition.y, testeY), "o mesmo no preserva a posicao de rolagem da descricao", falhas);
            informacao.ExibirNo(alternativo, alternativo.NomeExibicao);
            Exigir(Mathf.Approximately(descricaoRolagem.content.anchoredPosition.y, 0f), "um no diferente reinicia a rolagem da descricao no topo", falhas);
        }

        if (abas != null)
            abas.Mostrar(AbasPainelAnatomico.Aba.Estruturas);
        var navegacaoRolagem = EncontrarRolagemNavegacao(navegacao);
        var focoA = selecao.NoEmFoco ?? selecao.NoRaiz;
        NoInteracaoAnatomica focoB = null;
        if (focoA != null)
            foreach (var filho in focoA.NosFilhos)
                if (filho != null)
                {
                    focoB = filho;
                    break;
                }

        Exigir(navegacaoRolagem != null && navegacaoRolagem.content != null && focoA != null && focoB != null, "o ScrollRect de navegacao e dois niveis reais de foco estao disponiveis", falhas);
        if (navegacaoRolagem != null && navegacaoRolagem.content != null && focoA != null && focoB != null)
        {
            navegacao.Atualizar(focoA);
            Canvas.ForceUpdateCanvases();
            var ancorado2 = navegacaoRolagem.content.anchoredPosition;
            const float testeY2 = 24f;
            navegacaoRolagem.content.anchoredPosition = new Vector2(ancorado2.x, testeY2);
            navegacao.Atualizar(focoA);
            Canvas.ForceUpdateCanvases();
            var mesmoFocoY = navegacaoRolagem.content.anchoredPosition.y;
            Exigir(Mathf.Approximately(mesmoFocoY, testeY2), "o mesmo foco preserva a posicao de rolagem da navegacao", falhas);
            navegacao.Atualizar(focoB);
            Canvas.ForceUpdateCanvases();
            var alteradoFocoY = navegacaoRolagem.content.anchoredPosition.y;
            var focoNormalizadoAlterado = navegacaoRolagem.verticalNormalizedPosition;
            // Uma lista que cabe inteira na área visível não rola: o ScrollRect devolve 0 ou 1 conforme o arredondamento.
            // Nesse caso, "no topo" é o topo do conteúdo alinhado ao topo da área visível.
            var areaNavegacao = navegacaoRolagem.viewport != null ? navegacaoRolagem.viewport : (RectTransform)navegacaoRolagem.transform;
            var listaCabeInteira = navegacaoRolagem.content.rect.height <= areaNavegacao.rect.height + 0.5f;
            var distanciaTopo = DistanciaDoTopo(navegacaoRolagem.content, areaNavegacao);
            Debug.Log($"[Regressao de rolagem da interface espacial] navegação A.y={testeY2:F1}, A->A.y={mesmoFocoY:F1}, A->B.y={alteradoFocoY:F1}, " + $"A->B.normalized={focoNormalizadoAlterado:F3}, A->B.distância do topo={distanciaTopo:F1}, conteúdo={navegacaoRolagem.content.rect.height:F1}, área visível={navegacaoRolagem.viewport.rect.height:F1}, linhas={navegacaoRolagem.content.childCount}.");
            Exigir(listaCabeInteira ? Mathf.Abs(distanciaTopo) <= 0.5f : Mathf.Abs(focoNormalizadoAlterado - 1f) <= 0.001f, "um foco diferente reinicia a rolagem da navegacao no topo", falhas);
            navegacao.Atualizar(selecao.NoEmFoco ?? selecao.NoRaiz);
        }

        informacao.ExibirFoco(selecao.NoEmFoco ?? selecao.NoRaiz);
        if (abas != null)
            abas.Mostrar(abaAnterior);
    }

    // Quanto o topo do conteúdo está abaixo do topo da área visível, no espaço da área (0 = encostado no topo).
    private static float DistanciaDoTopo(RectTransform conteudo, RectTransform area)
    {
        var cantos = new Vector3[4];
        conteudo.GetWorldCorners(cantos);
        return area.rect.yMax - area.InverseTransformPoint(cantos[1]).y;
    }

    private static ScrollRect EncontrarRolagemDescricao(PainelInformacoesAnatomicas informacao)
    {
        foreach (var rolagem in informacao.GetComponentsInChildren<ScrollRect>(true))
            if (rolagem.content != null && rolagem.content.name == "Description")
                return rolagem;
        return null;
    }

    private static ScrollRect EncontrarRolagemNavegacao(PainelNavegacaoAnatomica navegacao)
    {
        var contêiner = typeof(PainelNavegacaoAnatomica).GetField("conteinerLista", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(navegacao) as Transform;
        if (contêiner == null)
            return null;
        // A lista pode estar na aba oculta: procurar também em objetos inativos.
        var rolagem = contêiner.GetComponentInParent<ScrollRect>(true);
        if (rolagem != null && rolagem.content == contêiner)
            return rolagem;
        return null;
    }

    private static void VerificarPersistenteRecentralizar(GameObject principal, List<string> falhas)
    {
        if (principal == null)
            return;
        var encontrado = false;
        foreach (var botao in principal.GetComponentsInChildren<Button>(true))
        {
            for (var i = 0; i < botao.onClick.GetPersistentEventCount(); i++)
            {
                if (botao.onClick.GetPersistentTarget(i) is ControlePainelMundo && botao.onClick.GetPersistentMethodName(i) == nameof(ControlePainelMundo.RecentralizarPainel))
                    encontrado = true;
            }
        }

        Exigir(encontrado, "o callback persistente de recentralizar aponta para BrainWorldPanelHandle", falhas);
    }

    private static void VerificarPersistenteAcoplar(GameObject pulso, AncoraEspacialPulso ancora, List<string> falhas)
    {
        if (pulso == null || ancora == null)
            return;
        var encontrado = false;
        foreach (var botao in pulso.GetComponentsInChildren<Button>(true))
        {
            if (botao.name != "Button_AcoplarPunho")
                continue;
            for (var i = 0; i < botao.onClick.GetPersistentEventCount(); i++)
            {
                if (botao.onClick.GetPersistentTarget(i) == ancora && botao.onClick.GetPersistentMethodName(i) == nameof(AncoraEspacialPulso.AcoplarAoPulso))
                    encontrado = true;
            }
        }

        Exigir(encontrado, "o callback persistente de Acoplar aponta para DockToWrist da ancora de pulso copiada", falhas);
    }

    private static void IniciarCenarioAgarreDuasMaos(List<string> falhas)
    {
        try
        {
            // Objetos normais de execução têm uma PhysicsScene válida. Mantém a raiz inativa
            // até atribuir todos os gerenciadores/referências e depois espera Awake/Start.
            var cenarioTeste = new GameObject("SpatialUiSyntheticFixture");
            cenarioTeste.SetActive(false);
            var gerenciador = cenarioTeste.AddComponent<XRInteractionManager>();
            var alvo = new GameObject("Target");
            alvo.transform.SetParent(cenarioTeste.transform, false);
            alvo.AddComponent<BoxCollider>();
            var corpo = alvo.AddComponent<Rigidbody>();
            corpo.isKinematic = true;
            corpo.useGravity = false;
            var agarrar = alvo.AddComponent<XRGrabInteractable>();
            agarrar.interactionManager = gerenciador;
            agarrar.selectMode = InteractableSelectMode.Multiple;
            var alca = alvo.AddComponent<ControlePainelMundo>();
            var ancora = alvo.AddComponent<AncoraEspacialPulso>();
            var temporario = new GameObject("DockReference");
            temporario.transform.SetParent(cenarioTeste.transform, false);
            ancora.Configurar(temporario.transform, temporario.transform, temporario.transform, null, agarrar);
            var esquerda = CriarInteratorDireto("Left", cenarioTeste.transform, gerenciador);
            var direito = CriarInteratorDireto("Right", cenarioTeste.transform, gerenciador);
            cenarioTestePendente = new CenarioAgarreDuasMaos(cenarioTeste, gerenciador, agarrar, alca, ancora, esquerda, direito);
            falhasExecucaoPendente = falhas;
            quadrosCenarioTestePendente = 2;
            cenarioTeste.SetActive(true);
            EditorApplication.update += AvancarCenarioAgarreDuasMaos;
        }
        catch (Exception excecao)
        {
            falhas.Add("excecao na configuracao da fixture sintetica de grab duplo: " + excecao);
        }
    }

    private static void AvancarCenarioAgarreDuasMaos()
    {
        if (cenarioTestePendente == null)
        {
            EditorApplication.update -= AvancarCenarioAgarreDuasMaos;
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            falhasExecucaoPendente.Add("o Modo de Jogo terminou antes de a fixture sintetica de ciclo de vida XRI inicializar");
            ConcluirCenarioAgarreDuasMaos();
            return;
        }

        if (--quadrosCenarioTestePendente > 0)
            return;
        try
        {
            var cenarioTeste = cenarioTestePendente;
            cenarioTeste.Gerenciador.SelectEnter((IXRSelectInteractor)cenarioTeste.Esquerda, (IXRSelectInteractable)cenarioTeste.Agarre);
            Exigir(cenarioTeste.Controle.EstadoPostura == ControlePainelMundo.EstadoPosturaPainel.Agarrado && cenarioTeste.Ancora.Estado == AncoraEspacialPulso.EstadoPostura.Agarrado, "o primeiro SelectEnter sintetico define as duas alcas como Grabbed", falhasExecucaoPendente);
            Exigir(ControlePainelMundo.AgarrePainelAtivoParaMao(cenarioTeste.Esquerda.transform) && !ControlePainelMundo.AgarrePainelAtivoParaMao(cenarioTeste.Direita.transform), "o primeiro grab sintetico registra somente a propria raiz de mao", falhasExecucaoPendente);
            cenarioTeste.Gerenciador.SelectEnter((IXRSelectInteractor)cenarioTeste.Direita, (IXRSelectInteractable)cenarioTeste.Agarre);
            Exigir(ControlePainelMundo.AgarrePainelAtivoParaMao(cenarioTeste.Esquerda.transform) && ControlePainelMundo.AgarrePainelAtivoParaMao(cenarioTeste.Direita.transform), "o segundo grab sintetico registra a outra mao sem substituir a primeira", falhasExecucaoPendente);
            cenarioTeste.Controle.DefinirAcoplamento(true);
            cenarioTeste.Ancora.AcoplarAoPulso();
            Exigir(cenarioTeste.Controle.EstadoPostura == ControlePainelMundo.EstadoPosturaPainel.Agarrado && cenarioTeste.Ancora.Estado == AncoraEspacialPulso.EstadoPostura.Agarrado, "acoplar durante a selecao nao substitui o estado grabbed", falhasExecucaoPendente);
            cenarioTeste.Gerenciador.SelectExit((IXRSelectInteractor)cenarioTeste.Esquerda, (IXRSelectInteractable)cenarioTeste.Agarre);
            Exigir(cenarioTeste.Controle.EstadoPostura == ControlePainelMundo.EstadoPosturaPainel.Agarrado && cenarioTeste.Ancora.Estado == AncoraEspacialPulso.EstadoPostura.Agarrado, "o primeiro SelectExit mantem Grabbed enquanto existe a segunda selecao", falhasExecucaoPendente);
            Exigir(!ControlePainelMundo.AgarrePainelAtivoParaMao(cenarioTeste.Esquerda.transform) && ControlePainelMundo.AgarrePainelAtivoParaMao(cenarioTeste.Direita.transform), "a primeira saida sintetica remove somente o registro da mao solta", falhasExecucaoPendente);
            cenarioTeste.Gerenciador.SelectExit((IXRSelectInteractor)cenarioTeste.Direita, (IXRSelectInteractable)cenarioTeste.Agarre);
            Exigir(cenarioTeste.Controle.EstadoPostura == ControlePainelMundo.EstadoPosturaPainel.FixadoNoMundo && cenarioTeste.Ancora.Estado == AncoraEspacialPulso.EstadoPostura.FixadoNoMundo, "o ultimo SelectExit torna as duas alcas WorldPinned", falhasExecucaoPendente);
            cenarioTeste.Ancora.enabled = false;
            cenarioTeste.Ancora.enabled = true;
            Exigir(cenarioTeste.Ancora.Estado != AncoraEspacialPulso.EstadoPostura.Agarrado, "desativar/ativar apos soltar nao congela a ancora em grabbed", falhasExecucaoPendente);
        }
        catch (Exception excecao)
        {
            falhasExecucaoPendente.Add("excecao da fixture sintetica de grab duplo: " + excecao);
        }
        finally
        {
            ConcluirCenarioAgarreDuasMaos();
        }
    }

    private static void ConcluirCenarioAgarreDuasMaos()
    {
        EditorApplication.update -= AvancarCenarioAgarreDuasMaos;
        if (cenarioTestePendente != null && cenarioTestePendente.Raiz != null)
            UnityEngine.Object.Destroy(cenarioTestePendente.Raiz);
        var falhas = falhasExecucaoPendente ?? new List<string> { "o estado do relatorio da fixture sintetica foi perdido" };
        cenarioTestePendente = null;
        falhasExecucaoPendente = null;
        quadrosCenarioTestePendente = 0;
        Relatorio("Execucao da interface espacial", falhas, "Somente verificacoes sinteticas da API de estado; nenhum rastreamento XR, pose do headset, conforto ou grab fisico e afirmado.");
    }

    private sealed class CenarioAgarreDuasMaos
    {
        public readonly GameObject Raiz;
        public readonly XRInteractionManager Gerenciador;
        public readonly XRGrabInteractable Agarre;
        public readonly ControlePainelMundo Controle;
        public readonly AncoraEspacialPulso Ancora;
        public readonly XRDirectInteractor Esquerda;
        public readonly XRDirectInteractor Direita;
        public CenarioAgarreDuasMaos(GameObject raiz, XRInteractionManager gerenciador, XRGrabInteractable agarrar, ControlePainelMundo alca, AncoraEspacialPulso ancora, XRDirectInteractor esquerda, XRDirectInteractor direito)
        {
            Raiz = raiz;
            Gerenciador = gerenciador;
            Agarre = agarrar;
            Controle = alca;
            Ancora = ancora;
            Esquerda = esquerda;
            Direita = direito;
        }
    }

    private static XRDirectInteractor CriarInteratorDireto(string nome, Transform pai, XRInteractionManager gerenciador)
    {
        var fonte = new GameObject(nome);
        fonte.transform.SetParent(pai, false);
        var colisor = fonte.AddComponent<SphereCollider>();
        colisor.isTrigger = true;
        colisor.radius = 0.02f;
        var corpo = fonte.AddComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
        var interator = fonte.AddComponent<XRDirectInteractor>();
        interator.interactionManager = gerenciador;
        return interator;
    }

    private static T ObterCampo<T>(object instancia, string nome)
        where T : class => typeof(AncoraEspacialPulso).GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instancia) as T;
    private static void Exigir(bool condicao, string mensagem, List<string> falhas)
    {
        if (!condicao)
            falhas.Add(mensagem);
    }

    private static void Relatorio(string titulo, List<string> falhas, string escopo)
    {
        if (falhas.Count == 0)
            Debug.Log("APROVADO: " + titulo + ". " + escopo);
        else
            Debug.LogError("FALHOU: " + titulo + "\n- " + string.Join("\n- ", falhas) + "\nEscopo: " + escopo);
    }
}
