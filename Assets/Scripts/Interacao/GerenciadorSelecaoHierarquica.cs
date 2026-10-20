using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRSNC.Interacao
{
    // Nome funcional em português: Gerenciador de Seleção Anatômica em Camadas
    // Nome técnico do script: GerenciadorSelecaoHierarquica
    // Função: controla foco, hover, seleção, isolamento e navegação hierárquica do SNC.
    // Relação com o protótipo: é o centro da seleção por raycast/trigger; conversa com painel,
    // visibilidade, destaque visual e reset sem alterar colliders ou reconstruir a hierarquia.
    // Objetos da cena: usado no objeto principal Sistema_Nervoso_Central.
    /// <summary>
    /// Mantém o estado de navegação anatômica.
    /// A regra principal é selecionar apenas filhos diretos do foco atual, permitindo aprofundar no SNC por camadas.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainNestedSelectionManager")]
    public sealed class GerenciadorSelecaoHierarquica : MonoBehaviour
    {
        // Evento serializável para que a Unity possa notificar outros componentes quando o nó muda.
        [Serializable]
        [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "NodeEvent")]
        public sealed class NoEvento : UnityEvent<NoInteracaoAnatomica>
        {
        }

        // Nó raiz da organização anatômica ativa.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rootNode")]
        private NoInteracaoAnatomica raizNo;
        // Nó cujos filhos estão disponíveis para seleção no momento.
        [UnityEngine.Serialization.FormerlySerializedAs("atualFocoNo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("currentFocusNode")]
        private NoInteracaoAnatomica noFocoAtual;
        // Estrutura confirmada pelo usuário via trigger, botão ou chamada do painel.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectedNode")]
        private NoInteracaoAnatomica selecionadoNo;
        // Estrutura sob o raio/ponteiro antes da seleção confirmada.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("hoveredNode")]
        private NoInteracaoAnatomica focoNo;
        // Quando ativo, entra automaticamente em um nó selecionado que possui filhos.
        [UnityEngine.Serialization.FormerlySerializedAs("automaticoEntrarSelecionadoNo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("autoEnterSelectedNode")]
        private bool entrarAutomaticamenteNoSelecionado;
        // Quando ativo, isola a estrutura assim que ela é selecionada.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("isolateOnSelect")]
        private bool isolarEmSelecionar;
        // Cores de destaque aplicadas via MaterialPropertyBlock, sem trocar materiais do modelo.
        [UnityEngine.Serialization.FormerlySerializedAs("fococor")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("hoverColor")]
        private Color corFoco = new(0.35f, 0.7f, 1f, 1f);
        [UnityEngine.Serialization.FormerlySerializedAs("selecionadocor")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectedColor")]
        private Color corSelecionado = new(1f, 0.82f, 0.25f, 1f);
        // Controladores que recebem atualização quando a seleção muda.
        [UnityEngine.Serialization.FormerlySerializedAs("visibilidadecontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("visibilityController")]
        private ControladorVisibilidadeAnatomica controladorVisibilidade;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("infoPanel")]
        private PainelInformacoesAnatomicas informacaoPainel;
        [UnityEngine.Serialization.FormerlySerializedAs("navegacaopainel")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("navigationPanel")]
        private PainelNavegacaoAnatomica painelNavegacao;
        // Eventos públicos usados pela cena para reagir a foco, hover, seleção e limpeza.
        [UnityEngine.Serialization.FormerlySerializedAs("onFocusChanged")]
        public NoEvento emFocoAlterado = new();
        [UnityEngine.Serialization.FormerlySerializedAs("onNodeHovered")]
        public NoEvento emNoEmFoco = new();
        [UnityEngine.Serialization.FormerlySerializedAs("onNodeSelected")]
        public NoEvento emNoSelecionado = new();
        [UnityEngine.Serialization.FormerlySerializedAs("onSelectionCleared")]
        public UnityEvent emSelecaoLimpo = new();
        // Estado original dos renderers para restaurar hover/seleção sem perder materiais.
        private readonly Dictionary<Renderer, MaterialPropertyBlock> bloqueiosOriginais = new();
        private readonly HashSet<Renderer> focoRenderizadores = new();
        private readonly HashSet<Renderer> renderizadoresSelecionados = new();
        private readonly List<Renderer> destacarRenderizadorTemporario = new();
        // MaterialPropertyBlock encapsula um objeto nativo; não pode ser criado no
        // inicializador de campo porque a Unity ainda não permite CreateImpl nessa fase.
        // Ele é criado no Awake (thread principal) e reutilizado nas transições de destaque.
        private MaterialPropertyBlock destacarBloqueio;
        private object proprietarioFoco;
        // IDs comuns de cor nos shaders usados pelos materiais importados.
        private static readonly int BaseCorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CorId = Shader.PropertyToID("_Color");
        public NoInteracaoAnatomica NoRaiz => raizNo;
        public NoInteracaoAnatomica NoEmFoco => noFocoAtual;
        public NoInteracaoAnatomica NoSelecionado => selecionadoNo;
        public NoInteracaoAnatomica EmFocoNo => focoNo;
        public bool EntrarAutomaticamenteNoSelecionado => entrarAutomaticamenteNoSelecionado;
        public bool EhIsolando => controladorVisibilidade != null && controladorVisibilidade.EhIsolando;
        /// <summary>
        /// Resolve componentes locais quando a cena não preencheu alguma referência no Inspector.
        /// </summary>
        private void Awake()
        {
            destacarBloqueio = new MaterialPropertyBlock();
            if (raizNo == null)
            {
                raizNo = GetComponent<NoInteracaoAnatomica>();
            }

            if (controladorVisibilidade == null)
            {
                controladorVisibilidade = GetComponent<ControladorVisibilidadeAnatomica>();
            }

            if (noFocoAtual == null)
            {
                RedefinirFoco();
            }
        }

        /// <summary>
        /// Configura o gerenciador a partir da ferramenta/editor sem recriar os nós anatômicos.
        /// </summary>
        public void Configurar(NoInteracaoAnatomica raiz, ControladorVisibilidadeAnatomica visibilidade, PainelInformacoesAnatomicas informacao, PainelNavegacaoAnatomica navegacao)
        {
            raizNo = raiz;
            controladorVisibilidade = visibilidade;
            informacaoPainel = informacao;
            painelNavegacao = navegacao;
            RedefinirFoco();
        }

        /// <summary>
        /// Valida se o nó pode ser selecionado agora.
        /// Regra anatômica: com foco definido, apenas filhos diretos do foco são candidatos válidos.
        /// </summary>
        public bool EhCandidatoValido(NoInteracaoAnatomica no)
        {
            if (no == null || !no.isActiveAndEnabled || !no.EhSelecionavel)
            {
                return false;
            }

            var foco = noFocoAtual != null ? noFocoAtual : raizNo;
            if (foco == null)
            {
                return true;
            }

            return no.EhFilhoDiretoDe(foco);
        }

        /// <summary>Mapeia um acerto em folha para o filho selecionável do foco anatômico atual.</summary>
        public NoInteracaoAnatomica ResolverCandidato(NoInteracaoAnatomica noAcerto)
        {
            var atual = noAcerto;
            while (atual != null)
            {
                if (EhCandidatoValido(atual))
                    return atual;
                atual = atual.NoPai;
            }

            return null;
        }

        /// <summary>
        /// Atualiza o hover vindo do raycast e aplica destaque temporário.
        /// </summary>
        public void DefinirNoSobFoco(NoInteracaoAnatomica no)
        {
            DefinirNoSobFoco(no, null);
        }

        public void LimparNoSobFoco(object responsavel)
        {
            if (ReferenceEquals(proprietarioFoco, responsavel))
                DefinirNoSobFoco(null, null);
        }

        public void DefinirNoSobFoco(NoInteracaoAnatomica no, object responsavel)
        {
            proprietarioFoco = responsavel;
            if (no == focoNo)
            {
                return;
            }

            LimparRealceFoco();
            focoNo = EhCandidatoValido(no) ? no : null;
            if (focoNo != null && focoNo != selecionadoNo)
            {
                AplicarRealce(focoNo, corFoco, focoRenderizadores);
            }

            emNoEmFoco.Invoke(focoNo);
        }

        /// <summary>
        /// Confirma a seleção do nó atualmente apontado.
        /// </summary>
        public void SelecionarNoSobFoco()
        {
            if (focoNo != null)
            {
                SelecionarNo(focoNo);
            }
        }

        /// <summary>
        /// Seleciona um nó válido, atualiza painel, destaque e isolamento opcional.
        /// </summary>
        public void SelecionarNo(NoInteracaoAnatomica no)
        {
            if (!EhCandidatoValido(no))
            {
                return;
            }

            // Clicar num grupo abre o grupo: ele vira o foco ("Em foco") em vez de ficar selecionado e todo amarelo.
            if (entrarAutomaticamenteNoSelecionado && no.NosFilhos.Count > 0)
            {
                if (selecionadoNo != null)
                {
                    LimparSelecao();
                }

                EntrarNo(no);
                return;
            }

            AplicarSelecao(no);
        }

        /// <summary>
        /// Seleciona o nó sem entrar nele, mesmo com a entrada automática ligada. É assim que o Voltar deixa marcada a
        /// "concha" de onde o usuário saiu: o grupo fica em âmbar, com a ficha aberta, e tocar nele de novo volta para dentro.
        /// </summary>
        public void SelecionarSemEntrar(NoInteracaoAnatomica no)
        {
            if (EhCandidatoValido(no))
            {
                AplicarSelecao(no);
            }
        }

        private void AplicarSelecao(NoInteracaoAnatomica no)
        {
            LimparRealceSelecao();
            selecionadoNo = no;
            AplicarRealce(selecionadoNo, corSelecionado, renderizadoresSelecionados);
            informacaoPainel?.ExibirNo(selecionadoNo, ObterCaminhoAtual(selecionadoNo));
            if (isolarEmSelecionar)
            {
                IsolarNo(selecionadoNo);
            }

            emNoSelecionado.Invoke(selecionadoNo);
            painelNavegacao?.Atualizar(noFocoAtual);
        }

        /// <summary>
        /// Entra na estrutura selecionada quando ela possui filhos diretos navegáveis. Como o clique num grupo, entrar
        /// desfaz a seleção: o grupo vira o foco, em vez de continuar todo em âmbar enquanto o usuário escolhe dentro dele.
        /// </summary>
        public void EntrarNoSelecionado()
        {
            if (selecionadoNo == null)
            {
                Debug.Log("[VR SNC] Nenhum nó selecionado para aprofundar.");
                return;
            }

            if (selecionadoNo.NosFilhos.Count == 0)
            {
                Debug.Log($"[VR SNC] '{selecionadoNo.NomeExibicao}' não possui filhos diretos para navegação.");
                return;
            }

            var grupo = selecionadoNo;
            LimparSelecao();
            EntrarNo(grupo);
        }

        /// <summary>
        /// Define se a seleção deve entrar automaticamente em nós com filhos.
        /// </summary>
        public void DefinirEntradaAutomaticaNoSelecionado(bool valor)
        {
            entrarAutomaticamenteNoSelecionado = valor;
        }

        /// <summary>
        /// Muda o foco de navegação para um nó específico.
        /// </summary>
        public void EntrarNo(NoInteracaoAnatomica no)
        {
            if (no == null)
            {
                return;
            }

            if (noFocoAtual != null)
            {
                noFocoAtual.DefinirFocoAtual(false);
            }

            noFocoAtual = no;
            noFocoAtual.DefinirFocoAtual(true);
            DefinirNoSobFoco(null);
            if (selecionadoNo == null)
            {
                informacaoPainel?.ExibirFoco(noFocoAtual);
            }

            painelNavegacao?.Atualizar(noFocoAtual);
            emFocoAlterado.Invoke(noFocoAtual);
        }

        /// <summary>
        /// Sobe exatamente um nível e deixa selecionado o grupo de onde o usuário saiu (a "concha" que contém o que
        /// estava aberto): com o Giro frontal médio selecionado dentro do Lobo frontal, Voltar abre o Córtex com o
        /// Lobo frontal em âmbar e a ficha dele. No nível inicial, Voltar só desfaz a seleção.
        /// </summary>
        public void VoltarAoNoPai()
        {
            var concha = noFocoAtual;
            var pai = concha != null ? concha.NoPai : null;
            if (pai == null)
            {
                if (selecionadoNo != null)
                {
                    LimparSelecao();
                }

                return;
            }

            LimparSelecao();
            EntrarNo(pai);
            SelecionarSemEntrar(concha);
        }

        /// <summary>Nome do grupo que o Voltar vai deixar selecionado, para o rótulo do botão; vazio quando não há para onde voltar.</summary>
        public string DestinoVoltar()
        {
            if (noFocoAtual != null && noFocoAtual.NoPai != null)
            {
                return noFocoAtual.NomeLegivel;
            }

            return selecionadoNo != null ? "Início" : string.Empty;
        }

        /// <summary>
        /// Volta o foco para a raiz ativa do SNC.
        /// </summary>
        public void RedefinirFoco()
        {
            if (noFocoAtual != null)
            {
                noFocoAtual.DefinirFocoAtual(false);
            }

            noFocoAtual = raizNo;
            noFocoAtual?.DefinirFocoAtual(true);
            DefinirNoSobFoco(null);
            informacaoPainel?.ExibirFoco(noFocoAtual);
            painelNavegacao?.Atualizar(noFocoAtual);
            emFocoAlterado.Invoke(noFocoAtual);
        }

        /// <summary>
        /// Remove seleção e destaque, mantendo o foco anatômico atual.
        /// </summary>
        public void LimparSelecao()
        {
            LimparRealceSelecao();
            selecionadoNo = null;
            informacaoPainel?.Limpar();
            informacaoPainel?.ExibirFoco(noFocoAtual);
            emSelecaoLimpo.Invoke();
        }

        /// <summary>
        /// Isola uma estrutura usando o controlador de visibilidade, sem remover objetos da cena.
        /// </summary>
        public void IsolarNo(NoInteracaoAnatomica no)
        {
            if (no != null && controladorVisibilidade != null)
            {
                controladorVisibilidade.ExibirSomenteNo(no);
            }
        }

        /// <summary>
        /// Alterna isolamento da estrutura selecionada e restauração da visibilidade total.
        /// </summary>
        public void AlternarIsolamento()
        {
            if (controladorVisibilidade == null)
            {
                return;
            }

            if (controladorVisibilidade.EhIsolando)
            {
                controladorVisibilidade.ExibirTudo();
            }
            else if (selecionadoNo != null)
            {
                controladorVisibilidade.ExibirSomenteNo(selecionadoNo);
            }
            else if (noFocoAtual != null && noFocoAtual != raizNo)
            {
                // Sem seleção, isola o grupo em que o usuário entrou.
                controladorVisibilidade.ExibirSomenteNo(noFocoAtual);
            }

            painelNavegacao?.AtualizarEstadosControles();
        }

        /// <summary>
        /// Remove todos os destaques de hover e seleção.
        /// </summary>
        public void RedefinirRealces()
        {
            LimparRealceFoco();
            LimparRealceSelecao();
        }

        /// <summary>
        /// Monta o caminho anatômico visível no painel, da raiz até o nó informado.
        /// </summary>
        public string ObterCaminhoAtual(NoInteracaoAnatomica no)
        {
            if (no == null)
            {
                return string.Empty;
            }

            var pilha = new Stack<string>();
            var atual = no;
            while (atual != null)
            {
                pilha.Push(atual.NomeExibicao);
                atual = atual.NoPai;
            }

            return string.Join(" / ", pilha);
        }

        /// <summary>
        /// Aplica cor temporária aos renderers de um nó sem modificar o material importado.
        /// </summary>
        private void AplicarRealce(NoInteracaoAnatomica no, Color cor, HashSet<Renderer> definirAlvo)
        {
            // Protege chamadas programáticas anteriores ao ciclo normal de Awake sem
            // reintroduzir uma criação de objeto nativo no inicializador de campo.
            foreach (var renderizador in ObterRenderizadoresParaRealce(no))
            {
                if (renderizador == null)
                {
                    continue;
                }

                AplicarCorRenderizador(renderizador, cor);
                definirAlvo.Add(renderizador);
            }
        }

        /// <summary>
        /// Pinta um renderer com a cor de destaque, guardando antes o bloco original para a restauração.
        /// </summary>
        private void AplicarCorRenderizador(Renderer renderizador, Color cor)
        {
            destacarBloqueio ??= new MaterialPropertyBlock();
            if (!bloqueiosOriginais.ContainsKey(renderizador))
            {
                var bloqueioOriginal = new MaterialPropertyBlock();
                renderizador.GetPropertyBlock(bloqueioOriginal);
                bloqueiosOriginais[renderizador] = bloqueioOriginal;
            }

            // GetPropertyBlock preserva propriedades externas que já estejam no
            // renderer; o bloco de trabalho é reutilizado porque SetPropertyBlock
            // copia seus valores no renderer.
            destacarBloqueio.Clear();
            renderizador.GetPropertyBlock(destacarBloqueio);
            destacarBloqueio.SetColor(BaseCorId, cor);
            destacarBloqueio.SetColor(CorId, cor);
            renderizador.SetPropertyBlock(destacarBloqueio);
        }

        /// <summary>
        /// Recria a lista de renderers somente quando o nó de hover/seleção muda. A lista
        /// é reutilizada, portanto novos renderers em qualquer descendente e reparenting
        /// entram na próxima transição sem alocar uma nova matriz e sem varrer por frame.
        /// </summary>
        private List<Renderer> ObterRenderizadoresParaRealce(NoInteracaoAnatomica no)
        {
            destacarRenderizadorTemporario.Clear();
            if (no != null)
            {
                no.GetComponentsInChildren(true, destacarRenderizadorTemporario);
            }

            return destacarRenderizadorTemporario;
        }

        /// <summary>
        /// Restaura apenas o destaque de hover; renderers ainda selecionados voltam à cor de seleção.
        /// </summary>
        private void LimparRealceFoco()
        {
            RestaurarRenderizadores(focoRenderizadores, renderizadoresSelecionados, corSelecionado);
            focoRenderizadores.Clear();
        }

        /// <summary>
        /// Restaura o destaque de seleção; renderers ainda em hover voltam à cor de hover.
        /// </summary>
        private void LimparRealceSelecao()
        {
            RestaurarRenderizadores(renderizadoresSelecionados, focoRenderizadores, corFoco);
            renderizadoresSelecionados.Clear();
        }

        /// <summary>
        /// Devolve os renderers aos MaterialPropertyBlocks capturados antes do destaque. Os que
        /// continuam no outro destaque recebem de novo a cor dele, em vez de manter a cor removida.
        /// </summary>
        private void RestaurarRenderizadores(HashSet<Renderer> renderizadores, HashSet<Renderer> manterDestacado, Color corMantida)
        {
            foreach (var renderizador in renderizadores)
            {
                if (renderizador == null)
                {
                    continue;
                }

                if (manterDestacado.Contains(renderizador))
                {
                    AplicarCorRenderizador(renderizador, corMantida);
                    continue;
                }

                if (bloqueiosOriginais.TryGetValue(renderizador, out var bloqueio))
                {
                    renderizador.SetPropertyBlock(bloqueio);
                }
                else
                {
                    renderizador.SetPropertyBlock(null);
                }
            }
        }
    }
}