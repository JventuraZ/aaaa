using System;
using UnityEngine;

namespace VRSNC.Interacao
{
    /// <summary>
    /// Coordena uma separação anatômica reversível com duas mãos. Um grupo selecionado de forma deliberada
    /// tem precedência; caso contrário, todo o catálogo do SNC é separado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ControladorExpansaoGlobal))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "VRSNC.Interaction", "Assembly-CSharp", "BrainAnatomyGestureCoordinator")]
    public sealed class CoordenadorGestoAnatomico : MonoBehaviour
    {
        private enum ModoSeparacao
        {
            Nenhum,
            Global,
            Limitado
        }

        [UnityEngine.Serialization.FormerlySerializedAs("globalexpandir")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("globalExplode")]
        private ControladorExpansaoGlobal controladorExpansaoGlobal;
        [UnityEngine.Serialization.FormerlySerializedAs("localexpandir")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("localExplode")]
        private ControladorVisaoExplodida controladorExpansaoLocal;
        [UnityEngine.Serialization.FormerlySerializedAs("selecaogerenciador")]
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("selectionManager")]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        private const float DiametroMarcadorMetros = 0.012f;
        [SerializeField, Min(0f)]
        [UnityEngine.Serialization.FormerlySerializedAs("separationDeadzone")]
        private float separacaoZonaMorta = 0.004f;
        [UnityEngine.Serialization.FormerlySerializedAs("completoSeparacaoVariacao")]
        [SerializeField, Min(0.001f)]
        [UnityEngine.Serialization.FormerlySerializedAs("fullSeparationDelta")]
        private float variacaoSeparacaoCompleta = 0.18f;
        private NoInteracaoAnatomica primeiraFolha;
        private NoInteracaoAnatomica segundaFolha;
        private NoInteracaoAnatomica noEscopo;
        private Vector3 posicaoPrimeiraPinca;
        private Vector3 posicaoSegundaPinca;
        private float distanciaInicial;
        private float fatorInicial;
        private uint versaoAtiva;
        private uint versaoConcluida;
        private ModoSeparacao modoAtivo;
        private ModoSeparacao modoConcluido;
        private GameObject primeiroMarcador;
        private GameObject segundoMarcador;
        private Material materialMarcador;
        public bool EstaAtivo => primeiraFolha != null;
        public bool EstaSeparando => EstaAtivo;
        public bool PossuiEstadoGlobalRetido => modoConcluido == ModoSeparacao.Global && versaoConcluida != 0 && controladorExpansaoGlobal != null && controladorExpansaoGlobal.Fator > 0f;
        public bool PossuiEstadoSeparacaoRetido => modoConcluido != ModoSeparacao.Nenhum && versaoConcluida != 0 && Fator > 0f;
        public float Fator => modoAtivo == ModoSeparacao.Limitado || modoConcluido == ModoSeparacao.Limitado ? controladorExpansaoLocal != null ? controladorExpansaoLocal.ProgressoAtualSeparacao : 0f : controladorExpansaoGlobal != null ? controladorExpansaoGlobal.Fator : 0f;
        public event Action<bool, float> GestoEstadoAlterado;
        private void Awake()
        {
            controladorExpansaoGlobal ??= GetComponent<ControladorExpansaoGlobal>();
            controladorExpansaoLocal ??= GetComponent<ControladorVisaoExplodida>();
            gerenciadorSelecao ??= GetComponent<GerenciadorSelecaoHierarquica>();
            CriarMarcadores();
        }

        /// <summary>Permite que testes determinísticos do Editor e a configuração da cena forneçam os controladores existentes.</summary>
        public void Configurar(ControladorExpansaoGlobal global, ControladorVisaoExplodida local, GerenciadorSelecaoHierarquica selecao)
        {
            controladorExpansaoGlobal = global != null ? global : GetComponent<ControladorExpansaoGlobal>();
            controladorExpansaoLocal = local != null ? local : GetComponent<ControladorVisaoExplodida>();
            gerenciadorSelecao = selecao != null ? selecao : GetComponent<GerenciadorSelecaoHierarquica>();
        }

