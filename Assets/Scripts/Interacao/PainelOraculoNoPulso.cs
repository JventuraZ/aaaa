using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRSNC.Anatomia;

namespace VRSNC.Interacao
{
    /// <summary>Texto didático e bibliografia opcional dentro do Unity Hand Menu nativo do projeto.</summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainWristOraclePanel")]
    public sealed class PainelOraculoNoPulso : MonoBehaviour
    {
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selection")]
        private GerenciadorSelecaoHierarquica selecao;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("heading")]
        private TMP_Text cabecalho;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("summary")]
        private TMP_Text resumo;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("bibliography")]
        private TMP_Text bibliografia;
        [UnityEngine.Serialization.FormerlySerializedAs("fontebotao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("sourceButton")]
        private Button botaoFonte;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("enterButton")]
        private Button entrarBotao;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("backButton")]
        private Button voltarBotao;
        [UnityEngine.Serialization.FormerlySerializedAs("isolarbotao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("isolateButton")]
        private Button botaoIsolar;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("scroll")]
        private ScrollRect rolagem;
        private RegistroOraculoAnatomico atual;
        private bool exibindoFontes;
        private bool atualizacaoDisposicaoPendente;
        public void Configurar(GerenciadorSelecaoHierarquica gerenciador, TMP_Text titulo, TMP_Text corpo, TMP_Text referencias, Button fonte, Button entrar, Button voltar, Button isolar, ScrollRect rolagemVisao)
        {
            Desvincular();
            selecao = gerenciador;
            cabecalho = titulo;
            resumo = corpo;
            bibliografia = referencias;
            botaoFonte = fonte;
            entrarBotao = entrar;
            voltarBotao = voltar;
            botaoIsolar = isolar;
            rolagem = rolagemVisao;
            if (Application.isPlaying && isActiveAndEnabled)
                Vincular();
        }

        private void OnEnable() => Vincular();
        private void OnDisable()
        {
            atualizacaoDisposicaoPendente = false;
            Desvincular();
        }

        private void LateUpdate()
        {
            if (!atualizacaoDisposicaoPendente)
                return;
            if (rolagem == null)
            {
                atualizacaoDisposicaoPendente = false;
                return;
            }

            var conteudo = rolagem.content;
            if (conteudo == null || !conteudo.gameObject.activeInHierarchy)
                return;
            // Limpa antes da reconstrução síncrona: callbacks durante a reconstrução podem enfileirar uma atualização posterior,
            // mas não podem recursar para outra reconstrução neste frame.
            atualizacaoDisposicaoPendente = false;
            LayoutRebuilder.ForceRebuildLayoutImmediate(conteudo);
            rolagem.verticalNormalizedPosition = 1f;
        }

        private void Vincular()
        {
            Desvincular();
            if (selecao == null)
                return;
            selecao.emNoSelecionado.AddListener(Exibir);
            selecao.emFocoAlterado.AddListener(ExibirFoco);
            selecao.emSelecaoLimpo.AddListener(ExibirFocoAtual);
            botaoFonte?.onClick.AddListener(AlternarFontes);
            entrarBotao?.onClick.AddListener(Entrar);
            voltarBotao?.onClick.AddListener(Voltar);
            botaoIsolar?.onClick.AddListener(Isolar);
            Exibir(selecao.NoSelecionado != null ? selecao.NoSelecionado : selecao.NoEmFoco);
        }

        private void Desvincular()
        {
            if (selecao != null)
            {
                selecao.emNoSelecionado.RemoveListener(Exibir);
                selecao.emFocoAlterado.RemoveListener(ExibirFoco);
                selecao.emSelecaoLimpo.RemoveListener(ExibirFocoAtual);
            }

            botaoFonte?.onClick.RemoveListener(AlternarFontes);
            entrarBotao?.onClick.RemoveListener(Entrar);
            voltarBotao?.onClick.RemoveListener(Voltar);
            botaoIsolar?.onClick.RemoveListener(Isolar);
        }

        private void ExibirFoco(NoInteracaoAnatomica _) => Exibir(selecao != null ? selecao.NoSelecionado ?? selecao.NoEmFoco : null);
        private void ExibirFocoAtual() => Exibir(selecao != null ? selecao.NoEmFoco : null);
        private void Entrar() => selecao?.EntrarNoSelecionado();
        private void Voltar() => selecao?.VoltarAoNoPai();
        private void Isolar() => selecao?.AlternarIsolamento();
        public void Exibir(NoInteracaoAnatomica no)
        {
            if (cabecalho == null || resumo == null || bibliografia == null)
                return;
            exibindoFontes = false;
            atual = null;
            if (no != null)
                OraculoAnatomico.TentarObter(no.VinculoAnatomico, out atual);
            cabecalho.text = no != null ? no.NomeLegivelComParte : "Oráculo anatômico";
            // O resumo didático vem primeiro; o estado de verificação fica no fim, como na ficha principal.
            resumo.text = (atual != null ? atual.resumoDidatico : "Selecione uma estrutura para consultar sua ficha anatômica.") + "\n\n" + OraculoAnatomico.ObterEstadoVerificacao(atual);
            bibliografia.text = atual != null ? OraculoAnatomico.ObterBibliografia(atual) : string.Empty;
            bibliografia.gameObject.SetActive(false);
            if (botaoFonte != null)
                botaoFonte.interactable = atual != null;
            DefinirRotuloFonte("Fonte");
            if (entrarBotao != null)
                entrarBotao.interactable = selecao != null && selecao.NoSelecionado != null && selecao.NoSelecionado.NosFilhos.Count > 0;
            foreach (var botao in new[] { botaoFonte, entrarBotao, voltarBotao, botaoIsolar })
                VRSNC.Interface.EstiloBotaoInterface.AtualizarAparencia(botao);
            AtualizarDisposicao();
        }

        public void AlternarFontes()
        {
            if (bibliografia == null)
                return;
            exibindoFontes = !exibindoFontes && atual != null;
            bibliografia.gameObject.SetActive(exibindoFontes);
            DefinirRotuloFonte(exibindoFontes ? "Ocultar fonte" : "Fonte");
            AtualizarDisposicao();
        }

        private void DefinirRotuloFonte(string texto)
        {
            var rotulo = botaoFonte != null ? botaoFonte.GetComponentInChildren<TMP_Text>(true) : null;
            if (rotulo != null)
                rotulo.text = texto;
        }

        private void AtualizarDisposicao()
        {
            if (rolagem != null)
                atualizacaoDisposicaoPendente = true;
        }
    }
}
