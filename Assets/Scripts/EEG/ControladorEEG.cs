using System;
using System.Threading.Tasks;
using UnityEngine;
using VRSNC.Interacao;

namespace VRSNC.EEG
{
    /// <summary>
    /// Centro do EEG no VR: carrega a gravação importada do Protobrain, guarda o tempo e o que o mapa
    /// mostra (banda, normalização, potência ou voltagem), filtra o traçado fora da thread principal e
    /// liga cada eletrodo ao giro equivalente do modelo anatômico. As vistas (escalpo, aba EEG, topoplot,
    /// traçados) só leem este estado e assinam os eventos; o tempo é a única fonte de verdade, então o
    /// cursor do traçado e a cor do escalpo nunca saem de sincronia.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControladorEEG : MonoBehaviour
    {
        public enum TipoMapa
        {
            Potencia,
            Voltagem
        }

        /// <summary>Translúcido mostra o cérebro por dentro; opaco deixa o mapa mais fácil de ler.</summary>
        public enum EstadoEscalpo
        {
            Translucido,
            Opaco,
            Desligado
        }

        [Serializable]
        public sealed class LigacaoGiro
        {
            public NoInteracaoAnatomica no;
            // Ponto do giro mais próximo do eletrodo, no espaço local do nó (acompanha a peça movida ou explodida).
            public Vector3 pontoLocal;
            public bool aproximada;
        }

        public static readonly float[] FaixasVoltagem = { 10f, 20f, 50f, 100f };

        [SerializeField]
        private DadosEEG dados;
        [SerializeField]
        private GerenciadorSelecaoHierarquica gerenciadorSelecao;
        [SerializeField]
        private Transform raizAnatomia;
        [Header("Alinhamento do fsaverage ao modelo (calculado pelo instalador)")]
        [SerializeField]
        private Vector3 posicaoLocalCabeca;
        [SerializeField]
        private Quaternion rotacaoLocalCabeca = Quaternion.identity;
        [SerializeField]
        private Vector3 escalaLocalCabeca = Vector3.one;
        [SerializeField]
        private LigacaoGiro[] ligacoes = Array.Empty<LigacaoGiro>();
        [Header("Materiais")]
        [SerializeField]
        private Material materialEscalpo;
        [SerializeField]
        private Material materialEletrodo;
        [SerializeField]
        private Material materialLigacao;

        private float tempo;
        private bool tocando;
        private int banda;
        private int modo;
        private TipoMapa tipoMapa = TipoMapa.Potencia;
        private float faixaVoltagem = 20f;
        private int canalSelecionado = -1;
        private EstadoEscalpo estadoEscalpo = EstadoEscalpo.Translucido;
        private bool modoAtivo;
        private int ultimoTique = -1;
        private FiltrosEEG.Configuracao filtros = FiltrosEEG.Padrao;
        private bool duplaBanana;
        private float sensibilidade = 7f;
        private Task<float[]> tarefaFiltros;
        private FiltrosEEG.Configuracao filtrosEmAndamento;
        private bool filtrosPendentes;

        public DadosEEG Dados => dados;
        public ConjuntoEEG Conjunto { get; private set; }
        public string ErroCarga { get; private set; }
        public bool Disponivel => Conjunto != null;
        public Transform RaizAnatomia => raizAnatomia;
        public GerenciadorSelecaoHierarquica GerenciadorSelecao => gerenciadorSelecao;
        public Matrix4x4 MatrizCabecaLocal => Matrix4x4.TRS(posicaoLocalCabeca, rotacaoLocalCabeca, escalaLocalCabeca);
        public Material MaterialEscalpo => materialEscalpo;
        public Material MaterialEletrodo => materialEletrodo;
        public Material MaterialLigacao => materialLigacao;
        public float Tempo => tempo;
        public float Duracao => Conjunto != null ? Conjunto.Duracao : 0f;
        public bool Tocando => tocando;
        public int Banda => banda;
        public int Modo => modo;
        public TipoMapa Mapa => tipoMapa;
        public float FaixaVoltagem => faixaVoltagem;
        public int CanalSelecionado => canalSelecionado;
        public EstadoEscalpo Escalpo => estadoEscalpo;
        public float OpacidadeEscalpo => estadoEscalpo == EstadoEscalpo.Opaco ? 0.92f : 0.65f;
        public bool ModoAtivo => modoAtivo;
        public bool CabecaVisivel => Disponivel && modoAtivo && estadoEscalpo != EstadoEscalpo.Desligado;
        public FiltrosEEG.Configuracao Filtros => filtros;
        public bool DuplaBanana => duplaBanana;
        public float Sensibilidade => sensibilidade;
        public bool Filtrando => tarefaFiltros != null || filtrosPendentes;
        // Sinal de leitura (referência média, filtros do traçado) e as faixas da montagem escolhida.
        public float[] SinaisFiltrados { get; private set; }
        public MontagensEEG.Resultado Faixas { get; private set; }

        /// <summary>O tempo mudou (a cada quadro de vídeo enquanto toca).</summary>
        public event Action TempoAlterado;
        /// <summary>O que o mapa pinta mudou: novo quadro ou amostra, banda, normalização, escala ou sinal filtrado.</summary>
        public event Action MapaAlterado;
        /// <summary>Banda, normalização, tipo de mapa, eletrodo, visibilidade ou ajustes do traçado mudaram.</summary>
        public event Action EstadoAlterado;
        /// <summary>Filtros ou montagem aplicados: o traçado deve ser refeito.</summary>
        public event Action SinaisAlterados;

        private void Awake()
        {
            if (gerenciadorSelecao == null)
                gerenciadorSelecao = FindFirstObjectByType<GerenciadorSelecaoHierarquica>();
            if (raizAnatomia == null && gerenciadorSelecao != null)
                raizAnatomia = gerenciadorSelecao.transform;
            Conjunto = ConjuntoEEG.Carregar(dados, out var erro);
            ErroCarga = erro;
            if (Conjunto == null)
            {
                Debug.LogWarning("[EEG] Gravação indisponível: " + erro);
                return;
            }

            // Padrões do Protobrain: alfa (a banda de interesse no repouso) em potência absoluta, que compara regiões.
            banda = dados.IndiceBanda("alpha");
            modo = dados.IndiceModo("absolute");
            SinaisFiltrados = Conjunto.Sinais;
            Faixas = MontagensEEG.Derivar(false, SinaisFiltrados, dados, Conjunto.Amostras);
            IniciarFiltragem();
        }

        private void Update()
        {
            if (Conjunto == null)
                return;
            ConcluirFiltragem();
            if (tocando)
            {
                var proximo = tempo + Time.deltaTime;
                if (proximo >= Duracao)
                    proximo -= Duracao;
                DefinirTempoInterno(proximo);
            }
        }

        private void OnDisable()
        {
            tocando = false;
        }

        public void AlternarReproducao() => DefinirTocando(!tocando);

        public void DefinirTocando(bool valor)
        {
            if (Conjunto == null || tocando == valor)
                return;
            tocando = valor;
            EstadoAlterado?.Invoke();
        }

        public void DefinirTempo(float valor)
        {
            if (Conjunto == null)
                return;
            DefinirTempoInterno(Mathf.Clamp(valor, 0f, Duracao));
        }

        private void DefinirTempoInterno(float valor)
        {
            if (Mathf.Approximately(valor, tempo))
                return;
            tempo = valor;
            TempoAlterado?.Invoke();
            var tique = TiqueAtual();
            if (tique != ultimoTique)
            {
                ultimoTique = tique;
                MapaAlterado?.Invoke();
            }
        }

        /// <summary>O "relógio" do mapa: quadro do cubo na potência, amostra do sinal na voltagem.</summary>
        public int TiqueAtual()
        {
            if (Conjunto == null)
                return 0;
            return tipoMapa == TipoMapa.Voltagem ? Conjunto.AmostraEm(tempo) : Conjunto.QuadroEm(tempo);
        }

        public void DefinirBanda(int indice)
        {
            if (Conjunto == null || indice < 0 || indice >= dados.bandas.Length)
                return;
            banda = indice;
            tipoMapa = TipoMapa.Potencia;
            AvisarMudancaDeMapa();
        }

        public void DefinirModo(int indice)
        {
            if (Conjunto == null || indice < 0 || indice >= dados.escalas.Length)
                return;
            modo = indice;
            tipoMapa = TipoMapa.Potencia;
            AvisarMudancaDeMapa();
        }

        public void DefinirTipoMapa(TipoMapa tipo)
        {
            if (Conjunto == null || tipoMapa == tipo)
                return;
            tipoMapa = tipo;
            AvisarMudancaDeMapa();
        }

        public void DefinirFaixaVoltagem(float faixaUv)
        {
            faixaVoltagem = Mathf.Max(1f, faixaUv);
            AvisarMudancaDeMapa();
        }

        private void AvisarMudancaDeMapa()
        {
            ultimoTique = TiqueAtual();
            EstadoAlterado?.Invoke();
            MapaAlterado?.Invoke();
        }

        /// <summary>
        /// Os valores dos eletrodos que o mapa pinta agora e a escala deles. Na potência, o quadro do cubo
        /// da normalização escolhida; na voltagem, a amostra do sinal de leitura no cursor (referência média,
        /// com os filtros do traçado, como o glossário da IFCN recomenda para mapas de voltagem).
        /// </summary>
        public EscalaMapa ValoresMapa(float[] destino)
        {
            if (tipoMapa == TipoMapa.Voltagem)
            {
                var amostra = Conjunto.AmostraEm(tempo);
                var sinais = SinaisFiltrados ?? Conjunto.Sinais;
                for (var canal = 0; canal < Conjunto.Canais; canal++)
                    destino[canal] = sinais[canal * Conjunto.Amostras + amostra];
                return EscalaMapa.Voltagem(faixaVoltagem);
            }

            Conjunto.CopiarQuadro(modo, Conjunto.QuadroEm(tempo), banda, destino);
            return dados.EscalaPara(modo, banda);
        }

        // ---------------------------------------------------------------- eletrodo selecionado

        public void SelecionarCanal(int canal, bool mostrarGiro)
        {
            if (Conjunto == null)
                return;
            canal = canal < 0 ? -1 : Mathf.Clamp(canal, 0, Conjunto.Canais - 1);
            canalSelecionado = canal;
            EstadoAlterado?.Invoke();
            if (mostrarGiro && canal >= 0)
                MostrarGiroDoCanal(canal);
        }

        public void SelecionarCanalVizinho(int passo)
        {
            if (Conjunto == null)
                return;
            var atual = canalSelecionado < 0 ? (passo > 0 ? -1 : 0) : canalSelecionado;
            SelecionarCanal((atual + passo + Conjunto.Canais) % Conjunto.Canais, true);
        }

        public LigacaoGiro LigacaoDe(int canal) => canal >= 0 && canal < ligacoes.Length ? ligacoes[canal] : null;

        /// <summary>Leva a navegação anatômica ao giro sob o eletrodo: entra no lobo e seleciona o giro.</summary>
        public bool MostrarGiroDoCanal(int canal)
        {
            var ligacao = LigacaoDe(canal);
            if (ligacao == null || ligacao.no == null || gerenciadorSelecao == null)
                return false;
            var pai = ligacao.no.NoPai;
            if (pai == null)
                return false;
            gerenciadorSelecao.LimparSelecao();
            gerenciadorSelecao.EntrarNo(pai);
            gerenciadorSelecao.SelecionarNo(ligacao.no);
            return gerenciadorSelecao.NoSelecionado == ligacao.no;
        }

        // ---------------------------------------------------------------- modo e visibilidade

        /// <summary>A aba EEG ou a janela de traçados estão abertas: o escalpo e os eletrodos aparecem.</summary>
        public void DefinirModoAtivo(bool ativo)
        {
            if (modoAtivo == ativo)
                return;
            modoAtivo = ativo;
            EstadoAlterado?.Invoke();
        }

        /// <summary>Translúcido → opaco → desligado → translúcido.</summary>
        public void AlternarEscalpo()
        {
            estadoEscalpo = (EstadoEscalpo)(((int)estadoEscalpo + 1) % 3);
            EstadoAlterado?.Invoke();
        }

        // ---------------------------------------------------------------- traçado

        public void DefinirFiltros(FiltrosEEG.Configuracao configuracao)
        {
            if (Conjunto == null || configuracao.Equals(filtros))
                return;
            filtros = configuracao;
            EstadoAlterado?.Invoke();
            IniciarFiltragem();
        }

        public void DefinirDuplaBanana(bool valor)
        {
            if (Conjunto == null || duplaBanana == valor)
                return;
            duplaBanana = valor;
            Faixas = MontagensEEG.Derivar(duplaBanana, SinaisFiltrados, dados, Conjunto.Amostras);
            EstadoAlterado?.Invoke();
            SinaisAlterados?.Invoke();
        }

        public void DefinirSensibilidade(float microvoltsPorMm)
        {
            sensibilidade = Mathf.Clamp(microvoltsPorMm, 1f, 100f);
            EstadoAlterado?.Invoke();
            SinaisAlterados?.Invoke();
        }

        /// <summary>Os filtros correm numa Task; se outra troca chegar no meio, ela entra quando a atual terminar.</summary>
        private void IniciarFiltragem()
        {
            if (tarefaFiltros != null)
            {
                filtrosPendentes = true;
                return;
            }

            filtrosPendentes = false;
            filtrosEmAndamento = filtros;
            var origem = Conjunto.Sinais;
            var canais = Conjunto.Canais;
            var amostras = Conjunto.Amostras;
            var fs = (double)dados.frequenciaAmostragem;
            var rede = (double)dados.frequenciaRede;
            var configuracao = filtros;
            tarefaFiltros = Task.Run(() => FiltrosEEG.Aplicar(origem, canais, amostras, fs, configuracao, rede));
        }

        private void ConcluirFiltragem()
        {
            if (tarefaFiltros == null || !tarefaFiltros.IsCompleted)
                return;
            var tarefa = tarefaFiltros;
            tarefaFiltros = null;
            if (tarefa.IsFaulted)
            {
                Debug.LogError("[EEG] Falha ao filtrar o traçado: " + tarefa.Exception?.GetBaseException().Message);
            }
            else if (filtrosEmAndamento.Equals(filtros))
            {
                SinaisFiltrados = tarefa.Result;
                Faixas = MontagensEEG.Derivar(duplaBanana, SinaisFiltrados, dados, Conjunto.Amostras);
                SinaisAlterados?.Invoke();
                if (tipoMapa == TipoMapa.Voltagem)
                    MapaAlterado?.Invoke();
            }

            if (filtrosPendentes || !filtrosEmAndamento.Equals(filtros))
                IniciarFiltragem();
            EstadoAlterado?.Invoke();
        }

        /// <summary>Espera a filtragem em curso (usado por testes no Editor).</summary>
        public bool AguardarFiltragem(int milissegundos)
        {
            if (tarefaFiltros != null && !tarefaFiltros.Wait(milissegundos))
                return false;
            ConcluirFiltragem();
            return tarefaFiltros == null || AguardarFiltragem(milissegundos);
        }

#if UNITY_EDITOR
        /// <summary>Chamado pelo instalador do Editor, que calcula o alinhamento e as ligações.</summary>
        public void Configurar(DadosEEG novosDados, GerenciadorSelecaoHierarquica gerenciador, Transform raiz, Vector3 posicao, Quaternion rotacao, Vector3 escala, LigacaoGiro[] novasLigacoes, Material escalpo, Material eletrodo, Material ligacao)
        {
            dados = novosDados;
            gerenciadorSelecao = gerenciador;
            raizAnatomia = raiz;
            posicaoLocalCabeca = posicao;
            rotacaoLocalCabeca = rotacao;
            escalaLocalCabeca = escala;
            ligacoes = novasLigacoes;
            materialEscalpo = escalpo;
            materialEletrodo = eletrodo;
            materialLigacao = ligacao;
        }
#endif
    }
}
