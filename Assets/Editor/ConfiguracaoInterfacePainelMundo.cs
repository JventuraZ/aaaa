using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.UI.BodyUI;
using VRSNC.Interface;

namespace VRSNC.FerramentasEditor
{
    /// <summary>Configuração idempotente, exclusiva do Editor, para o painel anatômico fixado e escalável com duas mãos.</summary>
    internal static class ConfiguracaoInterfacePainelMundo
    {
        private const string NomePainel = "Painel_Anatomico_VR_SNC";
        private const string NomeControle = "Barra_Manipulacao_Mundo";
        private const string NomePegadaDireita = "Grip_Direito_Barra_Manipulacao_Mundo";
        private const string RecentralizarBotaoNome = "Button_RecentrarPainel";
        private const string NomeBotaoAcompanhar = "Button_SeguirPainel";
        private const string FixarBotaoNome = "Button_FixarPainel";
        private const string NomeControlesEspaciais = "Controles_Espaciais_Painel";
        private const string JanelaFlutuanteAcoplarBotaoNome = "Button_AcoplarPunho";
        private const string NomeAgarreEsquerdoCabecalho = "Grip_Esquerdo_Cabecalho_Painel";
        private const string NomeAgarreDireitoCabecalho = "Grip_Direito_Cabecalho_Painel";
        private const string ManipularLegendaNome = "Instrucao_Manipulacao_Mundo";
        private const string NomeRaizRedimensionamento = "Bordas_Redimensionar_Janela";
        private const string ModeloContornoNome = "Moldura_Template_SpatialPanel";
        private const string CaminhoImagemArredondada = "Assets/VRTemplateAssets/Sprites/UI/Round Radius 10.png";
        private const string CaminhoImagemArredondadaSuperior = "Assets/VRTemplateAssets/Sprites/UI/Round Radius 10 Top.png";
        private const string CaminhoImagemArredondadaInferior = "Assets/VRTemplateAssets/Sprites/UI/Round Radius 10 Bottom.png";
        private const string DorsalMaosPredefinicaoCaminho = "Assets/VRTemplateAssets/UI/FollowPresets/OracleDorsalHandsFollowPreset.asset";
        private const string DorsalControladorPredefinicaoCaminho = "Assets/VRTemplateAssets/UI/FollowPresets/OracleDorsalControllerFollowPreset.asset";
        private const string CaminhoModeloPainelEspacial = "Assets/VRTemplateAssets/Prefabs/UI/Spatial Panel Scroll.prefab";
        [MenuItem("Ferramentas/VR SNC/Interface/Aplicar Painel Espacial Principal (Preservar Oráculo)", false, 210)]
        private static void AplicarPainelEspacialPrincipal()
        {
            var painel = GameObject.Find(NomePainel);
            if (painel == null)
            {
                Debug.LogError($"{NomePainel} não foi encontrado; nenhuma alteração de UI foi feita.");
                return;
            }

            var seguir = painel.GetComponent<AcompanhamentoCameraPainelInterface>();
            var expandida = painel.transform.Find("ExpandedRoot") as RectTransform;
            if (seguir == null || expandida == null)
            {
                Debug.LogError("O acompanhamento do painel ou ExpandedRoot está ausente; nenhuma alteração de UI foi feita.", painel);
                return;
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Aplicar o painel anatômico espacial principal");
            var desfazerGrupo = Undo.GetCurrentGroup();
            try
            {
                var alca = GarantirControle(painel, expandida, seguir);
                ReestilizarPainel(painel, expandida);
                GarantirContrasteTextoPrincipal(expandida);
                // O layout final é sempre o painel compacto com abas; reaplicar esta ferramenta não volta ao anterior.
                ConfiguracaoPainelCompactoAbas.Aplicar(painel, GameObject.Find("Oraculo_Anatomico_Punho"));
                EditorSceneManager.MarkSceneDirty(painel.scene);
                Debug.Log("Painel anatômico espacial principal aplicado: pino compacto no mundo, alça inferior de manipulação, bordas de redimensionamento e recentralização. O pop-up aprovado do Oráculo no pulso não foi alterado.", painel);
            }
            catch
            {
                Undo.RevertAllDownToGroup(desfazerGrupo);
                throw;
            }
            finally
            {
                Undo.CollapseUndoOperations(desfazerGrupo);
            }
        }

        private static ControlePainelMundo GarantirControle(GameObject painel, RectTransform expandida, AcompanhamentoCameraPainelInterface seguir)
        {
            // 560 x 820 unidades de UI na escala 0,001 formam uma superfície de leitura de 0,56 x 0,82 m.
            // XRI remains responsible for bounded user resizing afterwards.
            Undo.RecordObject(painel.transform, "Definir escala inicial confortável do painel no mundo");
            painel.transform.localScale = Vector3.one * 0.001f;
            var alcaObjeto = expandida.Find(NomeControle);
            if (alcaObjeto == null)
            {
                var criado = new GameObject(NomeControle, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BoxCollider));
                Undo.RegisterCreatedObjectUndo(criado, "Criar a alça do painel no mundo");
                criado.layer = painel.layer;
                criado.transform.SetParent(expandida, false);
                alcaObjeto = criado.transform;
            }

            var alcaRetangulo = (RectTransform)alcaObjeto;
            Undo.RecordObject(alcaRetangulo, "Organizar a alça do painel no mundo");
            // Affordance de repouso ao estilo Quest: um pequeno ponto inferior expande para barra apenas no hover.
            alcaRetangulo.anchorMin = new Vector2(0.5f, 0f);
            alcaRetangulo.anchorMax = new Vector2(0.5f, 0f);
            alcaRetangulo.sizeDelta = new Vector2(30f, 18f);
            alcaRetangulo.anchoredPosition = new Vector2(0f, -42f);
            var alcaImagem = ObterOuAdicionar<Image>(alcaObjeto.gameObject, "Estilizar a alça do painel no mundo");
            Undo.RecordObject(alcaImagem, "Estilizar a alça do painel no mundo");
            alcaImagem.sprite = CarregarSprite(CaminhoImagemArredondada);
            alcaImagem.type = Image.Type.Sliced;
            alcaImagem.color = new Color(0.73f, 0.77f, 0.80f, 1f);
            alcaImagem.raycastTarget = false;
            GarantirLegendaManipulacao(alcaRetangulo);
            // Esta é a única superfície de captura da raiz. Fica abaixo do conteúdo, para trigger/pinch mover livremente em 3D.
            var esquerdaAgarre = ObterOuAdicionar<BoxCollider>(alcaObjeto.gameObject, "Configurar a empunhadura esquerda do painel no mundo");
            // A barra de foco tem 224 mm de largura; seu alvo físico tem a mesma largura e fica
            // 42 mm abaixo do painel, livre tanto da borda inferior de redimensionamento quanto da linha de botões.
            ConfigurarPegada(esquerdaAgarre, Vector3.zero, new Vector3(224f, 30f, 24f));
            var direitoAgarre = alcaObjeto.Find(NomePegadaDireita);
            if (direitoAgarre != null && direitoAgarre.gameObject.activeSelf)
            {
                Undo.RecordObject(direitoAgarre.gameObject, "Remover a empunhadura redundante do painel no mundo");
                direitoAgarre.gameObject.SetActive(false);
            }

            // Earlier revisions also placed a collider over most of Header. It could overlap UI ray targets
            // e ficava assimétrica depois da disposição dos botões do cabeçalho. A barra livre agora é a única superfície física
            // superfície de grab, enquanto o cabeçalho permanece uma superfície normal de UI/leitura.
            var cabecalho = expandida.Find("Header") as RectTransform;
            if (cabecalho != null)
            {
                DefinirCabecalhoAderenciaAtivo(cabecalho, NomeAgarreEsquerdoCabecalho, false);
                DefinirCabecalhoAderenciaAtivo(cabecalho, NomeAgarreDireitoCabecalho, false);
            }

            var corpo = ObterOuAdicionar<Rigidbody>(painel, "Configurar o Rigidbody do painel no mundo");
            Undo.RecordObject(corpo, "Configurar o Rigidbody do painel no mundo");
            corpo.useGravity = false;
            corpo.isKinematic = true;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
            var agarrar = ObterOuAdicionar<XRGrabInteractable>(painel, "Configurar a captura do painel no mundo");
            Undo.RecordObject(agarrar, "Configurar a captura do painel no mundo");
            agarrar.selectMode = InteractableSelectMode.Multiple;
            agarrar.throwOnDetach = false;
            agarrar.trackPosition = true;
            agarrar.trackRotation = true;
            agarrar.trackScale = true;
            agarrar.retainTransformParent = true;
            agarrar.useDynamicAttach = true;
            agarrar.matchAttachPosition = true;
            agarrar.matchAttachRotation = true;
            agarrar.reinitializeDynamicAttachEverySingleGrab = true;
            agarrar.colliders.Clear();
            agarrar.colliders.Add(esquerdaAgarre);
            var transformador = ObterOuAdicionar<XRGeneralGrabTransformer>(painel, "Configurar o escalonamento do painel no mundo com duas mãos");
            Undo.RecordObject(transformador, "Configurar o escalonamento do painel no mundo com duas mãos");
            transformador.allowOneHandedScaling = false;
            transformador.allowTwoHandedScaling = true;
            transformador.clampScaling = true;
            transformador.minimumScaleRatio = 0.70f;
            transformador.maximumScaleRatio = 1.45f;
            transformador.thresholdMoveRatioForScale = 0.03f;
            var alcaMundo = ObterOuAdicionar<ControlePainelMundo>(painel, "Configurar a alça do painel no mundo");
            Undo.RecordObject(alcaMundo, "Configurar a alça do painel no mundo");
            alcaMundo.Configurar(seguir);
            Undo.RecordObject(seguir, "Posicionar o painel anatômico ao lado da anatomia central");
            seguir.ConfigurarPosicionamentoNoMundo(0.82f, 0.42f, -0.02f);
            seguir.ReposicionarDianteDaCamera();
            seguir.DefinirAcompanhamentoAtivo(false);
            ObterOuAdicionar<ZonaProximidadePainelInterface>(expandida.gameObject, "Configurar a zona de proximidade de UI do painel anatômico completo");
            GarantirEstiloJanelaQuest(painel, expandida, seguir, agarrar, alcaRetangulo, alcaImagem);
            GarantirIntermediariosSelecaoGatilhoControlador(painel);
            var controles = expandida.Find("Controls") as RectTransform;
            if (controles != null)
            {
                Undo.RecordObject(controles, "Reservar espaço de navegação do painel anatômico");
                controles.anchorMin = new Vector2(0f, 0.115f);
                controles.anchorMax = new Vector2(1f, 0.242f);
                var grade = controles.GetComponent<GridLayoutGroup>();
                if (grade != null)
                {
                    Undo.RecordObject(grade, "Dimensionar áreas de toque da grade de ações do painel anatômico");
                    grade.cellSize = new Vector2(grade.cellSize.x, 46f);
                    grade.spacing = new Vector2(grade.spacing.x, 6f);
                }
            }

            DisporConteudoLegivelPainel(expandida);
            GarantirAcompanharBotoes(expandida, alcaMundo);
            GarantirContrasteTextoPrincipal(expandida);
            RetirarLegadoAlcaRecentralizarBotao(alcaObjeto);
            return alcaMundo;
        }

