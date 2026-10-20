using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRSNC.Anatomia;
using VRSNC.Interacao;

/// <summary>Aplica apenas rótulos de exibição revisados do Oracle a nós anatômicos existentes e já vinculados.</summary>
public static class EditorRotulosCandidatosOraculo
{
    private const string CenaEsperada = "Assets/Scenes/SampleScene.unity";
    private const string NomeRaiz = "Sistema_Nervoso_Central";
    private const string CaminhoCandidato = "outputs/oraculo-cientifico-20260907/AnatomicalOracle.candidate.json";
    private const string CaminhoRecursoInstalado = "Assets/Resources/AnatomicalOracle.json";
    private const string AssinaturaEsperada = "1BFBC9FF59DB7B479BB043654F1EE6758ADB56E5F281986198C27FDCE60567C2";
    private const int QuantidadeNosEsperada = 272;
    private const int QuantidadeGruposEsperada = 32;
    private const int QuantidadeEstruturasEsperada = 240;
    private sealed class PlanoRotulos
    {
        [UnityEngine.Serialization.FormerlySerializedAs("scene")]
        public Scene cena;
        [UnityEngine.Serialization.FormerlySerializedAs("root")]
        public GameObject raiz;
        [UnityEngine.Serialization.FormerlySerializedAs("desejadoRotulos")]
        [UnityEngine.Serialization.FormerlySerializedAs("desiredLabels")]
        public Dictionary<NoInteracaoAnatomica, string> rotulosDesejados;
        [UnityEngine.Serialization.FormerlySerializedAs("candidateHash")]
        public string opcaoHash;
        [UnityEngine.Serialization.FormerlySerializedAs("geometriaimpressaoDigital")]
        [UnityEngine.Serialization.FormerlySerializedAs("geometryFingerprint")]
        public string assinaturaGeometria;
        [UnityEngine.Serialization.FormerlySerializedAs("changed")]
        public int alterado;
    }

    [MenuItem("Ferramentas/VR SNC/Anatomia/Candidato do Oráculo/Simular Rótulos")]
    public static void SimularRotulos()
    {
        var plano = CriarPlano(ExigirDocumentoCandidatos(), false);
        Debug.Log($"[Rótulos do oráculo] SIMULAÇÃO: hash {plano.opcaoHash}; {plano.rotulosDesejados.Count} nós, {plano.alterado} exibicaoNome changes; " + $"{QuantidadeGruposEsperada} grupos/{QuantidadeEstruturasEsperada} estruturas; geometria {plano.assinaturaGeometria}.");
    }

    [MenuItem("Ferramentas/VR SNC/Anatomia/Candidato do Oráculo/Aplicar Rótulos Revisados")]
    public static void AplicarRotulosRevisados()
    {
        ExigirOpcaoInstalada();
        var plano = CriarPlano(ExigirDocumentoCandidatos(), true);
        var caminhoCopiaSeguranca = CriarCopiaSegurancaCena(plano.cena.path);
        var antes = plano.assinaturaGeometria;
        PlanoRotulos aplicarAposAplicacao = null;
        Undo.IncrementCurrentGroup();
        var desfazerGrupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Aplicar rótulos de exibição revisados do oráculo");
        try
        {
            foreach (var par in plano.rotulosDesejados)
            {
                if (NomeExibicaoArmazenado(par.Key) == par.Value)
                    continue;
                Undo.RecordObject(par.Key, "Aplicar rótulo de exibição revisado do oráculo");
                var serializado = new SerializedObject(par.Key);
                var nomeExibicao = serializado.FindProperty("nomeExibicao");
                if (nomeExibicao == null)
                    throw new InvalidOperationException("BrainInteractionNode.displayName não foi encontrado.");
                nomeExibicao.stringValue = par.Value;
                if (!serializado.ApplyModifiedProperties())
                    throw new InvalidOperationException("Não foi possível salvar o rótulo de exibição revisado do oráculo.");
            }

            if (AssinaturaInvariantesCena(plano.raiz) != antes)
                throw new InvalidOperationException("A operação de rotulagem alterou invariantes da cena; as alterações foram desfeitas antes de salvar.");
            foreach (var par2 in plano.rotulosDesejados)
                if (NomeExibicaoArmazenado(par2.Key) != par2.Value)
                    throw new InvalidOperationException("O rótulo de exibição salvo não corresponde ao registro revisado do oráculo.");
            aplicarAposAplicacao = CriarPlano(ExigirDocumentoCandidatos(), false);
            if (aplicarAposAplicacao.alterado != 0)
                throw new InvalidOperationException($"A verificação de idempotência do rótulo falhou: a simulação ainda informa {aplicarAposAplicacao.alterado} changes.");
            EditorSceneManager.MarkSceneDirty(plano.cena);
            if (!EditorSceneManager.SaveScene(plano.cena))
                throw new IOException("Não foi possível salvar os rótulos revisados do oráculo.");
            Undo.CollapseUndoOperations(desfazerGrupo);
        }
        catch
        {
            Undo.RevertAllDownToGroup(desfazerGrupo);
            throw;
        }

        Debug.Log($"[Rótulos do oráculo] APLICADOS: hash {plano.opcaoHash}; {plano.rotulosDesejados.Count} rótulos; alterações iniciais {plano.alterado}; " + $"nova simulação {aplicarAposAplicacao.alterado}; backup {caminhoCopiaSeguranca}; a geometria não foi alterada.");
    }