        /// <summary>
        /// Duas folhas físicas iniciam um gesto contínuo. Um grupo selecionado separa
        /// apenas suas folhas físicas descendentes; uma folha selecionada deliberadamente não faz nada.
        /// Sem seleção de subestrutura, as 240 folhas do catálogo são separadas.
        /// </summary>
        public bool TentarIniciar(NoInteracaoAnatomica primeiro, NoInteracaoAnatomica segundo, Vector3 primeiroPinca, Vector3 segundoPinca)
        {
            if (EstaAtivo || controladorExpansaoGlobal == null || primeiro == null || segundo == null || !controladorExpansaoGlobal.ContemFolha(primeiro) || !controladorExpansaoGlobal.ContemFolha(segundo))
                return false;
            var escopoSelecionado = ResolverEscopoSelecionado();
            if (escopoSelecionado == null && gerenciadorSelecao != null && gerenciadorSelecao.NoSelecionado != null)
            {
                // Uma folha física pode ser selecionada pela ponte da mão. Ela não tem escopo
                // descendente relevante; não expanda acidentalmente todo o SNC.
                return false;
            }

            if (escopoSelecionado != null)
            {
                if (controladorExpansaoLocal == null || !controladorExpansaoLocal.IniciarSeparacaoPorGesto(escopoSelecionado))
                    return false;
                modoAtivo = ModoSeparacao.Limitado;
                noEscopo = escopoSelecionado;
                fatorInicial = controladorExpansaoLocal.ProgressoInicialGesto;
                versaoAtiva = controladorExpansaoLocal.SeparacaoEstadoVersao;
            }
            else
            {
                if (controladorExpansaoLocal != null && controladorExpansaoLocal.EstaExpandido)
                    controladorExpansaoLocal.ReagruparNivelAtual();
                if (!controladorExpansaoGlobal.IniciarGesto())
                    return false;
                modoAtivo = ModoSeparacao.Global;
                fatorInicial = controladorExpansaoGlobal.Fator;
                controladorExpansaoGlobal.DefinirFator(fatorInicial);
                versaoAtiva = controladorExpansaoGlobal.VersaoEstado;
            }

            primeiraFolha = primeiro;
            segundaFolha = segundo;
            posicaoPrimeiraPinca = primeiroPinca;
            posicaoSegundaPinca = segundoPinca;
            distanciaInicial = Vector3.Distance(primeiroPinca, segundoPinca);
            versaoConcluida = 0;
            modoConcluido = ModoSeparacao.Nenhum;
            DefinirMarcadores(true);
            GestoEstadoAlterado?.Invoke(true, Fator);
            return true;
        }

        public bool TentarAtualizar(Vector3 primeiroPinca, Vector3 segundoPinca)
        {
            if (!EstaAtivo)
                return false;
            posicaoPrimeiraPinca = primeiroPinca;
            posicaoSegundaPinca = segundoPinca;
            var variacao = Vector3.Distance(primeiroPinca, segundoPinca) - distanciaInicial;
            var variacaoEfetiva = Mathf.Abs(variacao) <= separacaoZonaMorta ? 0f : Mathf.Sign(variacao) * (Mathf.Abs(variacao) - separacaoZonaMorta);
            var fatorSolicitado = fatorInicial + variacaoEfetiva / variacaoSeparacaoCompleta;
            var atualizado = modoAtivo switch
            {
                ModoSeparacao.Global => controladorExpansaoGlobal != null && versaoAtiva == controladorExpansaoGlobal.VersaoEstado && controladorExpansaoGlobal.DefinirFator(fatorSolicitado),
                ModoSeparacao.Limitado => controladorExpansaoLocal != null && noEscopo != null && versaoAtiva == controladorExpansaoLocal.SeparacaoEstadoVersao && controladorExpansaoLocal.DefinirSeparacaoPorGesto(noEscopo, fatorSolicitado),
                _ => false,
            };
            if (!atualizado)
                return false;
            AtualizarMarcadores();
            GestoEstadoAlterado?.Invoke(true, Fator);
            return true;
        }

        public bool Concluir()
        {
            var concluido = modoAtivo switch
            {
                ModoSeparacao.Global => controladorExpansaoGlobal != null && versaoAtiva == controladorExpansaoGlobal.VersaoEstado && controladorExpansaoGlobal.ConcluirGesto(),
                ModoSeparacao.Limitado => controladorExpansaoLocal != null && versaoAtiva == controladorExpansaoLocal.SeparacaoEstadoVersao && controladorExpansaoLocal.ConcluirSeparacaoPorGesto(),
                _ => false,
            };
            if (concluido)
            {
                modoConcluido = modoAtivo;
                versaoConcluida = modoConcluido == ModoSeparacao.Global ? controladorExpansaoGlobal.VersaoEstado : controladorExpansaoLocal.SeparacaoEstadoVersao;
            }

            LimparEstadoAtivo();
            GestoEstadoAlterado?.Invoke(false, Fator);
            return concluido;
        }