        private static void RetirarLegadoAlcaRecentralizarBotao(Transform alca)
        {
            var botaoTransformacao = alca.Find(RecentralizarBotaoNome) as RectTransform;
            if (botaoTransformacao != null && botaoTransformacao.gameObject.activeSelf)
            {
                Undo.RecordObject(botaoTransformacao.gameObject, "Remover o botão de recentralização da barra física de empunhadura");
                botaoTransformacao.gameObject.SetActive(false);
            }
        }

        private static void GarantirLegendaManipulacao(RectTransform alca)
        {
            var legenda = alca.Find(ManipularLegendaNome) as RectTransform;
            if (legenda == null)
            {
                var criado = new GameObject(ManipularLegendaNome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                Undo.RegisterCreatedObjectUndo(criado, "Criar instrução de manipulação do painel espacial");
                criado.layer = alca.gameObject.layer;
                criado.transform.SetParent(alca, false);
                legenda = criado.GetComponent<RectTransform>();
            }

            Undo.RecordObject(legenda, "Organizar instrução de manipulação do painel espacial");
            legenda.anchorMin = new Vector2(0.04f, 0.10f);
            legenda.anchorMax = new Vector2(0.96f, 0.90f);
            legenda.offsetMin = Vector2.zero;
            legenda.offsetMax = Vector2.zero;
            legenda.SetSiblingIndex(0);
            var texto = legenda.GetComponent<Text>();
            Undo.RecordObject(texto, "Estilizar instrução de manipulação do painel espacial");
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = 10;
            texto.fontStyle = FontStyle.Bold;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = new Color(0.12f, 0.15f, 0.18f, 1f);
            texto.text = "";
            texto.raycastTarget = false;
        }

        private static void GarantirEstiloJanelaQuest(GameObject painel, RectTransform expandida, AcompanhamentoCameraPainelInterface seguir, XRGrabInteractable agarrar, RectTransform alca, Image alcaImagem)
        {
            var janela = ObterOuAdicionar<JanelaEspacialPrincipal>(painel, "Configurar janela espacial no estilo Quest");
            Undo.RecordObject(janela, "Configurar janela espacial no estilo Quest");
            // ExpandedRoot é um contêiner de conteúdo somente esticado (sizeDelta é zero). Redimensione o
            // RectTransform real do painel para que conteúdo e todas as affordances de borda ancoradas acompanhem.
            janela.Configurar(painel.transform as RectTransform ?? expandida, seguir, agarrar, alca, alcaImagem);
            var raiz = expandida.Find(NomeRaizRedimensionamento) as RectTransform;
            if (raiz == null)
            {
                var criado = new GameObject(NomeRaizRedimensionamento, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(criado, "Criar recursos de redimensionamento da janela espacial");
                criado.layer = expandida.gameObject.layer;
                criado.transform.SetParent(expandida, false);
                raiz = criado.GetComponent<RectTransform>();
            }

            Undo.RecordObject(raiz, "Organizar recursos de redimensionamento da janela espacial");
            raiz.anchorMin = Vector2.zero;
            raiz.anchorMax = Vector2.one;
            raiz.offsetMin = Vector2.zero;
            raiz.offsetMax = Vector2.zero;
            GarantirBordaRedimensionamento(raiz, "Top", new Vector2(0.12f, 0.97f), new Vector2(0.88f, 1f), Vector2.up, janela);
            GarantirBordaRedimensionamento(raiz, "Bottom", new Vector2(0.12f, 0f), new Vector2(0.88f, 0.028f), Vector2.down, janela);
            GarantirBordaRedimensionamento(raiz, "Left", new Vector2(0f, 0.12f), new Vector2(0.028f, 0.88f), Vector2.left, janela);
            GarantirBordaRedimensionamento(raiz, "Right", new Vector2(0.972f, 0.12f), new Vector2(1f, 0.88f), Vector2.right, janela);
            GarantirBordaRedimensionamento(raiz, "TopLeft", new Vector2(0f, 0.97f), new Vector2(0.06f, 1f), new Vector2(-1f, 1f), janela);
            GarantirBordaRedimensionamento(raiz, "TopRight", new Vector2(.94f, .97f), Vector2.one, new Vector2(1f, 1f), janela);
            GarantirBordaRedimensionamento(raiz, "BottomLeft", Vector2.zero, new Vector2(.06f, .03f), new Vector2(-1f, -1f), janela);
            GarantirBordaRedimensionamento(raiz, "BottomRight", new Vector2(.94f, 0f), new Vector2(1f, .03f), new Vector2(1f, -1f), janela);
        }

        private static void GarantirIntermediariosSelecaoGatilhoControlador(GameObject painel)
        {
            var modalidade = Object.FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(candidato => candidato != null && candidato.gameObject.scene == painel.scene);
            if (modalidade == null)
                return;
            ConfigurarControladorGatilhoIntermediario(modalidade.leftController);
            ConfigurarControladorGatilhoIntermediario(modalidade.rightController);
        }

        private static void ConfigurarControladorGatilhoIntermediario(GameObject controladorGrupo)
        {
            if (controladorGrupo == null)
                return;
            foreach (var proximoDistante in controladorGrupo.GetComponentsInChildren<NearFarInteractor>(true))
            {
                if (proximoDistante == null)
                    continue;
                var intermediario = ObterOuAdicionar<IntermediarioSelecaoControladorJanelaPrincipal>(proximoDistante.gameObject, "Add controller trigger reader for main spatial window");
                Undo.RecordObject(proximoDistante, "Encaminhar o gatilho do controle somente para a janela espacial principal");
                Undo.RecordObject(intermediario, "Configurar o leitor do gatilho do controle para a janela espacial principal");
                intermediario.Configurar(proximoDistante);
            }
        }

        private static void GarantirBordaRedimensionamento(RectTransform pai, string nome, Vector2 ancoraMinimo, Vector2 ancoraMaximo, Vector2 direcao, JanelaEspacialPrincipal janela)
        {
            var borda = pai.Find(nome) as RectTransform;
            if (borda == null)
            {
                var criado = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BoxCollider), typeof(XRSimpleInteractable), typeof(LimiteRedimensionamentoJanelaPrincipal));
                Undo.RegisterCreatedObjectUndo(criado, "Criar borda de redimensionamento da janela espacial");
                criado.layer = pai.gameObject.layer;
                criado.transform.SetParent(pai, false);
                borda = criado.GetComponent<RectTransform>();
            }

            Undo.RecordObject(borda, "Organizar borda de redimensionamento da janela espacial");
            borda.anchorMin = ancoraMinimo;
            borda.anchorMax = ancoraMaximo;
            borda.offsetMin = Vector2.zero;
            borda.offsetMax = Vector2.zero;
            var imagem = ObterOuAdicionar<Image>(borda.gameObject, "Estilizar borda de redimensionamento da janela espacial");
            Undo.RecordObject(imagem, "Estilizar borda de redimensionamento da janela espacial");
            imagem.color = new Color(.30f, .36f, .42f, .08f);
            imagem.raycastTarget = false;
            var colisor = ObterOuAdicionar<BoxCollider>(borda.gameObject, "Configurar borda de redimensionamento da janela espacial");
            Undo.RecordObject(colisor, "Configurar borda de redimensionamento da janela espacial");
            colisor.center = Vector3.zero;
            colisor.size = new Vector3(32f, 32f, 24f);
            colisor.isTrigger = true;
            var interagivel = ObterOuAdicionar<XRSimpleInteractable>(borda.gameObject, "Configurar borda de redimensionamento da janela espacial");
            Undo.RecordObject(interagivel, "Configurar borda de redimensionamento da janela espacial");
            interagivel.colliders.Clear();
            interagivel.colliders.Add(colisor);
            var redimensionar = ObterOuAdicionar<LimiteRedimensionamentoJanelaPrincipal>(borda.gameObject, "Configurar borda de redimensionamento da janela espacial");
            Undo.RecordObject(redimensionar, "Configurar borda de redimensionamento da janela espacial");
            redimensionar.Configurar(janela, direcao);
        }