    private static PlanoRotulos CriarPlano(DocumentoOraculoAnatomico documento, bool exigirLimpoCena)
    {
        ExigirEditorPronto();
        var cena = SceneManager.GetActiveScene();
        if (cena.path != CenaEsperada)
            throw new InvalidOperationException("Abra a SampleScene antes de aplicar os rótulos revisados do oráculo.");
        if (exigirLimpoCena && cena.isDirty)
            throw new InvalidOperationException("Revise ou salve as alterações já feitas na cena antes de aplicar os rótulos revisados do oráculo.");
        var raiz = cena.GetRootGameObjects().SingleOrDefault(objeto => objeto.name == NomeRaiz);
        if (raiz == null)
            throw new InvalidOperationException("A raiz anatômica esperada não foi encontrada.");
        var nos = raiz.GetComponentsInChildren<NoInteracaoAnatomica>(true);
        if (nos.Length != QuantidadeNosEsperada)
            throw new InvalidOperationException($"Eram esperados {QuantidadeNosEsperada} nós de interação; foram encontrados {nos.Length}.");
        var registrosPorCatalogoId = ValidarDocumento(documento);
        var cenaCatalogoIds = new HashSet<string>(StringComparer.Ordinal);
        var rotulosDesejados = new Dictionary<NoInteracaoAnatomica, string>();
        var valoresRotulosDesejados = new HashSet<string>(StringComparer.Ordinal);
        var quantidadeGrupos = 0;
        var quantidadeEstruturas = 0;
        foreach (var no in nos)
        {
            var vinculo = no.VinculoAnatomico;
            if (vinculo == null || string.IsNullOrWhiteSpace(vinculo.CatalogoId) || !cenaCatalogoIds.Add(vinculo.CatalogoId))
                throw new InvalidOperationException("Cada nó de interação deve ter um ID de catálogo de vínculo anatômico exclusivo.");
            if (!registrosPorCatalogoId.TryGetValue(vinculo.CatalogoId, out var registro))
                throw new InvalidOperationException("O vínculo da cena não consta no oráculo revisado: " + vinculo.CatalogoId);
            var ehGrupo = no.EstruturaInformacao == null;
            var esperadoGrupo = registro.tipo == "group";
            if (ehGrupo != esperadoGrupo || vinculo.EhGrupo != esperadoGrupo)
                throw new InvalidOperationException("O tipo do vínculo da cena não corresponde ao oráculo revisado: " + vinculo.CatalogoId);
            if (esperadoGrupo)
                quantidadeGrupos++;
            else
                quantidadeEstruturas++;
            var rotulo = esperadoGrupo ? registro.nomePortugues : registro.nomePortugues + " [" + registro.idArquivo + "]";
            if (!valoresRotulosDesejados.Add(rotulo))
                throw new InvalidOperationException("Os rótulos do oráculo revisado devem ser exclusivos: " + rotulo);
            rotulosDesejados.Add(no, rotulo);
        }

        if (quantidadeGrupos != QuantidadeGruposEsperada || quantidadeEstruturas != QuantidadeEstruturasEsperada || !cenaCatalogoIds.SetEquals(registrosPorCatalogoId.Keys))
            throw new InvalidOperationException("A correspondência entre cena e oráculo deve conter exatamente 32 grupos e 240 estruturas.");
        return new PlanoRotulos
        {
            cena = cena,
            raiz = raiz,
            rotulosDesejados = rotulosDesejados,
            opcaoHash = HashArquivo(CaminhoProjeto(CaminhoCandidato)),
            assinaturaGeometria = AssinaturaInvariantesCena(raiz),
            alterado = rotulosDesejados.Count(par => NomeExibicaoArmazenado(par.Key) != par.Value)
        };
    }

