using UnityEngine;

namespace VRSNC.EEG
{
    /// <summary>
    /// Escalpo do fsaverage em volta do cérebro do VR, pintado com a topografia, e os 32 eletrodos.
    /// Como no headplot do EEGLAB e no Protobrain, a cor fica no ESCALPO, onde a medida existe (espaço de
    /// sensores); o cérebro por dentro é só referência anatômica e nunca recebe a cor do mapa.
    /// A cabeça fica fora da hierarquia anatômica e segue a pose da raiz do SNC a cada quadro.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ControladorEEG))]
    public sealed class CabecaEEG : MonoBehaviour
    {
        private const float RaioEletrodoMetros = 0.0045f;
        private const float EscalaSelecionado = 1.7f;
        private const float EscalaApontado = 1.35f;
        // Repintura máxima no mapa de voltagem, que muda a cada amostra enquanto toca (o de potência muda a 4 Hz).
        private const float IntervaloMinimoVoltagem = 1f / 15f;
        // "Sem dado" (#1a1f28), mais escuro que o cinza neutro da rampa divergente.
        private static readonly Color32 SemDado = new(0x1a, 0x1f, 0x28, 255);

        private ControladorEEG controlador;
        private Transform seguidor;
        private Transform cabeca;
        private Mesh malha;
        private Color32[] cores;
        private float[] campo;
        private float[] valores;
        private Transform[] eletrodos;
        private Renderer[] renderizadoresEletrodos;
        private MaterialPropertyBlock bloco;
        private MeshRenderer renderizadorEscalpo;
        private MaterialPropertyBlock blocoEscalpo;
        private float opacidadeAplicada = -1f;
        private LineRenderer ligacao;
        private int apontado = -1;
        private bool pinturaPendente = true;
        private float ultimaPintura = float.NegativeInfinity;
        private bool visivel;

        public bool Visivel => visivel;
        public int Apontado => apontado;

        private void Awake()
        {
            controlador = GetComponent<ControladorEEG>();
        }

        private void OnEnable()
        {
            controlador.MapaAlterado += MarcarPintura;
            controlador.EstadoAlterado += MarcarPintura;
        }

        private void OnDisable()
        {
            controlador.MapaAlterado -= MarcarPintura;
            controlador.EstadoAlterado -= MarcarPintura;
            if (seguidor != null)
                seguidor.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (seguidor != null)
                Destroy(seguidor.gameObject);
            if (malha != null)
                Destroy(malha);
        }

        private void MarcarPintura() => pinturaPendente = true;

        private void LateUpdate()
        {
            if (!controlador.Disponivel)
                return;
            if (seguidor == null)
                Construir();
            var deveMostrar = controlador.CabecaVisivel && controlador.RaizAnatomia != null;
            if (seguidor.gameObject.activeSelf != deveMostrar)
            {
                seguidor.gameObject.SetActive(deveMostrar);
                pinturaPendente = true;
            }

            visivel = deveMostrar;
            if (!deveMostrar)
                return;
            var raiz = controlador.RaizAnatomia;
            seguidor.SetPositionAndRotation(raiz.position, raiz.rotation);
            seguidor.localScale = raiz.lossyScale;
            if (!Mathf.Approximately(opacidadeAplicada, controlador.OpacidadeEscalpo))
            {
                opacidadeAplicada = controlador.OpacidadeEscalpo;
                blocoEscalpo.SetFloat("_Opacidade", opacidadeAplicada);
                renderizadorEscalpo.SetPropertyBlock(blocoEscalpo);
            }

            var podePintar = controlador.Mapa != ControladorEEG.TipoMapa.Voltagem || Time.unscaledTime - ultimaPintura >= IntervaloMinimoVoltagem;
            if (pinturaPendente && podePintar)
                Pintar();
            AtualizarEletrodos();
        }

        /// <summary>Posição do centro de um eletrodo no mundo.</summary>
        public Vector3 PosicaoEletrodo(int canal) => eletrodos != null && canal >= 0 && canal < eletrodos.Length ? eletrodos[canal].position : Vector3.zero;

        /// <summary>Raio do eletrodo no mundo, já com a escala do modelo.</summary>
        public float RaioEletrodoMundo
        {
            get
            {
                if (cabeca == null)
                    return RaioEletrodoMetros;
                var escala = cabeca.lossyScale;
                return RaioEletrodoMetros * (Mathf.Abs(escala.x) + Mathf.Abs(escala.y) + Mathf.Abs(escala.z)) / 3f;
            }
        }

        public int QuantidadeEletrodos => eletrodos != null ? eletrodos.Length : 0;

        public void DefinirApontado(int canal)
        {
            apontado = canal;
        }