        public void CancelarERestaurar()
        {
            if (DeveRestaurarGlobal())
                controladorExpansaoGlobal.CancelarERestaurar();
            else if (DeveRestaurarLimitado())
                controladorExpansaoLocal.CancelarSeparacaoPorGesto();
            versaoConcluida = 0;
            modoConcluido = ModoSeparacao.Nenhum;
            LimparEstadoAtivo();
            GestoEstadoAlterado?.Invoke(false, Fator);
        }

        private void OnDisable() => CancelarERestaurar();
        private void OnDestroy()
        {
            if (primeiroMarcador != null)
                Destroy(primeiroMarcador);
            if (segundoMarcador != null)
                Destroy(segundoMarcador);
            if (materialMarcador != null)
                Destroy(materialMarcador);
        }

        private NoInteracaoAnatomica ResolverEscopoSelecionado()
        {
            var selecionado = gerenciadorSelecao != null ? gerenciadorSelecao.NoSelecionado : null;
            if (selecionado != null)
                return selecionado.NosFilhos.Count > 0 ? selecionado : null;
            var foco = gerenciadorSelecao != null ? gerenciadorSelecao.NoEmFoco : null;
            return foco != null && !foco.EhNoRaiz && foco.NosFilhos.Count > 0 ? foco : null;
        }

        private bool DeveRestaurarGlobal()
        {
            return controladorExpansaoGlobal != null && ((modoAtivo == ModoSeparacao.Global && EstaAtivo && versaoAtiva == controladorExpansaoGlobal.VersaoEstado) || (modoConcluido == ModoSeparacao.Global && versaoConcluida == controladorExpansaoGlobal.VersaoEstado));
        }

        private bool DeveRestaurarLimitado()
        {
            return controladorExpansaoLocal != null && ((modoAtivo == ModoSeparacao.Limitado && EstaAtivo && versaoAtiva == controladorExpansaoLocal.SeparacaoEstadoVersao) || (modoConcluido == ModoSeparacao.Limitado && versaoConcluida == controladorExpansaoLocal.SeparacaoEstadoVersao));
        }

        private void CriarMarcadores()
        {
            primeiroMarcador = CriarMarcador("GlobalExplodeMarker_Left");
            segundoMarcador = CriarMarcador("GlobalExplodeMarker_Right");
        }

        private GameObject CriarMarcador(string nomeMarcador)
        {
            materialMarcador ??= CriarMaterialMarcador();
            var marcador = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marcador.name = nomeMarcador;
            marcador.hideFlags = HideFlags.DontSave;
            // Fica na raiz da cena, fora da hierarquia anatômica: sob o SNC, o marcador entraria
            // nos limites renderizáveis da raiz e deslocaria o centro usado pela vista explodida.
            marcador.transform.localScale = Vector3.one * DiametroMarcadorMetros;
            var colisor = marcador.GetComponent<Collider>();
            if (colisor != null)
                Destroy(colisor);
            var renderizador = marcador.GetComponent<Renderer>();
            if (renderizador != null && materialMarcador != null)
                renderizador.sharedMaterial = materialMarcador;
            marcador.SetActive(false);
            return marcador;
        }

        private void AtualizarMarcadores()
        {
            if (primeiroMarcador != null)
                primeiroMarcador.transform.position = posicaoPrimeiraPinca;
            if (segundoMarcador != null)
                segundoMarcador.transform.position = posicaoSegundaPinca;
        }

        private static Material CriarMaterialMarcador()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("UI/Default");
            if (shader == null)
                return null;
            var material = new Material(shader)
            {
                color = new Color(0.20f, 0.78f, 1f, 0.95f)
            };
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        private void DefinirMarcadores(bool visivel)
        {
            AtualizarMarcadores();
            if (primeiroMarcador != null)
                primeiroMarcador.SetActive(visivel);
            if (segundoMarcador != null)
                segundoMarcador.SetActive(visivel);
        }

        private void LimparEstadoAtivo()
        {
            DefinirMarcadores(false);
            primeiraFolha = null;
            segundaFolha = null;
            noEscopo = null;
            posicaoPrimeiraPinca = Vector3.zero;
            posicaoSegundaPinca = Vector3.zero;
            distanciaInicial = 0f;
            fatorInicial = 0f;
            versaoAtiva = 0;
            modoAtivo = ModoSeparacao.Nenhum;
        }
    }
}