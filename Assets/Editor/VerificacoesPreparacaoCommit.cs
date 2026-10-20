using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using VRSNC.Anatomia;
using VRSNC.Interacao;

namespace VRSNC.FerramentasEditor
{
    /// <summary>Entrada reproduzível para validar lógica e migração antes do commit. Não salva a cena.</summary>
    public static class VerificacoesPreparacaoCommit
    {
        [MenuItem("Ferramentas/VR SNC/Executar verificações de preparação do commit")]
        public static void Executar()
        {
            var falhas = new List<string>();
            var verificacoes = new List<string>();
            Action<string, Action> executar = (nome, teste) =>
            {
                var errosConsole = new List<string>();
                Application.LogCallback registrarErro = (mensagem, pilha, tipo) =>
                {
                    if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert)
                        errosConsole.Add(mensagem);
                };
                Application.logMessageReceived += registrarErro;
                try
                {
                    teste();
                    if (errosConsole.Count > 0) throw new InvalidOperationException(string.Join(" | ", errosConsole));
                    verificacoes.Add(nome);
                }
                catch (Exception erro) { falhas.Add(nome + ": " + (erro.InnerException ?? erro).Message); }
                finally { Application.logMessageReceived -= registrarErro; }
            };
            executar("atalhos exclusivos e alternativa sem mapeador", VerificarPrioridadeAtalhos);
            executar("referência de câmera configurada na âncora de pulso", VerificarReferenciaCameraPulso);
            executar("migração JSON e limites científicos das 272 fichas", VerificarOraculo);
            executar("reset global ativo, concluído e retomada sem salto", () => InvocarTeste(typeof(MenuRegressaoGestoSeparacaoAnatomica), "Executar"));
            executar("histerese da seleção por raio", VerificacoesHistereseSelecaoRaio.Validar);
            executar("entrada com mão aberta", VerificacoesRegressaoEntradaMaoAberta.Validar);
            executar("contexto de raio próximo e distante", VerificacoesRegressaoContextoRaioMao.Validar);
            executar("agarre com controlador", () => InvocarTeste(typeof(MenuRegressaoAgarreControlador), "Executar"));
            executar("referências e valores migrados da cena", VerificarCena);
            executar("configuração serializada da interface espacial", () => InvocarTeste(typeof(VerificacoesRegressaoInterfaceEspacial), "ValidarConfiguracao"));
            executar("EEG do Protobrain: dados, filtros, cores, montagens e ligação aos giros", VerificacoesEEG.Validar);
            executar("Voltar deixa a concha selecionada", VerificacoesInterfaceNova.ValidarVoltarConcha);
            executar("gestos das mãos no ar: mover, girar e escalar com as duas mãos", VerificacoesInterfaceNova.ValidarGestosDuasMaos);
            executar("tema Grafite, Voltar no cabeçalho, legendas dos controles e gestos instalados na cena", VerificacoesInterfaceNova.ValidarTemaNaCena);
            var relatorio = new RelatorioPreparacao
            {
                aprovado = falhas.Count == 0,
                unity = Application.unityVersion,
                geradoEmUtc = DateTime.UtcNow.ToString("O"),
                verificacoesAprovadas = verificacoes.ToArray(),
                falhas = falhas.ToArray(),
                quest = "PENDENTE: requer dispositivo físico carregado e conectado; esta suíte não mede desempenho XR."
            };
            Directory.CreateDirectory("outputs/validacao-commit");
            File.WriteAllText("outputs/validacao-commit/editor.json", JsonUtility.ToJson(relatorio, true));
            foreach (var falha in falhas) Debug.LogError("[VR SNC] " + falha);
            if (falhas.Count > 0) throw new InvalidOperationException("Preparação do commit: " + falhas.Count + " verificações falharam.");
            Debug.Log("[VR SNC] APROVADO: " + verificacoes.Count + " verificações de preparação do commit.");
        }

