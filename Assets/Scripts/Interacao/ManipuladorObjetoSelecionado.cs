using UnityEngine;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Manipulador da Estrutura Selecionada
    // Nome técnico do script: ManipuladorObjetoSelecionado
    // Função: move, rotaciona ou agarra a estrutura selecionada, usando o controle direito do Meta Quest 2.
    // Relação com o protótipo: preserva a seleção em camadas e manipula apenas o alvo ativo sem alterar colliders,
    // XR Grab Interactable ou XR Ray Interactor.
    // Objetos da cena: usado no Sistema_Nervoso_Central.
    /// <summary>
    /// Aplica manipulação direta ao nó selecionado; se não houver seleção, usa o alvo raiz configurado.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainSelectedObjectManipulator")]
    public sealed class ManipuladorObjetoSelecionado : MonoBehaviour
    {
        // Alvo de fallback quando nenhuma estrutura anatômica está selecionada.
        [UnityEngine.Serialization.FormerlySerializedAs("raizalvo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rootTarget")]
        private Transform alvoRaiz;
        // Gerenciador que informa qual estrutura está selecionada no momento.
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        // Câmera usada para interpretar movimento para frente/trás e eixo vertical de rotação.
        [UnityEngine.Serialization.FormerlySerializedAs("cameratransformacao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("cameraTransform")]
        private Transform transformacaoCamera;
        // Zona morta do analógico para evitar tremores involuntários.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("deadzone")]
        private float zonaMorta = 0.18f;
        // Velocidade do movimento no eixo vertical do analógico.
        [UnityEngine.Serialization.FormerlySerializedAs("movervelocidade")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("moveSpeed")]
        private float velocidadeMovimento = 0.45f;
        // Velocidade de rotação horizontal do analógico.
        [UnityEngine.Serialization.FormerlySerializedAs("rotacaovelocidade")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rotationSpeed")]
        private float velocidadeRotacao = 85f;
        // Mantém a rotação alinhada ao "para cima" da câmera, deixando o controle natural em VR.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rotateAroundCameraUp")]
        private bool girarEmTornoCameraCima = true;
        // Corrige o comportamento atual do thumbstick horizontal: direita gira/move para direita, esquerda para esquerda.
        [SerializeField]
        private bool inverterEixoHorizontal = true;
        // Suavização do grab em posição para que o alvo siga a mão de modo firme, mas sem saltos bruscos.
        [UnityEngine.Serialization.FormerlySerializedAs("agarrePosicaoSuavizacao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("grabPositionSmoothing")]
        private float suavizacaoPosicaoAgarre = 18f;
        // Suavização do grab em rotação para acompanhar a orientação da mão sem trepidação excessiva.
        [UnityEngine.Serialization.FormerlySerializedAs("agarreRotacaoSuavizacao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("grabRotationSmoothing")]
        private float suavizacaoRotacaoAgarre = 18f;
        // Estado temporário usado enquanto o grip está segurando uma estrutura.
        private Transform alvoAgarreAtivo;
        private Vector3 posicaoInicialAgarrador;
        private Quaternion rotacaoInicialAgarrador;
        private Vector3 posicaoInicialAlvo;
        private Quaternion rotacaoInicialAlvo;
        private NoInteracaoAnatomica selecaoAgarre;
        private NoInteracaoAnatomica focoAgarre;
        private uint agarrarVersao;
        private bool agarrando;
        private bool girarEmTornoDoPontoDeAgarre;
        public Transform AlvoAtivo => ResolverAlvoAtivo();
        public bool EstaAgarrando => agarrando;
        public uint VersaoAgarre => agarrarVersao;
        /// <summary>
        /// Resolve referências locais e câmera principal quando necessário.
        /// </summary>
        private void Awake()
        {
            if (alvoRaiz == null)
            {
                alvoRaiz = transform;
            }

            if (gerenciadorSelecao == null)
            {
                gerenciadorSelecao = GetComponent<GerenciadorSelecaoHierarquica>();
            }

            if (transformacaoCamera == null && Camera.main != null)
            {
                transformacaoCamera = Camera.main.transform;
            }
        }

        /// <summary>
        /// Configura as referências principais sem criar novos componentes.
        /// </summary>
        public void Configurar(Transform raiz, GerenciadorSelecaoHierarquica selecao, Transform camera)
        {
            alvoRaiz = raiz != null ? raiz : transform;
            gerenciadorSelecao = selecao;
            transformacaoCamera = camera;
        }

        /// <summary>
        /// Inicia o agarrar pelo grip, capturando pose inicial do controle e do alvo.
        /// </summary>
        public void IniciarAgarre(Transform agarrador)
        {
            IniciarAgarre(agarrador, false);
        }

        /// <summary>Preserva o ponto de grip original para mãos rastreadas e grips de controle.</summary>
        public void IniciarAgarre(Transform agarrador, bool manterPontoDeAgarre)
        {
            var alvo = ResolverAlvoAtivo();
            if (alvo == null || agarrador == null)
            {
                return;
            }

            alvoAgarreAtivo = alvo;
            posicaoInicialAgarrador = agarrador.position;
            rotacaoInicialAgarrador = agarrador.rotation;
            posicaoInicialAlvo = alvo.position;
            rotacaoInicialAlvo = alvo.rotation;
            selecaoAgarre = gerenciadorSelecao != null ? gerenciadorSelecao.NoSelecionado : null;
            focoAgarre = gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null;
            unchecked
            {
                agarrarVersao++;
            }

            agarrando = true;
            girarEmTornoDoPontoDeAgarre = manterPontoDeAgarre;
        }

        /// <summary>
        /// Atualiza posição e rotação do alvo enquanto o grip permanece pressionado.
        /// </summary>
        public void AtualizarAgarre(Transform agarrador)
        {
            AtualizarAgarre(agarrador, Time.deltaTime);
        }

        /// <summary>
        /// Atualiza uma etapa de captura usando um tempo decorrido explícito. Chamadores em execução usam a
        /// sobrecarga de um argumento; a forma explícita mantém a mesma matemática de movimento
        /// disponível para verificações determinísticas de regressão no Editor.
        /// </summary>
        public void AtualizarAgarre(Transform agarrador, float variacaoTempo)
        {
            if (!agarrando || alvoAgarreAtivo == null || agarrador == null)
            {
                return;
            }

            if (gerenciadorSelecao != null && (gerenciadorSelecao.NoSelecionado != selecaoAgarre || gerenciadorSelecao.NoEmFoco != focoAgarre || ResolverAlvoAtivo() != alvoAgarreAtivo))
            {
                EncerrarAgarre();
                return;
            }

            var variacaoRotacao = agarrador.rotation * Quaternion.Inverse(rotacaoInicialAgarrador);
            var posicaoDesejada = girarEmTornoDoPontoDeAgarre ? agarrador.position + variacaoRotacao * (posicaoInicialAlvo - posicaoInicialAgarrador) : posicaoInicialAlvo + (agarrador.position - posicaoInicialAgarrador);
            var rotacaoDesejada = variacaoRotacao * rotacaoInicialAlvo;
            // O alvo continua sendo somente a seleção atual, ou o SNC raiz quando nada está selecionado.
            // A interpolação melhora a fluidez do grip sem mudar a hierarquia nem interferir em pais/filhos.
            var decorrido = Mathf.Max(0f, variacaoTempo);
            var posicaoCombinacao = 1f - Mathf.Exp(-Mathf.Max(0.01f, suavizacaoPosicaoAgarre) * decorrido);
            var rotacaoCombinacao = 1f - Mathf.Exp(-Mathf.Max(0.01f, suavizacaoRotacaoAgarre) * decorrido);
            var rotacaoSuavizada = Quaternion.Slerp(alvoAgarreAtivo.rotation, rotacaoDesejada, rotacaoCombinacao);
            if (girarEmTornoDoPontoDeAgarre)
            {
                // Mantém o ponto de anexo original do interactor restrito enquanto a rotação
                // é suavizada; interpolar o centro independentemente o faria escorregar.
                var variacaoRotacaoSuavizada = rotacaoSuavizada * Quaternion.Inverse(rotacaoInicialAlvo);
                var posicaoLimitada = agarrador.position + variacaoRotacaoSuavizada * (posicaoInicialAlvo - posicaoInicialAgarrador);
                alvoAgarreAtivo.SetPositionAndRotation(posicaoLimitada, rotacaoSuavizada);
            }
            else
            {
                alvoAgarreAtivo.position = Vector3.Lerp(alvoAgarreAtivo.position, posicaoDesejada, posicaoCombinacao);
                alvoAgarreAtivo.rotation = rotacaoSuavizada;
            }
        }

        /// <summary>
        /// Finaliza o agarrar sem remover ou reconfigurar componentes da estrutura.
        /// </summary>
        public void EncerrarAgarre()
        {
            agarrando = false;
            alvoAgarreAtivo = null;
            selecaoAgarre = null;
            focoAgarre = null;
        }

        /// <summary>
        /// Usa o analógico para aproximar/afastar e rotacionar o alvo ativo.
        /// O eixo horizontal pode ser invertido por configuração para corrigir o sentido percebido no Meta Quest 2.
        /// </summary>
        public void Manipular(Vector2 entrada)
        {
            if (agarrando || entrada.sqrMagnitude < zonaMorta * zonaMorta)
            {
                return;
            }

            var alvo = ResolverAlvoAtivo();
            if (alvo == null)
            {
                return;
            }

            if (inverterEixoHorizontal)
            {
                entrada.x = -entrada.x;
            }

            var direcaoFrenteCamera = ResolverDirecaoFrenteCamera();
            if (Mathf.Abs(entrada.y) >= zonaMorta)
            {
                alvo.position += direcaoFrenteCamera * entrada.y * velocidadeMovimento * Time.deltaTime;
            }

            if (Mathf.Abs(entrada.x) >= zonaMorta)
            {
                var eixo = girarEmTornoCameraCima ? ResolverDirecaoCimaCamera() : Vector3.up;
                alvo.Rotate(eixo, entrada.x * velocidadeRotacao * Time.deltaTime, Space.World);
            }
        }

        /// <summary>
        /// Arraste da mão no ar: gira o alvo ativo em torno do seu centro visual. O horizontal gira em torno do eixo
        /// vertical da câmera e o vertical em torno do eixo horizontal dela, para a face voltada ao usuário acompanhar a mão.
        /// </summary>
        public void GirarPorArraste(Transform alvo, Vector3 pivo, float grausHorizontal, float grausVertical)
        {
            if (alvo == null || agarrando)
            {
                return;
            }

            var rotacao = Quaternion.AngleAxis(grausHorizontal, ResolverDirecaoCimaCamera()) * Quaternion.AngleAxis(grausVertical, ResolverDirecaoDireitaCamera());
            GirarEmTornoDoPivo(alvo, pivo, rotacao);
        }

        /// <summary>Aplica uma rotação de mundo ao alvo mantendo o pivô parado.</summary>
        public static void GirarEmTornoDoPivo(Transform alvo, Vector3 pivo, Quaternion rotacao)
        {
            alvo.SetPositionAndRotation(pivo + rotacao * (alvo.position - pivo), rotacao * alvo.rotation);
        }

        /// <summary>Centro dos renderers ativos do alvo, no mundo; sem renderer, a própria posição.</summary>
        public static Vector3 CentroVisual(Transform alvo)
        {
            var limites = new Bounds(alvo.position, Vector3.zero);
            var primeiro = true;
            foreach (var renderizador in alvo.GetComponentsInChildren<Renderer>())
            {
                if (!renderizador.enabled)
                {
                    continue;
                }

                if (primeiro)
                {
                    limites = renderizador.bounds;
                    primeiro = false;
                }
                else
                {
                    limites.Encapsulate(renderizador.bounds);
                }
            }

            return limites.center;
        }

        private Vector3 ResolverDirecaoDireitaCamera()
        {
            var camera = transformacaoCamera != null ? transformacaoCamera : Camera.main != null ? Camera.main.transform : null;
            return camera != null ? camera.right.normalized : Vector3.right;
        }

        /// <summary>
        /// Atualiza a câmera usada como referência dos movimentos.
        /// </summary>
        public void DefinirTransformacaoCamera(Transform camera)
        {
            transformacaoCamera = camera;
        }

        /// <summary>
        /// Atualiza o alvo raiz de fallback.
        /// </summary>
        public void DefinirAlvoRaiz(Transform alvo)
        {
            alvoRaiz = alvo;
        }

        /// <summary>
        /// Prioriza a estrutura selecionada; sem seleção, usa o grupo em foco e, no nível inicial, o SNC completo.
        /// </summary>
        private Transform ResolverAlvoAtivo()
        {
            var selecionado = gerenciadorSelecao != null ? gerenciadorSelecao.NoSelecionado : null;
            if (selecionado != null)
            {
                return selecionado.transform;
            }

            // Clicar num grupo entra nele sem selecioná-lo; o grupo aberto continua manipulável.
            var foco = gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null;
            if (foco != null && foco.NoPai != null)
            {
                return foco.transform;
            }

            return alvoRaiz != null ? alvoRaiz : transform;
        }

        /// <summary>
        /// Obtém o vetor frontal da câmera para movimento de aproximação/afastamento.
        /// </summary>
        private Vector3 ResolverDirecaoFrenteCamera()
        {
            var camera = transformacaoCamera != null ? transformacaoCamera : Camera.main != null ? Camera.main.transform : null;
            if (camera == null)
            {
                return transform.forward;
            }

            return camera.forward.normalized;
        }

        /// <summary>
        /// Obtém o eixo vertical da câmera para rotação horizontal mais natural em VR.
        /// </summary>
        private Vector3 ResolverDirecaoCimaCamera()
        {
            var camera = transformacaoCamera != null ? transformacaoCamera : Camera.main != null ? Camera.main.transform : null;
            return camera != null ? camera.up.normalized : Vector3.up;
        }
    }
}