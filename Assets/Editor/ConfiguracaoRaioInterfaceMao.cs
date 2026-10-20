using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRSNC.Interacao;

/// <summary>Instalador exclusivo do Editor, sem salvamento, para os raios nativos de UI apenas das mãos.</summary>
public static class ConfiguracaoRaioInterfaceMao
{
    private const int InterfaceCamada = 5;
    private const int InterfaceCamadaMascara = 1 << InterfaceCamada;
    [MenuItem("Ferramentas/VR SNC/Mãos/Aplicar raio de UI apenas das mãos")]
    public static void AplicarSomenteInterfaceDasMaos()
    {
        var cena = SceneManager.GetActiveScene();
        if (!cena.IsValid() || !cena.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Use uma cena carregada e mantenha o Editor ocioso no Modo de Edição.");
        var modalidade = UnityEngine.Object.FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault(x => x.gameObject.scene == cena);
        if (modalidade == null || modalidade.leftHand == null || modalidade.rightHand == null)
            throw new InvalidOperationException("As raízes das mãos não estão configuradas no XRInputModalityManager.");
        Undo.IncrementCurrentGroup();
        var desfazerGrupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Aplicar raios nativos de UI apenas das mãos");
        try
        {
            AplicarParaMao(modalidade.leftHand, "Left");
            AplicarParaMao(modalidade.rightHand, "Right");
            Validar();
            Undo.CollapseUndoOperations(desfazerGrupo);
            Debug.Log("[Raio UI Mãos] Instalados e validados dois raios nativos de UI apenas das mãos. A cena não foi salva.");
        }
        catch
        {
            Undo.RevertAllDownToGroup(desfazerGrupo);
            throw;
        }
    }

    [MenuItem("Ferramentas/VR SNC/Mãos/Validar raio de UI apenas das mãos")]
    public static void ValidarPeloMenu()
    {
        Validar();
        Debug.Log("[Raio UI Mãos] APROVADO: rotas de raio de UI esquerdo/direito, leitores, origens, camada de UI e nenhuma rota duplicada.");
    }

    private static void AplicarParaMao(GameObject maoRaiz, string lado)
    {
        var proximoDistantes = maoRaiz.GetComponentsInChildren<NearFarInteractor>(true);
        if (proximoDistantes.Length != 1)
            throw new InvalidOperationException($"Esperado exatamente um NearFarInteractor da mão {lado}; encontrados {proximoDistantes.Length}.");
        var proximoDistante = proximoDistantes[0];
        var rotas = maoRaiz.GetComponentsInChildren<PonteRaioInterfaceMao>(true).Where(x => x.InteratorMaoProximoDistante == proximoDistante).ToArray();
        if (rotas.Length > 1)
            throw new InvalidOperationException($"A mão {lado} já tem rotas de raio de UI duplicadas.");
        PonteRaioInterfaceMao rota;
        XRRayInteractor raio;
        XRInteractorLineVisual linhaVisual;
        if (rotas.Length == 1)
        {
            rota = rotas[0];
            raio = rota.InterfaceRaio != null ? rota.InterfaceRaio : rota.GetComponent<XRRayInteractor>();
            if (raio == null)
                throw new InvalidOperationException($"A rota de raio de UI {lado} não possui XRRayInteractor.");
            Undo.RecordObject(rota.gameObject, "Configurar o raio da interface da mão");
            Undo.RecordObject(rota, "Configurar o raio da interface da mão");
            Undo.RecordObject(raio, "Configurar o raio da interface da mão");
        }
        else
        {
            var raioObjeto = new GameObject($"{lado} Raio da interface da mão");
            Undo.RegisterCreatedObjectUndo(raioObjeto, "Criar raio da interface da mão");
            raioObjeto.transform.SetParent(proximoDistante.transform, false);
            raio = Undo.AddComponent<XRRayInteractor>(raioObjeto);
            rota = Undo.AddComponent<PonteRaioInterfaceMao>(raioObjeto);
        }

        // Adiciona e verifica o renderizador necessário antes do visual. Não use
        // coalescência nula aqui: a semântica de nulo para objeto destruído do Unity pode manter
        // uma referência de componente obsoleta após a primeira tentativa de menu parcialmente desfeita.
        var linha = raio.GetComponent<LineRenderer>();
        if (linha == null)
            linha = Undo.AddComponent<LineRenderer>(raio.gameObject);
        if (linha == null)
            throw new InvalidOperationException($"O raio de UI {lado} não pôde criar o LineRenderer necessário.");
        linhaVisual = raio.GetComponent<XRInteractorLineVisual>();
        if (linhaVisual == null)
            linhaVisual = Undo.AddComponent<XRInteractorLineVisual>(raio.gameObject);
        if (linhaVisual == null)
            throw new InvalidOperationException($"O raio de UI {lado} não pôde criar seu visual de linha nativo.");
        Undo.RecordObject(linha, "Configure hand UI ray line");
        Undo.RecordObject(linhaVisual, "Configure hand UI ray line");
        Undo.RecordObject(proximoDistante, "Direcionar a interface da mão pelo raio nativo");
        var origem = proximoDistante.farInteractionCaster != null ? proximoDistante.farInteractionCaster.effectiveCastOrigin : proximoDistante.transform;
        raio.rayOriginTransform = origem;
        raio.attachTransform = origem;
        raio.interactionManager = proximoDistante.interactionManager;
        raio.lineType = XRRayInteractor.LineType.StraightLine;
        raio.hitDetectionType = XRRayInteractor.HitDetectionType.Raycast;
        raio.raycastMask = InterfaceCamadaMascara;
        raio.interactionLayers = 0;
        raio.selectInput = new XRInputButtonReader("Disabled UI Ray Select", inputSourceMode: XRInputButtonReader.InputSourceMode.Unused);
        raio.uiPressInput = proximoDistante.uiPressInput;
        raio.uiScrollInput = proximoDistante.uiScrollInput;
        raio.enableUIInteraction = true;
        raio.enabled = false;
        proximoDistante.enableUIInteraction = false;
        linha.useWorldSpace = true;
        linha.enabled = false;
        linhaVisual.lineWidth = 0.0015f;
        linhaVisual.enabled = false;
        rota.Configurar(proximoDistante, raio, linhaVisual, maoRaiz.transform);
        EditorUtility.SetDirty(proximoDistante);
        EditorUtility.SetDirty(raio);
        EditorUtility.SetDirty(rota);
        EditorUtility.SetDirty(linha);
        EditorUtility.SetDirty(linhaVisual);
    }

    private static void Validar()
    {
        var cena = SceneManager.GetActiveScene();
        var modalidade = UnityEngine.Object.FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.gameObject.scene == cena);
        ValidarMao(modalidade.leftHand, "Left");
        ValidarMao(modalidade.rightHand, "Right");
    }

