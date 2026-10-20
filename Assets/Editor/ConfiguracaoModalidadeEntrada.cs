using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using VRSNC.Interacao;

/// <summary>Instalador e leitor exclusivos do Editor, sem salvamento, para diagnósticos independentes de modalidade.</summary>
public static class ConfiguracaoModalidadeEntrada
{
    [MenuItem("Ferramentas/VR SNC/Entrada/Instalar diagnósticos independentes de modalidade")]
    public static void InstalarDiagnosticosIndependentes()
    {
        var modalidade = EncontrarGerenciadorModalidadeCena();
        ValidarEstruturaRig(modalidade);
        Undo.IncrementCurrentGroup();
        var desfazerGrupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Instalar diagnósticos independentes de modalidade");
        try
        {
            var diagnosticos = modalidade.GetComponent<DiagnosticosModalidadeEntrada>();
            if (diagnosticos == null)
                diagnosticos = Undo.AddComponent<DiagnosticosModalidadeEntrada>(modalidade.gameObject);
            EditorUtility.SetDirty(diagnosticos);
            ValidarCena();
            Undo.CollapseUndoOperations(desfazerGrupo);
            Debug.Log("[Modalidade de entrada] Relator independente instalado. A cena não foi salva; XRI continua sendo o único responsável pela modalidade. " + DescreverMetaConfiguracao());
        }
        catch
        {
            Undo.RevertAllDownToGroup(desfazerGrupo);
            throw;
        }
    }

    [MenuItem("Ferramentas/VR SNC/Entrada/Validar modalidade automática de mão e controle")]
    public static void ValidarPeloMenu()
    {
        ValidarCena();
        Debug.Log("[Modalidade de entrada] APROVADO: um gerenciador XRI nativo, quatro grupos distintos e nenhum relator independente duplicado. " + DescreverMetaConfiguracao());
    }

    [MenuItem("Ferramentas/VR SNC/Entrada/Relatar modalidade automática de mão e controle")]
    public static void ExibirRelatorioPeloMenu()
    {
        var modalidade = EncontrarGerenciadorModalidadeCena(exigirEditarModo: false);
        if (Application.isPlaying)
            modalidade.GetComponent<DiagnosticosModalidadeEntrada>()?.EmitirRelatorioAgora();
        Debug.Log("[Modalidade de entrada] " + DescreverMetaConfiguracao() + " " + DescreverProprioXRIEstado(modalidade));
    }

    private static void ValidarCena()
    {
        var modalidade = EncontrarGerenciadorModalidadeCena();
        ValidarEstruturaRig(modalidade);
        if (modalidade.GetComponents<DiagnosticosModalidadeEntrada>().Length > 1)
            throw new InvalidOperationException("O XRInputModalityManager deve ter, no máximo, um relator independente de modalidade.");
    }

    private static XRInputModalityManager EncontrarGerenciadorModalidadeCena(bool exigirEditarModo = true)
    {
        var cena = SceneManager.GetActiveScene();
        if (!cena.IsValid() || !cena.isLoaded || EditorApplication.isCompiling || EditorApplication.isUpdating || (exigirEditarModo && EditorApplication.isPlayingOrWillChangePlaymode))
            throw new InvalidOperationException(exigirEditarModo ? "Use uma cena carregada e mantenha o Editor ocioso no Modo de Edição." : "Use uma cena carregada e estável.");
        var gerenciadores = UnityEngine.Object.FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.gameObject.scene == cena).ToArray();
        if (gerenciadores.Length != 1)
            throw new InvalidOperationException($"Esperado um XRInputModalityManager na cena ativa; encontrados {gerenciadores.Length}.");
        return gerenciadores[0];
    }

    private static void ValidarEstruturaRig(XRInputModalityManager modalidade)
    {
        if (modalidade.leftHand == null || modalidade.rightHand == null || modalidade.leftController == null || modalidade.rightController == null)
            throw new InvalidOperationException("XRInputModalityManager deve referenciar os grupos de mão e controle esquerdo/direito.");
        var grupos = new[] { modalidade.leftHand, modalidade.rightHand, modalidade.leftController, modalidade.rightController };
        if (grupos.Distinct().Count() != grupos.Length)
            throw new InvalidOperationException("Os quatro grupos de modalidade devem ser GameObjects distintos.");
    }

    private static string DescreverMetaConfiguracao()
    {
        return "A alternância de mão e controle em cada lado é gerenciada pelo XRInputModalityManager e pelos perfis de entrada ativos do OpenXR. A disponibilidade dos dispositivos em execução só é verificada após a inicialização do XR.";
    }

    private static string DescreverProprioXRIEstado(XRInputModalityManager modalidade)
    {
        var grupos = $"XRI habilitado={modalidade.enabled}; modes(L={LerProprioModo(modalidade, "leftInputMode")}, " + $"R={LerProprioModo(modalidade, "rightInputMode")}); left(H={DescreverGrupo(modalidade.leftHand)}, C={DescreverGrupo(modalidade.leftController)}), " + $"right(H={DescreverGrupo(modalidade.rightHand)}, C={DescreverGrupo(modalidade.rightController)}).";
        if (!Application.isPlaying)
            return grupos + " Runtime tracked-device state requires Play Mode.";
        var dispositivos = InputSystem.devices.Where(dispositivo => dispositivo.usages.Contains(CommonUsages.LeftHand) || dispositivo.usages.Contains(CommonUsages.RightHand)).Select(DescreverRastreadoDispositivo).ToArray();
        return grupos + (dispositivos.Length == 0 ? " No left/right Input System device is currently published." : " Devices: " + string.Join("; ", dispositivos));
    }

    private static string DescreverGrupo(GameObject grupo)
    {
        return grupo.activeInHierarchy ? "active" : grupo.activeSelf ? "parent-disabled" : "disabled";
    }

    // O XRI expõe intencionalmente estes valores por lado como API interna de análise. Esta
    // observação exclusiva do Editor os lê sem assinar, substituir ou alterar
    // o gerenciador; ela é protegida contra futuras mudanças do pacote XRI.
    private static string LerProprioModo(XRInputModalityManager modalidade, string propriedadeNome)
    {
        var propriedade = typeof(XRInputModalityManager).GetProperty(propriedadeNome, BindingFlags.Instance | BindingFlags.NonPublic);
        return propriedade?.GetValue(modalidade)?.ToString() ?? "unavailable";
    }

    private static string DescreverRastreadoDispositivo(InputDevice dispositivo)
    {
        var lado = dispositivo.usages.Contains(CommonUsages.LeftHand) ? "L" : "R";
        var rastreado = dispositivo.TryGetChildControl<ButtonControl>("isTracked");
        var rastreamentoEstado = dispositivo.TryGetChildControl<IntegerControl>("trackingState");
        var rastreadoTexto = rastreado == null ? "isTracked=n/a" : $"isTracked={rastreado.isPressed}";
        var estadoTexto = rastreamentoEstado == null ? "trackingState=n/a" : $"trackingState={rastreamentoEstado.ReadValue()}";
        return $"{lado}:{dispositivo.layout}/{dispositivo.displayName} {rastreadoTexto},{estadoTexto}";
    }
}