using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRSNC.EEG;
using VRSNC.Interacao;

/// <summary>
/// Traz a gravação do Protobrain para o VR. O pipeline Python continua no projeto dele e gera
/// <c>data/build/</c> (python python.py build); aqui só se importa o resultado:
/// <list type="bullet">
/// <item><b>Importar dados</b>: copia os binários para Assets/EEG/Dados como .bytes, converte o escalpo do
/// fsaverage para o referencial do Unity e grava o DadosEEG.asset com o manifest tipado.</item>
/// <item><b>Instalar na cena</b>: alinha o fsaverage ao cérebro do modelo (eixos anatômicos e tamanho do
/// córtex), liga cada eletrodo ao giro equivalente, cria o Controlador_EEG e a aba EEG. Salva a cena.</item>
/// </list>
/// Conversão de eixos: o Protobrain usa o padrão do three.js (x = direita, y = superior, z = posterior,
/// 1 unidade = 100 mm); no Unity fica (x, y, −z) × 0,1 m, com z = anterior.
/// </summary>
public static class InstaladorEEG
{
    public const string ChavePasta = "VRSNC.EEG.PastaProtobrain";
    public const string CaminhoDados = "Assets/EEG/DadosEEG.asset";
    private const string PastaDados = "Assets/EEG/Dados";
    private const string PastaMateriais = "Assets/EEG/Materiais";
    private const string CaminhoEscalpo = PastaDados + "/Escalpo_EEG.asset";
    private const string CaminhoCena = "Assets/Scenes/SampleScene.unity";
    private const string NomeControlador = "Controlador_EEG";
    private static readonly string[] OrdemModos = { "absolute", "relative", "zscore" };
    private static readonly Dictionary<string, string> RotulosModos = new() { ["absolute"] = "Absoluta", ["relative"] = "Relativa", ["zscore"] = "Variação (z)" };