        private static void GarantirContrasteTextoPrincipal(RectTransform expandida)
        {
            foreach (var texto in expandida.GetComponentsInChildren<Text>(true))
            {
                if (texto == null)
                    continue;
                Undo.RecordObject(texto, "Definir contraste do texto do painel espacial principal");
                texto.color = Color.black;
            }

            foreach (var texto2 in expandida.GetComponentsInChildren<TMP_Text>(true))
            {
                if (texto2 == null)
                    continue;
                Undo.RecordObject(texto2, "Definir contraste do texto TMP do painel espacial principal");
                texto2.color = Color.black;
            }
        }

        private static void GarantirAcompanharBotoes(RectTransform expandida, ControlePainelMundo alcaMundo)
        {
            var controles = expandida.Find(NomeControlesEspaciais) as RectTransform;
            if (controles == null)
            {
                var criado = new GameObject(NomeControlesEspaciais, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(criado, "Criar linha de controles do painel espacial");
                criado.layer = expandida.gameObject.layer;
                criado.transform.SetParent(expandida, false);
                controles = criado.GetComponent<RectTransform>();
            }

            Undo.RecordObject(controles, "Organizar linha de controles do painel espacial");
            controles.anchorMin = new Vector2(0.04f, 0.044f);
            controles.anchorMax = new Vector2(0.96f, 0.103f);
            controles.offsetMin = Vector2.zero;
            controles.offsetMax = Vector2.zero;
            var seguir = GarantirPainelAcaoBotao(controles, NomeBotaoAcompanhar, "SEGUIR", new Vector2(0.02f, 0.10f), new Vector2(0.31f, 0.90f));
            var recentralizar = GarantirPainelAcaoBotao(controles, RecentralizarBotaoNome, "RECENTRAR", new Vector2(0.35f, 0.10f), new Vector2(0.65f, 0.90f));
            var fixar = GarantirPainelAcaoBotao(controles, FixarBotaoNome, "FIXAR", new Vector2(0.69f, 0.10f), new Vector2(0.98f, 0.90f));
            DefinirOuvinteBooleanoPersistente(seguir, alcaMundo, nameof(ControlePainelMundo.DefinirAcoplamento), true, alcaMundo.DefinirAcoplamento);
            DefinirOuvintePersistente(recentralizar, alcaMundo, nameof(ControlePainelMundo.RecentralizarPainel), alcaMundo.RecentralizarPainel);
            DefinirOuvinteBooleanoPersistente(fixar, alcaMundo, nameof(ControlePainelMundo.DefinirAcoplamento), false, alcaMundo.DefinirAcoplamento);
        }

        private static void DisporConteudoLegivelPainel(RectTransform expandida)
        {
            // Mantém o cartão científico e a rolagem de navegação em faixas verticais separadas.
            // O painel físico tem 560 x 820 unidades de UI: a altura extra vai para a ficha e para a lista,
            // que mostra cerca de seis estruturas por vez; cabeçalho e botões mantêm o tamanho anterior.
            var informacao = expandida.Find("InfoPanel") as RectTransform;
            if (informacao != null)
            {
                Undo.RecordObject(informacao, "Reservar faixa legível de informações anatômicas");
                informacao.anchorMin = new Vector2(0f, 0.551f);
                informacao.anchorMax = new Vector2(1f, 0.837f);
                informacao.offsetMin = new Vector2(16f, 0f);
                informacao.offsetMax = new Vector2(-16f, -4f);
            }

            var navegacao = expandida.Find("NavigationPanel") as RectTransform;
            if (navegacao != null)
            {
                Undo.RecordObject(navegacao, "Reservar faixa legível de navegação anatômica");
                navegacao.anchorMin = new Vector2(0f, 0.246f);
                navegacao.anchorMax = new Vector2(1f, 0.551f);
                navegacao.offsetMin = new Vector2(16f, 0f);
                navegacao.offsetMax = new Vector2(-16f, -4f);
            }
        }

        private static Button GarantirPainelAcaoBotao(RectTransform pai, string nome, string rotulo, Vector2 ancoraMinimo, Vector2 ancoraMaximo)
        {
            var botaoRetangulo = pai.Find(nome) as RectTransform;
            if (botaoRetangulo == null)
            {
                var criado = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(criado, "Criar controle explícito de acoplamento do painel");
                criado.layer = pai.gameObject.layer;
                criado.transform.SetParent(pai, false);
                botaoRetangulo = criado.GetComponent<RectTransform>();
                var texto = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                Undo.RegisterCreatedObjectUndo(texto, "Criar rótulo do controle de acoplamento do painel");
                texto.layer = criado.layer;
                texto.transform.SetParent(criado.transform, false);
                var textoRetangulo = texto.GetComponent<RectTransform>();
                textoRetangulo.anchorMin = Vector2.zero;
                textoRetangulo.anchorMax = Vector2.one;
                textoRetangulo.offsetMin = Vector2.zero;
                textoRetangulo.offsetMax = Vector2.zero;
                var textoElementoVisual = texto.GetComponent<Text>();
                textoElementoVisual.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                textoElementoVisual.fontSize = 16;
                textoElementoVisual.alignment = TextAnchor.MiddleCenter;
                textoElementoVisual.raycastTarget = false;
            }

            Undo.RecordObject(botaoRetangulo, "Organizar controle explícito de acoplamento do painel");
            botaoRetangulo.anchorMin = ancoraMinimo;
            botaoRetangulo.anchorMax = ancoraMaximo;
            botaoRetangulo.offsetMin = Vector2.zero;
            botaoRetangulo.offsetMax = Vector2.zero;
            var imagem = botaoRetangulo.GetComponent<Image>();
            Undo.RecordObject(imagem, "Estilizar controle explícito de acoplamento do painel");
            imagem.sprite = CarregarSprite(CaminhoImagemArredondada);
            imagem.type = Image.Type.Sliced;
            imagem.color = new Color(0.96f, 0.97f, 0.98f, 1f);
            imagem.raycastTarget = true;
            var botao = botaoRetangulo.GetComponent<Button>();
            Undo.RecordObject(botao, "Conectar controle explícito de acoplamento do painel");
            botao.targetGraphic = imagem;
            var rotuloTexto = botaoRetangulo.GetComponentInChildren<Text>(true);
            if (rotuloTexto != null)
            {
                Undo.RecordObject(rotuloTexto, "Definir rótulo explícito de acoplamento do painel");
                rotuloTexto.text = rotulo;
                rotuloTexto.color = CorRotuloBotao;
            }

            return botao;
        }

        private static BoxCollider GarantirPegadaDireita(Transform alca)
        {
            var agarre = alca.Find(NomePegadaDireita);
            if (agarre == null)
            {
                var criado = new GameObject(NomePegadaDireita, typeof(BoxCollider));
                Undo.RegisterCreatedObjectUndo(criado, "Criar a empunhadura direita do painel no mundo");
                criado.layer = alca.gameObject.layer;
                criado.transform.SetParent(alca, false);
                agarre = criado.transform;
            }

            var colisor = ObterOuAdicionar<BoxCollider>(agarre.gameObject, "Configurar a empunhadura direita do painel no mundo");
            ConfigurarPegada(colisor, new Vector3(158f, 0f, 0f), new Vector3(240f, 52f, 24f));
            return colisor;
        }

        private static void ConfigurarPegada(BoxCollider colisor, Vector3 centro, Vector3 tamanho)
        {
            Undo.RecordObject(colisor, "Configurar o colisor da empunhadura do painel no mundo");
            colisor.center = centro;
            colisor.size = tamanho;
            colisor.isTrigger = true;
        }

        /// <summary>
        /// Adiciona grab do cabeçalho principal ao mesmo XRGrabInteractable raiz. Se o cabeçalho já tiver
        /// botões, as faixas de grab mantêm sua união horizontal livre para interação com UI.
        /// </summary>
        private static List<BoxCollider> GarantirColisoresAgarreCabecalho(RectTransform cabecalho, int camada)
        {
            var colisores = new List<BoxCollider>(2);
            if (cabecalho == null)
            {
                return colisores;
            }

            var retangulo = cabecalho.rect;
            var minimoExcluido = float.PositiveInfinity;
            var maximoExcluido = float.NegativeInfinity;
            foreach (var botao in cabecalho.GetComponentsInChildren<Button>(true))
            {
                var botaoRetangulo = botao.transform as RectTransform;
                if (botaoRetangulo == null)
                {
                    continue;
                }

                var cantos = new Vector3[4];
                botaoRetangulo.GetWorldCorners(cantos);
                for (var indice = 0; indice < cantos.Length; indice++)
                {
                    var local = cabecalho.InverseTransformPoint(cantos[indice]);
                    minimoExcluido = Mathf.Min(minimoExcluido, local.x);
                    maximoExcluido = Mathf.Max(maximoExcluido, local.x);
                }
            }

            if (float.IsPositiveInfinity(minimoExcluido))
            {
                colisores.Add(ConfigurarAgarreCabecalho(cabecalho, NomeAgarreEsquerdoCabecalho, camada, retangulo.xMin, retangulo.xMax));
                DefinirCabecalhoAderenciaAtivo(cabecalho, NomeAgarreDireitoCabecalho, false);
                return colisores;
            }

            minimoExcluido = Mathf.Clamp(minimoExcluido, retangulo.xMin, retangulo.xMax);
            maximoExcluido = Mathf.Clamp(maximoExcluido, retangulo.xMin, retangulo.xMax);
            if (minimoExcluido > retangulo.xMin)
            {
                colisores.Add(ConfigurarAgarreCabecalho(cabecalho, NomeAgarreEsquerdoCabecalho, camada, retangulo.xMin, minimoExcluido));
            }
            else
            {
                DefinirCabecalhoAderenciaAtivo(cabecalho, NomeAgarreEsquerdoCabecalho, false);
            }

            if (maximoExcluido < retangulo.xMax)
            {
                colisores.Add(ConfigurarAgarreCabecalho(cabecalho, NomeAgarreDireitoCabecalho, camada, maximoExcluido, retangulo.xMax));
            }
            else
            {
                DefinirCabecalhoAderenciaAtivo(cabecalho, NomeAgarreDireitoCabecalho, false);
            }

            return colisores;
        }

        private static BoxCollider ConfigurarAgarreCabecalho(RectTransform cabecalho, string nome, int camada, float minimoX, float maximoX)
        {
            var agarre = cabecalho.Find(nome);
            if (agarre == null)
            {
                var criado = new GameObject(nome, typeof(BoxCollider));
                Undo.RegisterCreatedObjectUndo(criado, "Criar a empunhadura do cabeçalho do painel principal");
                criado.layer = camada;
                criado.transform.SetParent(cabecalho, false);
                agarre = criado.transform;
            }

            agarre.gameObject.SetActive(true);
            var colisor = ObterOuAdicionar<BoxCollider>(agarre.gameObject, "Configurar a empunhadura do cabeçalho do painel principal");
            var espessuraFisica = 0.02f;
            var escalaZ = Mathf.Max(0.00001f, Mathf.Abs(cabecalho.lossyScale.z));
            Undo.RecordObject(colisor, "Configurar a empunhadura do cabeçalho do painel principal");
            colisor.center = new Vector3((minimoX + maximoX) * 0.5f, cabecalho.rect.center.y, 0f);
            colisor.size = new Vector3(Mathf.Max(0f, maximoX - minimoX), cabecalho.rect.height, espessuraFisica / escalaZ);
            colisor.isTrigger = true;
            return colisor;
        }

        private static void DefinirCabecalhoAderenciaAtivo(RectTransform cabecalho, string nome, bool ativo)
        {
            var agarre = cabecalho.Find(nome);
            if (agarre != null && agarre.gameObject.activeSelf != ativo)
            {
                Undo.RecordObject(agarre.gameObject, "Alternar a empunhadura do cabeçalho do painel principal");
                agarre.gameObject.SetActive(ativo);
            }
        }

        private static void ReestilizarPainel(GameObject painel, RectTransform expandida)
        {
            GarantirContornoModeloEspacial(expandida);
            EstiloImagem(expandida.Find("Background")?.GetComponent<Image>(), new Color(0.94f, 0.95f, 0.96f, 0.98f), CaminhoImagemArredondada);
            EstiloImagem(expandida.Find("Header")?.GetComponent<Image>(), new Color(0.84f, 0.87f, 0.89f, 1f), CaminhoImagemArredondadaSuperior);
            EstiloImagem(expandida.Find("InfoPanel")?.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.96f), CaminhoImagemArredondada);
            EstiloImagem(expandida.Find("NavigationPanel")?.GetComponent<Image>(), new Color(0.97f, 0.98f, 0.99f, 0.96f), CaminhoImagemArredondada);
            var cabecalho = expandida.Find("Header") as RectTransform;
            if (cabecalho != null)
            {
                Undo.RecordObject(cabecalho, "Reservar cabeçalho espaçoso para o painel espacial");
                cabecalho.anchorMin = new Vector2(0f, 0.837f);
                cabecalho.anchorMax = Vector2.one;
                cabecalho.offsetMin = new Vector2(16f, 0f);
                cabecalho.offsetMax = new Vector2(-16f, -14f);
                DisporTextoCabecalho(cabecalho.Find("Title") as RectTransform, new Vector2(0.03f, 0.62f), new Vector2(0.50f, 0.98f), 26, TextAnchor.MiddleLeft);
                DisporTextoCabecalho(cabecalho.Find("Status") as RectTransform, new Vector2(0.50f, 0.62f), new Vector2(0.97f, 0.98f), 15, TextAnchor.MiddleRight);
                DisporTextoCabecalho(cabecalho.Find("Path") as RectTransform, new Vector2(0.03f, 0.30f), new Vector2(0.97f, 0.61f), 14, TextAnchor.MiddleLeft);
                foreach (var elementoVisual in cabecalho.GetComponentsInChildren<Graphic>(true))
                {
                    Undo.RecordObject(elementoVisual, "Manter o cabeçalho espacial livre para captura física");
                    elementoVisual.raycastTarget = false;
                }
            }

            foreach (var texto in expandida.GetComponentsInChildren<Text>(true))
            {
                Undo.RecordObject(texto, "Estilizar o texto do painel anatômico");
                texto.color = new Color(0.10f, 0.12f, 0.15f, 1f);
            }

            foreach (var texto2 in expandida.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(texto2, "Estilizar o texto TMP do painel anatômico");
                texto2.color = new Color(0.10f, 0.12f, 0.15f, 1f);
            }

            foreach (var botao in expandida.GetComponentsInChildren<Button>(true))
            {
                var imagem = botao.targetGraphic as Image ?? botao.GetComponent<Image>();
                if (imagem == null)
                {
                    continue;
                }

                Undo.RecordObject(imagem, "Estilizar o botão do painel anatômico");
                imagem.sprite = CarregarSprite(CaminhoImagemArredondada);
                imagem.type = Image.Type.Sliced;
                imagem.color = new Color(0.96f, 0.97f, 0.98f, 1f);
                var cores = botao.colors;
                cores.normalColor = Color.white;
                cores.highlightedColor = new Color(0.82f, 0.86f, 0.90f, 1f);
                cores.pressedColor = new Color(0.66f, 0.72f, 0.79f, 1f);
                Undo.RecordObject(botao, "Estilizar o botão do painel anatômico");
                botao.colors = cores;
                EstiloBotaoRotulo(botao.transform);
            }
        }