    private static void ValidarMao(GameObject maoRaiz, string lado)
    {
        var proximoDistante = maoRaiz.GetComponentsInChildren<NearFarInteractor>(true).Single();
        var rotas = maoRaiz.GetComponentsInChildren<PonteRaioInterfaceMao>(true).Where(x => x.InteratorMaoProximoDistante == proximoDistante).ToArray();
        if (rotas.Length != 1)
            throw new InvalidOperationException($"{lado}: esperada exatamente uma rota de raio de UI.");
        var raio = rotas[0].InterfaceRaio;
        var linhaVisual = raio != null ? raio.GetComponent<XRInteractorLineVisual>() : null;
        var esperadoOrigem = proximoDistante.farInteractionCaster != null ? proximoDistante.farInteractionCaster.effectiveCastOrigin : proximoDistante.transform;
        if (raio == null || raio.raycastMask.value != InterfaceCamadaMascara || raio.interactionLayers.value != 0 || raio.rayOriginTransform != esperadoOrigem || raio.attachTransform != esperadoOrigem || !raio.enableUIInteraction || raio.uiPressInput.inputSourceMode != proximoDistante.uiPressInput.inputSourceMode || raio.uiPressInput.GetObjectReference() != proximoDistante.uiPressInput.GetObjectReference() || proximoDistante.enableUIInteraction || raio.selectInput.inputSourceMode != XRInputButtonReader.InputSourceMode.Unused || proximoDistante.uiPressInput.inputSourceMode != XRInputButtonReader.InputSourceMode.ObjectReference || linhaVisual == null || raio.GetComponent<LineRenderer>() == null || !Mathf.Approximately(linhaVisual.lineWidth, 0.0015f))
            throw new InvalidOperationException($"{lado}: a configuração de leitor/origem/camada do raio apenas de UI é inválida.");
        foreach (var grupo in UnityEngine.Object.FindObjectsByType<XRInteractionGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (grupo.gameObject.scene == maoRaiz.scene && grupo.startingGroupMembers.Any(membro => membro == raio))
                throw new InvalidOperationException($"{lado}: o raio apenas de UI não pode ser membro de XRInteractionGroup.");
        }
    }
}