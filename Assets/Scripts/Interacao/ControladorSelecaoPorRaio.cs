using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using VRSNC.Interface;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Alvo que disputa o raio do controle com a anatomia, como os eletrodos do EEG. Não usa física:
    /// cada alvo testa o raio sozinho. Ganha quando está mais perto que a estrutura anatômica atingida,
    /// e então o gatilho confirma o alvo em vez de selecionar a anatomia.
    /// </summary>
    public interface IAlvoRaioPrioritario
    {
        /// <summary>Distância do primeiro acerto ao longo do raio, até a distância máxima.</summary>
        bool TentarAcertar(Ray raio, float distanciaMaxima, out float distancia);

        /// <summary>O alvo ganhou (verdadeiro) ou deixou de ganhar (falso) o raio.</summary>
        void DefinirApontado(bool apontado);

        /// <summary>O gatilho foi apertado com o alvo apontado.</summary>
        void Confirmar();
    }

    // Nome funcional em português: Controlador de Seleção por Raio
    // Nome técnico do script: ControladorSelecaoPorRaio
    // Função: lança raycast a partir do controle/câmera e informa ao gerenciador qual nó anatômico está em hover.
    // Relação com o protótipo: mantém o ponteiro do Meta Quest 2 apontando para estruturas do SNC sem trocar XR Ray Interactor.
    // Objetos da cena: usado no Sistema_Nervoso_Central, com origem configurada para o controle direito ou fallback da câmera.
    /// <summary>
    /// Faz a ponte entre o raio físico/visual e a seleção anatômica.
    /// Ele não seleciona qualquer collider livremente: primeiro encontra BrainInteractionNode e depois pede validação ao gerenciador.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainRaySelectionController")]
    public sealed class ControladorSelecaoPorRaio : MonoBehaviour
    {
        // Origem do raio, normalmente o controle direito do Meta Quest 2.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rayOrigin")]
        private Transform raioOrigem;
        // Alcance máximo usado pelas consultas físicas do raio.
        [UnityEngine.Serialization.FormerlySerializedAs("maximoDistancia")]
        [SerializeField, Min(0.01f)]
        [UnityEngine.Serialization.FormerlySerializedAs("maxDistance")]
        private float distanciaMaxima = 6f;
        // Máscara de camadas mantida ampla para preservar colliders existentes da cena.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("layerMask")]
        private LayerMask camadaMascara = ~0;
        // Gerenciador que valida hover e seleção por camada anatômica.
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        // Permite usar a câmera como origem em editor/testes quando o controle não está disponível.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("useMainCameraFallback")]
        private bool usarPrincipalCameraAlternativa = true;
        // Desenha o raio no editor para depuração sem afetar a experiência VR.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("drawDebugRay")]
        private bool desenharDepuracaoRaio;
        // Reutiliza o buffer no caminho normal de hover. Se ele saturar, o fallback abaixo
        // conserva a semântica de escolher o candidato válido mais próximo.
        private const int RaioBufferCapacidade = 16;
        private readonly RaycastHit[] raioBuffer = new RaycastHit[RaioBufferCapacidade];
        [Header("Realce Estável")]
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("candidateSwitchTolerance")]
        private float opcaoTrocaTolerancia = 0.015f;
        [SerializeField, Range(0.06f, 0.10f)]
        [UnityEngine.Serialization.FormerlySerializedAs("candidateSwitchDwellSeconds")]
        private float opcaoTrocaEsperaSegundos = 0.08f;
        // Estas referências são mantidas deliberadamente no proprietário do raio, não nos
        // dados anatômicos. Elas evitam trocas de ordem de collider em um frame sem alocar
        // nem alterar os colliders/camadas originais.
        private HistereseCandidatoSelecao histereseCandidato;
        private static readonly List<IAlvoRaioPrioritario> alvosPrioritarios = new();
        private IAlvoRaioPrioritario alvoPrioritarioApontado;
        private Transform origemEstabilizadorEmCache;
        private XRTransformStabilizer estabilizadorEmCache;
        public IAlvoRaioPrioritario AlvoPrioritarioApontado => alvoPrioritarioApontado;
        public NoInteracaoAnatomica EmFocoNo => gerenciadorSelecao != null ? gerenciadorSelecao.EmFocoNo : null;

        public static void RegistrarAlvoPrioritario(IAlvoRaioPrioritario alvo)
        {
            if (alvo != null && !alvosPrioritarios.Contains(alvo))
                alvosPrioritarios.Add(alvo);
        }

        public static void RemoverAlvoPrioritario(IAlvoRaioPrioritario alvo) => alvosPrioritarios.Remove(alvo);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RedefinirAlvosPrioritarios() => alvosPrioritarios.Clear();
        public Transform OrigemRaioAtiva => ResolverRaioOrigem();
        private Vector3? pontoAcertoAtual;
        /// <summary>Ponto onde o raio toca a estrutura em hover neste quadro; nulo quando o raio não aponta nenhuma.</summary>
        public Vector3? PontoAcertoAtual => pontoAcertoAtual;
        public float DistanciaMaxima => DistanciaMaximaEfetiva;
        private float DistanciaMaximaEfetiva => Mathf.Min(Mathf.Max(0.01f, distanciaMaxima), 6f);
        /// <summary>
        /// Resolve o gerenciador local caso a referência não esteja serializada.
        /// </summary>
        private void Awake()
        {
            histereseCandidato = new HistereseCandidatoSelecao(opcaoTrocaTolerancia, opcaoTrocaEsperaSegundos);
            if (gerenciadorSelecao == null)
            {
                gerenciadorSelecao = GetComponent<GerenciadorSelecaoHierarquica>();
            }
        }

        /// <summary>
        /// Atualiza o hover a cada frame para manter highlight responsivo.
        /// </summary>
        private void Update()
        {
            AtualizarFocoRaio();
        }

        /// <summary>
        /// Configura o controlador a partir de ferramentas/editor, preservando o mesmo raycast.
        /// </summary>
        public void Configurar(GerenciadorSelecaoHierarquica gerenciador, Transform origem)
        {
            gerenciadorSelecao = gerenciador;
            raioOrigem = origem;
            RedefinirCandidatoEstavel();
            gerenciadorSelecao?.LimparNoSobFoco(this);
        }

        /// <summary>
        /// Troca a origem do raio, útil quando o controle direito é encontrado depois da cena iniciar.
        /// </summary>
        public void DefinirOrigemRaio(Transform origem)
        {
            raioOrigem = origem;
            RedefinirCandidatoEstavel();
            gerenciadorSelecao?.LimparNoSobFoco(this);
        }

        /// <summary>
        /// Lança o raycast e escolhe o primeiro nó válido na ordem de distância.
        /// </summary>
        public void AtualizarFocoRaio()
        {
            pontoAcertoAtual = null;
            // O template desativa o controle enquanto a mão rastreada possui a entrada.
            // Não sobrescreve o foco do interactor de mão com uma pose de controle inativa.
            if (raioOrigem != null && !OrigemRaioDisponivel(raioOrigem))
            {
                DefinirAlvoPrioritario(null);
                RedefinirCandidatoEstavel();
                gerenciadorSelecao?.LimparNoSobFoco(this);
                return;
            }

            var origem = ResolverRaioOrigem();
            if (origem == null || gerenciadorSelecao == null)
            {
                DefinirAlvoPrioritario(null);
                RedefinirCandidatoEstavel();
                gerenciadorSelecao?.LimparNoSobFoco(this);
                return;
            }

            if (desenharDepuracaoRaio && origem != null)
            {
                Debug.DrawRay(origem.position, origem.forward * DistanciaMaximaEfetiva, Color.cyan);
            }

            // A interação near/poke possui a zona do painel enquanto o raio físico está
            // próximo dela. Um raio distante permanece disponível; nenhum collider anatômico grande
            // ou arbitragem de EventSystem é introduzido aqui.
            if (origem != null && ZonaProximidadePainelInterface.PontoEstaProximoDoPainel(origem.position))
            {
                DefinirAlvoPrioritario(null);
                RedefinirCandidatoEstavel();
                gerenciadorSelecao?.LimparNoSobFoco(this);
                return;
            }

            if (!CandidatoEstavelValido())
            {
                RedefinirCandidatoEstavel();
                gerenciadorSelecao?.LimparNoSobFoco(this);
            }

            var consultaDistancia = DistanciaMaximaEfetiva;
            if (ZonaProximidadePainelInterface.TentarObterDistanciaDeBloqueioDoRaio(new Ray(origem.position, origem.forward), DistanciaMaximaEfetiva, out var interfaceBloqueioDistancia))
            {
                consultaDistancia = Mathf.Min(consultaDistancia, interfaceBloqueioDistancia);
            }

            var estavelEstavaPresente = false;
            var distanciaEstavelAtual = float.PositiveInfinity;
            var temAnatomia = TentarObterAcertoValidoMaisProximo(ObterCandidatoEstavel(), consultaDistancia, out var candidato, out var candidatoDistancia, out estavelEstavaPresente, out distanciaEstavelAtual);
            // Um alvo prioritário (ex.: eletrodo) à frente da anatomia ganha o raio e o próximo gatilho.
            var prioritario = EncontrarAlvoPrioritario(new Ray(origem.position, origem.forward), consultaDistancia, out var distanciaPrioritario);
            if (prioritario != null && (!temAnatomia || distanciaPrioritario < candidatoDistancia))
            {
                RedefinirCandidatoEstavel();
                gerenciadorSelecao?.LimparNoSobFoco(this);
                DefinirAlvoPrioritario(prioritario);
                return;
            }

            DefinirAlvoPrioritario(null);
            if (!temAnatomia)
            {
                RedefinirCandidatoEstavel();
                gerenciadorSelecao?.LimparNoSobFoco(this);
                return;
            }

            var estavel = HistereseCandidato.Avaliar(candidato, candidatoDistancia, estavelEstavaPresente, distanciaEstavelAtual, Time.unscaledTime) as NoInteracaoAnatomica;
            if (estavel != null)
            {
                gerenciadorSelecao.DefinirNoSobFoco(estavel, this);
                var distanciaEstavel = estavel == candidato ? candidatoDistancia : distanciaEstavelAtual;
                if (!float.IsInfinity(distanciaEstavel))
                    pontoAcertoAtual = origem.position + origem.forward * distanciaEstavel;
            }
        }

        /// <summary>
        /// Retorna o nó anatômico válido mais próximo e sua distância usando o mesmo raio do hover.
        /// </summary>
        public bool TentarObterAcertoValidoMaisProximo(out NoInteracaoAnatomica no, out float distanciaAcerto)
        {
            return TentarObterAcertoValidoMaisProximo(null, DistanciaMaximaEfetiva, out no, out distanciaAcerto, out _, out _);
        }

        private bool TentarObterAcertoValidoMaisProximo(NoInteracaoAnatomica rastreadoCandidato, float consultaDistancia, out NoInteracaoAnatomica no, out float distanciaAcerto, out bool rastreadoCandidatoPresente, out float rastreadoCandidatoDistancia)
        {
            no = null;
            distanciaAcerto = float.PositiveInfinity;
            rastreadoCandidatoPresente = false;
            rastreadoCandidatoDistancia = float.PositiveInfinity;
            var origem = ResolverRaioOrigem();
            if (origem == null || !OrigemRaioDisponivel(origem) || gerenciadorSelecao == null)
            {
                return false;
            }

            var quantidadeAcertos = Physics.RaycastNonAlloc(origem.position, origem.forward, raioBuffer, consultaDistancia, camadaMascara, QueryTriggerInteraction.Collide);
            // RaycastNonAlloc retorna o comprimento do buffer quando ele está exatamente cheio e quando
            // foi truncado. Prefere o caminho de excesso com alocação a ignorar silenciosamente
            // um collider anatômico mais próximo fora do buffer reutilizável.
            if (quantidadeAcertos == raioBuffer.Length)
            {
                var excessoAcertos = Physics.RaycastAll(origem.position, origem.forward, consultaDistancia, camadaMascara, QueryTriggerInteraction.Collide);
                return TentarEncontrarAcertoValidoMaisProximo(excessoAcertos, excessoAcertos.Length, rastreadoCandidato, out no, out distanciaAcerto, out rastreadoCandidatoPresente, out rastreadoCandidatoDistancia);
            }

            return TentarEncontrarAcertoValidoMaisProximo(raioBuffer, quantidadeAcertos, rastreadoCandidato, out no, out distanciaAcerto, out rastreadoCandidatoPresente, out rastreadoCandidatoDistancia);
        }

        /// <summary>
        /// Escolhe o candidato anatômico válido com menor distância sem depender da ordem
        /// retornada pela consulta física. Funciona para o buffer reutilizado e o fallback.
        /// </summary>
        private bool TentarEncontrarAcertoValidoMaisProximo(RaycastHit[] acertos, int quantidadeAcertos, NoInteracaoAnatomica rastreadoCandidato, out NoInteracaoAnatomica no, out float distanciaAcerto, out bool rastreadoCandidatoPresente, out float rastreadoCandidatoDistancia)
        {
            no = null;
            distanciaAcerto = float.PositiveInfinity;
            rastreadoCandidatoPresente = false;
            rastreadoCandidatoDistancia = float.PositiveInfinity;
            for (var indice = 0; indice < quantidadeAcertos; indice++)
            {
                var acerto = acertos[indice];
                if (acerto.collider == null)
                {
                    continue;
                }

                var candidato = gerenciadorSelecao.ResolverCandidato(acerto.collider.GetComponentInParent<NoInteracaoAnatomica>());
                if (candidato == null)
                {
                    continue;
                }

                if (candidato == rastreadoCandidato)
                {
                    rastreadoCandidatoPresente = true;
                    if (acerto.distance < rastreadoCandidatoDistancia)
                    {
                        rastreadoCandidatoDistancia = acerto.distance;
                    }
                }

                if (acerto.distance >= distanciaAcerto)
                {
                    continue;
                }

                no = candidato;
                distanciaAcerto = acerto.distance;
            }

            return no != null;
        }

        /// <summary>
        /// Seleciona o nó atualmente em hover, normalmente chamado pelo trigger.
        /// </summary>
        public void SelecionarFocoAtual()
        {
            if (alvoPrioritarioApontado != null)
            {
                alvoPrioritarioApontado.Confirmar();
                return;
            }

            gerenciadorSelecao?.SelecionarNoSobFoco();
        }

        /// <summary>
        /// Limpa o hover quando o reset ou outro fluxo precisa remover destaque temporário.
        /// </summary>
        public void LimparFoco()
        {
            DefinirAlvoPrioritario(null);
            RedefinirCandidatoEstavel();
            gerenciadorSelecao?.LimparNoSobFoco(this);
        }

        private static IAlvoRaioPrioritario EncontrarAlvoPrioritario(Ray raio, float distanciaMaxima, out float distancia)
        {
            distancia = float.PositiveInfinity;
            IAlvoRaioPrioritario melhor = null;
            for (var indice = alvosPrioritarios.Count - 1; indice >= 0; indice--)
            {
                var alvo = alvosPrioritarios[indice];
                if (alvo == null || (alvo is UnityEngine.Object objeto && objeto == null))
                {
                    alvosPrioritarios.RemoveAt(indice);
                    continue;
                }

                if (alvo.TentarAcertar(raio, distanciaMaxima, out var candidata) && candidata < distancia)
                {
                    distancia = candidata;
                    melhor = alvo;
                }
            }

            return melhor;
        }

        private void DefinirAlvoPrioritario(IAlvoRaioPrioritario alvo)
        {
            if (ReferenceEquals(alvoPrioritarioApontado, alvo))
            {
                // O mesmo alvo pode ter mudado de item (de um eletrodo para o vizinho): avisa de novo.
                alvo?.DefinirApontado(true);
                return;
            }
            var anterior = alvoPrioritarioApontado;
            alvoPrioritarioApontado = alvo;
            if (anterior != null && !(anterior is UnityEngine.Object antigo && antigo == null))
                anterior.DefinirApontado(false);
            alvo?.DefinirApontado(true);
        }

        private void OnDisable() => LimparFoco();
        private bool CandidatoEstavelValido()
        {
            var estavel = ObterCandidatoEstavel();
            return estavel != null && gerenciadorSelecao != null && estavel.isActiveAndEnabled && gerenciadorSelecao.EhCandidatoValido(estavel);
        }

        private NoInteracaoAnatomica ObterCandidatoEstavel()
        {
            return HistereseCandidato.CandidatoEstavel as NoInteracaoAnatomica;
        }

        private HistereseCandidatoSelecao HistereseCandidato
        {
            get
            {
                return histereseCandidato ??= new HistereseCandidatoSelecao(opcaoTrocaTolerancia, opcaoTrocaEsperaSegundos);
            }
        }

        private void RedefinirCandidatoEstavel()
        {
            HistereseCandidato.Redefinir();
        }

        /// <summary>
        /// A origem estabilizada do template (ex.: Right Controller Teleport Stabilized Origin) é irmã do
        /// controle, não filha: continua ativa, parada na última pose, quando a mão rastreada assume. O que
        /// decide é o grupo do dispositivo que o estabilizador segue (o Right Controller) estar ativo.
        /// </summary>
        public bool OrigemRaioDisponivel(Transform origem)
        {
            if (origem == null || !origem.gameObject.activeInHierarchy)
                return false;
            if (origem != origemEstabilizadorEmCache)
            {
                origemEstabilizadorEmCache = origem;
                estabilizadorEmCache = origem.GetComponentInParent<XRTransformStabilizer>();
            }

            var alvo = estabilizadorEmCache != null ? estabilizadorEmCache.targetTransform : null;
            if (alvo == null)
                return true;
            var irmaos = estabilizadorEmCache.transform.parent;
            var grupo = alvo;
            while (grupo.parent != null && grupo.parent != irmaos)
                grupo = grupo.parent;
            return (grupo.parent == irmaos ? grupo : alvo).gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Decide a origem ativa do raio com fallback para câmera principal e, por último, o próprio objeto.
        /// </summary>
        private Transform ResolverRaioOrigem()
        {
            if (raioOrigem != null)
            {
                return raioOrigem;
            }

            if (usarPrincipalCameraAlternativa && Camera.main != null)
            {
                return Camera.main.transform;
            }

            return transform;
        }
    }
}