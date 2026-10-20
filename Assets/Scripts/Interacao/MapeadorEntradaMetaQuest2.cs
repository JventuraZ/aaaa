using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VRSNC.Interface;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

#endif
namespace VRSNC.Interacao
{
    // Nome funcional em português: Mapeador de Entrada Meta Quest 2 para o SNC
    // Nome técnico do script: MapeadorEntradaMetaQuest2
    // Função: conecta os botões e analógicos do Meta Quest 2 às ações de seleção, grab, navegação, painel,
    // reset, isolamento, visibilidade e vista explodida.
    // Relação com o protótipo: preserva a lógica atual de trigger para selecionar, grip para agarrar,
    // analógico para manipular e painel VR para documentação anatômica.
    // Objetos da cena: usado no Sistema_Nervoso_Central.
    /// <summary>
    /// Mapeia o InputActionAsset específico do Meta Quest 2 para os controladores do protótipo VR SNC.
    /// A classe também contém utilitários de editor para configurar a cena, mas a execução normal apenas lê inputs.
    /// </summary>
    [DisallowMultipleComponent]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "MetaQuest2BrainInputMapper")]
    public sealed class MapeadorEntradaMetaQuest2 : MonoBehaviour
    {
        // Nome do Action Map no InputActionAsset do projeto.
        private const string NomeMapaControlesAnatomia = "BrainControls";
        [UnityEngine.Serialization.FormerlySerializedAs("entradaroteador")]
        [Header("Gerenciadores")]
        // Controladores que recebem os comandos vindos do headset/controles.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("inputRouter")]
        private RoteadorEntradaXR roteadorEntrada;
        [UnityEngine.Serialization.FormerlySerializedAs("raioselecaocontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("raySelectionController")]
        private ControladorSelecaoPorRaio controladorSelecaoPorRaio;
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [UnityEngine.Serialization.FormerlySerializedAs("modelomanipulador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("modelManipulator")]
        private ManipuladorModeloAnatomico manipuladorModelo;
        [UnityEngine.Serialization.FormerlySerializedAs("selecionadoobjetomanipulador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectedObjectManipulator")]
        private ManipuladorObjetoSelecionado manipuladorObjetoSelecionado;
        [UnityEngine.Serialization.FormerlySerializedAs("redefinirgerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("resetManager")]
        private GerenciadorRestauracao gerenciadorRestauracao;
        [UnityEngine.Serialization.FormerlySerializedAs("expandidoVisaoControlador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("explodedViewController")]
        private ControladorVisaoExplodida controladorVisaoExplodida;
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
        [UnityEngine.Serialization.FormerlySerializedAs("direitacontrolador")]
        [Header("Referências da Cena")]
        // Referências físicas usadas para raycast e grab.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightController")]
        private Transform controladorDireito;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdacontrolador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftController")]
        private Transform controladorEsquerdo;
        [UnityEngine.Serialization.FormerlySerializedAs("cameratransformacao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("cameraTransform")]
        private Transform transformacaoCamera;
        [UnityEngine.Serialization.FormerlySerializedAs("entradamodalidadegerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("inputModalityManager")]
        private XRInputModalityManager gerenciadorModalidadeEntrada;
        // Raycaster do painel anatômico usado para comparar UI e anatomia pela distância real do hit.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("blockingUiRaycaster")]
        private TrackedDeviceGraphicRaycaster bloqueioInterfaceEmissorRaio;
        [Header("Alternativa do Recurso de Entrada")]
        // Asset usado quando as InputActionReference individuais não estão serializadas.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("inputActions")]
        private InputActionAsset entradaAcoes;
        [UnityEngine.Serialization.FormerlySerializedAs("direitaselecionarestrutura")]
        [Header("Ações do Meta Quest 2")]
        // Ações discretas e contínuas do controle direito.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightSelectStructure")]
        private InputActionReference acaoSelecionarEstruturaDireita;
        [UnityEngine.Serialization.FormerlySerializedAs("direitaagarreSelecionado")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightGrabSelected")]
        private InputActionReference agarreDireitoSelecionado;
        [UnityEngine.Serialization.FormerlySerializedAs("direitoVoltarParaPai")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightBackToParent")]
        private InputActionReference acaoVoltarAoPaiDireita;
        [UnityEngine.Serialization.FormerlySerializedAs("direitoEntrarSelecionadoNo")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightEnterSelectedNode")]
        private InputActionReference acaoEntrarNoSelecionadoDireita;
        [UnityEngine.Serialization.FormerlySerializedAs("direitoManipularSelecionado")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightManipulateSelected")]
        private InputActionReference acaoManipularSelecionadoDireita;
        [UnityEngine.Serialization.FormerlySerializedAs("direitoAlternarExpandidoVisao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("rightToggleExplodedView")]
        private InputActionReference acaoAlternarVisaoExplodidaDireita;
        // Ações do controle esquerdo para reset, limpeza, visibilidade e painel.
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdaredefinirtodos")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftResetAll")]
        private InputActionReference acaoRestaurarTudoEsquerda;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdalimparselecao")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftClearSelection")]
        private InputActionReference acaoLimparSelecaoEsquerda;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdaalternarisolamento")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftToggleIsolation")]
        private InputActionReference acaoAlternarIsolamentoEsquerda;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdaalternarvisibilidade")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftToggleVisibility")]
        private InputActionReference acaoAlternarVisibilidadeEsquerda;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdoNavegarPainel")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftNavigatePanel")]
        private InputActionReference acaoNavegarPainelEsquerda;
        [UnityEngine.Serialization.FormerlySerializedAs("esquerdaalternarpainel")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("leftTogglePanel")]
        private InputActionReference acaoAlternarPainelEsquerda;
        [Header("Comportamento")]
        // Zona morta do analógico para evitar movimentos acidentais.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("deadzone")]
        private float zonaMorta = 0.18f;
        // Ativa atalhos de teclado/mouse para teste no editor sem Quest 2.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("enableEditorFallbacks")]
        private bool habilitarEditorAlternativas = true;
        // Quando o raio direito está sobre o painel VR, a UI recebe prioridade e o SNC não é movido por acidente.
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("bloquearManipulacaoQuandoApontaUIPainel")]
        private bool bloquearManipulacaoQuandoApontaInterfacePainel = true;
        // Distância máxima para considerar um painel world-space como bloqueador de manipulação anatômica.
        [SerializeField]
        private float distanciaMaximaBloqueioUIPainel = 8f;
        // Clicar num grupo já entra nele; desligado, o grupo fica selecionado e Entrar (B) desce um nível.
        [SerializeField]
        private bool abrirGrupoAoClicar = true;
        public bool AbreGrupoAoClicar => abrirGrupoAoClicar;
        // Ações resolvidas em runtime, vindas da referência direta ou do asset por nome.
        private InputAction acaoSelecionarDireita;
        private InputAction acaoAgarreDireito;
        private InputAction acaoVoltarDireita;
        private InputAction acaoEntrarDireita;
        private InputAction acaoManipularDireita;
        private InputAction acaoExpandirDireita;
        private InputAction acaoRestaurarEsquerda;
        private InputAction acaoLimparEsquerda;
        private InputAction acaoIsolamentoEsquerda;
        private InputAction acaoVisibilidadeEsquerda;
        private InputAction acaoNavegarEsquerda;
        private InputAction acaoPainelEsquerda;
        // Estado do grip direito usado para atualizar o grab enquanto o botão está pressionado.
        private bool direitoAgarreSegurado;
        private uint versaoAgarreDireito;
        // Buffers reutilizados para evitar alocações durante a arbitragem do ponteiro XR.
        private EventSystem bloqueioInterfaceEventoSistema;
        private TrackedDeviceEventData bloqueioInterfaceEventoDados;
        private readonly List<Vector3> bloqueioInterfaceRaioPontos = new(2);
        private readonly List<RaycastResult> bloqueioInterfaceResultados = new(8);
        /// <summary>
        /// Resolve referências e aplica a regra de navegação: por padrão, clicar num grupo já entra nele.
        /// </summary>
        private void Awake()
        {
            ResolverReferencias();
            gerenciadorSelecao?.DefinirEntradaAutomaticaNoSelecionado(abrirGrupoAoClicar);
        }

        /// <summary>
        /// Re-resolve referências e registra ações do Input System.
        /// </summary>
        private void OnEnable()
        {
            ResolverReferencias();
            VincularAcoes();
        }

        /// <summary>
        /// Remove callbacks das ações para evitar listeners duplicados.
        /// </summary>
        private void OnDisable()
        {
            // Libera somente uma captura iniciada por este mapeador. Uma mão rastreada pode possuir a
            // manipulador compartilhado enquanto o caminho de entrada do controle é desabilitado.
            if (direitoAgarreSegurado)
            {
                EncerrarAgarreDireito();
            }

            DesvincularAcoes();
        }

        /// <summary>
        /// Atualiza grab contínuo, manipulação pelo analógico direito e fallbacks de editor.
        /// </summary>
        private void Update()
        {
            var controladorDireitoDisponivel = EhDireitoControladorDisponivel();
            if (direitoAgarreSegurado && !ControlaAgarreDireito())
            {
                LimparPropriedadeAgarreDireito();
            }

            // Uma seleção XRI real na alça do painel de mundo toma completamente o controle.
            // Não continua movendo a anatomia com a mesma mão física.
            if (direitoAgarreSegurado && AgarrePainelDireitoAtivo())
            {
                EncerrarAgarreDireito();
            }

            if (direitoAgarreSegurado && !controladorDireitoDisponivel)
            {
                EncerrarAgarreDireito();
            }

            var direitoAlavanca = LerVetor2(acaoManipularDireita);
            var analogicoManipulacaoSolicitado = controladorDireitoDisponivel && direitoAlavanca.sqrMagnitude >= zonaMorta * zonaMorta;
            // A UI pode recusar um novo grab do controle (e manipulação inativa pelo analógico), mas
            // não deve pausar um grip já pertencente quando seu raio de fundo cruza
            // o painel. Pausar reteria a base antiga de grab e causaria um salto
            // quando o raio saísse novamente da UI.
            var interfaceBloqueiaNovaManipulacao = !direitoAgarreSegurado && analogicoManipulacaoSolicitado && EhPonteiroSobreBloqueioInterface();
            var painelPossuiControleDireito = AgarrePainelDireitoAtivo();
            if (direitoAgarreSegurado)
            {
                manipuladorObjetoSelecionado?.AtualizarAgarre(controladorDireito != null ? controladorDireito : transform);
                if (!ControlaAgarreDireito())
                {
                    // O alvo pode se cancelar após uma alteração de foco/seleção.
                    // Libera a posse deste mapeador antes que uma mão possa adquirir o alvo.
                    LimparPropriedadeAgarreDireito();
                }
            }

            if (!painelPossuiControleDireito && !interfaceBloqueiaNovaManipulacao && analogicoManipulacaoSolicitado && manipuladorObjetoSelecionado != null && !manipuladorObjetoSelecionado.EstaAgarrando)
            {
                manipuladorObjetoSelecionado.Manipular(direitoAlavanca);
            }

            // Analógico esquerdo: rola a aba aberta do painel, ficha ou lista (Left_NavigatePanel).
            var esquerdoAlavanca = LerVetor2(acaoNavegarEsquerda);
            if (Mathf.Abs(esquerdoAlavanca.y) >= zonaMorta)
            {
                var abas = painelNavegacao != null ? painelNavegacao.GetComponent<AbasPainelAnatomico>() : null;
                if (abas != null)
                    abas.Rolar(esquerdoAlavanca.y * Time.deltaTime);
                else
                    painelNavegacao?.Rolar(esquerdoAlavanca.y * Time.deltaTime);
            }

            ManipularEditorAlternativas();
        }

        /// <summary>
        /// Configuração usada pela ferramenta de cena para preencher todas as dependências.
        /// </summary>
        public void Configurar(RoteadorEntradaXR roteador, ControladorSelecaoPorRaio raioControlador, GerenciadorSelecaoHierarquica selecao, ManipuladorModeloAnatomico raizManipulador, ManipuladorObjetoSelecionado noManipulador, GerenciadorRestauracao redefinir, ControladorVisaoExplodida expandido, ControladorVisibilidadeAnatomica visibilidade, PainelInformacoesAnatomicas informacao, PainelNavegacaoAnatomica navegacao, Transform direito, Transform esquerda, Transform camera, InputActionAsset recursoAcao, XRInputModalityManager gerenciadorModalidade = null)
        {
            roteadorEntrada = roteador;
            controladorSelecaoPorRaio = raioControlador;
            gerenciadorSelecao = selecao;
            manipuladorModelo = raizManipulador;
            manipuladorObjetoSelecionado = noManipulador;
            gerenciadorRestauracao = redefinir;
            controladorVisaoExplodida = expandido;
            controladorVisibilidade = visibilidade;
            informacaoPainel = informacao;
            painelNavegacao = navegacao;
            controladorDireito = direito;
            controladorEsquerdo = esquerda;
            transformacaoCamera = camera;
            entradaAcoes = recursoAcao;
            gerenciadorModalidadeEntrada = gerenciadorModalidade;
            gerenciadorSelecao?.DefinirEntradaAutomaticaNoSelecionado(abrirGrupoAoClicar);
            manipuladorObjetoSelecionado?.Configurar(transform, gerenciadorSelecao, transformacaoCamera);
            ResolverBloqueioInterfaceEmissorRaio();
        }

        /// <summary>
        /// Trigger direito: atualiza hover e seleciona a estrutura apontada.
        /// </summary>
        public void SelecionarEstruturaApontada()
        {
            if (EhPonteiroSobreBloqueioInterface())
            {
                controladorSelecaoPorRaio?.LimparFoco();
                return;
            }

            controladorSelecaoPorRaio?.AtualizarFocoRaio();
            controladorSelecaoPorRaio?.SelecionarFocoAtual();
        }

        /// <summary>
        /// Grip direito pressionado: inicia grab do alvo ativo.
        /// </summary>
        public void IniciarAgarreDireito()
        {
            if (direitoAgarreSegurado || !EhDireitoControladorDisponivel())
                return;
            // Uma mão rastreada ou outra fonte de entrada já possui esta manipulação.
            if (manipuladorObjetoSelecionado == null || manipuladorObjetoSelecionado.EstaAgarrando)
                return;
            if (AgarrePainelDireitoAtivo())
            {
                LimparPropriedadeAgarreDireito();
                return;
            }

            if (EhPonteiroSobreBloqueioInterface())
            {
                LimparPropriedadeAgarreDireito();
                return;
            }

            // Corresponde à geometria de grab da mão rastreada: mantém o ponto de grip original fixo
            // enquanto o controle gira, sem inscrever controles na
            // ponte de anatomia XRI exclusiva de mãos.
            manipuladorObjetoSelecionado.IniciarAgarre(controladorDireito != null ? controladorDireito : transform, true);
            direitoAgarreSegurado = manipuladorObjetoSelecionado.EstaAgarrando;
            versaoAgarreDireito = direitoAgarreSegurado ? manipuladorObjetoSelecionado.VersaoAgarre : 0;
        }

        /// <summary>
        /// Grip direito solto: encerra grab.
        /// </summary>
        public void EncerrarAgarreDireito()
        {
            if (!direitoAgarreSegurado)
                return;
            var controlaAgarre = ControlaAgarreDireito();
            LimparPropriedadeAgarreDireito();
            if (controlaAgarre)
            {
                manipuladorObjetoSelecionado.EncerrarAgarre();
            }
        }

        private bool ControlaAgarreDireito() => direitoAgarreSegurado && manipuladorObjetoSelecionado != null && manipuladorObjetoSelecionado.EstaAgarrando && manipuladorObjetoSelecionado.VersaoAgarre == versaoAgarreDireito;
        private void LimparPropriedadeAgarreDireito()
        {
            direitoAgarreSegurado = false;
            versaoAgarreDireito = 0;
        }

        /// <summary>
        /// Retorna o grupo de controle pertencente à modalidade em vez de adivinhar um
        /// filho interactor pelo nome. Raios Near/Far podem ser descendentes desta raiz.
        /// </summary>
        private Transform ResolverRaizControladorDireito()
        {
            if (gerenciadorModalidadeEntrada == null)
            {
                gerenciadorModalidadeEntrada = FindFirstObjectByType<XRInputModalityManager>();
            }

            return gerenciadorModalidadeEntrada != null && gerenciadorModalidadeEntrada.rightController != null ? gerenciadorModalidadeEntrada.rightController.transform : controladorDireito != null ? controladorDireito : transform;
        }

        private bool AgarrePainelDireitoAtivo() => ControlePainelMundo.AgarrePainelAtivoParaMao(ResolverRaizControladorDireito());
        /// <summary>
        /// A referência nula mantém o fallback de editor/simulação; uma referência
        /// configurada precisa continuar ativa para possuir a manipulação.
        /// </summary>
        private bool EhDireitoControladorDisponivel() => controladorDireito == null || controladorDireito.gameObject.activeInHierarchy;
        /// <summary>
        /// Botão de entrar: aprofunda no nó selecionado, se ele tiver filhos.
        /// </summary>
        public void EntrarNoSelecionado()
        {
            gerenciadorSelecao?.EntrarNoSelecionado();
        }

        /// <summary>
        /// Botão de voltar: sobe para o pai do foco atual.
        /// </summary>
        public void VoltarAoNoPai()
        {
            gerenciadorSelecao?.VoltarAoNoPai();
        }

        /// <summary>
        /// Alterna painel VR entre aberto e recolhido.
        /// </summary>
        public void AlternarPainel()
        {
            informacaoPainel?.AlternarPainel();
        }

        /// <summary>
        /// Resolve componentes locais e referências globais leves, sem criar objetos.
        /// </summary>
        private void ResolverReferencias()
        {
            roteadorEntrada ??= GetComponent<RoteadorEntradaXR>();
            controladorSelecaoPorRaio ??= GetComponent<ControladorSelecaoPorRaio>();
            gerenciadorSelecao ??= GetComponent<GerenciadorSelecaoHierarquica>();
            manipuladorModelo ??= GetComponent<ManipuladorModeloAnatomico>();
            manipuladorObjetoSelecionado ??= GetComponent<ManipuladorObjetoSelecionado>();
            gerenciadorRestauracao ??= GetComponent<GerenciadorRestauracao>();
            controladorVisaoExplodida ??= GetComponent<ControladorVisaoExplodida>();
            controladorVisibilidade ??= GetComponent<ControladorVisibilidadeAnatomica>();
            informacaoPainel ??= FindFirstObjectByType<PainelInformacoesAnatomicas>();
            painelNavegacao ??= FindFirstObjectByType<PainelNavegacaoAnatomica>();
            if (transformacaoCamera == null && Camera.main != null)
            {
                transformacaoCamera = Camera.main.transform;
            }

            ResolverBloqueioInterfaceEmissorRaio();
        }

        /// <summary>
        /// Localiza ações no asset e liga cada uma ao método correspondente.
        /// </summary>
        private void VincularAcoes()
        {
            acaoSelecionarDireita = ResolverAcao(acaoSelecionarEstruturaDireita, "Right_SelectStructure");
            acaoAgarreDireito = ResolverAcao(agarreDireitoSelecionado, "Right_GrabSelected");
            acaoVoltarDireita = ResolverAcao(acaoVoltarAoPaiDireita, "Right_BackToParent");
            acaoEntrarDireita = ResolverAcao(acaoEntrarNoSelecionadoDireita, "Right_EnterSelectedNode");
            acaoManipularDireita = ResolverAcao(acaoManipularSelecionadoDireita, "Right_ManipulateSelected");
            acaoExpandirDireita = ResolverAcao(acaoAlternarVisaoExplodidaDireita, "Right_ToggleExplodedView");
            acaoRestaurarEsquerda = ResolverAcao(acaoRestaurarTudoEsquerda, "Left_ResetAll");
            acaoLimparEsquerda = ResolverAcao(acaoLimparSelecaoEsquerda, "Left_ClearSelection");
            acaoIsolamentoEsquerda = ResolverAcao(acaoAlternarIsolamentoEsquerda, "Left_ToggleIsolation");
            acaoVisibilidadeEsquerda = ResolverAcao(acaoAlternarVisibilidadeEsquerda, "Left_ToggleVisibility");
            acaoNavegarEsquerda = ResolverAcao(acaoNavegarPainelEsquerda, "Left_NavigatePanel");
            acaoPainelEsquerda = ResolverAcao(acaoAlternarPainelEsquerda, "Left_TogglePanel");
            HabilitarBotao(acaoSelecionarDireita, EmDireitoSelecionar);
            HabilitarBotao(acaoVoltarDireita, EmDireitoVoltar);
            HabilitarBotao(acaoEntrarDireita, EmDireitoEntrar);
            HabilitarBotao(acaoExpandirDireita, EmDireitoExpandir);
            HabilitarBotao(acaoRestaurarEsquerda, EmEsquerdoRedefinir);
            HabilitarBotao(acaoLimparEsquerda, EmEsquerdoLimpar);
            HabilitarBotao(acaoIsolamentoEsquerda, EmEsquerdoIsolamento);
            HabilitarBotao(acaoVisibilidadeEsquerda, EmEsquerdoVisibilidade);
            HabilitarBotao(acaoPainelEsquerda, EmEsquerdoPainel);
            if (acaoAgarreDireito != null)
            {
                acaoAgarreDireito.Enable();
                acaoAgarreDireito.performed += EmDireitoAgarrarExecutado;
                acaoAgarreDireito.canceled += EmDireitoAgarrarCancelado;
            }

            acaoManipularDireita?.Enable();
            acaoNavegarEsquerda?.Enable();
        }

        /// <summary>
        /// Desliga callbacks das ações mapeadas.
        /// </summary>
        private void DesvincularAcoes()
        {
            DesabilitarBotao(acaoSelecionarDireita, EmDireitoSelecionar);
            DesabilitarBotao(acaoVoltarDireita, EmDireitoVoltar);
            DesabilitarBotao(acaoEntrarDireita, EmDireitoEntrar);
            DesabilitarBotao(acaoExpandirDireita, EmDireitoExpandir);
            DesabilitarBotao(acaoRestaurarEsquerda, EmEsquerdoRedefinir);
            DesabilitarBotao(acaoLimparEsquerda, EmEsquerdoLimpar);
            DesabilitarBotao(acaoIsolamentoEsquerda, EmEsquerdoIsolamento);
            DesabilitarBotao(acaoVisibilidadeEsquerda, EmEsquerdoVisibilidade);
            DesabilitarBotao(acaoPainelEsquerda, EmEsquerdoPainel);
            if (acaoAgarreDireito != null)
            {
                acaoAgarreDireito.performed -= EmDireitoAgarrarExecutado;
                acaoAgarreDireito.canceled -= EmDireitoAgarrarCancelado;
            }
        }

        /// <summary>
        /// Resolve uma ação por referência direta ou por nome dentro do Action Map BrainControls.
        /// </summary>
        private InputAction ResolverAcao(InputActionReference referencia, string nomeAcao)
        {
            if (referencia != null && referencia.action != null)
            {
                return referencia.action;
            }

            if (entradaAcoes == null)
            {
                return null;
            }

            return entradaAcoes.FindAction($"{NomeMapaControlesAnatomia}/{nomeAcao}", false) ?? entradaAcoes.FindAction(nomeAcao, false);
        }

        // Callbacks discretos do Meta Quest 2. Cada um chama o controlador responsável pela lógica real.
        private void EmDireitoSelecionar(InputAction.CallbackContext _) => SelecionarEstruturaApontada();
        private void EmDireitoVoltar(InputAction.CallbackContext _) => VoltarAoNoPai();
        private void EmDireitoEntrar(InputAction.CallbackContext _) => EntrarNoSelecionado();
        private void EmDireitoExpandir(InputAction.CallbackContext _) => AlternarVisaoExplodidaEAtualizarPainel();
        private void EmEsquerdoRedefinir(InputAction.CallbackContext _) => gerenciadorRestauracao?.RestaurarTudo();
        private void EmEsquerdoLimpar(InputAction.CallbackContext _) => LimparSelecaoEAtualizarPainel();
        private void EmEsquerdoIsolamento(InputAction.CallbackContext _) => AlternarIsolamentoEAtualizarPainel();
        private void EmEsquerdoVisibilidade(InputAction.CallbackContext _) => controladorVisibilidade?.AlternarNoOuRestaurar(gerenciadorSelecao?.NoSelecionado);
        private void EmEsquerdoPainel(InputAction.CallbackContext _) => AlternarPainel();
        private void EmDireitoAgarrarExecutado(InputAction.CallbackContext _) => IniciarAgarreDireito();
        private void EmDireitoAgarrarCancelado(InputAction.CallbackContext _) => EncerrarAgarreDireito();
        private void AlternarVisaoExplodidaEAtualizarPainel()
        {
            controladorVisaoExplodida?.AlternarExpansaoNivelAtual();
            painelNavegacao?.AtualizarEstadosControles();
        }

        private void LimparSelecaoEAtualizarPainel()
        {
            gerenciadorSelecao?.LimparSelecao();
            painelNavegacao?.AtualizarEstadosControles();
        }

        private void AlternarIsolamentoEAtualizarPainel()
        {
            gerenciadorSelecao?.AlternarIsolamento();
            painelNavegacao?.AtualizarEstadosControles();
        }

        /// <summary>
        /// Habilita uma ação de botão e registra callback.
        /// </summary>
        private static void HabilitarBotao(InputAction acao, Action<InputAction.CallbackContext> aoExecutar)
        {
            if (acao == null)
            {
                return;
            }

            acao.Enable();
            acao.performed += aoExecutar;
        }

        /// <summary>
        /// Remove callback de uma ação de botão.
        /// </summary>
        private static void DesabilitarBotao(InputAction acao, Action<InputAction.CallbackContext> aoExecutar)
        {
            if (acao == null)
            {
                return;
            }

            acao.performed -= aoExecutar;
        }

        /// <summary>
        /// Lê um Vector2 de forma defensiva para não quebrar caso o binding esteja ausente.
        /// </summary>
        private static Vector2 LerVetor2(InputAction acao)
        {
            if (acao == null)
            {
                return Vector2.zero;
            }

            try
            {
                return acao.ReadValue<Vector2>();
            }
            catch
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Resolve e configura o raycaster do painel anatômico.
        /// </summary>
        private void ResolverBloqueioInterfaceEmissorRaio()
        {
            if (bloqueioInterfaceEmissorRaio == null && informacaoPainel != null)
            {
                bloqueioInterfaceEmissorRaio = informacaoPainel.GetComponentInParent<TrackedDeviceGraphicRaycaster>();
            }

            if (bloqueioInterfaceEmissorRaio == null)
            {
                return;
            }

            bloqueioInterfaceEmissorRaio.checkFor3DOcclusion = true;
            bloqueioInterfaceEmissorRaio.blockingMask = ~0;
            bloqueioInterfaceEmissorRaio.raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
        }

        /// <summary>
        /// Retorna a distância do primeiro Graphic realmente atingido no painel anatômico.
        /// Graphics inativos, mascarados ou sem raycastTarget são filtrados pelo raycaster XR.
        /// </summary>
        private bool TentarObterMaisProximoBloqueioInterfaceAcerto(Transform ponteiro, float distanciaMaxima, out float distanciaAcerto)
        {
            distanciaAcerto = float.PositiveInfinity;
            if (ponteiro == null || bloqueioInterfaceEmissorRaio == null || !bloqueioInterfaceEmissorRaio.isActiveAndEnabled)
            {
                return false;
            }

            var eventoSistema = EventSystem.current;
            if (eventoSistema == null)
            {
                return false;
            }

            if (bloqueioInterfaceEventoDados == null || bloqueioInterfaceEventoSistema != eventoSistema)
            {
                bloqueioInterfaceEventoSistema = eventoSistema;
                bloqueioInterfaceEventoDados = new TrackedDeviceEventData(eventoSistema);
            }

            var distanciaProjecao = Mathf.Max(0.01f, distanciaMaxima);
            bloqueioInterfaceRaioPontos.Clear();
            bloqueioInterfaceRaioPontos.Add(ponteiro.position);
            bloqueioInterfaceRaioPontos.Add(ponteiro.position + ponteiro.forward * distanciaProjecao);
            bloqueioInterfaceEventoDados.rayPoints = bloqueioInterfaceRaioPontos;
            bloqueioInterfaceEventoDados.layerMask = ~0;
            bloqueioInterfaceResultados.Clear();
            bloqueioInterfaceEmissorRaio.Raycast(bloqueioInterfaceEventoDados, bloqueioInterfaceResultados);
            foreach (var resultado in bloqueioInterfaceResultados)
            {
                if (resultado.gameObject == null || !resultado.gameObject.activeInHierarchy || resultado.distance < 0f)
                {
                    continue;
                }

                if (resultado.distance < distanciaAcerto)
                {
                    distanciaAcerto = resultado.distance;
                }
            }

            return distanciaAcerto < float.PositiveInfinity;
        }

        /// <summary>
        /// Dá prioridade à UI somente quando um Graphic ativo é o hit mais próximo do mesmo raio.
        /// </summary>
        private bool EhPonteiroSobreBloqueioInterface()
        {
            if (!bloquearManipulacaoQuandoApontaInterfacePainel)
            {
                return false;
            }

            var ponteiro = controladorSelecaoPorRaio != null ? controladorSelecaoPorRaio.OrigemRaioAtiva : controladorDireito != null ? controladorDireito : transformacaoCamera != null ? transformacaoCamera : Camera.main != null ? Camera.main.transform : null;
            if (!TentarObterMaisProximoBloqueioInterfaceAcerto(ponteiro, distanciaMaximaBloqueioUIPainel, out var interfaceDistancia))
            {
                return false;
            }

            var anatomiaDistancia = float.PositiveInfinity;
            var anatomiaFoiAcerto = controladorSelecaoPorRaio != null && controladorSelecaoPorRaio.TentarObterAcertoValidoMaisProximo(out _, out anatomiaDistancia);
            // Em empate quase coplanar a UI vence, impedindo que um único trigger execute duas ações.
            const float distanciaEpsilon = 0.001f;
            return !anatomiaFoiAcerto || interfaceDistancia <= anatomiaDistancia + distanciaEpsilon;
        }

        /// <summary>
        /// Atalhos de editor para validar a cena sem headset, preservando os mesmos controladores.
        /// </summary>
        public bool ProcessaAtalhosDoEditor => isActiveAndEnabled && habilitarEditorAlternativas;

        /// <summary>
        /// Verdadeiro enquanto o XR Interaction Simulator do XRI roda no Editor. Ele usa Q/E, R, C, V e I para mover
        /// a cabeça e os controles simulados, então os atalhos próprios ficam desligados para não dispararem junto.
        /// </summary>
        public static bool SimuladorXRAtivo => UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRInteractionSimulator.instance != null;

        private void ManipularEditorAlternativas()
        {
#if UNITY_EDITOR
            if (!habilitarEditorAlternativas || Keyboard.current == null || SimuladorXRAtivo)
            {
                return;
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
                gerenciadorRestauracao?.RestaurarTudo();
            if (Keyboard.current.qKey.wasPressedThisFrame)
                gerenciadorSelecao?.VoltarAoNoPai();
            if (Keyboard.current.eKey.wasPressedThisFrame)
                AlternarVisaoExplodidaEAtualizarPainel();
            if (Keyboard.current.cKey.wasPressedThisFrame)
                LimparSelecaoEAtualizarPainel();
            if (Keyboard.current.iKey.wasPressedThisFrame)
                AlternarIsolamentoEAtualizarPainel();
            if (Keyboard.current.vKey.wasPressedThisFrame)
                controladorVisibilidade?.AlternarNoOuRestaurar(gerenciadorSelecao?.NoSelecionado);
            if (Keyboard.current.enterKey.wasPressedThisFrame)
                SelecionarEstruturaApontada();
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                SelecionarEstruturaApontada();
            }
#endif
        }

#if UNITY_EDITOR
        private const string CaminhoCena = "Assets/Scenes/SampleScene.unity";
        private const string CaminhoRecursoEntrada = "Assets/Input/VR_SNC_MetaQuest2_InputActions.inputactions";
        private const string NomeRaiz = "Sistema_Nervoso_Central";
        private const string LegadoRaizNome = "Brain_AnatomicalOrganization";
        private const string VRPainelNome = "Painel_Anatomico_VR_SNC";
        private const string LegadoVRPainelNome = "Brain_VR_AnatomyPanel";
        private const string AntigoPainelNome = "Painel_Antigo_SNC";
        private const string LegadoAntigoPainelNome = "Brain_UI_Panel";
        private const string NomePonteiroDireito = "Ponteiro_Direito_SNC";
        private const string LegadoDireitoPonteiroNome = "Brain_Right_LaserPointer";
        private const string Nome = "Brain";
        /// <summary>
        /// Entrada para execução em batchmode: abre a SampleScene e aplica o setup.
        /// </summary>
        public static void ConfigurarCenaPelaLinhaDeComando()
        {
            if (SceneManager.GetActiveScene().path != CaminhoCena)
            {
                EditorSceneManager.OpenScene(CaminhoCena);
            }

            ConfiguracaoCena(salvarCena: true);
        }

        /// <summary>
        /// Menu de editor para aplicar o setup manualmente quando necessário.
        /// </summary>
        [MenuItem("Ferramentas/VR SNC/Configurar Entrada do Meta Quest 2 e Painel Anatômico")]
        public static void ConfigurarCenaPeloMenu()
        {
            ConfiguracaoCena(salvarCena: false);
        }

        /// <summary>
        /// Configura referências de cena para o protótipo.
        /// Esta rotina é de editor; a execução normal do jogo não recria painel nem hierarquia.
        /// </summary>
        private static void ConfiguracaoCena(bool salvarCena)
        {
            if (!File.Exists(CaminhoRecursoEntrada))
            {
                Debug.LogError($"[VR SNC] InputActionAsset não encontrado: {CaminhoRecursoEntrada}");
                return;
            }

            AssetDatabase.ImportAsset(CaminhoRecursoEntrada, ImportAssetOptions.ForceUpdate);
            var entradaRecurso = AssetDatabase.LoadAssetAtPath<InputActionAsset>(CaminhoRecursoEntrada);
            if (entradaRecurso == null)
            {
                Debug.LogError($"[VR SNC] Falha ao carregar InputActionAsset: {CaminhoRecursoEntrada}");
                return;
            }

            var raizObjeto = EncontrarObjetoCena(NomeRaiz, LegadoRaizNome);
            var cerebro = EncontrarObjetoCena(Nome);
            if (raizObjeto == null)
            {
                Debug.LogError("[VR SNC] Sistema_Nervoso_Central/Brain_AnatomicalOrganization não encontrado. Setup abortado.");
                return;
            }

            if (cerebro != null)
            {
                cerebro.SetActive(false);
                EditorUtility.SetDirty(cerebro);
            }

            raizObjeto.SetActive(true);
            EditorUtility.SetDirty(raizObjeto);
            var raizNo = raizObjeto.GetComponent<NoInteracaoAnatomica>();
            var selecao = raizObjeto.GetComponent<GerenciadorSelecaoHierarquica>();
            var raio = raizObjeto.GetComponent<ControladorSelecaoPorRaio>();
            var modelo = raizObjeto.GetComponent<ManipuladorModeloAnatomico>();
            var redefinir = raizObjeto.GetComponent<GerenciadorRestauracao>();
            var expandido = raizObjeto.GetComponent<ControladorVisaoExplodida>();
            var visibilidade = raizObjeto.GetComponent<ControladorVisibilidadeAnatomica>();
            var roteador = raizObjeto.GetComponent<RoteadorEntradaXR>();
            var manipuladorSelecionado = GarantirComponente<ManipuladorObjetoSelecionado>(raizObjeto);
            var mapeador = GarantirComponente<MapeadorEntradaMetaQuest2>(raizObjeto);
            selecao?.DefinirEntradaAutomaticaNoSelecionado(mapeador.AbreGrupoAoClicar);
            var transformacaoCamera = Camera.main != null ? Camera.main.transform : EncontrarTransformacaoCamera();
            var direito = EncontrarControladorSemelhanteTransformacao(raizObjeto.transform, true) ?? transformacaoCamera;
            var esquerda = EncontrarControladorSemelhanteTransformacao(raizObjeto.transform, false);
            raio?.DefinirOrigemRaio(direito);
            manipuladorSelecionado.Configurar(raizObjeto.transform, selecao, transformacaoCamera);
            var painel = GarantirPainelAnatomia(selecao, redefinir, expandido, visibilidade, transformacaoCamera, out var informacaoPainel, out var navegacaoPainel);
            selecao?.Configurar(raizNo, visibilidade, informacaoPainel, navegacaoPainel);
            mapeador.Configurar(roteador, raio, selecao, modelo, manipuladorSelecionado, redefinir, expandido, visibilidade, informacaoPainel, navegacaoPainel, direito, esquerda, transformacaoCamera, entradaRecurso);
            GarantirLaser(raizObjeto, raio, direito);
            GarantirEventoSistemaParaXRInterface();
            DesabilitarLocomocaoETeletransporteNovamente();
            EditorUtility.SetDirty(mapeador);
            EditorUtility.SetDirty(manipuladorSelecionado);
            EditorUtility.SetDirty(painel);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[VR SNC] Configuração de entrada do Meta Quest 2 e painel anatômico de VR concluída.");
            Debug.Log("[VR SNC] Mapa de Ações: BrainControls. Ações: Right_SelectStructure, Right_GrabSelected, Right_BackToParent, Right_EnterSelectedNode, Right_ManipulateSelected, Right_ToggleExplodedView, Left_ResetAll, Left_ClearSelection, Left_ToggleIsolation, Left_ToggleVisibility, Left_NavigatePanel, Left_TogglePanel.");
            Debug.Log($"[VR SNC] Referência do raio/controle direito: {(direito != null ? ObterCaminhoHierarquia(direito) : "não encontrado")}");
            Debug.Log($"[VR SNC] Referência do controle esquerdo: {(esquerda != null ? ObterCaminhoHierarquia(esquerda) : "não encontrado")}");
            Debug.Log("[VR SNC] Painel_Antigo_SNC preservado e desativado se existir. Painel_Anatomico_VR_SNC ativo.");
            if (salvarCena)
            {
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Debug.Log($"[VR SNC] Cena salva: {CaminhoCena}");
            }
        }

        private static GameObject GarantirPainelAnatomia(GerenciadorSelecaoHierarquica selecao, GerenciadorRestauracao redefinir, ControladorVisaoExplodida expandido, ControladorVisibilidadeAnatomica visibilidade, Transform transformacaoCamera, out PainelInformacoesAnatomicas informacaoPainel, out PainelNavegacaoAnatomica navegacaoPainel)
        {
            // Mantém o painel antigo na cena, apenas desativado, para preservar referências e histórico do protótipo.
            var antigoPainel = EncontrarObjetoCena(AntigoPainelNome, LegadoAntigoPainelNome);
            if (antigoPainel != null)
            {
                antigoPainel.SetActive(false);
                EditorUtility.SetDirty(antigoPainel);
            }

            // Procura tanto o nome novo em português quanto o nome técnico antigo para evitar duplicação.
            var painel = EncontrarObjetoCena(VRPainelNome, LegadoVRPainelNome);
            if (painel == null)
            {
                painel = new GameObject(VRPainelNome, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }
            else
            {
                // Esta limpeza pertence somente ao setup de editor; a execução normal do jogo não recria o painel.
                LimparFilhos(painel.transform);
            }

            painel.layer = 5;
            var retangulo = painel.GetComponent<RectTransform>();
            retangulo.sizeDelta = new Vector2(560f, 650f);
            retangulo.localScale = Vector3.one * 0.0018f;
            PosicionarPainel(painel.transform, transformacaoCamera);
            var canvas = painel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = transformacaoCamera != null ? transformacaoCamera.GetComponent<Camera>() : Camera.main;
            canvas.sortingOrder = 50;
            var escalonador = painel.GetComponent<CanvasScaler>();
            escalonador.dynamicPixelsPerUnit = 18f;
            GarantirRastreadoDispositivoElementoVisualEmissorRaio(painel);
            var seguir = GarantirComponente<AcompanhamentoCameraPainelInterface>(painel);
            seguir.Configurar(transformacaoCamera, true);
            var raizExpandida = CriarRetangulo("ExpandedRoot", retangulo, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fundo = CriarImagem("Background", raizExpandida.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.025f, 0.035f, 0.88f));
            var contorno = fundo.gameObject.AddComponent<Outline>();
            contorno.effectColor = new Color(0f, 0.58f, 1f, 0.95f);
            contorno.effectDistance = new Vector2(2f, -2f);
            var cabecalho = CriarImagem("Header", raizExpandida.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -86f), new Vector2(-16f, -14f), new Color(0.04f, 0.08f, 0.12f, 0.94f));
            var titulo = CriarTexto("Title", cabecalho.transform, new Vector2(0f, 0.55f), new Vector2(0.45f, 1f), new Vector2(12f, 0f), new Vector2(0f, 0f), 28, TextAnchor.MiddleLeft, "VR SNC");
            var estado = CriarTexto("Status", cabecalho.transform, new Vector2(0.45f, 0.55f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-12f, 0f), 16, TextAnchor.MiddleRight, "Nenhuma estrutura selecionada");
            var caminho = CriarTexto("Path", cabecalho.transform, new Vector2(0f, 0f), new Vector2(1f, 0.55f), new Vector2(12f, 4f), new Vector2(-12f, -2f), 15, TextAnchor.MiddleLeft, "Caminho anatômico: Sistema Nervoso Central");
            var informacaoRaiz = CriarImagem("InfoPanel", raizExpandida.transform, new Vector2(0f, 0.47f), new Vector2(1f, 0.86f), new Vector2(16f, 0f), new Vector2(-16f, -4f), new Color(0.06f, 0.075f, 0.095f, 0.86f));
            var nomeNo = CriarTexto("NodeName", informacaoRaiz.transform, new Vector2(0f, 0.72f), new Vector2(1f, 1f), new Vector2(14f, 0f), new Vector2(-14f, -4f), 22, TextAnchor.MiddleLeft, "Nenhuma estrutura selecionada");
            var exibicao = CriarTexto("DisplayName", informacaoRaiz.transform, new Vector2(0f, 0.55f), new Vector2(1f, 0.74f), new Vector2(14f, 0f), new Vector2(-14f, 0f), 16, TextAnchor.MiddleLeft, "-");
            var divisao = CriarTexto("Division", informacaoRaiz.transform, new Vector2(0f, 0.34f), new Vector2(1f, 0.56f), new Vector2(14f, 0f), new Vector2(-14f, 0f), 14, TextAnchor.MiddleLeft, "Divisão: -");
            var nivel = CriarTexto("Level", informacaoRaiz.transform, new Vector2(0f, 0.22f), new Vector2(1f, 0.36f), new Vector2(14f, 0f), new Vector2(-14f, 0f), 14, TextAnchor.MiddleLeft, "Nível: -");
            var descricao = CriarTexto("Description", informacaoRaiz.transform, new Vector2(0f, 0f), new Vector2(1f, 0.23f), new Vector2(14f, 8f), new Vector2(-14f, 0f), 14, TextAnchor.UpperLeft, "Nenhuma estrutura selecionada");
            var listaRaiz = CriarImagem("NavigationPanel", raizExpandida.transform, new Vector2(0f, 0.18f), new Vector2(1f, 0.47f), new Vector2(16f, 0f), new Vector2(-16f, -6f), new Color(0.04f, 0.055f, 0.075f, 0.86f));
            var listaTitulo = CriarTexto("NavigationTitle", listaRaiz.transform, new Vector2(0f, 0.83f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f), 16, TextAnchor.MiddleLeft, "Estruturas de: Sistema Nervoso Central");
            var areaVisivel = CriarImagem("Viewport", listaRaiz.transform, new Vector2(0f, 0f), new Vector2(1f, 0.82f), new Vector2(12f, 10f), new Vector2(-12f, -4f), new Color(0f, 0f, 0f, 0.16f));
            var mascara = areaVisivel.gameObject.AddComponent<Mask>();
            mascara.showMaskGraphic = false;
            var conteudo = CriarRetangulo("Content", areaVisivel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            // Pivô no topo: uma lista curta fica encostada no alto da área, e não centralizada.
            conteudo.pivot = new Vector2(0.5f, 1f);
            var organizacaoConteudo = conteudo.gameObject.AddComponent<VerticalLayoutGroup>();
            organizacaoConteudo.childControlHeight = false;
            organizacaoConteudo.childControlWidth = true;
            organizacaoConteudo.childForceExpandHeight = false;
            organizacaoConteudo.childForceExpandWidth = true;
            organizacaoConteudo.spacing = 6f;
            var ajustador = conteudo.gameObject.AddComponent<ContentSizeFitter>();
            ajustador.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var rolagem = listaRaiz.gameObject.AddComponent<ScrollRect>();
            rolagem.viewport = areaVisivel.rectTransform;
            rolagem.content = conteudo;
            rolagem.horizontal = false;
            rolagem.vertical = true;
            rolagem.movementType = ScrollRect.MovementType.Clamped;
            var controles = CriarRetangulo("Controls", raizExpandida.transform, new Vector2(0f, 0f), new Vector2(1f, 0.18f), new Vector2(16f, 14f), new Vector2(-16f, -8f));
            var grade = controles.gameObject.AddComponent<GridLayoutGroup>();
            grade.cellSize = new Vector2(125f, 34f);
            grade.spacing = new Vector2(8f, 8f);
            grade.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grade.constraintCount = 4;
            var voltarBotao = CriarBotao("Button_Back", controles.transform, "Voltar");
            var entrarBotao = CriarBotao("Button_Enter", controles.transform, "Entrar");
            var redefinirBotao = CriarBotao("Button_Reset", controles.transform, "Resetar SNC");
            var expandirBotao = CriarBotao("Button_Explode", controles.transform, "Restaurar");
            var isolarBotao = CriarBotao("Button_Isolate", controles.transform, "Isolar");
            var limparBotao = CriarBotao("Button_Clear", controles.transform, "Limpar seleção");
            var alternarListaBotao = CriarBotao("Button_ToggleList", controles.transform, "Ocultar lista");
            var recolherBotao = CriarBotao("Button_CollapsePanel", controles.transform, "Minimizar");
            var recolhidaRaiz = CriarImagem("CollapsedTab", retangulo, new Vector2(0f, 0.82f), new Vector2(0.42f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Color(0.02f, 0.025f, 0.035f, 0.92f));
            var expandirBotao2 = CriarBotao("Button_ExpandPanel", recolhidaRaiz.transform, "Mostrar painel");
            var expandirRetangulo = expandirBotao2.GetComponent<RectTransform>();
            expandirRetangulo.anchorMin = Vector2.zero;
            expandirRetangulo.anchorMax = Vector2.one;
            expandirRetangulo.offsetMin = new Vector2(8f, 8f);
            expandirRetangulo.offsetMax = new Vector2(-8f, -8f);
            informacaoPainel = GarantirComponente<PainelInformacoesAnatomicas>(painel);
            navegacaoPainel = GarantirComponente<PainelNavegacaoAnatomica>(painel);
            informacaoPainel.ConfigurarPainelVR(painel, raizExpandida.gameObject, recolhidaRaiz.gameObject, titulo, estado, caminho, nomeNo, exibicao, descricao, divisao, nivel, recolherBotao, expandirBotao2);
            navegacaoPainel.ConfigurarVR(selecao, conteudo, listaRaiz.gameObject, listaTitulo, alternarListaBotao.GetComponentInChildren<Text>(), voltarBotao, entrarBotao, redefinirBotao, expandirBotao, isolarBotao, limparBotao, alternarListaBotao, redefinir, expandido);
            recolhidaRaiz.gameObject.SetActive(false);
            EditorUtility.SetDirty(informacaoPainel);
            EditorUtility.SetDirty(navegacaoPainel);
            return painel;
        }

        /// <summary>
        /// Garante o objeto visual do ponteiro direito, aceitando nome novo e nome antigo como fallback.
        /// </summary>
        private static void GarantirLaser(GameObject raizObjeto, ControladorSelecaoPorRaio raio, Transform direito)
        {
            var laserObjeto = EncontrarObjetoCena(NomePonteiroDireito, LegadoDireitoPonteiroNome);
            if (laserObjeto == null)
            {
                laserObjeto = new GameObject(NomePonteiroDireito);
                laserObjeto.transform.SetParent(direito != null ? direito : raizObjeto.transform, false);
            }

            var linha = GarantirComponente<LineRenderer>(laserObjeto);
            var laser = GarantirComponente<VisualPonteiroLaser>(laserObjeto);
            laser.Configurar(raio, direito);
            linha.enabled = true;
            EditorUtility.SetDirty(laserObjeto);
        }

        /// <summary>
        /// Garante um EventSystem compatível com UI XR, sem substituir o existente.
        /// </summary>
        private static void GarantirEventoSistemaParaXRInterface()
        {
            var eventoSistema = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventoSistema == null)
            {
                var eventoObjeto = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                EditorUtility.SetDirty(eventoObjeto);
                return;
            }

            // XRUIInputModule is already a BaseInputModule. Adding a second UI input
            // módulo faz uma única ação do controle ser despachada duas vezes em VR.
            if (eventoSistema.GetComponent<BaseInputModule>() == null)
            {
                eventoSistema.gameObject.AddComponent<InputSystemUIInputModule>();
                EditorUtility.SetDirty(eventoSistema.gameObject);
            }
        }

        /// <summary>
        /// Desativa componentes e objetos de locomoção/teleporte para manter o protótipo fixo.
        /// </summary>
        private static void DesabilitarLocomocaoETeletransporteNovamente()
        {
            var componenteTokens = new[] { "TeleportationProvider", "TeleportationArea", "TeleportationAnchor", "LocomotionSystem", "ContinuousMoveProvider", "ContinuousTurnProvider", "SnapTurnProvider" };
            foreach (var comportamento in FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (comportamento == null)
                {
                    continue;
                }

                var tipoNome = comportamento.GetType().Name;
                foreach (var token in componenteTokens)
                {
                    if (tipoNome.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        comportamento.enabled = false;
                        EditorUtility.SetDirty(comportamento);
                        break;
                    }
                }
            }

            var objetoTokens = new[] { "Teleport Area Setup", "Teleportation Area", "Teleport Area", "Teleport Anchor", "Locomotion" };
            foreach (var transformacao in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transformacao == null || transformacao.gameObject.name.IndexOf("XRRay", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                foreach (var token2 in objetoTokens)
                {
                    if (transformacao.gameObject.name.IndexOf(token2, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        transformacao.gameObject.SetActive(false);
                        EditorUtility.SetDirty(transformacao.gameObject);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adiciona TrackedDeviceGraphicRaycaster quando o pacote XR Interaction Toolkit está disponível.
        /// </summary>
        private static void GarantirRastreadoDispositivoElementoVisualEmissorRaio(GameObject painel)
        {
            var emissorRaio = GarantirComponente<TrackedDeviceGraphicRaycaster>(painel);
            emissorRaio.checkFor3DOcclusion = true;
            emissorRaio.blockingMask = ~0;
            emissorRaio.raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
            EditorUtility.SetDirty(emissorRaio);
        }

        /// <summary>
        /// Retorna um componente existente ou adiciona o componente exigido pelo setup de editor.
        /// </summary>
        private static T GarantirComponente<T>(GameObject alvo)
            where T : Component
        {
            var componente = alvo.GetComponent<T>();
            if (componente == null)
            {
                componente = alvo.AddComponent<T>();
            }

            return componente;
        }

        /// <summary>
        /// Busca objetos da cena por uma lista de nomes, permitindo fallbacks antigo/novo.
        /// </summary>
        private static GameObject EncontrarObjetoCena(params string[] objetoNomes)
        {
            foreach (var transformacao in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transformacao == null || !transformacao.gameObject.scene.IsValid())
                {
                    continue;
                }

                foreach (var objetoNome in objetoNomes)
                {
                    if (string.Equals(transformacao.gameObject.name, objetoNome, StringComparison.Ordinal))
                    {
                        return transformacao.gameObject;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Encontra a primeira câmera válida da cena quando Camera.main não está disponível.
        /// </summary>
        private static Transform EncontrarTransformacaoCamera()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera != null && camera.gameObject.scene.IsValid())
                {
                    return camera.transform;
                }
            }

            return null;
        }

        /// <summary>
        /// Procura uma referência provável de controle/interactor do lado informado, evitando filhos da anatomia.
        /// </summary>
        private static Transform EncontrarControladorSemelhanteTransformacao(Transform anatomiaRaiz, bool direito)
        {
            var lado = direito ? "right" : "left";
            Transform melhor = null;
            var melhorPontuacao = -1;
            foreach (var transformacao in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transformacao == null || !transformacao.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (anatomiaRaiz != null && transformacao.IsChildOf(anatomiaRaiz))
                {
                    continue;
                }

                var nome = transformacao.name.ToLowerInvariant();
                if (!nome.Contains(lado))
                {
                    continue;
                }

                var pontuacao = 0;
                if (nome.Contains("controller"))
                    pontuacao += 4;
                if (nome.Contains("near") || nome.Contains("far") || nome.Contains("ray"))
                    pontuacao += 5;
                if (nome.Contains("interator"))
                    pontuacao += 3;
                if (nome.Contains("hand"))
                    pontuacao += 2;
                if (ObterCaminhoHierarquia(transformacao).IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0)
                    pontuacao += 4;
                if (pontuacao > melhorPontuacao)
                {
                    melhorPontuacao = pontuacao;
                    melhor = transformacao;
                }
            }

            return melhorPontuacao > 0 ? melhor : null;
        }

        /// <summary>
        /// Posiciona o painel de forma mais central à frente da câmera, mantendo distância confortável.
        /// </summary>
        private static void PosicionarPainel(Transform painel, Transform camera)
        {
            if (camera == null)
            {
                painel.position = new Vector3(0.05f, 1.35f, 1.35f);
                painel.rotation = Quaternion.Euler(0f, 180f, 0f);
                return;
            }

            painel.position = camera.position + camera.forward * 1.35f + camera.right * 0.05f - camera.up * 0.04f;
            var olharDirecao = (painel.position - camera.position).normalized;
            painel.rotation = Quaternion.LookRotation(olharDirecao, Vector3.up);
        }

        /// <summary>
        /// Cria um RectTransform filho com âncoras e offsets definidos.
        /// </summary>
        private static RectTransform CriarRetangulo(string nome, Transform pai, Vector2 ancoraMinimo, Vector2 ancoraMaximo, Vector2 deslocamentoMinimo, Vector2 deslocamentoMaximo)
        {
            var item = new GameObject(nome, typeof(RectTransform));
            item.layer = 5;
            item.transform.SetParent(pai, false);
            var retangulo = item.GetComponent<RectTransform>();
            retangulo.anchorMin = ancoraMinimo;
            retangulo.anchorMax = ancoraMaximo;
            retangulo.offsetMin = deslocamentoMinimo;
            retangulo.offsetMax = deslocamentoMaximo;
            return retangulo;
        }

        /// <summary>
        /// Cria uma imagem de UI para fundo, painel ou viewport.
        /// </summary>
        private static Image CriarImagem(string nome, Transform pai, Vector2 ancoraMinimo, Vector2 ancoraMaximo, Vector2 deslocamentoMinimo, Vector2 deslocamentoMaximo, Color cor)
        {
            var retangulo = CriarRetangulo(nome, pai, ancoraMinimo, ancoraMaximo, deslocamentoMinimo, deslocamentoMaximo);
            var imagem = retangulo.gameObject.AddComponent<Image>();
            imagem.color = cor;
            imagem.raycastTarget = true;
            return imagem;
        }

        /// <summary>
        /// Cria texto Unity UI já configurado para leitura em painel world-space.
        /// </summary>
        private static Text CriarTexto(string nome, Transform pai, Vector2 ancoraMinimo, Vector2 ancoraMaximo, Vector2 deslocamentoMinimo, Vector2 deslocamentoMaximo, int tamanhoFonte, TextAnchor ancora, string texto)
        {
            var retangulo = CriarRetangulo(nome, pai, ancoraMinimo, ancoraMaximo, deslocamentoMinimo, deslocamentoMaximo);
            var rotulo = retangulo.gameObject.AddComponent<Text>();
            rotulo.text = texto;
            rotulo.color = Color.white;
            rotulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rotulo.fontSize = tamanhoFonte;
            rotulo.alignment = ancora;
            rotulo.horizontalOverflow = HorizontalWrapMode.Wrap;
            rotulo.verticalOverflow = VerticalWrapMode.Overflow;
            return rotulo;
        }

        /// <summary>
        /// Cria um botão de UI com rótulo em português para o painel VR.
        /// </summary>
        private static Button CriarBotao(string nome, Transform pai, string rotulo)
        {
            var retangulo = CriarRetangulo(nome, pai, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var imagem = retangulo.gameObject.AddComponent<Image>();
            imagem.color = new Color(0.12f, 0.16f, 0.22f, 0.96f);
            var contorno = retangulo.gameObject.AddComponent<Outline>();
            contorno.effectColor = new Color(0f, 0.45f, 0.85f, 0.8f);
            contorno.effectDistance = new Vector2(1f, -1f);
            var botao = retangulo.gameObject.AddComponent<Button>();
            var texto = CriarTexto("Text", retangulo, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f), 14, TextAnchor.MiddleCenter, rotulo);
            texto.resizeTextForBestFit = true;
            texto.resizeTextMinSize = 9;
            texto.resizeTextMaxSize = 15;
            return botao;
        }

        /// <summary>
        /// Remove filhos de um painel durante o setup de editor para recriar a UI controlada pela ferramenta.
        /// </summary>
        private static void LimparFilhos(Transform pai)
        {
            var filhos = new List<GameObject>();
            foreach (Transform filho in pai)
            {
                filhos.Add(filho.gameObject);
            }

            foreach (var filho2 in filhos)
            {
                DestroyImmediate(filho2);
            }
        }

        /// <summary>
        /// Monta o caminho de hierarquia de um Transform para logs de diagnóstico.
        /// </summary>
        private static string ObterCaminhoHierarquia(Transform transformacao)
        {
            if (transformacao == null)
            {
                return string.Empty;
            }

            var pilha = new Stack<string>();
            var atual = transformacao;
            while (atual != null)
            {
                pilha.Push(atual.name);
                atual = atual.parent;
            }

            return string.Join("/", pilha);
        }
#endif
    }
}