        private static void VerificarPrioridadeAtalhos()
        {
            Exigir(!RoteadorEntradaXR.DeveProcessarAtalhos(true, true), "Roteador não cedeu ao mapeador.");
            Exigir(RoteadorEntradaXR.DeveProcessarAtalhos(true, false), "Alternativa sem mapeador foi perdida.");
            Exigir(!RoteadorEntradaXR.DeveProcessarAtalhos(false, true) && !RoteadorEntradaXR.DeveProcessarAtalhos(false, false), "Roteador desabilitado processaria teclado.");
            var modoAnterior = InputSystem.settings.updateMode;
            var tecladoAnterior = Keyboard.current;
            // Mesmo recurso interno usado pelo pacote para permitir testes sem Play Mode.
            // A opção e as configurações são restauradas mesmo se uma asserção falhar.
            var gerenciadorEntrada = typeof(InputSystem).GetField("s_Manager", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            Exigir(gerenciadorEntrada != null, "Gerenciador do Input System não encontrado para o teste.");
            var permitirTeste = gerenciadorEntrada.GetType().GetProperty("runPlayerUpdatesInEditMode");
            Exigir(permitirTeste != null, "A versão do Input System não oferece atualização de teste fora de Play Mode.");
            var atualizacaoAnterior = (bool)permitirTeste.GetValue(gerenciadorEntrada);
            Keyboard tecladoTeste = null;
            GameObject objetoTeste = null;
            try
            {
                InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                permitirTeste.SetValue(gerenciadorEntrada, true);
                tecladoTeste = InputSystem.AddDevice<Keyboard>();
                tecladoTeste.MakeCurrent();
                objetoTeste = new GameObject("Verificacao_Atalhos_Exclusivos") { hideFlags = HideFlags.HideAndDontSave };
                var selecao = objetoTeste.AddComponent<GerenciadorSelecaoHierarquica>();
                var mapeador = objetoTeste.AddComponent<MapeadorEntradaMetaQuest2>();
                var roteador = objetoTeste.AddComponent<RoteadorEntradaXR>();
                selecao.Configurar(null, null, null, null);
                mapeador.Configurar(roteador, null, selecao, null, null, null, null, null, null, null, null, null, null, null);
                roteador.Configurar(null, selecao, null, null, null, null, null);
                var quantidadeAcoes = 0;
                selecao.emSelecaoLimpo.AddListener(() => quantidadeAcoes++);
                InputSystem.QueueStateEvent(tecladoTeste, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(tecladoTeste, new KeyboardState(Key.C));
                InputSystem.Update();
                Exigir(tecladoTeste.cKey.wasPressedThisFrame, "A tecla de teste não foi processada pelo Input System.");
                InvocarComponente(mapeador, "ManipularEditorAlternativas");
                InvocarComponente(roteador, "ManipularEditorAlternativas");
                Exigir(quantidadeAcoes == 1, "Uma tecla executou " + quantidadeAcoes + " ações com os dois componentes ativos.");
                mapeador.enabled = false;
                InputSystem.QueueStateEvent(tecladoTeste, new KeyboardState());
                InputSystem.Update();
                InputSystem.QueueStateEvent(tecladoTeste, new KeyboardState(Key.C));
                InputSystem.Update();
                InvocarComponente(roteador, "ManipularEditorAlternativas");
                Exigir(quantidadeAcoes == 2, "O roteador alternativo não executou exatamente uma ação com o mapeador desativado.");
            }
            finally
            {
                if (objetoTeste != null) UnityEngine.Object.DestroyImmediate(objetoTeste);
                if (tecladoTeste != null) InputSystem.RemoveDevice(tecladoTeste);
                permitirTeste.SetValue(gerenciadorEntrada, atualizacaoAnterior);
                InputSystem.settings.updateMode = modoAnterior;
                if (tecladoAnterior != null && tecladoAnterior.added) tecladoAnterior.MakeCurrent();
            }
        }

        private static void VerificarReferenciaCameraPulso()
        {
            var objetoAncora = new GameObject("Verificacao_Ancora_Pulso") { hideFlags = HideFlags.HideAndDontSave };
            var objetoCamera = new GameObject("Verificacao_Camera_Pulso") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var ancora = objetoAncora.AddComponent<VRSNC.Interface.AncoraEspacialPulso>();
                ancora.Configurar(null, null, objetoCamera.transform, null, null);
                var configuracao = new SerializedObject(ancora);
                Exigir(configuracao.FindProperty("dispositivoVR").objectReferenceValue == objetoCamera.transform,
                    "A âncora não preservou a referência de câmera fornecida na configuração.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(objetoAncora);
                UnityEngine.Object.DestroyImmediate(objetoCamera);
            }
        }

        private static void VerificarOraculo()
        {
            var recurso = Resources.Load<TextAsset>("AnatomicalOracle");
            Exigir(recurso != null, "Recurso científico ausente.");
            var documento = LeitorJsonCompativel.Ler<DocumentoOraculoAnatomico>(recurso.text);
            Exigir(documento.registros != null && documento.registros.Length == 272, "Migração do JSON perdeu os 272 registros.");
            Exigir(documento.fontes != null && documento.fontes.Length == 52, "Migração do JSON perdeu as 52 fontes.");
            var textoComAspas = "records e sources são texto, não chaves; \"namePt\": preserve a descrição";
            var textoSerializado = JsonUtility.ToJson(new RegistroOraculoAnatomico { nomePortugues = textoComAspas });
            var exemplo = LeitorJsonCompativel.Ler<RegistroOraculoAnatomico>(textoSerializado.Replace("\"nomePortugues\":", "\"namePt\":"));
            Exigir(exemplo.nomePortugues == textoComAspas, "A migração alterou conteúdo textual com aspas escapadas.");
            Exigir(documento.registros.Select(r => r.idCatalogo).Distinct().Count() == 272, "Identificadores científicos duplicados.");
            Exigir(documento.registros.Count(r => r.estadoVerificacao == "source_checked") == 148, "Estado de verificação conferida divergente.");
            Exigir(documento.registros.Count(r => r.estadoVerificacao == "limited_specificity") == 124, "Estado de especificidade limitada divergente.");
            foreach (var registro in documento.registros)
            {
                Exigir(!string.IsNullOrWhiteSpace(registro.nomePortugues) && !string.IsNullOrWhiteSpace(registro.descricaoCientifica) && !string.IsNullOrWhiteSpace(registro.resumoDidatico), "Migração perdeu conteúdo de " + registro.idCatalogo);
                var estado = OraculoAnatomico.ObterEstadoVerificacao(registro);
                Exigir(estado.Contains("Não revisado por docente"), "Revisão docente não foi exibida: " + registro.idCatalogo);
                if (registro.estadoVerificacao == "limited_specificity") Exigir(estado.Contains("especificidade limitada"), "Limitação não foi exibida.");
                if (!string.IsNullOrWhiteSpace(registro.notasRevisao)) Exigir(estado.Contains(registro.notasRevisao), "Nota de revisão oculta.");
                Exigir(!string.IsNullOrWhiteSpace(OraculoAnatomico.ObterBibliografia(registro)), "Bibliografia perdida: " + registro.idCatalogo);
            }
        }

        private static void VerificarCena()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("A cena atual contém alterações não salvas; validação da cena cancelada para preservar o trabalho.");
            var cena = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var objetos = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
            Exigir(objetos.All(o => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(o) == 0), "A cena contém script ausente.");
            var raiz = objetos.Single(o => o.name == "Sistema_Nervoso_Central");
            Exigir(raiz.GetComponentsInChildren<VinculoAnatomico>(true).Length == 272, "Vínculos anatômicos perdidos.");
            foreach (var vinculo in raiz.GetComponentsInChildren<VinculoAnatomico>(true))
                Exigir(OraculoAnatomico.TentarObter(vinculo, out _), "Referência científica não resolve: " + vinculo.CatalogoId);
            var mapeador = raiz.GetComponent<MapeadorEntradaMetaQuest2>();
            Exigir(mapeador != null && mapeador.ProcessaAtalhosDoEditor, "A migração perdeu a configuração de atalhos do mapeador.");
            Exigir(raiz.GetComponent<GerenciadorRestauracao>() != null, "Gerenciador de restauração ausente.");
            foreach (var objeto in objetos)
            {
                foreach (var botao in objeto.GetComponents<UnityEngine.UI.Button>())
                {
                    for (var indice = 0; indice < botao.onClick.GetPersistentEventCount(); indice++)
                    {
                        var alvo = botao.onClick.GetPersistentTarget(indice);
                        var metodo = botao.onClick.GetPersistentMethodName(indice);
                        if (alvo == null || alvo.GetType().Assembly != typeof(GerenciadorRestauracao).Assembly) continue;
                        Exigir(alvo.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(m => m.Name == metodo),
                            "A renomeação perdeu o método persistente do botão " + objeto.name + ": " + metodo);
                    }
                }
            }
        }

        private static void InvocarTeste(Type tipo, string metodo)
            => tipo.GetMethod(metodo, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);

        private static void InvocarComponente(object componente, string metodo)
            => componente.GetType().GetMethod(metodo, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(componente, null);


        private static void Exigir(bool condicao, string mensagem)
        {
            if (!condicao) throw new InvalidOperationException(mensagem);
        }

        [Serializable]
        private sealed class RelatorioPreparacao
        {
            public bool aprovado;
            public string unity;
            public string geradoEmUtc;
            public string[] verificacoesAprovadas;
            public string[] falhas;
            public string quest;
        }
    }
}
