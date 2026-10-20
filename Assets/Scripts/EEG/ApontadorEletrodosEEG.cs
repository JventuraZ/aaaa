using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VRSNC.Interacao;
using VRSNC.Interface;

namespace VRSNC.EEG
{
    /// <summary>
    /// Deixa apontar e tocar os eletrodos como a anatomia: com o raio do controle direito (gatilho) e
    /// com o raio de cada mão rastreada (pinça). Não usa física: testa o raio contra a esfera de cada
    /// eletrodo (um pouco maior que a visível, para ficar fácil de acertar). Apontado, o eletrodo
    /// cresce e o nome aparece junto ao raio.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ControladorEEG), typeof(CabecaEEG))]
    public sealed class ApontadorEletrodosEEG : MonoBehaviour, IAlvoRaioPrioritario
    {
        // Folga de acerto: o raio do Quest treme alguns milímetros a 60 cm.
        private const float FatorRaioAcerto = 1.8f;
        private const float RaioAcertoMinimoMetros = 0.007f;
        // Mesmo alcance do raio do controle (ControladorSelecaoPorRaio).
        private const float AlcanceMaoMetros = 6f;

        private ControladorEEG controlador;
        private CabecaEEG cabeca;
        private RotuloEstruturaApontada rotulo;
        private XRInputModalityManager modalidade;
        private bool modalidadeProcurada;
        private readonly MaoApontadora[] maos = { new MaoApontadora(), new MaoApontadora() };
        private readonly RaycastHit[] acertosAnatomia = new RaycastHit[16];
        // Candidato do último teste do raio do controle e o eletrodo que esse raio aponta.
        private int candidato = -1;
        private int apontadoControle = -1;
        // Mão que detém o apontamento quando o controle não aponta nada (0 = esquerda, 1 = direita).
        private int maoDona = -1;
        private int apontado = -1;

        /// <summary>Uma mão rastreada: o raio distante do NearFar do rig XRI e o eletrodo que ele aponta.</summary>
        private sealed class MaoApontadora
        {
            public GameObject raiz;
            public NearFarInteractor interator;
            public int canal = -1;
            public bool reservada;
        }

        /// <summary>Eletrodo apontado agora, pelo controle ou por uma mão (-1 se nenhum).</summary>
        public int Apontado => apontado;

        private void Awake()
        {
            controlador = GetComponent<ControladorEEG>();
            cabeca = GetComponent<CabecaEEG>();
        }

        private void OnEnable() => ControladorSelecaoPorRaio.RegistrarAlvoPrioritario(this);

        private void OnDisable()
        {
            ControladorSelecaoPorRaio.RemoverAlvoPrioritario(this);
            LiberarMaos();
            DefinirApontado(false);
        }

        private void Update()
        {
            AtualizarMaos();
            AplicarApontado();
            if (apontado < 0 || !cabeca.Visivel)
                return;
            if (rotulo == null)
                rotulo = FindFirstObjectByType<RotuloEstruturaApontada>();
            if (rotulo == null)
                return;
            var canal = controlador.Dados.canais[apontado];
            var regiao = controlador.Dados.RegiaoDe(apontado);
            var texto = canal.nome + "  ·  eletrodo";
            if (regiao != null && regiao.chave != "unknown")
                texto += "  ·  " + regiao.nome + (canal.linhaMedia ? "" : canal.hemisferioEsquerdo ? " (E)" : " (D)");
            rotulo.DefinirRotuloExterno(texto, cabeca.PosicaoEletrodo(apontado));
        }

        public bool TentarAcertar(Ray raio, float distanciaMaxima, out float distancia)
        {
            candidato = -1;
            distancia = float.PositiveInfinity;
            if (!isActiveAndEnabled || !cabeca.Visivel)
                return false;
            return TentarEletrodoMaisProximo(raio, distanciaMaxima, out candidato, out distancia);
        }

        public void DefinirApontado(bool valor)
        {
            apontadoControle = valor ? candidato : -1;
            AplicarApontado();
        }

        public void Confirmar()
        {
            if (apontadoControle >= 0)
                controlador.SelecionarCanal(apontadoControle, true);
        }

        /// <summary>
        /// Eletrodo que um raio de mão aponta, ou -1. O painel e a anatomia à frente do eletrodo
        /// (ex.: estrutura separada na vista explodida) ficam com o raio.
        /// </summary>
        public int EletrodoNoRaioDaMao(Ray raio)
        {
            if (!isActiveAndEnabled || !cabeca.Visivel)
                return -1;
            var alcance = AlcanceMaoMetros;
            if (ZonaProximidadePainelInterface.TentarObterDistanciaDeBloqueioDoRaio(raio, alcance, out var distanciaPainel))
                alcance = Mathf.Min(alcance, distanciaPainel);
            if (!TentarEletrodoMaisProximo(raio, alcance, out var canal, out var distancia))
                return -1;
            return AnatomiaAntes(raio, distancia) ? -1 : canal;
        }