        private static void DisporTextoCabecalho(RectTransform alvo, Vector2 ancoraMinimo, Vector2 ancoraMaximo, int tamanho, TextAnchor alinhamento)
        {
            if (alvo == null)
            {
                return;
            }

            Undo.RecordObject(alvo, "Organizar o texto do cabeçalho do painel espacial");
            alvo.anchorMin = ancoraMinimo;
            alvo.anchorMax = ancoraMaximo;
            alvo.offsetMin = Vector2.zero;
            alvo.offsetMax = Vector2.zero;
            var texto = alvo.GetComponent<Text>();
            if (texto != null)
            {
                Undo.RecordObject(texto, "Estilizar o texto do cabeçalho do painel espacial");
                texto.fontSize = tamanho;
                texto.fontStyle = alvo.name == "Title" ? FontStyle.Bold : FontStyle.Normal;
                texto.alignment = alinhamento;
            }
        }

        /// <summary>
        /// Reutiliza PanelOutline do template XR real como borda apenas visual. Somente esse pequeno
        /// decorative child is copied; no template scroll content or existing anatomy UI is replaced.
        /// </summary>
        private static void GarantirContornoModeloEspacial(RectTransform expandida)
        {
            var contorno = expandida.Find(ModeloContornoNome) as RectTransform;
            if (contorno == null)
            {
                var modeloRaiz = PrefabUtility.LoadPrefabContents(CaminhoModeloPainelEspacial);
                try
                {
                    var fonte = modeloRaiz.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(candidato2 => candidato2.name == "PanelOutline");
                    if (fonte == null)
                    {
                        throw new InvalidDataException("O modelo de rolagem do painel espacial não possui o filho PanelOutline.");
                    }

                    var criado = Object.Instantiate(fonte.gameObject, expandida, false);
                    Undo.RegisterCreatedObjectUndo(criado, "Copiar o contorno do painel espacial XR");
                    criado.name = ModeloContornoNome;
                    contorno = criado.GetComponent<RectTransform>();
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(modeloRaiz);
                }
            }

            Undo.RecordObject(contorno, "Organizar o contorno do painel espacial XR");
            contorno.anchorMin = Vector2.zero;
            contorno.anchorMax = Vector2.one;
            contorno.offsetMin = new Vector2(3f, 3f);
            contorno.offsetMax = new Vector2(-3f, -3f);
            contorno.SetSiblingIndex(1);
            var imagem = contorno.GetComponent<Image>();
            if (imagem != null)
            {
                Undo.RecordObject(imagem, "Estilizar o contorno do painel espacial XR");
                imagem.color = new Color(0.32f, 0.40f, 0.48f, 0.85f);
                imagem.raycastTarget = false;
            }
        }

