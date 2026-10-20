using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.XR.Haptics;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Retorno tátil e sonoro das ações principais: vibração leve no controle direito ao apontar uma estrutura,
    /// média ao selecionar, entrar, voltar, isolar, ocultar e explodir, e forte nos dois controles ao resetar.
    /// Os sons são curtos e baixos. Durante o reset os passos intermediários ficam em silêncio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetornoSensorialInteracao : MonoBehaviour
    {
        [SerializeField]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [SerializeField]
        private ControladorVisibilidadeAnatomica controladorVisibilidade;
        [SerializeField]
        private ControladorVisaoExplodida controladorVisaoExplodida;
        [SerializeField]
        private GerenciadorRestauracao gerenciadorRestauracao;
        [SerializeField]
        private AudioClip somSelecao;
        [SerializeField]
        private AudioClip somNavegacao;
        [SerializeField]
        private AudioClip somReset;
        [SerializeField, Range(0f, 1f)]
        private float volume = 0.35f;
        // Evita sons e vibrações empilhados quando uma ação dispara vários avisos no mesmo instante.
        private const float IntervaloMinimoSegundos = 0.08f;
        // Ignora os avisos da montagem inicial da cena.
        private const float SilencioInicialSegundos = 0.75f;
        private AudioSource fonteAudio;
        private float ultimoRetorno = float.NegativeInfinity;
        private float inicio;
        private bool restaurando;

        private void Awake()
        {
            if (gerenciadorSelecao == null)
                gerenciadorSelecao = GetComponent<GerenciadorSelecaoHierarquica>();
            if (controladorVisibilidade == null)
                controladorVisibilidade = GetComponent<ControladorVisibilidadeAnatomica>();
            if (controladorVisaoExplodida == null)
                controladorVisaoExplodida = GetComponent<ControladorVisaoExplodida>();
            if (gerenciadorRestauracao == null)
                gerenciadorRestauracao = GetComponent<GerenciadorRestauracao>();
            fonteAudio = gameObject.AddComponent<AudioSource>();
            fonteAudio.playOnAwake = false;
            fonteAudio.spatialBlend = 0f;
            fonteAudio.volume = volume;
        }

        private void OnEnable()
        {
            inicio = Time.unscaledTime;
            if (gerenciadorSelecao != null)
            {
                gerenciadorSelecao.emNoEmFoco.AddListener(AoApontar);
                gerenciadorSelecao.emNoSelecionado.AddListener(AoSelecionar);
                gerenciadorSelecao.emFocoAlterado.AddListener(AoNavegar);
            }

            if (controladorVisibilidade != null)
                controladorVisibilidade.emVisibilidadeAlterada += AoAlterarVisibilidade;
            if (controladorVisaoExplodida != null)
                controladorVisaoExplodida.emExpansaoAlternada += AoAlterarVisibilidade;
            if (gerenciadorRestauracao != null)
            {
                gerenciadorRestauracao.emRestaurando += AoIniciarReset;
                gerenciadorRestauracao.emRestaurado += AoConcluirReset;
            }
        }

        private void OnDisable()
        {
            if (gerenciadorSelecao != null)
            {
                gerenciadorSelecao.emNoEmFoco.RemoveListener(AoApontar);
                gerenciadorSelecao.emNoSelecionado.RemoveListener(AoSelecionar);
                gerenciadorSelecao.emFocoAlterado.RemoveListener(AoNavegar);
            }

            if (controladorVisibilidade != null)
                controladorVisibilidade.emVisibilidadeAlterada -= AoAlterarVisibilidade;
            if (controladorVisaoExplodida != null)
                controladorVisaoExplodida.emExpansaoAlternada -= AoAlterarVisibilidade;
            if (gerenciadorRestauracao != null)
            {
                gerenciadorRestauracao.emRestaurando -= AoIniciarReset;
                gerenciadorRestauracao.emRestaurado -= AoConcluirReset;
            }
        }

        private void AoApontar(NoInteracaoAnatomica no)
        {
            if (no != null && PodeResponder(false))
                Vibrar(CommonUsages.RightHand, 0.12f, 0.02f);
        }

        private void AoSelecionar(NoInteracaoAnatomica _) => Responder(somSelecao, 0.35f, 0.05f);
        private void AoNavegar(NoInteracaoAnatomica _) => Responder(somNavegacao, 0.35f, 0.05f);
        private void AoAlterarVisibilidade() => Responder(somSelecao, 0.3f, 0.05f);
        private void AoIniciarReset() => restaurando = true;

        private void AoConcluirReset()
        {
            restaurando = false;
            if (Time.unscaledTime - inicio < SilencioInicialSegundos)
                return;
            ultimoRetorno = Time.unscaledTime;
            Tocar(somReset != null ? somReset : somNavegacao);
            Vibrar(CommonUsages.RightHand, 0.7f, 0.15f);
            Vibrar(CommonUsages.LeftHand, 0.7f, 0.15f);
        }

        private void Responder(AudioClip som, float amplitude, float duracao)
        {
            if (!PodeResponder(true))
                return;
            ultimoRetorno = Time.unscaledTime;
            Tocar(som);
            Vibrar(CommonUsages.RightHand, amplitude, duracao);
        }

        private bool PodeResponder(bool respeitarIntervalo)
        {
            if (!Application.isPlaying || restaurando || Time.unscaledTime - inicio < SilencioInicialSegundos)
                return false;
            return !respeitarIntervalo || Time.unscaledTime - ultimoRetorno >= IntervaloMinimoSegundos;
        }

        private void Tocar(AudioClip som)
        {
            if (som == null || fonteAudio == null)
                return;
            fonteAudio.volume = volume;
            fonteAudio.PlayOneShot(som);
        }

        /// <summary>Comando de vibração padrão do Input System para controles XR; dispositivos sem vibração o ignoram.</summary>
        private static void Vibrar(InternedString mao, float amplitude, float duracaoSegundos)
        {
            var controle = InputSystem.GetDevice<XRController>(mao);
            if (controle == null)
                return;
            var comando = SendHapticImpulseCommand.Create(0, amplitude, duracaoSegundos);
            controle.ExecuteCommand(ref comando);
        }
    }
}