        private void AtualizarMaos()
        {
            if (!cabeca.Visivel || !ResolverMaos())
            {
                LiberarMaos();
                return;
            }

            for (var lado = 0; lado < maos.Length; lado++)
            {
                var mao = maos[lado];
                mao.canal = EletrodoApontadoPelaMao(mao);
                var reservar = mao.canal >= 0;
                if (reservar != mao.reservada)
                {
                    mao.reservada = reservar;
                    PonteInteracaoMao.DefinirMaoReservada(mao.raiz != null ? mao.raiz.transform : null, reservar);
                }

                // A pinça confirma o eletrodo desta mão, como o gatilho no controle.
                if (mao.canal >= 0 && mao.interator.selectInput != null && mao.interator.selectInput.ReadWasPerformedThisFrame())
                {
                    maoDona = lado;
                    controlador.SelecionarCanal(mao.canal, true);
                }
            }

            if (maoDona < 0 || maos[maoDona].canal < 0)
                maoDona = maos[1].canal >= 0 ? 1 : maos[0].canal >= 0 ? 0 : -1;
        }

        private int EletrodoApontadoPelaMao(MaoApontadora mao)
        {
            var interator = mao.interator;
            // Só a mão rastreada e livre aponta: segurando uma estrutura ou o painel, o raio é dela.
            if (mao.raiz == null || !mao.raiz.activeInHierarchy || interator == null || !interator.isActiveAndEnabled || interator.hasSelection)
                return -1;
            var lancador = interator.farInteractionCaster;
            var origem = lancador != null && lancador.effectiveCastOrigin != null ? lancador.effectiveCastOrigin : interator.transform;
            // Perto do painel a mão usa o toque e o raio de interface.
            if (ZonaProximidadePainelInterface.PontoEstaProximoDoPainel(origem.position))
                return -1;
            return EletrodoNoRaioDaMao(new Ray(origem.position, origem.forward));
        }

        private bool ResolverMaos()
        {
            if (modalidade == null && !modalidadeProcurada)
            {
                modalidadeProcurada = true;
                modalidade = FindFirstObjectByType<XRInputModalityManager>();
            }

            if (modalidade == null)
                return false;
            Ligar(maos[0], modalidade.leftHand);
            Ligar(maos[1], modalidade.rightHand);
            return true;
        }

        private static void Ligar(MaoApontadora mao, GameObject raiz)
        {
            if (mao.raiz == raiz && (raiz == null || mao.interator != null))
                return;
            mao.raiz = raiz;
            mao.interator = raiz != null ? raiz.GetComponentInChildren<NearFarInteractor>(true) : null;
        }

        private void LiberarMaos()
        {
            foreach (var mao in maos)
            {
                if (mao.reservada)
                    PonteInteracaoMao.DefinirMaoReservada(mao.raiz != null ? mao.raiz.transform : null, false);
                mao.reservada = false;
                mao.canal = -1;
            }

            maoDona = -1;
        }

        private void AplicarApontado()
        {
            var novo = apontadoControle >= 0 ? apontadoControle : maoDona >= 0 ? maos[maoDona].canal : -1;
            if (novo == apontado)
                return;
            apontado = novo;
            if (cabeca != null)
                cabeca.DefinirApontado(apontado);
        }

        private bool AnatomiaAntes(Ray raio, float distancia)
        {
            var gerenciador = controlador.GerenciadorSelecao;
            var quantidade = Physics.RaycastNonAlloc(raio, acertosAnatomia, distancia, ~0, QueryTriggerInteraction.Collide);
            for (var indice = 0; indice < quantidade; indice++)
            {
                var colisor = acertosAnatomia[indice].collider;
                var no = colisor != null ? colisor.GetComponentInParent<NoInteracaoAnatomica>() : null;
                if (no != null && (gerenciador == null || gerenciador.ResolverCandidato(no) != null))
                    return true;
            }

            return false;
        }

        private bool TentarEletrodoMaisProximo(Ray raio, float distanciaMaxima, out int canalMaisProximo, out float distancia)
        {
            canalMaisProximo = -1;
            distancia = float.PositiveInfinity;
            var raioAcerto = Mathf.Max(RaioAcertoMinimoMetros, cabeca.RaioEletrodoMundo * FatorRaioAcerto);
            for (var canal = 0; canal < cabeca.QuantidadeEletrodos; canal++)
            {
                if (TentarEsfera(raio, cabeca.PosicaoEletrodo(canal), raioAcerto, out var d) && d <= distanciaMaxima && d < distancia)
                {
                    distancia = d;
                    canalMaisProximo = canal;
                }
            }

            return canalMaisProximo >= 0;
        }

        private static bool TentarEsfera(Ray raio, Vector3 centro, float raioEsfera, out float distancia)
        {
            var paraCentro = centro - raio.origin;
            var projecao = Vector3.Dot(paraCentro, raio.direction);
            distancia = projecao;
            if (projecao < 0f)
                return false;
            var distanciaQuadrada = paraCentro.sqrMagnitude - projecao * projecao;
            var raioQuadrado = raioEsfera * raioEsfera;
            if (distanciaQuadrada > raioQuadrado)
                return false;
            distancia = projecao - Mathf.Sqrt(raioQuadrado - distanciaQuadrada);
            return true;
        }
    }
}