    private static Dictionary<string, RegistroOraculoAnatomico> ValidarDocumento(DocumentoOraculoAnatomico documento)
    {
        if (documento == null || documento.registros == null || documento.registros.Length != QuantidadeNosEsperada)
            throw new InvalidOperationException("O oráculo revisado deve conter exatamente 272 registros.");
        var registrosPorCatalogoId = new Dictionary<string, RegistroOraculoAnatomico>(StringComparer.Ordinal);
        var chaves = new HashSet<string>(StringComparer.Ordinal);
        var idsArquivo = new HashSet<string>(StringComparer.Ordinal);
        var grupos = 0;
        var estruturas = 0;
        foreach (var registro in documento.registros)
        {
            if (registro == null || string.IsNullOrWhiteSpace(registro.idCatalogo) || string.IsNullOrWhiteSpace(registro.chave) || string.IsNullOrWhiteSpace(registro.nomePortugues) || !registrosPorCatalogoId.TryAdd(registro.idCatalogo, registro) || !chaves.Add(registro.chave))
                throw new InvalidOperationException("O oráculo revisado exige IDs de catálogo, chaves e nomes em português exclusivos e não vazios.");
            if (registro.tipo == "group")
            {
                if (!string.IsNullOrWhiteSpace(registro.idArquivo))
                    throw new InvalidOperationException("Os registros de grupo não podem inventar um ID de arquivo terminal.");
                grupos++;
            }
            else if (registro.tipo == "structure")
            {
                if (string.IsNullOrWhiteSpace(registro.idArquivo) || !idsArquivo.Add(registro.idArquivo))
                    throw new InvalidOperationException("Os registros de estrutura exigem IDs de arquivo exclusivos e não vazios.");
                estruturas++;
            }
            else
            {
                throw new InvalidOperationException("O registro do oráculo revisado tem um tipo inesperado: " + registro.tipo);
            }
        }

        if (grupos != QuantidadeGruposEsperada || estruturas != QuantidadeEstruturasEsperada)
            throw new InvalidOperationException("O oráculo revisado deve conter exatamente 32 grupos e 240 estruturas.");
        return registrosPorCatalogoId;
    }

    private static DocumentoOraculoAnatomico ExigirDocumentoCandidatos()
    {
        var caminho = CaminhoProjeto(CaminhoCandidato);
        if (!File.Exists(caminho))
            throw new FileNotFoundException("O candidato revisado do oráculo não está neste computador (" + CaminhoCandidato + "). Este menu só serve à revisão editorial das fichas; o arquivo fica em outputs/, fora do repositório.", caminho);
        if (HashArquivo(caminho) != AssinaturaEsperada)
            throw new InvalidOperationException("O hash do candidato do oráculo revisado não corresponde ao hash de publicação aprovado.");
        var documento = LeitorJsonCompativel.Ler<DocumentoOraculoAnatomico>(File.ReadAllText(caminho));
        if (documento == null || documento.versao != "2026-09-07.2")
            throw new InvalidOperationException("A versão do candidato do oráculo revisado não é 2026-09-07.2.");
        return documento;
    }