        private void Construir()
        {
            var dados = controlador.Dados;
            seguidor = new GameObject("EEG_Cabeca").transform;
            cabeca = new GameObject("Escalpo_EEG").transform;
            cabeca.SetParent(seguidor, false);
            var matriz = controlador.MatrizCabecaLocal;
            cabeca.localPosition = matriz.GetColumn(3);
            cabeca.localRotation = matriz.rotation;
            cabeca.localScale = matriz.lossyScale;
            // Cópia da malha: as cores por vértice mudam sem tocar no asset importado.
            malha = Instantiate(dados.escalpo);
            malha.name = "Escalpo_EEG (execução)";
            malha.MarkDynamic();
            cabeca.gameObject.AddComponent<MeshFilter>().sharedMesh = malha;
            renderizadorEscalpo = cabeca.gameObject.AddComponent<MeshRenderer>();
            renderizadorEscalpo.sharedMaterial = controlador.MaterialEscalpo;
            renderizadorEscalpo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderizadorEscalpo.receiveShadows = false;
            blocoEscalpo = new MaterialPropertyBlock();
            opacidadeAplicada = -1f;
            cores = new Color32[malha.vertexCount];
            campo = new float[malha.vertexCount];
            valores = new float[controlador.Conjunto.Canais];

            // Eletrodos assentados no escalpo ao longo da normal, como no viewer do Protobrain. Ficam no espaço da
            // cabeça (metros do fsaverage), que o alinhamento já leva ao tamanho do modelo.
            var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var malhaEsfera = esfera.GetComponent<MeshFilter>().sharedMesh;
            Destroy(esfera);
            var grupo = new GameObject("Eletrodos_EEG").transform;
            grupo.SetParent(cabeca, false);
            eletrodos = new Transform[dados.canais.Length];
            renderizadoresEletrodos = new Renderer[dados.canais.Length];
            bloco = new MaterialPropertyBlock();
            for (var canal = 0; canal < dados.canais.Length; canal++)
            {
                var info = dados.canais[canal];
                var eletrodo = new GameObject("Eletrodo_" + info.nome).transform;
                eletrodo.SetParent(grupo, false);
                eletrodo.localPosition = info.posicao + info.normal.normalized * (RaioEletrodoMetros * 0.6f);
                eletrodo.localScale = Vector3.one * (RaioEletrodoMetros * 2f);
                eletrodo.gameObject.AddComponent<MeshFilter>().sharedMesh = malhaEsfera;
                var render = eletrodo.gameObject.AddComponent<MeshRenderer>();
                render.sharedMaterial = controlador.MaterialEletrodo;
                render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eletrodos[canal] = eletrodo;
                renderizadoresEletrodos[canal] = render;
            }

            ligacao = new GameObject("Ligacao_Eletrodo_Giro").AddComponent<LineRenderer>();
            ligacao.transform.SetParent(seguidor, false);
            ligacao.useWorldSpace = true;
            ligacao.positionCount = 2;
            ligacao.widthMultiplier = 0.0015f;
            ligacao.sharedMaterial = controlador.MaterialLigacao;
            ligacao.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ligacao.enabled = false;
            pinturaPendente = true;
        }

        /// <summary>
        /// Interpola o VALOR e só depois converte em cor: a spline é linear no valor medido, e cortar a escala
        /// antes de interpolar deformaria o mapa perto de um eletrodo saturado. Fora da área interpolada a cor
        /// se mistura com "sem dado".
        /// </summary>
        private void Pintar()
        {
            pinturaPendente = false;
            ultimaPintura = Time.unscaledTime;
            var conjunto = controlador.Conjunto;
            var escala = controlador.ValoresMapa(valores);
            ConjuntoEEG.Interpolar(conjunto.PesosEscalpo, valores, campo);
            var tabela = MapaCoresEEG.Tabela(escala.divergente);
            var cobertura = conjunto.CoberturaEscalpo;
            for (var v = 0; v < campo.Length; v++)
            {
                var cor = tabela[Mathf.Clamp(Mathf.RoundToInt(MapaCoresEEG.Normalizar(campo[v], escala) * 255f), 0, 255)];
                var k = cobertura[v];
                if (k < 0.999f)
                    cor = Color32.Lerp(SemDado, cor, k);
                cores[v] = cor;
            }

            malha.SetColors(cores);
            for (var canal = 0; canal < eletrodos.Length; canal++)
            {
                bloco.SetColor("_BaseColor", MapaCoresEEG.Cor(valores[canal], escala));
                renderizadoresEletrodos[canal].SetPropertyBlock(bloco);
            }
        }

        private void AtualizarEletrodos()
        {
            var selecionado = controlador.CanalSelecionado;
            for (var canal = 0; canal < eletrodos.Length; canal++)
            {
                var escala = canal == selecionado ? EscalaSelecionado : canal == apontado ? EscalaApontado : 1f;
                eletrodos[canal].localScale = Vector3.one * (RaioEletrodoMetros * 2f * escala);
            }

            // Ligação do eletrodo escolhido ao ponto mais próximo do giro equivalente, que acompanha a peça.
            var info = controlador.LigacaoDe(selecionado);
            var mostrarLigacao = selecionado >= 0 && info != null && info.no != null && info.no.isActiveAndEnabled;
            ligacao.enabled = mostrarLigacao;
            if (mostrarLigacao)
            {
                ligacao.SetPosition(0, eletrodos[selecionado].position);
                ligacao.SetPosition(1, info.no.transform.TransformPoint(info.pontoLocal));
            }
        }
    }
}