        private static void ConfigurarMenuDorsalPulso()
        {
            var pulso = GameObject.Find("Oraculo_Anatomico_Punho");
            var menu = pulso != null ? pulso.GetComponent<HandMenu>() : null;
            if (menu == null)
            {
                Debug.LogWarning("O HandMenu do Oráculo no pulso não foi encontrado; a UI do painel foi aplicada sem alterações no pulso.");
                return;
            }

            Undo.RecordObject(menu, "Configurar o menu dorsal do pulso");
            menu.hideMenuWhenGazeDiverges = false;
            menu.hideMenuOnSelect = false;
            ClonarPredefinicaoDorsal(menu, "m_HandTrackingFollowPreset", DorsalMaosPredefinicaoCaminho);
            ClonarPredefinicaoDorsal(menu, "m_ControllerFollowPreset", DorsalControladorPredefinicaoCaminho);
            GarantirModernizacaoPainelPulso(pulso, menu);
        }

        // Preparado somente pelo menu de setup idempotente; nunca toca no prefab do template.
        private static void GarantirModernizacaoPainelPulso(GameObject pulso, HandMenu menu)
        {
            var posturaRaiz = menu.handMenuUIGameObject != null ? menu.handMenuUIGameObject : pulso;
            var rolagem = posturaRaiz.GetComponentInChildren<ScrollRect>(true);
            var painelVisivel = rolagem != null ? rolagem.transform.parent as RectTransform : null;
            if (rolagem == null || painelVisivel == null)
            {
                throw new InvalidDataException("O ScrollHandMenu copiado não possui RectTransform de painel visível.");
            }

            var cabecalho = painelVisivel.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x => x.name == "Header");
            if (cabecalho == null)
            {
                throw new InvalidDataException("O ScrollHandMenu copiado não possui RectTransform de cabeçalho.");
            }