    public static string PastaPadrao => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "protobrain-main", "protobrain-main", "data", "build");

    public static string PastaProtobrain
    {
        get => EditorPrefs.GetString(ChavePasta, PastaPadrao);
        set => EditorPrefs.SetString(ChavePasta, value);
    }

    [MenuItem("Ferramentas/VR SNC/EEG/Escolher pasta do Protobrain (data/build)...", false, 300)]
    private static void EscolherPasta()
    {
        var escolhida = EditorUtility.OpenFolderPanel("Pasta data/build do Protobrain", PastaProtobrain, "");
        if (!string.IsNullOrEmpty(escolhida))
        {
            PastaProtobrain = escolhida;
            Debug.Log("[EEG] Pasta do Protobrain: " + escolhida);
        }
    }

    [MenuItem("Ferramentas/VR SNC/EEG/Importar dados do Protobrain", false, 301)]
    private static void ImportarPeloMenu() => Debug.Log("[EEG] " + Importar(PastaProtobrain).resumo);

    [MenuItem("Ferramentas/VR SNC/EEG/Instalar EEG na cena", false, 302)]
    private static void InstalarPeloMenu() => Debug.Log("[EEG] " + InstalarNaCena());

    // ================================================================ importação

    public static (DadosEEG dados, string resumo) Importar(string pasta)
    {
        var manifestoCaminho = Path.Combine(pasta, "manifest.json");
        if (!File.Exists(manifestoCaminho))
            throw new FileNotFoundException("manifest.json não encontrado. Rode 'python python.py build' no Protobrain e escolha a pasta data/build em Ferramentas > VR SNC > EEG.", manifestoCaminho);
        var manifesto = JObject.Parse(File.ReadAllText(manifestoCaminho, Encoding.UTF8));
        Directory.CreateDirectory(PastaDados);
        Directory.CreateDirectory(PastaMateriais);

        var canaisJson = (JArray)manifesto["channels"];
        var quantidadeCanais = canaisJson.Count;
        var sinaisForma = manifesto["signals"]["shape"].ToObject<int[]>();
        Exigir(sinaisForma[0] == quantidadeCanais, "signals.shape não bate com o número de canais");
        var quadros = (int)manifesto["frames"]["count"];
        var bandas = manifesto["bands"].ToObject<string[]>();
        var vertices = manifesto["scalp"]["vertices"]["shape"].ToObject<int[]>()[0];
        var grade = (int)manifesto["topo"]["grid"];

        // Binários copiados como TextAsset (.bytes), conferindo o tamanho contra a forma do manifest.
        var cubos = new List<TextAsset>();
        var escalas = new List<DadosEEG.Escala>();
        foreach (var modo in OrdemModos)
        {
            var descritor = manifesto["binaries"]?[modo];
            if (descritor == null)
                continue;
            cubos.Add(CopiarBinario(pasta, (string)descritor["file"], "bands_" + modo + ".bytes", quadros * bandas.Length * quantidadeCanais * 4L));
            escalas.Add(LerEscala(manifesto["scales"][modo], modo, bandas));
        }

        Exigir(cubos.Count > 0, "nenhum cubo de potência no manifest");
        var sinais = CopiarBinario(pasta, (string)manifesto["signals"]["file"], "sinais.bytes", (long)sinaisForma[0] * sinaisForma[1] * 4L);
        var pesosEscalpo = CopiarBinario(pasta, (string)manifesto["scalp"]["weights"]["file"], "pesos_escalpo.bytes", (long)vertices * quantidadeCanais * 4L);
        var cobertura = CopiarBinario(pasta, (string)manifesto["scalp"]["coverage"]["file"], "cobertura_escalpo.bytes", vertices * 4L);
        var pesosTopo = CopiarBinario(pasta, (string)manifesto["topo"]["weights"]["file"], "pesos_topo.bytes", (long)grade * grade * quantidadeCanais * 4L);

        var escalpo = CriarEscalpo(pasta, manifesto["scalp"]);
        var (cortexMinimo, cortexMaximo) = LerLimitesCortex(pasta, manifesto["cortex"]);

        var dados = AssetDatabase.LoadAssetAtPath<DadosEEG>(CaminhoDados);
        var novo = dados == null;
        if (novo)
            dados = ScriptableObject.CreateInstance<DadosEEG>();
        var fonte = manifesto["source"];
        var pre = manifesto["preprocessing"];
        dados.arquivoOrigem = (string)fonte["file"];
        dados.gravadoEm = (string)fonte["recorded"];
        dados.frequenciaAmostragem = (float)fonte["sfreq"];
        dados.duracaoSegundos = (float)fonte["duration"];
        dados.referencia = (string)fonte["reference"];
        var filtro = fonte["filter"].ToObject<float[]>();
        dados.filtroAnalise = new Vector2(filtro[0], filtro[1]);
        dados.montagemEletrodos = (string)fonte["montage"];
        dados.frequenciaRede = pre?["line_frequency"] != null ? (float)pre["line_frequency"] : 60f;
        dados.canaisInterpolados = pre?["bad_channels"] is JArray ruins ? ruins.Select(item => (string)item["name"]).ToArray() : Array.Empty<string>();
        dados.resumoIca = ResumirIca(pre?["ica"]);
        dados.pastaImportada = pasta.Replace('\\', '/');
        dados.importadoEmUtc = DateTime.UtcNow.ToString("O");
        dados.canais = canaisJson.Select(LerCanal).ToArray();
        dados.regioes = ((JArray)manifesto["regions"]).Select(regiao => new DadosEEG.Regiao
        {
            chave = (string)regiao["key"],
            nome = (string)regiao["name"],
            lobo = (string)regiao["lobe"],
            funcao = (string)regiao["function"],
        }).ToArray();
        dados.areasBrodmann = ((JObject)manifesto["brodmann"]).Properties()
            .Select(propriedade => new DadosEEG.AreaBrodmann { numero = int.Parse(propriedade.Name), funcao = (string)propriedade.Value })
            .OrderBy(area => area.numero).ToArray();
        dados.bandas = bandas;
        var rotulos = manifesto["band_labels"] as JObject;
        dados.rotulosBandas = bandas.Select(banda => rotulos?[banda] != null ? (string)rotulos[banda] : banda).ToArray();
        dados.faixasBandas = bandas.Select(banda => { var faixa = manifesto["band_ranges"][banda].ToObject<float[]>(); return new Vector2(faixa[0], faixa[1]); }).ToArray();
        dados.quadros = quadros;
        dados.passoQuadro = (float)manifesto["frames"]["hop"];
        dados.janelaQuadro = (float)manifesto["frames"]["window"];
        dados.inicioQuadros = (float)manifesto["frames"]["t0"];
        dados.escalas = escalas.ToArray();
        dados.cubos = cubos.ToArray();
        dados.sinais = sinais;
        dados.amostras = sinaisForma[1];
        dados.eventos = manifesto["events"] is JArray eventos
            ? eventos.Select(evento => new DadosEEG.Evento { tempo = (float)evento["time"], codigo = (int)evento["code"] }).ToArray()
            : Array.Empty<DadosEEG.Evento>();
        dados.escalpo = escalpo;
        dados.pesosEscalpo = pesosEscalpo;
        dados.coberturaEscalpo = cobertura;
        dados.pesosTopo = pesosTopo;
        dados.gradeTopo = grade;
        dados.extensaoTopo = (float)manifesto["topo"]["extent"];
        dados.raioCabecaTopo = (float)manifesto["topo"]["head_radius"];
        dados.intradTopo = (float)manifesto["topo"]["intrad"];
        dados.cortexMinimo = cortexMinimo;
        dados.cortexMaximo = cortexMaximo;
        if (novo)
            AssetDatabase.CreateAsset(dados, CaminhoDados);
        EditorUtility.SetDirty(dados);
        CriarMateriais();
        AssetDatabase.SaveAssets();

        var resumo = $"Importado de {pasta}: {dados.canais.Length} canais, {dados.amostras} amostras a {dados.frequenciaAmostragem} Hz ({dados.duracaoSegundos:0.0} s), " +
                     $"{dados.quadros} quadros × {dados.bandas.Length} bandas × {dados.escalas.Length} normalizações, escalpo com {escalpo.vertexCount} vértices, rede {dados.frequenciaRede} Hz.";
        return (dados, resumo);
    }

    private static DadosEEG.Canal LerCanal(JToken canal)
    {
        var cortex = canal["cortex"];
        return new DadosEEG.Canal
        {
            nome = (string)canal["name"],
            posicao = ParaUnity(canal["pos"].ToObject<float[]>(), true),
            normal = ParaUnity(canal["normal"].ToObject<float[]>(), false),
            interpolado = canal["interpolated"] != null && (bool)canal["interpolated"],
            topo = canal["topo"] is JArray topo ? new Vector2((float)topo[0], (float)topo[1]) : Vector2.zero,
            desvioUv = canal["std_uv"] != null ? (float)canal["std_uv"] : 0f,
            regiao = cortex?["region"] != null ? (int)cortex["region"] : 0,
            brodmann = cortex?["brodmann"] != null ? (int)cortex["brodmann"] : 0,
            hemisferioEsquerdo = (string)cortex?["hemisphere"] != "rh",
            linhaMedia = cortex?["midline"] != null && (bool)cortex["midline"],
            distanciaCortexMm = cortex?["distance_mm"] != null ? (float)cortex["distance_mm"] : 0f,
        };
    }

    private static DadosEEG.Escala LerEscala(JToken json, string modo, string[] bandas)
    {
        var escala = new DadosEEG.Escala
        {
            modo = modo,
            rotulo = RotulosModos.TryGetValue(modo, out var rotulo) ? rotulo : modo,
            divergente = json["diverging"] != null && (bool)json["diverging"],
            unidade = (string)json["unit"] ?? "",
            vmin = json["vmin"] != null ? (float)json["vmin"] : 0f,
            vmax = json["vmax"] != null ? (float)json["vmax"] : 1f,
        };
        if (json["per_band"] is JObject porBanda)
        {
            escala.vminPorBanda = bandas.Select(banda => porBanda[banda] != null ? (float)porBanda[banda]["vmin"] : escala.vmin).ToArray();
            escala.vmaxPorBanda = bandas.Select(banda => porBanda[banda] != null ? (float)porBanda[banda]["vmax"] : escala.vmax).ToArray();
            escala.vmin = escala.vminPorBanda.Min();
            escala.vmax = escala.vmaxPorBanda.Max();
        }

        return escala;
    }

    private static string ResumirIca(JToken ica)
    {
        if (ica == null || ica.Type == JTokenType.Null)
            return "ICA desligada";
        var removidos = ica["removed"] as JArray;
        var total = ica["n_components"] != null ? (int)ica["n_components"] : 0;
        return $"ICA: {removidos?.Count ?? 0} de {total} componentes removidos (ICLabel)";
    }

    /// <summary>three.js (x direita, y superior, z posterior; 100 mm) → Unity (x direita, y superior, z anterior; metros).</summary>
    public static Vector3 ParaUnity(float[] xyz, bool ponto)
    {
        var escala = ponto ? 0.1f : 1f;
        return new Vector3(xyz[0] * escala, xyz[1] * escala, -xyz[2] * escala);
    }

    private static TextAsset CopiarBinario(string pasta, string arquivo, string destinoNome, long bytesEsperados)
    {
        var origem = Path.Combine(pasta, arquivo);
        Exigir(File.Exists(origem), "binário ausente: " + arquivo);
        var tamanho = new FileInfo(origem).Length;
        Exigir(tamanho == bytesEsperados, $"{arquivo} tem {tamanho} bytes; o manifest pede {bytesEsperados}");
        var destino = PastaDados + "/" + destinoNome;
        File.Copy(origem, destino, true);
        AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceUpdate);
        return Exigir(AssetDatabase.LoadAssetAtPath<TextAsset>(destino), "importação de " + destinoNome);
    }

    private static float[] LerFloats(string caminho)
    {
        var bytes = File.ReadAllBytes(caminho);
        var valores = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, valores, 0, valores.Length * 4);
        return valores;
    }

    private static int[] LerIndices(string caminho)
    {
        var bytes = File.ReadAllBytes(caminho);
        var valores = new int[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, valores, 0, valores.Length * 4);
        return valores;
    }

    private static Mesh CriarEscalpo(string pasta, JToken descritor)
    {
        var posicoes = LerFloats(Path.Combine(pasta, (string)descritor["vertices"]["file"]));
        var indices = LerIndices(Path.Combine(pasta, (string)descritor["indices"]["file"]));
        var vertices = new Vector3[posicoes.Length / 3];
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = new Vector3(posicoes[i * 3] * 0.1f, posicoes[i * 3 + 1] * 0.1f, -posicoes[i * 3 + 2] * 0.1f);
        var malha = AssetDatabase.LoadAssetAtPath<Mesh>(CaminhoEscalpo);
        var nova = malha == null;
        if (nova)
            malha = new Mesh { name = "Escalpo_EEG" };
        malha.Clear();
        malha.indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        malha.vertices = vertices;
        malha.triangles = indices;
        malha.RecalculateNormals();
        // Espelhar o eixo z inverte o sentido das faces; confere que as normais apontam para fora e corrige se preciso.
        var centro = vertices.Aggregate(Vector3.zero, (soma, v) => soma + v) / vertices.Length;
        var normais = malha.normals;
        var paraFora = 0f;
        for (var i = 0; i < vertices.Length; i++)
            paraFora += Vector3.Dot(normais[i], vertices[i] - centro);
        if (paraFora < 0f)
        {
            for (var t = 0; t < indices.Length; t += 3)
                (indices[t + 1], indices[t + 2]) = (indices[t + 2], indices[t + 1]);
            malha.triangles = indices;
            malha.RecalculateNormals();
        }

        malha.colors32 = Enumerable.Repeat(new Color32(128, 128, 128, 255), vertices.Length).ToArray();
        malha.RecalculateBounds();
        if (nova)
            AssetDatabase.CreateAsset(malha, CaminhoEscalpo);
        EditorUtility.SetDirty(malha);
        return malha;
    }

    private static (Vector3 minimo, Vector3 maximo) LerLimitesCortex(string pasta, JToken descritor)
    {
        var posicoes = LerFloats(Path.Combine(pasta, (string)descritor["vertices"]["file"]));
        var minimo = Vector3.positiveInfinity;
        var maximo = Vector3.negativeInfinity;
        for (var i = 0; i < posicoes.Length; i += 3)
        {
            var v = new Vector3(posicoes[i] * 0.1f, posicoes[i + 1] * 0.1f, -posicoes[i + 2] * 0.1f);
            minimo = Vector3.Min(minimo, v);
            maximo = Vector3.Max(maximo, v);
        }

        return (minimo, maximo);
    }

    private static void CriarMateriais()
    {
        GarantirMaterial(PastaMateriais + "/Escalpo_EEG.mat", "VRSNC/EscalpoEEG", material =>
        {
            material.SetFloat("_Opacidade", 0.5f);
            material.SetFloat("_Ambiente", 0.6f);
        });
        GarantirMaterial(PastaMateriais + "/Eletrodo_EEG.mat", "Universal Render Pipeline/Lit", material =>
        {
            material.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f, 1f));
            material.SetFloat("_Smoothness", 0.35f);
        });
        GarantirMaterial(PastaMateriais + "/Ligacao_EEG.mat", "Universal Render Pipeline/Unlit", material =>
        {
            // Âmbar (#e0b252): não existe em nenhuma rampa de dados, então nunca é lido como valor medido.
            material.SetColor("_BaseColor", new Color(0.878f, 0.698f, 0.322f, 1f));
        });
    }

    private static Material GarantirMaterial(string caminho, string shader, Action<Material> configurar)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        var sombreador = Exigir(Shader.Find(shader), "shader " + shader);
        if (material == null)
        {
            material = new Material(sombreador) { name = Path.GetFileNameWithoutExtension(caminho) };
            AssetDatabase.CreateAsset(material, caminho);
        }
        else if (material.shader != sombreador)
        {
            material.shader = sombreador;
        }

        configurar(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ================================================================ instalação na cena

    public static string InstalarNaCena()
    {
        var cena = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || cena.path != CaminhoCena || cena.isDirty)
            throw new InvalidOperationException("Use um Editor ocioso com a SampleScene salva, sem alterações, no Modo de Edição.");
        var dados = AssetDatabase.LoadAssetAtPath<DadosEEG>(CaminhoDados);
        if (dados == null)
            dados = Importar(PastaProtobrain).dados;
        var raizSnc = Exigir(GameObject.Find("Sistema_Nervoso_Central"), "Sistema_Nervoso_Central").transform;
        var gerenciador = Exigir(raizSnc.GetComponent<GerenciadorSelecaoHierarquica>(), "GerenciadorSelecaoHierarquica");
        var painel = Exigir(GameObject.Find("Painel_Anatomico_VR_SNC"), "Painel_Anatomico_VR_SNC");

        Undo.IncrementCurrentGroup();
        var grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Instalar o EEG do Protobrain");
        try
        {
            var alinhamento = CalcularAlinhamento(raizSnc, dados);
            var ligacoes = CalcularLigacoes(raizSnc, dados, alinhamento.matriz, out var resolvidas, out var aproximadas);
            var objeto = GameObject.Find(NomeControlador);
            if (objeto == null)
            {
                objeto = new GameObject(NomeControlador);
                Undo.RegisterCreatedObjectUndo(objeto, "Criar o controlador do EEG");
            }

            var controlador = objeto.GetComponent<ControladorEEG>();
            if (controlador == null)
                controlador = Undo.AddComponent<ControladorEEG>(objeto);
            if (objeto.GetComponent<CabecaEEG>() == null)
                Undo.AddComponent<CabecaEEG>(objeto);
            if (objeto.GetComponent<ApontadorEletrodosEEG>() == null)
                Undo.AddComponent<ApontadorEletrodosEEG>(objeto);
            Undo.RecordObject(controlador, "Configurar o EEG");
            controlador.Configurar(dados, gerenciador, raizSnc, alinhamento.posicao, alinhamento.rotacao, alinhamento.escala, ligacoes,
                AssetDatabase.LoadAssetAtPath<Material>(PastaMateriais + "/Escalpo_EEG.mat"),
                AssetDatabase.LoadAssetAtPath<Material>(PastaMateriais + "/Eletrodo_EEG.mat"),
                AssetDatabase.LoadAssetAtPath<Material>(PastaMateriais + "/Ligacao_EEG.mat"));
            EditorUtility.SetDirty(controlador);

            // A aba EEG e o painel dela vêm da ferramenta do painel compacto.
            ConfiguracaoPainelCompactoAbas.Aplicar(painel, GameObject.Find("Oraculo_Anatomico_Punho"));
            EditorSceneManager.MarkSceneDirty(cena);
            if (!EditorSceneManager.SaveScene(cena))
                throw new IOException("Não foi possível salvar a cena com o EEG.");
            Undo.CollapseUndoOperations(grupo);
            return $"Instalado: {resolvidas}/{dados.canais.Length} eletrodos ligados a um giro do modelo ({aproximadas} por equivalência aproximada). {alinhamento.relatorio}";
        }
        catch
        {
            Undo.RevertAllDownToGroup(grupo);
            throw;
        }
    }

    public struct Alinhamento
    {
        public Vector3 posicao;
        public Quaternion rotacao;
        public Vector3 escala;
        public Matrix4x4 matriz;
        public string relatorio;
        public int lobosCoerentes;
    }

    private sealed class PontoCortex
    {
        public Vector3 local;
        public string lobo;
        public bool? esquerdo;
    }

    /// <summary>
    /// Alinha o córtex do fsaverage ao córtex do modelo BodyParts3D, no espaço local da raiz do SNC:
    /// eixos anatômicos tirados do próprio modelo (direita = giros direitos − esquerdos; anterior = lobo
    /// frontal − occipital; superior = córtex − tronco encefálico), escala por eixo pela extensão dos dois
    /// córtices (limitada a ±15% da escala média, para não deformar o escalpo) e centros coincidentes.
    /// </summary>
    public static Alinhamento CalcularAlinhamento(Transform raiz, DadosEEG dados)
    {
        var cortex = Exigir(raiz.Find("Cerebro/Telencefalo/Cortex"), "grupo Cerebro/Telencefalo/Cortex");
        var tronco = raiz.Find("Tronco_Encefalico");
        var pontos = ColetarPontosCortex(raiz, cortex);
        Exigir(pontos.Count > 1000, "malhas do córtex sem vértices legíveis");
        Vector3 Media(IEnumerable<Vector3> lista)
        {
            var soma = Vector3.zero;
            var n = 0;
            foreach (var p in lista)
            {
                soma += p;
                n++;
            }

            return n > 0 ? soma / n : Vector3.zero;
        }

        var esquerda = Media(pontos.Where(p => p.esquerdo == true).Select(p => p.local));
        var direitaCentro = Media(pontos.Where(p => p.esquerdo == false).Select(p => p.local));
        var direita = (direitaCentro - esquerda).normalized;
        var frontal = Media(pontos.Where(p => p.lobo == "Frontal").Select(p => p.local));
        var occipital = Media(pontos.Where(p => p.lobo == "Occipital").Select(p => p.local));
        var anterior = Vector3.ProjectOnPlane(frontal - occipital, direita).normalized;
        var centroCortex = Media(pontos.Select(p => p.local));
        var centroTronco = tronco != null ? Media(ColetarPontos(raiz, tronco, 8).Select(p => p.local)) : centroCortex - Vector3.up;
        var superior = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(centroCortex - centroTronco, direita), anterior).normalized;

        var minimo = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximo = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        foreach (var ponto in pontos)
        {
            var coordenadas = new Vector3(Vector3.Dot(ponto.local, direita), Vector3.Dot(ponto.local, superior), Vector3.Dot(ponto.local, anterior));
            minimo = Vector3.Min(minimo, coordenadas);
            maximo = Vector3.Max(maximo, coordenadas);
        }

        var extensaoModelo = maximo - minimo;
        var meioModelo = (maximo + minimo) * 0.5f;
        var extensaoFs = dados.cortexMaximo - dados.cortexMinimo;
        var meioFs = (dados.cortexMaximo + dados.cortexMinimo) * 0.5f;
        var escala = new Vector3(extensaoModelo.x / extensaoFs.x, extensaoModelo.y / extensaoFs.y, extensaoModelo.z / extensaoFs.z);
        var media = Mathf.Pow(Mathf.Abs(escala.x * escala.y * escala.z), 1f / 3f);
        escala = new Vector3(Mathf.Clamp(escala.x, media * 0.85f, media * 1.15f), Mathf.Clamp(escala.y, media * 0.85f, media * 1.15f), Mathf.Clamp(escala.z, media * 0.85f, media * 1.15f));
        var rotacao = Quaternion.LookRotation(anterior, superior);
        // Se o modelo estiver espelhado em relação ao referencial do Unity, a direita anatômica fica do outro lado: reflexão em x.
        if (Vector3.Dot(rotacao * Vector3.right, direita) < 0f)
            escala.x = -escala.x;
        var centroModelo = direita * meioModelo.x + superior * meioModelo.y + anterior * meioModelo.z;
        var posicao = centroModelo - rotacao * Vector3.Scale(escala, meioFs);
        var matriz = Matrix4x4.TRS(posicao, rotacao, escala);

        // Conferência: o lobo do ponto do córtex mais próximo de cada eletrodo deve ser o mesmo do atlas Desikan.
        var coerentes = 0;
        var distancias = new List<float>();
        for (var canal = 0; canal < dados.canais.Length; canal++)
        {
            var eletrodo = matriz.MultiplyPoint3x4(dados.canais[canal].posicao);
            PontoCortex maisProximo = null;
            var melhor = float.PositiveInfinity;
            foreach (var ponto in pontos)
            {
                var d = (ponto.local - eletrodo).sqrMagnitude;
                if (d < melhor)
                {
                    melhor = d;
                    maisProximo = ponto;
                }
            }

            // O espaço local da raiz pode não estar em metros (o modelo vem em mm): converte pela escala dela.
            distancias.Add(Mathf.Sqrt(melhor) * Mathf.Abs(raiz.lossyScale.x) * 1000f);
            var regiao = dados.RegiaoDe(canal);
            if (maisProximo != null && regiao != null && string.Equals(regiao.lobo, maisProximo.lobo, StringComparison.OrdinalIgnoreCase))
                coerentes++;
        }

        distancias.Sort();
        var relatorio = $"Alinhamento: escala ({escala.x:0.000}, {escala.y:0.000}, {escala.z:0.000}), lobo do córtex mais próximo coerente com o Desikan em {coerentes}/{dados.canais.Length}, " +
                        $"distância eletrodo → córtex do modelo: mediana {distancias[distancias.Count / 2]:0} mm (de {distancias[0]:0} a {distancias[distancias.Count - 1]:0} mm).";
        return new Alinhamento { posicao = posicao, rotacao = rotacao, escala = escala, matriz = matriz, relatorio = relatorio, lobosCoerentes = coerentes };
    }

    private static List<PontoCortex> ColetarPontosCortex(Transform raiz, Transform cortex)
    {
        var pontos = new List<PontoCortex>();
        foreach (Transform lobo in cortex)
        {
            var nomeLobo = lobo.name switch
            {
                "Lobo_Frontal" => "Frontal",
                "Lobo_Parietal" => "Parietal",
                "Lobo_Temporal" => "Temporal",
                "Lobo_Occipital" => "Occipital",
                "Lobo_Limbico" => "Límbico",
                "Insula" => "Ínsula",
                _ => lobo.name,
            };
            foreach (var ponto in ColetarPontos(raiz, lobo, 4))
            {
                ponto.lobo = nomeLobo;
                pontos.Add(ponto);
            }
        }

        return pontos;
    }

    private static List<PontoCortex> ColetarPontos(Transform raiz, Transform grupo, int passo)
    {
        var pontos = new List<PontoCortex>();
        var paraRaiz = raiz.worldToLocalMatrix;
        foreach (var filtro in grupo.GetComponentsInChildren<MeshFilter>(true))
        {
            var malha = filtro.sharedMesh;
            if (malha == null)
                continue;
            var no = filtro.GetComponentInParent<NoInteracaoAnatomica>();
            var nome = no != null ? (no.NomeLegivel ?? "").ToLowerInvariant() : "";
            bool? esquerdo = nome.Contains("esquerd") ? true : nome.Contains("direit") ? false : null;
            var matriz = paraRaiz * filtro.transform.localToWorldMatrix;
            var vertices = malha.vertices;
            for (var i = 0; i < vertices.Length; i += passo)
                pontos.Add(new PontoCortex { local = matriz.MultiplyPoint3x4(vertices[i]), esquerdo = esquerdo });
        }

        return pontos;
    }

    /// <summary>Para cada eletrodo, o giro equivalente do modelo e o ponto dele mais próximo do eletrodo.</summary>
    private static ControladorEEG.LigacaoGiro[] CalcularLigacoes(Transform raiz, DadosEEG dados, Matrix4x4 cabeca, out int resolvidas, out int aproximadas)
    {
        resolvidas = 0;
        aproximadas = 0;
        var ligacoes = new ControladorEEG.LigacaoGiro[dados.canais.Length];
        for (var canal = 0; canal < dados.canais.Length; canal++)
        {
            var info = dados.canais[canal];
            var regiao = dados.RegiaoDe(canal);
            var no = CorrespondenciaGirosEEG.Resolver(raiz, regiao?.chave, info.hemisferioEsquerdo, out var aproximada);
            var ligacao = new ControladorEEG.LigacaoGiro { no = no, aproximada = aproximada };
            if (no != null)
            {
                resolvidas++;
                if (aproximada)
                    aproximadas++;
                var eletrodoMundo = raiz.TransformPoint(cabeca.MultiplyPoint3x4(info.posicao));
                var melhor = float.PositiveInfinity;
                var pontoMundo = no.transform.position;
                foreach (var filtro in no.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filtro.sharedMesh == null)
                        continue;
                    var matriz = filtro.transform.localToWorldMatrix;
                    foreach (var vertice in filtro.sharedMesh.vertices)
                    {
                        var mundo = matriz.MultiplyPoint3x4(vertice);
                        var d = (mundo - eletrodoMundo).sqrMagnitude;
                        if (d < melhor)
                        {
                            melhor = d;
                            pontoMundo = mundo;
                        }
                    }
                }

                ligacao.pontoLocal = no.transform.InverseTransformPoint(pontoMundo);
            }

            ligacoes[canal] = ligacao;
        }

        return ligacoes;
    }

    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao)
            throw new InvalidDataException("EEG: " + mensagem + ".");
    }

    private static T Exigir<T>(T valor, string nome) where T : class
    {
        if (valor == null || (valor is UnityEngine.Object objeto && objeto == null))
            throw new InvalidDataException("EEG: " + nome + " não encontrado.");
        return valor;
    }
}
