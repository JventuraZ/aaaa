using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using VRSNC.Interacao;
using VRSNC.Interface;

namespace VRSNC.FerramentasEditor
{
    /// <summary>
    /// Instala os gestos das mãos no ar (<see cref="GestosMaosNoAr"/>) num objeto próprio, <c>Gestos_Maos_VRSNC</c>, na
    /// raiz da cena e fora do <c>Sistema_Nervoso_Central</c>: a hierarquia anatômica conferida pelo verificador externo
    /// não muda. Exige a cena salva e a salva no fim, como o instalador do EEG.
    /// </summary>
    public static class InstaladorGestosMaos
    {
        public const string NomeObjeto = "Gestos_Maos_VRSNC";
        private const string CaminhoCena = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Ferramentas/VR SNC/Mãos/Instalar gestos no ar", false, 330)]
        private static void InstalarPeloMenu()
        {
            var cena = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || cena.path != CaminhoCena || cena.isDirty)
                throw new InvalidOperationException("Use um Editor ocioso com a SampleScene salva no Modo de Edição.");
            Undo.IncrementCurrentGroup();
            var grupo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Instalar gestos das mãos no ar");
            try
            {
                var gestos = Instalar();
                EditorSceneManager.MarkSceneDirty(cena);
                if (!EditorSceneManager.SaveScene(cena))
                    throw new IOException("Não foi possível salvar a cena com os gestos das mãos.");
                Undo.CollapseUndoOperations(grupo);
                Debug.Log("[VR SNC] Gestos das mãos no ar instalados em " + NomeObjeto + ": uma mão gira, duas mãos movem, giram e escalam o SNC, pinça curta da mão esquerda volta.", gestos);
            }
            catch
            {
                Undo.RevertAllDownToGroup(grupo);
                throw;
            }
        }

        /// <summary>Cria (ou reaproveita) o objeto e liga as referências da cena. Idempotente; não salva.</summary>
        public static GestosMaosNoAr Instalar()
        {
            var snc = GameObject.Find("Sistema_Nervoso_Central");
            if (snc == null)
                throw new InvalidDataException("Sistema_Nervoso_Central não foi encontrado.");
            var objeto = GameObject.Find(NomeObjeto);
            if (objeto == null)
            {
                objeto = new GameObject(NomeObjeto);
                Undo.RegisterCreatedObjectUndo(objeto, "Criar " + NomeObjeto);
            }

            var gestos = objeto.GetComponent<GestosMaosNoAr>();
            if (gestos == null)
                gestos = Undo.AddComponent<GestosMaosNoAr>(objeto);
            var so = new SerializedObject(gestos);
            so.FindProperty("gerenciadorSelecao").objectReferenceValue = snc.GetComponent<GerenciadorSelecaoHierarquica>();
            so.FindProperty("manipulador").objectReferenceValue = snc.GetComponent<ManipuladorObjetoSelecionado>();
            so.FindProperty("ponte").objectReferenceValue = snc.GetComponent<PonteInteracaoMao>();
            so.FindProperty("coordenadorGesto").objectReferenceValue = snc.GetComponent<CoordenadorGestoAnatomico>();
            so.FindProperty("gerenciadorRestauracao").objectReferenceValue = snc.GetComponent<GerenciadorRestauracao>();
            so.FindProperty("raizModelo").objectReferenceValue = snc.transform;
            so.FindProperty("modalidade").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<XRInputModalityManager>(FindObjectsInactive.Include);
            so.FindProperty("rotulo").objectReferenceValue = snc.GetComponent<RotuloEstruturaApontada>();
            so.ApplyModifiedProperties();
            return gestos;
        }
    }
}
