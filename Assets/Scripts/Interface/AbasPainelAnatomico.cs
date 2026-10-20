using System;
using UnityEngine;
using UnityEngine.UI;
using VRSNC.Interacao;

namespace VRSNC.Interface
{
    /// <summary>
    /// Abas "Ficha", "Estruturas" e "EEG" do painel compacto: uma área de conteúdo por vez, na altura toda.
    /// Entrar num grupo abre "Estruturas"; selecionar uma estrutura final abre "Ficha". Com a aba EEG aberta
    /// nada troca sozinho: tocar um eletrodo seleciona o giro sob ele sem tirar o usuário do EEG.
    /// O analógico esquerdo rola a aba que estiver aberta.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbasPainelAnatomico : MonoBehaviour
    {
        public enum Aba
        {
            Ficha,
            Estruturas,
            EEG
        }

        private const float VelocidadeRolagemPixelsPorSegundo = 420f;

        [SerializeField]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [SerializeField]
        private PainelNavegacaoAnatomica painelNavegacao;
        [SerializeField]
        private Button botaoFicha;
        [SerializeField]
        private Button botaoEstruturas;
        [SerializeField]
        private GameObject conteudoFicha;
        [SerializeField]
        private GameObject conteudoEstruturas;
        [SerializeField]
        private ScrollRect rolagemFicha;
        [SerializeField]
        private Button botaoEEG;
        [SerializeField]
        private GameObject conteudoEEG;
        // A tela inicial abre na ficha, onde fica a orientação "Como explorar".
        [SerializeField]
        private Aba abaInicial = Aba.Ficha;
        private Aba abaAtual;

        public Aba AbaAtual => abaAtual;
        public Button BotaoEEG => botaoEEG;
        public GameObject ConteudoEEG => conteudoEEG;

        /// <summary>A aba mostrada mudou (por clique ou troca automática).</summary>
        public event Action<Aba> AbaAlterada;

        private void Awake()
        {
            if (gerenciadorSelecao == null)
                gerenciadorSelecao = FindFirstObjectByType<GerenciadorSelecaoHierarquica>();
            if (painelNavegacao == null)
                painelNavegacao = GetComponent<PainelNavegacaoAnatomica>();
            botaoFicha?.onClick.AddListener(() => Mostrar(Aba.Ficha));
            botaoEstruturas?.onClick.AddListener(() => Mostrar(Aba.Estruturas));
            botaoEEG?.onClick.AddListener(() => Mostrar(Aba.EEG));
        }

        /// <summary>Esconde a aba EEG quando a cena não tem gravação instalada.</summary>
        public void DefinirAbaEEGDisponivel(bool disponivel)
        {
            if (botaoEEG != null)
                botaoEEG.gameObject.SetActive(disponivel);
            if (!disponivel && abaAtual == Aba.EEG)
                Mostrar(Aba.Ficha);
        }

        private void OnEnable()
        {
            if (gerenciadorSelecao == null)
                return;
            gerenciadorSelecao.emFocoAlterado.AddListener(AoMudarFoco);
            gerenciadorSelecao.emNoSelecionado.AddListener(AoSelecionar);
        }

        private void OnDisable()
        {
            if (gerenciadorSelecao == null)
                return;
            gerenciadorSelecao.emFocoAlterado.RemoveListener(AoMudarFoco);
            gerenciadorSelecao.emNoSelecionado.RemoveListener(AoSelecionar);
        }

        private void Start() => Mostrar(abaInicial);

        // Entrar num grupo leva à lista de subestruturas; voltar ao nível inicial reabre a ficha com a orientação.
        // Na aba EEG nada troca sozinho: o usuário está lendo o EEG e tocou um eletrodo, não a anatomia.
        private void AoMudarFoco(NoInteracaoAnatomica foco)
        {
            if (abaAtual != Aba.EEG)
                Mostrar(foco != null && foco.NoPai == null ? Aba.Ficha : Aba.Estruturas);
        }

        private void AoSelecionar(NoInteracaoAnatomica _)
        {
            if (abaAtual != Aba.EEG)
                Mostrar(Aba.Ficha);
        }

        public void Mostrar(Aba aba)
        {
            if (aba == Aba.EEG && (botaoEEG == null || !botaoEEG.gameObject.activeSelf || conteudoEEG == null))
                aba = Aba.Ficha;
            var mudou = abaAtual != aba;
            abaAtual = aba;
            if (conteudoFicha != null)
                conteudoFicha.SetActive(aba == Aba.Ficha);
            if (conteudoEstruturas != null)
                conteudoEstruturas.SetActive(aba == Aba.Estruturas);
            if (conteudoEEG != null)
                conteudoEEG.SetActive(aba == Aba.EEG);
            Estilizar(botaoFicha, aba == Aba.Ficha);
            Estilizar(botaoEstruturas, aba == Aba.Estruturas);
            Estilizar(botaoEEG, aba == Aba.EEG);
            if (mudou)
                AbaAlterada?.Invoke(aba);
        }

        /// <summary>Rola a aba aberta em velocidade constante; valores positivos sobem o conteúdo.</summary>
        public void Rolar(float entradaVezesSegundos)
        {
            if (abaAtual == Aba.EEG)
                return;
            if (abaAtual == Aba.Estruturas || rolagemFicha == null)
            {
                painelNavegacao?.Rolar(entradaVezesSegundos);
                return;
            }

            if (rolagemFicha.content == null)
                return;
            var area = rolagemFicha.viewport != null ? rolagemFicha.viewport : rolagemFicha.transform as RectTransform;
            var excedente = rolagemFicha.content.rect.height - (area != null ? area.rect.height : 0f);
            if (excedente <= 0f)
                return;
            rolagemFicha.StopMovement();
            rolagemFicha.verticalNormalizedPosition = Mathf.Clamp01(rolagemFicha.verticalNormalizedPosition + entradaVezesSegundos * VelocidadeRolagemPixelsPorSegundo / excedente);
        }

        // Seletor de abas do tema: a aba aberta fica em superfície azulada e negrito.
        private static void Estilizar(Button botao, bool ativa) => TemaVRSNC.Marcar(botao, ativa);
    }
}