    private static void ExigirOpcaoInstalada()
    {
        var caminho = CaminhoProjeto(CaminhoRecursoInstalado);
        if (!File.Exists(caminho) || HashArquivo(caminho) != AssinaturaEsperada)
            throw new InvalidOperationException("Instale em Resources o candidato aprovado do oráculo antes de aplicar os rótulos.");
    }

    private static void ExigirEditorPronto()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Para aplicar os rótulos do oráculo revisado, o Editor deve estar ocioso no Modo de Edição, sem compilação ou atualização em andamento.");
    }

    private static string NomeExibicaoArmazenado(NoInteracaoAnatomica no)
    {
        var propriedade = new SerializedObject(no).FindProperty("nomeExibicao");
        if (propriedade == null)
            throw new InvalidOperationException("BrainInteractionNode.displayName não foi encontrado.");
        return propriedade.stringValue;
    }

    private static string AssinaturaInvariantesCena(GameObject raiz)
    {
        var texto = new StringBuilder();
        foreach (var transformacao in raiz.GetComponentsInChildren<Transform>(true).OrderBy(item => CaminhoDe(item), StringComparer.Ordinal))
        {
            texto.Append(CaminhoDe(transformacao)).Append('|').Append(transformacao.localPosition).Append('|').Append(transformacao.localRotation).Append('|').Append(transformacao.localScale).Append('|').Append(transformacao.gameObject.activeSelf).Append('|');
            var no = transformacao.GetComponent<NoInteracaoAnatomica>();
            var vinculo = no != null ? no.VinculoAnatomico : null;
            texto.Append(no != null ? no.NomeNo : string.Empty).Append('|').Append(vinculo != null ? vinculo.CatalogoId : string.Empty).Append('|');
            foreach (var filtro in transformacao.GetComponents<MeshFilter>())
                texto.Append("MeshFilter:").Append(filtro.GetInstanceID()).Append(':').Append(filtro.sharedMesh != null ? filtro.sharedMesh.GetInstanceID() : 0).Append('|');
            foreach (var colisor in transformacao.GetComponents<MeshCollider>())
                texto.Append("MeshCollider:").Append(colisor.GetInstanceID()).Append(':').Append(colisor.sharedMesh != null ? colisor.sharedMesh.GetInstanceID() : 0).Append(':').Append(colisor.convex).Append('|');
            foreach (var componente in transformacao.GetComponents<Component>().OrderBy(item2 => item2 == null ? "" : item2.GetType().FullName, StringComparer.Ordinal))
                texto.Append(componente == null ? "MissingScript" : componente.GetType().FullName).Append(':').Append(componente == null ? 0 : componente.GetInstanceID()).Append('|');
            texto.AppendLine();
        }

        return HashTexto(texto.ToString());
    }

    private static string CriarCopiaSegurancaCena(string cenaCaminho)
    {
        var pasta = CaminhoProjeto("Docs/Anatomy/Backups");
        Directory.CreateDirectory(pasta);
        var backup = Path.Combine(pasta, "SampleScene-before-oracle-labels-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".unity");
        File.Copy(CaminhoProjeto(cenaCaminho), backup, false);
        return backup.Replace('\\', '/');
    }

    private static string CaminhoProjeto(string caminhoRelativo)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", caminhoRelativo));
    }

    private static string HashArquivo(string caminho)
    {
        using var assinatura = SHA256.Create();
        using var fluxo = File.OpenRead(caminho);
        return BitConverter.ToString(assinatura.ComputeHash(fluxo)).Replace("-", string.Empty);
    }

    private static string HashTexto(string valor)
    {
        using var assinatura = SHA256.Create();
        return BitConverter.ToString(assinatura.ComputeHash(Encoding.UTF8.GetBytes(valor))).Replace("-", string.Empty);
    }

    private static string CaminhoDe(Transform transformacao)
    {
        var nomes = new List<string>();
        for (var atual = transformacao; atual != null; atual = atual.parent)
            nomes.Add(atual.name);
        nomes.Reverse();
        return string.Join("/", nomes);
    }
}
