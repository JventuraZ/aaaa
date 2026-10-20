using System.Globalization;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRSNC.Interface;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Gestos das mãos rastreadas no ar, sem precisar acertar uma estrutura:
    /// <list type="bullet">
    /// <item>pinçar o vazio com uma mão e arrastar gira o alvo ativo (seleção, grupo em foco ou o SNC inteiro), como o
    /// analógico do controle;</item>
    /// <item>pinçar o vazio com as duas mãos move, gira e escala o SNC inteiro: afastar as mãos aumenta, aproximar diminui;</item>
    /// <item>uma pinça curta da mão esquerda no vazio faz o Voltar (sobe um nível e deixa a concha selecionada).</item>
    /// </list>
    /// "No vazio" quer dizer que a mão não aponta anatomia, interface, painel nem eletrodo, e não segura nada. Pinçar
    /// estruturas continua selecionando e agarrando, e duas pinças em estruturas continuam separando o catálogo.
    /// Durante um giro, a mão fica reservada (<see cref="PonteInteracaoMao.DefinirMaoReservada"/>) para a anatomia por
    /// onde o raio passa não reagir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GestosMaosNoAr : MonoBehaviour
    {
        public const float DuracaoMinimaToque = 0.08f;
        public const float DuracaoMaximaToque = 0.35f;
        public const float DeslocamentoMaximoToque = 0.025f;
        public const float DeslocamentoInicioGiro = 0.015f;
        public const float IntervaloEntreVoltar = 0.6f;
        // 10 cm de arraste = 30°.
        public const float GrausPorMetro = 300f;
        // Fator de escala do SNC em relação ao tamanho original da cena.
        public const float EscalaMinima = 0.5f;
        public const float EscalaMaxima = 2.5f;
        private const float DuracaoAvisoVoltar = 0.8f;
        // Palma voltada para o rosto é o gesto de sistema da Meta: aí a pinça curta não vale como Voltar.
        private const float LimitePalmaParaRosto = 0.5f;
        private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");

        [SerializeField]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [SerializeField]
        private ManipuladorObjetoSelecionado manipulador;
        [SerializeField]
        private PonteInteracaoMao ponte;
        [SerializeField]
        private CoordenadorGestoAnatomico coordenadorGesto;
        [SerializeField]
        private GerenciadorRestauracao gerenciadorRestauracao;
        // Raiz do SNC: o alvo do gesto com as duas mãos.
        [SerializeField]
        private Transform raizModelo;
        [SerializeField]
        private XRInputModalityManager modalidade;
        [SerializeField]
        private RotuloEstruturaApontada rotulo;

        private enum Estado { Livre, Pinca, Girando, DuasMaos, EsperandoSoltar }

        private sealed class Mao
        {
            public bool esquerda;
            public GameObject raiz;
            public NearFarInteractor interator;
            public Estado estado;
            public float inicio;
            public Vector3 pontoInicial;
            public Vector3 ultimoPonto;
            public float deslocamentoMaximo;
            public bool reservada;
        }

        private readonly Mao esquerda = new() { esquerda = true };
        private readonly Mao direita = new() { esquerda = false };
        private XRHandSubsystem subsistema;
        private Transform origemXR;
        private Transform alvoGiro;
        private Vector3 pivoGiroLocal;
        private float escalaOriginal = 1f;
        private float ultimoVoltar = float.NegativeInfinity;
        private string textoAviso;
        private Vector3 pontoAviso;
        private float avisoAte = float.NegativeInfinity;
        private LineRenderer linhaDuasMaos;
        // Estado do gesto com as duas mãos, capturado ao começar.
        private Vector3 inicioA;
        private Vector3 inicioB;
        private Vector3 posicaoInicial;
        private Quaternion rotacaoInicial;
        private Vector3 escalaInicial;
        private Vector3 pivoInicial;
        private float fatorInicial;
        private float fatorAtual = 1f;

        /// <summary>Fator de escala do SNC em relação ao tamanho original (1 = como na cena).</summary>
        public float FatorEscala => fatorAtual;
        public bool GirandoComUmaMao => esquerda.estado == Estado.Girando || direita.estado == Estado.Girando;
        public bool ManipulandoComDuasMaos => esquerda.estado == Estado.DuasMaos;

        private void Awake()
        {
            if (gerenciadorSelecao == null)
                gerenciadorSelecao = FindFirstObjectByType<GerenciadorSelecaoHierarquica>();
            if (gerenciadorSelecao != null)
            {
                manipulador ??= gerenciadorSelecao.GetComponent<ManipuladorObjetoSelecionado>();
                ponte ??= gerenciadorSelecao.GetComponent<PonteInteracaoMao>();
                coordenadorGesto ??= gerenciadorSelecao.GetComponent<CoordenadorGestoAnatomico>();
                gerenciadorRestauracao ??= gerenciadorSelecao.GetComponent<GerenciadorRestauracao>();
                rotulo ??= gerenciadorSelecao.GetComponent<RotuloEstruturaApontada>();
                if (raizModelo == null)
                    raizModelo = gerenciadorSelecao.transform;
            }

            if (modalidade == null)
                modalidade = FindFirstObjectByType<XRInputModalityManager>();
            if (raizModelo != null)
                escalaOriginal = Mathf.Max(1e-6f, raizModelo.localScale.x);
        }

        private void OnEnable()
        {
            if (gerenciadorRestauracao != null)
                gerenciadorRestauracao.emRestaurando += Cancelar;
        }

        private void OnDisable()
        {
            if (gerenciadorRestauracao != null)
                gerenciadorRestauracao.emRestaurando -= Cancelar;
            Cancelar();
        }

        /// <summary>Encerra qualquer gesto em curso (reset, troca de modalidade): as mãos precisam soltar a pinça antes do próximo.</summary>
        public void Cancelar()
        {
            foreach (var mao in new[] { esquerda, direita })
            {
                if (mao.estado != Estado.Livre)
                    mao.estado = Estado.EsperandoSoltar;
                Reservar(mao, false);
            }

            alvoGiro = null;
            MostrarLinha(false);
        }

        private void Update()
        {
            if (!ResolverMaos())
                return;
            var temSubsistema = PincaMao.TentarObterSubsistema(ref subsistema, out var mapa);
            if (origemXR == null)
            {
                var origem = FindFirstObjectByType<XROrigin>();
                origemXR = origem != null ? origem.transform : null;
            }

            var pontoEsquerda = Vector3.zero;
            var pontoDireita = Vector3.zero;
            var temEsquerda = temSubsistema && PincaMao.TentarObterPonto(mapa.leftHand, origemXR, out pontoEsquerda);
            var temDireita = temSubsistema && PincaMao.TentarObterPonto(mapa.rightHand, origemXR, out pontoDireita);
            Atualizar(esquerda, direita, temEsquerda, pontoEsquerda, temSubsistema ? mapa.leftHand : default);
            Atualizar(direita, esquerda, temDireita, pontoDireita, temSubsistema ? mapa.rightHand : default);
            if (esquerda.estado == Estado.DuasMaos)
                AtualizarDuasMaos(temEsquerda && temDireita, pontoEsquerda, pontoDireita);
            if (Time.unscaledTime < avisoAte && rotulo != null)
                rotulo.DefinirRotuloExterno(textoAviso, pontoAviso);
        }

        private void Atualizar(Mao mao, Mao outra, bool temPonto, Vector3 ponto, XRHand dadosMao)
        {
            // Mão que perdeu o rastreamento (ou virou controle) encerra o gesto sem ação: soltar por perda de
            // rastreamento não pode virar um Voltar.
            if (mao.estado != Estado.DuasMaos && (mao.raiz == null || !mao.raiz.activeInHierarchy))
            {
                if (mao.estado == Estado.Girando)
                    alvoGiro = null;
                Reservar(mao, false);
                mao.estado = Estado.Livre;
                return;
            }

            var pincando = LerPinca(mao, out var comecou);
            switch (mao.estado)
            {
                case Estado.Livre:
                    if (comecou && temPonto && EstaNoAr(mao))
                    {
                        mao.estado = Estado.Pinca;
                        mao.inicio = Time.unscaledTime;
                        mao.pontoInicial = mao.ultimoPonto = ponto;
                        mao.deslocamentoMaximo = 0f;
                    }

                    break;
                case Estado.Pinca:
                    if (!pincando)
                    {
                        if (mao.esquerda && EhToqueRapido(mao) && PalmaNaoVoltadaParaORosto(dadosMao))
                            Voltar(ponto);
                        mao.estado = Estado.Livre;
                        break;
                    }

                    if (!temPonto)
                    {
                        mao.estado = Estado.EsperandoSoltar;
                        break;
                    }

                    mao.deslocamentoMaximo = Mathf.Max(mao.deslocamentoMaximo, Vector3.Distance(ponto, mao.pontoInicial));
                    if (outra.estado == Estado.Pinca || outra.estado == Estado.Girando)
                    {
                        IniciarDuasMaos();
                        break;
                    }

                    if (mao.deslocamentoMaximo > DeslocamentoInicioGiro && PodeManipular())
                        IniciarGiro(mao, ponto);
                    break;
                case Estado.Girando:
                    if (!pincando || !temPonto)
                    {
                        Reservar(mao, false);
                        alvoGiro = null;
                        mao.estado = pincando ? Estado.EsperandoSoltar : Estado.Livre;
                        break;
                    }

                    if (outra.estado == Estado.Pinca)
                    {
                        IniciarDuasMaos();
                        break;
                    }

                    Girar(mao, ponto);
                    break;
                case Estado.EsperandoSoltar:
                    if (!pincando)
                        mao.estado = Estado.Livre;
                    break;
            }
        }

        /// <summary>Pinça da mão pelo mesmo leitor que o XRI usa para selecionar (indicador e polegar).</summary>
        private static bool LerPinca(Mao mao, out bool comecou)
        {
            comecou = false;
            var leitor = mao.interator != null ? mao.interator.selectInput : null;
            if (leitor == null || mao.raiz == null || !mao.raiz.activeInHierarchy)
                return false;
            comecou = leitor.ReadWasPerformedThisFrame();
            return leitor.ReadIsPerformed();
        }

        /// <summary>A mão está rastreada, livre e sem nada na mira (anatomia, interface, painel ou eletrodo).</summary>
        private bool EstaNoAr(Mao mao)
        {
            var interator = mao.interator;
            if (mao.raiz == null || !mao.raiz.activeInHierarchy || interator == null || !interator.isActiveAndEnabled)
                return false;
            if (interator.hasSelection || interator.interactablesHovered.Count > 0)
                return false;
            if (PonteRaioInterfaceMao.RaioEstaAlvejandoInterface(interator) || ZonaProximidadePainelInterface.InteratorEstaProximoDoPainel(interator))
                return false;
            if (PonteInteracaoMao.MaoReservada(mao.raiz.transform) || ControlePainelMundo.AgarrePainelAtivoParaMao(mao.raiz.transform))
                return false;
            return true;
        }

        private bool PodeManipular()
        {
            if (manipulador == null || manipulador.EstaAgarrando)
                return false;
            if (ponte != null && ponte.EstaAgarrando)
                return false;
            return coordenadorGesto == null || !coordenadorGesto.EstaAtivo;
        }

        private bool EhToqueRapido(Mao mao)
        {
            var duracao = Time.unscaledTime - mao.inicio;
            return duracao >= DuracaoMinimaToque && duracao <= DuracaoMaximaToque && mao.deslocamentoMaximo < DeslocamentoMaximoToque
                && Time.unscaledTime - ultimoVoltar >= IntervaloEntreVoltar;
        }

        private bool PalmaNaoVoltadaParaORosto(XRHand mao)
        {
            var camera = Camera.main;
            if (camera == null || origemXR == null || !mao.isTracked || !mao.GetJoint(XRHandJointID.Palm).TryGetPose(out var palma))
                return true;
            var posicao = origemXR.TransformPoint(palma.position);
            // O eixo para cima da palma aponta para o dorso; a palma olha para o lado oposto.
            var normalPalma = -(origemXR.rotation * palma.rotation * Vector3.up);
            return Vector3.Dot(normalPalma, (camera.transform.position - posicao).normalized) < LimitePalmaParaRosto;
        }

        private void Voltar(Vector3 ponto)
        {
            if (gerenciadorSelecao == null)
                return;
            var destino = gerenciadorSelecao.DestinoVoltar();
            if (string.IsNullOrEmpty(destino))
                return;
            ultimoVoltar = Time.unscaledTime;
            gerenciadorSelecao.VoltarAoNoPai();
            textoAviso = "‹ " + destino;
            pontoAviso = ponto;
            avisoAte = Time.unscaledTime + DuracaoAvisoVoltar;
        }

        private void IniciarGiro(Mao mao, Vector3 ponto)
        {
            alvoGiro = manipulador.AlvoAtivo;
            if (alvoGiro == null)
                return;
            pivoGiroLocal = alvoGiro.InverseTransformPoint(ManipuladorObjetoSelecionado.CentroVisual(alvoGiro));
            mao.ultimoPonto = ponto;
            mao.estado = Estado.Girando;
            Reservar(mao, true);
        }

        private void Girar(Mao mao, Vector3 ponto)
        {
            var delta = ponto - mao.ultimoPonto;
            mao.ultimoPonto = ponto;
            var camera = Camera.main;
            if (alvoGiro == null || camera == null || !PodeManipular())
                return;
            var horizontal = Vector3.Dot(delta, camera.transform.right);
            var vertical = Vector3.Dot(delta, camera.transform.up);
            // A face voltada para o usuário acompanha a mão: arrastar para a direita gira para a direita.
            manipulador.GirarPorArraste(alvoGiro, alvoGiro.TransformPoint(pivoGiroLocal), -horizontal * GrausPorMetro, vertical * GrausPorMetro);
        }

        private void IniciarDuasMaos()
        {
            if (raizModelo == null || !PodeManipular())
                return;
            foreach (var mao in new[] { esquerda, direita })
            {
                Reservar(mao, false);
                mao.estado = Estado.DuasMaos;
                Reservar(mao, true);
            }

            alvoGiro = null;
            posicaoInicial = raizModelo.position;
            rotacaoInicial = raizModelo.rotation;
            escalaInicial = raizModelo.localScale;
            pivoInicial = ManipuladorObjetoSelecionado.CentroVisual(raizModelo);
            fatorInicial = raizModelo.localScale.x / escalaOriginal;
            // Os pontos de partida (inicioA/inicioB) vêm do primeiro quadro em AtualizarDuasMaos.
            primeiroQuadroDuasMaos = true;
        }

        private bool primeiroQuadroDuasMaos;

        private void AtualizarDuasMaos(bool temPontos, Vector3 a, Vector3 b)
        {
            var soltou = !LerPinca(esquerda, out _) || !LerPinca(direita, out _);
            if (soltou || !temPontos || !PodeManipular())
            {
                foreach (var mao in new[] { esquerda, direita })
                {
                    Reservar(mao, false);
                    mao.estado = LerPinca(mao, out _) ? Estado.EsperandoSoltar : Estado.Livre;
                }

                MostrarLinha(false);
                return;
            }

            // Os pontos de partida são os do primeiro quadro com as duas mãos, para não haver salto.
            if (primeiroQuadroDuasMaos)
            {
                inicioA = a;
                inicioB = b;
                primeiroQuadroDuasMaos = false;
            }

            TransformarDuasMaos(inicioA, inicioB, a, b, posicaoInicial, rotacaoInicial, escalaInicial, pivoInicial, fatorInicial, out var posicao, out var rotacao, out var escala, out fatorAtual);
            raizModelo.SetPositionAndRotation(posicao, rotacao);
            raizModelo.localScale = escala;
            MostrarLinha(true, a, b);
            if (rotulo != null)
                rotulo.DefinirRotuloExterno("×" + fatorAtual.ToString("0.0", Cultura), (a + b) * 0.5f);
        }

        /// <summary>
        /// Transformação do gesto com as duas mãos, a partir do começo do gesto: o meio entre as mãos leva o modelo junto,
        /// o giro das mãos no plano horizontal gira o modelo em torno do seu centro e a distância entre elas o escala, com o
        /// fator total preso entre <see cref="EscalaMinima"/> e <see cref="EscalaMaxima"/> do tamanho original.
        /// </summary>
        public static void TransformarDuasMaos(Vector3 a0, Vector3 b0, Vector3 a, Vector3 b, Vector3 posicao0, Quaternion rotacao0, Vector3 escala0, Vector3 pivo0, float fator0,
            out Vector3 posicao, out Quaternion rotacao, out Vector3 escala, out float fatorTotal)
        {
            var distancia0 = Mathf.Max(0.05f, Vector3.Distance(a0, b0));
            var fatorBruto = Vector3.Distance(a, b) / distancia0;
            fator0 = Mathf.Max(1e-6f, fator0);
            fatorTotal = Mathf.Clamp(fator0 * fatorBruto, EscalaMinima, EscalaMaxima);
            var fator = fatorTotal / fator0;
            var horizontal0 = Vector3.ProjectOnPlane(b0 - a0, Vector3.up);
            var horizontal = Vector3.ProjectOnPlane(b - a, Vector3.up);
            var giroGraus = horizontal0.sqrMagnitude > 1e-6f && horizontal.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(horizontal0, horizontal, Vector3.up) : 0f;
            var giro = Quaternion.AngleAxis(giroGraus, Vector3.up);
            var pivo = pivo0 + ((a + b) - (a0 + b0)) * 0.5f;
            posicao = pivo + giro * ((posicao0 - pivo0) * fator);
            rotacao = giro * rotacao0;
            escala = escala0 * fator;
        }

        private void Reservar(Mao mao, bool reservar)
        {
            if (mao.reservada == reservar || mao.raiz == null)
                return;
            mao.reservada = reservar;
            PonteInteracaoMao.DefinirMaoReservada(mao.raiz.transform, reservar);
        }

        private bool ResolverMaos()
        {
            if (modalidade == null)
                return false;
            Ligar(esquerda, modalidade.leftHand);
            Ligar(direita, modalidade.rightHand);
            return true;
        }

        private static void Ligar(Mao mao, GameObject raiz)
        {
            if (mao.raiz == raiz && (raiz == null || mao.interator != null))
                return;
            mao.raiz = raiz;
            mao.interator = raiz != null ? raiz.GetComponentInChildren<NearFarInteractor>(true) : null;
        }

        private void MostrarLinha(bool visivel, Vector3 a = default, Vector3 b = default)
        {
            if (!visivel)
            {
                if (linhaDuasMaos != null && linhaDuasMaos.enabled)
                    linhaDuasMaos.enabled = false;
                return;
            }

            if (linhaDuasMaos == null)
            {
                var objeto = new GameObject("Linha_Duas_Maos");
                objeto.transform.SetParent(transform, false);
                linhaDuasMaos = objeto.AddComponent<LineRenderer>();
                linhaDuasMaos.useWorldSpace = true;
                linhaDuasMaos.positionCount = 2;
                linhaDuasMaos.widthMultiplier = 0.003f;
                linhaDuasMaos.numCapVertices = 4;
                linhaDuasMaos.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                linhaDuasMaos.material.color = TemaVRSNC.Destaque;
                linhaDuasMaos.startColor = linhaDuasMaos.endColor = TemaVRSNC.Destaque;
                linhaDuasMaos.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                linhaDuasMaos.receiveShadows = false;
            }

            linhaDuasMaos.enabled = true;
            linhaDuasMaos.SetPosition(0, a);
            linhaDuasMaos.SetPosition(1, b);
        }

        private void OnDestroy()
        {
            if (linhaDuasMaos != null && linhaDuasMaos.material != null)
                Destroy(linhaDuasMaos.material);
        }
    }
}