            var corpo = ObterOuAdicionar<Rigidbody>(posturaRaiz, "Configurar o Rigidbody do painel de pulso");
            Undo.RecordObject(corpo, "Configurar o Rigidbody do painel de pulso");
            corpo.isKinematic = true;
            corpo.useGravity = false;
            corpo.interpolation = RigidbodyInterpolation.Interpolate;
            var colisor = ObterOuAdicionar<BoxCollider>(cabecalho.gameObject, "Configurar a captura do cabeçalho de pulso");
            var espessuraFisica = 0.02f;
            var escalaZ = Mathf.Max(0.00001f, Mathf.Abs(cabecalho.lossyScale.z));
            Undo.RecordObject(colisor, "Configurar a captura do cabeçalho de pulso");
            colisor.center = new Vector3(cabecalho.rect.center.x, cabecalho.rect.center.y, 0f);
            colisor.size = new Vector3(cabecalho.rect.width, cabecalho.rect.height, espessuraFisica / escalaZ);
            colisor.isTrigger = true;
            var agarrar = ObterOuAdicionar<XRGrabInteractable>(posturaRaiz, "Configurar a captura do cabeçalho de pulso");
            Undo.RecordObject(agarrar, "Configurar a captura do cabeçalho de pulso");
            agarrar.selectMode = InteractableSelectMode.Multiple;
            agarrar.throwOnDetach = false;
            agarrar.trackPosition = true;
            agarrar.trackRotation = true;
            agarrar.trackScale = true;
            agarrar.retainTransformParent = true;
            agarrar.useDynamicAttach = true;
            agarrar.matchAttachPosition = true;
            agarrar.matchAttachRotation = true;
            agarrar.reinitializeDynamicAttachEverySingleGrab = true;
            agarrar.colliders.Clear();
            agarrar.colliders.Add(colisor);
            var transformador = ObterOuAdicionar<XRGeneralGrabTransformer>(posturaRaiz, "Configurar o escalonamento do painel de pulso");
            Undo.RecordObject(transformador, "Configurar o escalonamento do painel de pulso");
            transformador.allowOneHandedScaling = false;
            transformador.allowTwoHandedScaling = true;
            transformador.clampScaling = true;
            transformador.minimumScaleRatio = .8f;
            transformador.maximumScaleRatio = 1.25f;
            // poseRoot é o GameObject Follow simples do HandMenu. O painel visível real de 440x520 é
            // scroll.transform.parent; somente ele pode receber uma zona de proximidade baseada em RectTransform.
            ObterOuAdicionar<ZonaProximidadePainelInterface>(painelVisivel.gameObject, "Configure full wrist UI proximity zone");
            var alcaMundo = ObterOuAdicionar<ControlePainelMundo>(posturaRaiz, "Registrar o estado global de captura do painel de pulso");
            Undo.RecordObject(alcaMundo, "Registrar o estado global de captura do painel de pulso");
            alcaMundo.Configurar(null);
            var ancora = ObterOuAdicionar<AncoraEspacialPulso>(posturaRaiz, "Configurar a âncora espacial de pulso");
            var modalidade = Object.FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(candidato3 => candidato3.gameObject.scene == pulso.scene);
            ancora.Configurar(menu.leftPalmAnchor, menu.rightPalmAnchor, Camera.main != null ? Camera.main.transform : null, menu, agarrar, modalidade);
            GarantirBotaoAcoplarPulso(rolagem, ancora);
        }

