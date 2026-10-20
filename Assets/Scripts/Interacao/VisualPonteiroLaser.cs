using UnityEngine;
using VRSNC.Interface;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Visual do Ponteiro Direito do SNC
    // Nome técnico do script: VisualPonteiroLaser
    // Função: desenha uma linha visual para o raio de seleção e muda a cor quando há hover anatômico.
    // Relação com o protótipo: complementa o raycast do controle direito sem substituir XR Ray Interactor ou lógica de seleção.
    // Objetos da cena: usado no Ponteiro_Direito_SNC.
    /// <summary>
    /// Renderiza o laser visual usado para apontar estruturas anatômicas.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainLaserPointerVisual")]
    public sealed class VisualPonteiroLaser : MonoBehaviour
    {
        // Controlador que informa origem ativa e nó em hover.
        [UnityEngine.Serialization.FormerlySerializedAs("raioselecaocontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("raySelectionController")]
        private ControladorSelecaoPorRaio controladorSelecaoPorRaio;
        // Origem visual opcional; normalmente o controle direito.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rayOrigin")]
        private Transform raioOrigem;
        // Comprimento e espessura do laser.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("length")]
        private float comprimento = 6f;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("width")]
        private float largura = 0.008f;
        // Cores para estado neutro e estado de hover sobre estrutura válida.
        [UnityEngine.Serialization.FormerlySerializedAs("inativoCor")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("idleColor")]
        private Color corInativa = new(0.1f, 0.75f, 1f, 0.75f);
        [UnityEngine.Serialization.FormerlySerializedAs("fococor")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("hoverColor")]
        private Color corFoco = new(1f, 0.85f, 0.25f, 0.95f);
        // Evita mostrar o laser no editor quando a cena não está em execução.
        [UnityEngine.Serialization.FormerlySerializedAs("mostrarApenasEmExecucaoModo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("showOnlyInPlayMode")]
        private bool mostrarSomenteEmModoExecucao = true;
        // Componentes/runtime assets criados para desenhar a linha.
        private LineRenderer renderizadorLinha;
        private Material materialExecucao;
        /// <summary>
        /// Prepara o LineRenderer ao iniciar.
        /// </summary>
        private void Awake()
        {
            renderizadorLinha = GetComponent<LineRenderer>();
            ConfigurarRenderizadorLinha();
        }

        /// <summary>
        /// Remove o material criado em runtime para evitar resíduos no editor.
        /// </summary>
        private void OnDestroy()
        {
            if (materialExecucao != null)
            {
                Destroy(materialExecucao);
            }
        }

        /// <summary>
        /// Atualiza posição, direção e cor do laser depois dos movimentos do frame.
        /// </summary>
        private void LateUpdate()
        {
            if (mostrarSomenteEmModoExecucao && !Application.isPlaying)
            {
                renderizadorLinha.enabled = false;
                return;
            }

            var origem = raioOrigem != null ? raioOrigem : controladorSelecaoPorRaio != null ? controladorSelecaoPorRaio.OrigemRaioAtiva : transform;
            if (origem == null)
            {
                renderizadorLinha.enabled = false;
                return;
            }

            // Com a mão rastreada no lugar do controle, a origem estabilizada fica parada na última pose
            // do controle: sem laser, para não sobrar um raio congelado no ar.
            if (controladorSelecaoPorRaio != null && !controladorSelecaoPorRaio.OrigemRaioDisponivel(origem))
            {
                renderizadorLinha.enabled = false;
                return;
            }

            // Near/poke possui o painel enquanto a mão/controle está dentro da sua
            // zona de proximidade. Não desenha um raio através da UI durante essa transição.
            if (ZonaProximidadePainelInterface.PontoEstaProximoDoPainel(origem.position))
            {
                renderizadorLinha.enabled = false;
                return;
            }

            var comprimentoVisual = Mathf.Min(comprimento, controladorSelecaoPorRaio != null ? controladorSelecaoPorRaio.DistanciaMaxima : comprimento);
            if (ZonaProximidadePainelInterface.TentarObterDistanciaDeBloqueioDoRaio(new Ray(origem.position, origem.forward), comprimento, out var interfaceBloqueioDistancia))
            {
                comprimentoVisual = Mathf.Min(comprimentoVisual, interfaceBloqueioDistancia);
            }

            renderizadorLinha.enabled = true;
            renderizadorLinha.SetPosition(0, origem.position);
            renderizadorLinha.SetPosition(1, origem.position + origem.forward * comprimentoVisual);
            var cor = controladorSelecaoPorRaio != null && controladorSelecaoPorRaio.EmFocoNo != null ? corFoco : corInativa;
            renderizadorLinha.startColor = cor;
            renderizadorLinha.endColor = new Color(cor.r, cor.g, cor.b, 0.05f);
        }

        /// <summary>
        /// Configura controlador de raycast e origem do ponteiro.
        /// </summary>
        public void Configurar(ControladorSelecaoPorRaio raioControlador, Transform origem)
        {
            controladorSelecaoPorRaio = raioControlador;
            raioOrigem = origem;
        }

        /// <summary>
        /// Define parâmetros básicos do LineRenderer sem depender de assets externos.
        /// </summary>
        private void ConfigurarRenderizadorLinha()
        {
            renderizadorLinha.positionCount = 2;
            renderizadorLinha.useWorldSpace = true;
            renderizadorLinha.startWidth = largura;
            renderizadorLinha.endWidth = largura * 0.2f;
            renderizadorLinha.numCapVertices = 4;
            renderizadorLinha.numCornerVertices = 2;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                materialExecucao = new Material(shader)
                {
                    name = "Runtime_BrainLaserPointer"
                };
                renderizadorLinha.material = materialExecucao;
            }
        }
    }
}