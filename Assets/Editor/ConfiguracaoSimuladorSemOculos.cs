using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

/// <summary>
/// Liga e desliga o XR Interaction Simulator do XRI para testar o protótipo no Editor sem óculos.
/// O simulador só é criado no Modo de Jogo do Editor; a cena e os builds do Quest não mudam.
/// </summary>
[InitializeOnLoad]
public static class ConfiguracaoSimuladorSemOculos
{
    private const string CaminhoMenu = "Ferramentas/VR SNC/Entrada/Testar sem óculos (simulador XR)";
    private const string CaminhoConfiguracao = "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset";
    private const string NomePrefabSimulador = "XR Interaction Simulator";

    static ConfiguracaoSimuladorSemOculos()
    {
        EditorApplication.playModeStateChanged += AvisarSeHaOculosConectados;
    }

    [MenuItem(CaminhoMenu)]
    public static void Alternar()
    {
        var configuracao = CarregarConfiguracao();
        var ligar = !configuracao.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue;
        if (ligar)
        {
            var prefab = EncontrarPrefabSimulador();
            if (prefab == null)
                throw new InvalidOperationException("Importe a amostra \"XR Interaction Simulator\" do XR Interaction Toolkit (Window > Package Manager > XR Interaction Toolkit > Samples) antes de ligar o simulador.");
            configuracao.FindProperty("m_SimulatorPrefab").objectReferenceValue = prefab;
            configuracao.FindProperty("m_UseClassic").boolValue = false;
        }

        configuracao.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue = ligar;
        // Mantém o simulador fora dos aplicativos gerados para o Quest.
        configuracao.FindProperty("m_AutomaticallyInstantiateInEditorOnly").boolValue = true;
        configuracao.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(configuracao.targetObject);
        Debug.Log(ligar
            ? "[Simulador XR] Ligado: ao entrar no Modo de Jogo, teclado e mouse simulam a cabeça e os controles. Desligue antes de testar com o Quest pelo Link."
            : "[Simulador XR] Desligado: o Modo de Jogo volta a usar apenas os óculos conectados.");
    }

    [MenuItem(CaminhoMenu, true)]
    private static bool ValidarAlternar()
    {
        Menu.SetChecked(CaminhoMenu, EstaLigado());
        return !EditorApplication.isPlayingOrWillChangePlaymode && AssetDatabase.LoadAssetAtPath<ScriptableObject>(CaminhoConfiguracao) != null;
    }

    public static bool EstaLigado()
    {
        var configuracao = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CaminhoConfiguracao);
        return configuracao != null && new SerializedObject(configuracao).FindProperty("m_AutomaticallyInstantiateSimulatorPrefab").boolValue;
    }

    // O simulador remove o headset real da sessão para não haver dois; com o Link ativo, a cabeça deixaria de seguir os óculos.
    private static void AvisarSeHaOculosConectados(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.EnteredPlayMode && XRSettings.isDeviceActive && EstaLigado())
            Debug.LogWarning("[Simulador XR] Há óculos conectados, mas o simulador está ligado e substitui a cabeça e os controles reais. Para testar com o Quest pelo Link, desligue em " + CaminhoMenu + ".");
    }

    private static SerializedObject CarregarConfiguracao()
    {
        var configuracao = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CaminhoConfiguracao);
        if (configuracao == null)
            throw new InvalidOperationException("Configuração do simulador do XRI não encontrada em " + CaminhoConfiguracao + ". Abra Edit > Project Settings > XR Plug-in Management > XR Interaction Toolkit uma vez para criá-la.");
        return new SerializedObject(configuracao);
    }

    private static GameObject EncontrarPrefabSimulador()
    {
        foreach (var guid in AssetDatabase.FindAssets("\"" + NomePrefabSimulador + "\" t:Prefab"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.TryGetComponent<XRInteractionSimulator>(out _))
                return prefab;
        }

        return null;
    }
}