        private static void GarantirBotaoAcoplarPulso(ScrollRect rolagem, AncoraEspacialPulso ancora)
        {
            if (rolagem.content == null)
            {
                throw new InvalidDataException("O ScrollHandMenu copiado não possui conteúdo ScrollRect para Acoplar.");
            }

            var botaoTransformacao = rolagem.content.Find(JanelaFlutuanteAcoplarBotaoNome) as RectTransform;
            if (botaoTransformacao == null)
            {
                var criado = new GameObject(JanelaFlutuanteAcoplarBotaoNome, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(criado, "Criar botão de acoplamento do painel de pulso");
                criado.layer = rolagem.content.gameObject.layer;
                criado.transform.SetParent(rolagem.content, false);
                botaoTransformacao = criado.GetComponent<RectTransform>();
                var rotuloObjeto = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(rotuloObjeto, "Criar rótulo de acoplamento do painel de pulso");
                rotuloObjeto.layer = criado.layer;
                rotuloObjeto.transform.SetParent(criado.transform, false);
                var rotuloRetangulo = rotuloObjeto.GetComponent<RectTransform>();
                rotuloRetangulo.anchorMin = Vector2.zero;
                rotuloRetangulo.anchorMax = Vector2.one;
                rotuloRetangulo.offsetMin = new Vector2(8f, 4f);
                rotuloRetangulo.offsetMax = new Vector2(-8f, -4f);
                var rotuloTexto = rotuloObjeto.GetComponent<TextMeshProUGUI>();
                var textoExistente = rolagem.GetComponentInChildren<TMP_Text>(true);
                rotuloTexto.font = textoExistente != null ? textoExistente.font : TMP_Settings.defaultFontAsset;
                rotuloTexto.fontSize = 19f;
                rotuloTexto.alignment = TextAlignmentOptions.Center;
                rotuloTexto.raycastTarget = false;
            }

            var imagem = botaoTransformacao.GetComponent<Image>();
            Undo.RecordObject(imagem, "Estilizar botão de acoplamento do painel de pulso");
            var fonteImagem = rolagem.GetComponentsInParent<Image>(true).FirstOrDefault(candidato4 => candidato4.sprite != null);
            imagem.sprite = fonteImagem != null ? fonteImagem.sprite : CarregarSprite(CaminhoImagemArredondada);
            imagem.type = Image.Type.Sliced;
            imagem.color = new Color(0.96f, 0.97f, 0.98f, 1f);
            imagem.raycastTarget = true;
            var organizacao = botaoTransformacao.GetComponent<LayoutElement>();
            Undo.RecordObject(organizacao, "Organizar botão de acoplamento do painel de pulso");
            organizacao.preferredHeight = 40f;
            var rotuloTextoEncontrado = botaoTransformacao.GetComponentInChildren<TMP_Text>(true);
            if (rotuloTextoEncontrado != null)
            {
                Undo.RecordObject(rotuloTextoEncontrado, "Definir rótulo de acoplamento do painel de pulso");
                rotuloTextoEncontrado.text = "Acoplar ao punho";
                rotuloTextoEncontrado.color = CorRotuloBotao;
            }

            var botao = botaoTransformacao.GetComponent<Button>();
            Undo.RecordObject(botao, "Conectar botão de acoplamento do painel de pulso");
            botao.targetGraphic = imagem;
            var cores = botao.colors;
            cores.normalColor = Color.white;
            cores.highlightedColor = new Color(0.82f, 0.86f, 0.90f, 1f);
            cores.pressedColor = new Color(0.66f, 0.72f, 0.79f, 1f);
            botao.colors = cores;
            DefinirOuvintePersistente(botao, ancora, nameof(AncoraEspacialPulso.AcoplarAoPulso), ancora.AcoplarAoPulso);
        }

        private static void ClonarPredefinicaoDorsal(HandMenu menu, string menuPropriedadeNome, string destinoCaminho)
        {
            var menuObjeto = new SerializedObject(menu);
            var predefinicaoPropriedade = menuObjeto.FindProperty(menuPropriedadeNome);
            var variavelPropriedade = predefinicaoPropriedade?.FindPropertyRelative("m_Variable");
            if (variavelPropriedade == null)
            {
                throw new InvalidDataException($"HandMenu preset property {menuPropriedadeNome} is unavailable.");
            }

            var predefinicao = AssetDatabase.LoadAssetAtPath<FollowPresetDatum>(destinoCaminho);
            if (predefinicao == null)
            {
                var fonte = variavelPropriedade.objectReferenceValue as FollowPresetDatum;
                if (fonte == null)
                {
                    throw new InvalidDataException($"HandMenu preset {menuPropriedadeNome} não possui dado de origem.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinoCaminho));
                predefinicao = Object.Instantiate(fonte);
                predefinicao.name = Path.GetFileNameWithoutExtension(destinoCaminho);
                AssetDatabase.CreateAsset(predefinicao, destinoCaminho);
            }

            ConfigurarPredefinicaoDorsal(predefinicao);
            variavelPropriedade.objectReferenceValue = predefinicao;
            predefinicaoPropriedade.FindPropertyRelative("m_UseConstant").boolValue = false;
            menuObjeto.ApplyModifiedProperties();
        }

        private static void ConfigurarPredefinicaoDorsal(FollowPresetDatum predefinicao)
        {
            var predefinicaoObjeto = new SerializedObject(predefinicao);
            predefinicaoObjeto.FindProperty("m_ReadOnly").boolValue = false;
            var valor = predefinicaoObjeto.FindProperty("m_Value");
            valor.FindPropertyRelative("rightHandLocalPosition").vector3Value = new Vector3(0.17f, 0f, -0.10f);
            valor.FindPropertyRelative("leftHandLocalPosition").vector3Value = new Vector3(-0.17f, 0f, -0.10f);
            valor.FindPropertyRelative("rightHandLocalRotation").vector3Value = new Vector3(90f, 0f, 0f);
            valor.FindPropertyRelative("leftHandLocalRotation").vector3Value = new Vector3(90f, 0f, 0f);
            valor.FindPropertyRelative("requirePalmFacingUser").boolValue = false;
            valor.FindPropertyRelative("requirePalmFacingUp").boolValue = false;
            valor.FindPropertyRelative("snapToGaze").boolValue = false;
            valor.FindPropertyRelative("allowSmoothing").boolValue = true;
            valor.FindPropertyRelative("hideDelaySeconds").floatValue = 0.15f;
            predefinicaoObjeto.FindProperty("m_ReadOnly").boolValue = true;
            predefinicaoObjeto.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(predefinicao);
            AssetDatabase.SaveAssetIfDirty(predefinicao);
        }

        private static T ObterOuAdicionar<T>(GameObject jogoObjeto, string desfazerNome)
            where T : Component
        {
            var componente = jogoObjeto.GetComponent<T>();
            return componente != null ? componente : Undo.AddComponent<T>(jogoObjeto);
        }

        private static readonly Color CorRotuloBotao = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static void DefinirOuvintePersistente(Button botao, Object alvo, string metodoNome, UnityAction ouvinte)
        {
            for (var indice = botao.onClick.GetPersistentEventCount() - 1; indice >= 0; indice--)
            {
                if (botao.onClick.GetPersistentTarget(indice) == alvo && botao.onClick.GetPersistentMethodName(indice) == metodoNome)
                {
                    UnityEventTools.RemovePersistentListener(botao.onClick, indice);
                }
            }

            UnityEventTools.AddPersistentListener(botao.onClick, ouvinte);
            EditorUtility.SetDirty(botao);
        }

        private static void DefinirOuvinteBooleanoPersistente(Button botao, Object alvo, string metodoNome, bool valor, UnityAction<bool> ouvinte)
        {
            for (var indice = botao.onClick.GetPersistentEventCount() - 1; indice >= 0; indice--)
            {
                if (botao.onClick.GetPersistentTarget(indice) == alvo && botao.onClick.GetPersistentMethodName(indice) == metodoNome)
                {
                    UnityEventTools.RemovePersistentListener(botao.onClick, indice);
                }
            }

            UnityEventTools.AddBoolPersistentListener(botao.onClick, ouvinte, valor);
            EditorUtility.SetDirty(botao);
        }

        private static void EstiloBotaoRotulo(Transform botaoTransformacao)
        {
            foreach (var texto in botaoTransformacao.GetComponentsInChildren<Text>(true))
            {
                Undo.RecordObject(texto, "Style anatomy panel button label");
                texto.color = CorRotuloBotao;
            }

            foreach (var texto2 in botaoTransformacao.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(texto2, "Estilizar o rótulo TMP do botão do painel anatômico");
                texto2.color = CorRotuloBotao;
            }
        }

        private static Sprite CarregarSprite(string caminho)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
            if (sprite == null)
            {
                throw new FileNotFoundException($"O sprite de UI esperado não foi encontrado: {caminho}");
            }

            return sprite;
        }

        private static void EstiloImagem(Image imagem, Color cor, string spriteCaminho)
        {
            if (imagem == null)
            {
                return;
            }

            Undo.RecordObject(imagem, "Estilizar a superfície do painel anatômico");
            imagem.sprite = CarregarSprite(spriteCaminho);
            imagem.type = Image.Type.Sliced;
            imagem.color = cor;
        }
    }
}
