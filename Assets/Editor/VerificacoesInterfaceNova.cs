using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRSNC.Interacao;
using VRSNC.Interface;

namespace VRSNC.FerramentasEditor
{
    /// <summary>
    /// Verificações do HUD de 19/10: o Voltar que deixa a "concha" selecionada (hierarquia temporária, sem tocar a cena),
    /// a matemática do gesto com as duas mãos no ar e o tema "Grafite suave" com as legendas dos controles na cena salva.
    /// </summary>
    public static class VerificacoesInterfaceNova
    {
        [MenuItem("Ferramentas/VR SNC/Interface/Validar HUD novo (Voltar, gestos, tema)", false, 260)]
        private static void ValidarPeloMenu()
        {
            ValidarVoltarConcha();
            ValidarGestosDuasMaos();
            ValidarTemaNaCena();
            Debug.Log("[VR SNC] APROVADO: Voltar com concha selecionada, gestos das mãos no ar e tema Grafite na cena.");
        }

        /// <summary>
        /// Raiz › Lobo › Giro: com o giro selecionado dentro do lobo, Voltar abre a raiz com o lobo selecionado; de novo,
        /// só desfaz a seleção; tocar na concha entra nela.
        /// </summary>
        public static void ValidarVoltarConcha()
        {
            GameObject raizObjeto = null;
            try
            {
                raizObjeto = new GameObject("__VR_SNC_Voltar_Concha") { hideFlags = HideFlags.HideAndDontSave };
                raizObjeto.SetActive(false);
                var raiz = CriarNo(raizObjeto.transform, "Raiz", out var raizNo, 0);
                var lobo = CriarNo(raiz, "Lobo", out var loboNo, 1);
                var outroLobo = CriarNo(raiz, "Outro lobo", out var outroLoboNo, 1);
                CriarNo(lobo, "Giro", out var giroNo, 2);
                CriarNo(lobo, "Outro giro", out var outroGiroNo, 2);
                raizNo.Configurar("Raiz", "Raiz", 0, null, new[] { loboNo, outroLoboNo }, true, true, true);
                loboNo.Configurar("Lobo", "Lobo", 1, raizNo, new[] { giroNo, outroGiroNo }, true, true, false);
                outroLoboNo.Configurar("Outro lobo", "Outro lobo", 1, raizNo, Array.Empty<NoInteracaoAnatomica>(), true, true, false);
                giroNo.Configurar("Giro", "Giro", 2, loboNo, Array.Empty<NoInteracaoAnatomica>(), true, true, false);
                outroGiroNo.Configurar("Outro giro", "Outro giro", 2, loboNo, Array.Empty<NoInteracaoAnatomica>(), true, true, false);
                var selecao = raizObjeto.AddComponent<GerenciadorSelecaoHierarquica>();
                selecao.Configurar(raizNo, null, null, null);
                selecao.DefinirEntradaAutomaticaNoSelecionado(true);
                raizObjeto.SetActive(true);

                selecao.EntrarNo(loboNo);
                selecao.SelecionarNo(giroNo);
                Exigir(selecao.NoEmFoco == loboNo && selecao.NoSelecionado == giroNo, "preparação: giro selecionado dentro do lobo");
                Exigir(selecao.DestinoVoltar() == loboNo.NomeLegivel, "o Voltar deveria anunciar a concha (" + selecao.DestinoVoltar() + ")");

                selecao.VoltarAoNoPai();
                Exigir(selecao.NoEmFoco == raizNo, "Voltar deveria abrir o nível de cima (foco " + Nome(selecao.NoEmFoco) + ")");
                Exigir(selecao.NoSelecionado == loboNo, "Voltar deveria deixar a concha selecionada (seleção " + Nome(selecao.NoSelecionado) + ")");
                Exigir(selecao.DestinoVoltar() == "Início", "na raiz com seleção, o Voltar anuncia Início");

                selecao.VoltarAoNoPai();
                Exigir(selecao.NoEmFoco == raizNo && selecao.NoSelecionado == null, "na raiz, Voltar só desfaz a seleção");
                Exigir(string.IsNullOrEmpty(selecao.DestinoVoltar()), "sem seleção na raiz não há para onde voltar");
                selecao.VoltarAoNoPai();
                Exigir(selecao.NoEmFoco == raizNo && selecao.NoSelecionado == null, "Voltar sem destino não muda nada");

                // Concha selecionada de novo e tocada: entra nela, como clicar num grupo.
                selecao.EntrarNo(loboNo);
                selecao.VoltarAoNoPai();
                selecao.SelecionarNo(loboNo);
                Exigir(selecao.NoEmFoco == loboNo && selecao.NoSelecionado == null, "tocar na concha selecionada deveria entrar nela");

                // O mesmo pelo Entrar (botão do painel ou B): entra e desfaz a seleção, sem deixar o grupo todo em âmbar.
                selecao.VoltarAoNoPai();
                selecao.EntrarNoSelecionado();
                Exigir(selecao.NoEmFoco == loboNo && selecao.NoSelecionado == null, "Entrar na concha deveria abri-la sem deixá-la selecionada");
            }
            finally
            {
                if (raizObjeto != null)
                    UnityEngine.Object.DestroyImmediate(raizObjeto);
            }
        }

        /// <summary>Gesto com as duas mãos: parado não muda nada; afastar escala; girar gira; mover leva junto; limites de escala.</summary>
        public static void ValidarGestosDuasMaos()
        {
            var a0 = new Vector3(-0.2f, 1.2f, 0.5f);
            var b0 = new Vector3(0.2f, 1.2f, 0.5f);
            var posicao0 = new Vector3(0f, 1.1f, 0.9f);
            var rotacao0 = Quaternion.Euler(0f, 30f, 0f);
            var escala0 = Vector3.one * 0.001f;
            var pivo0 = new Vector3(0f, 1.2f, 0.9f);

            GestosMaosNoAr.TransformarDuasMaos(a0, b0, a0, b0, posicao0, rotacao0, escala0, pivo0, 1f, out var posicao, out var rotacao, out var escala, out var fator);
            Exigir(Perto(posicao, posicao0) && Quaternion.Angle(rotacao, rotacao0) < 0.01f && Perto(escala, escala0) && Mathf.Approximately(fator, 1f), "mãos paradas não podem mexer no modelo");

            GestosMaosNoAr.TransformarDuasMaos(a0, b0, a0 - Vector3.right * 0.2f, b0 + Vector3.right * 0.2f, posicao0, rotacao0, escala0, pivo0, 1f, out posicao, out rotacao, out escala, out fator);
            Exigir(Mathf.Abs(fator - 2f) < 1e-4f && Perto(escala, escala0 * 2f), "afastar as mãos ao dobro deveria dobrar o tamanho (fator " + fator + ")");
            Exigir(Perto(posicao, pivo0 + (posicao0 - pivo0) * 2f), "a escala deveria ser em torno do centro do modelo");

            var giro = Quaternion.AngleAxis(90f, Vector3.up);
            var meio = (a0 + b0) * 0.5f;
            GestosMaosNoAr.TransformarDuasMaos(a0, b0, meio + giro * (a0 - meio), meio + giro * (b0 - meio), posicao0, rotacao0, escala0, pivo0, 1f, out posicao, out rotacao, out escala, out fator);
            Exigir(Quaternion.Angle(rotacao, giro * rotacao0) < 0.1f && Perto(escala, escala0), "girar as mãos 90° deveria girar o modelo 90° sem escalar");
            Exigir(Perto(posicao, pivo0 + giro * (posicao0 - pivo0)), "o giro deveria ser em torno do centro do modelo");

            var deslocamento = new Vector3(0.1f, -0.05f, 0.2f);
            GestosMaosNoAr.TransformarDuasMaos(a0, b0, a0 + deslocamento, b0 + deslocamento, posicao0, rotacao0, escala0, pivo0, 1f, out posicao, out _, out _, out _);
            Exigir(Perto(posicao, posicao0 + deslocamento), "mover as duas mãos juntas deveria levar o modelo junto");

            GestosMaosNoAr.TransformarDuasMaos(a0, b0, a0 - Vector3.right * 4f, b0 + Vector3.right * 4f, posicao0, rotacao0, escala0, pivo0, 1f, out _, out _, out escala, out fator);
            Exigir(Mathf.Approximately(fator, GestosMaosNoAr.EscalaMaxima) && Perto(escala, escala0 * GestosMaosNoAr.EscalaMaxima), "a escala deveria parar no limite máximo");
            GestosMaosNoAr.TransformarDuasMaos(a0, b0, meio - Vector3.right * 0.01f, meio + Vector3.right * 0.01f, posicao0, rotacao0, escala0, pivo0, 1f, out _, out _, out _, out fator);
            Exigir(Mathf.Approximately(fator, GestosMaosNoAr.EscalaMinima), "a escala deveria parar no limite mínimo");
        }

        /// <summary>Tema aplicado ao painel salvo, Voltar no cabeçalho, Entrar na barra, legendas e gestos instalados.</summary>
        public static void ValidarTemaNaCena()
        {
            var painel = GameObject.Find("Painel_Anatomico_VR_SNC");
            Exigir(painel != null, "painel principal ausente");
            var expandida = painel.transform.Find("ExpandedRoot");
            Exigir(CorIgual(expandida.Find("Background"), TemaVRSNC.Painel), "fundo do painel fora do tema (rode Aplicar Painel Compacto com Abas)");
            Exigir(CorIgual(expandida.Find("Header"), TemaVRSNC.Cabecalho), "cabeçalho fora do tema");
            foreach (var cartao in new[] { "InfoPanel", "NavigationPanel", "EEGPanel" })
                Exigir(CorIgual(expandida.Find(cartao), TemaVRSNC.Cartao), cartao + " fora do tema");
            var comContorno = painel.GetComponentsInChildren<Button>(true).Where(b => b.GetComponents<Outline>().Any(o => o.GetType() == typeof(Outline))).Select(b => b.name).ToArray();
            Exigir(comContorno.Length == 0, "botões ainda com contorno azul: " + string.Join(", ", comContorno));
            var voltar = expandida.Find("Header/Button_Back");
            Exigir(voltar != null && voltar.gameObject.activeSelf && voltar.GetComponent<Image>().raycastTarget, "o Voltar deveria estar no cabeçalho, ativo e clicável");
            var entrar = expandida.Find("Controls/Button_Enter");
            Exigir(entrar != null && entrar.gameObject.activeSelf, "o Entrar deveria estar de volta à barra de ações");
            var lista = expandida.Find("NavigationPanel")?.GetComponent<ScrollRect>()?.content;
            Exigir(lista != null && Mathf.Approximately(lista.pivot.y, 1f), "a lista de estruturas deveria ter o pivô no topo (uma lista curta ficaria centralizada)");
            var legendas = UnityEngine.Object.FindObjectsByType<LegendaControleVRSNC>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Exigir(legendas.Length == 2, "esperadas 2 legendas de controle, há " + legendas.Length);
            var baloes = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Affordance Callouts", StringComparison.Ordinal) && t.gameObject.activeSelf).Select(t => t.name).ToArray();
            Exigir(baloes.Length == 0, "balões do template ainda ligados: " + string.Join(", ", baloes));
            var gestos = UnityEngine.Object.FindFirstObjectByType<GestosMaosNoAr>(FindObjectsInactive.Include);
            Exigir(gestos != null, "gestos das mãos no ar não instalados (Ferramentas > VR SNC > Mãos > Instalar gestos no ar)");
            Exigir(gestos.GetComponentInParent<GerenciadorSelecaoHierarquica>(true) == null, "os gestos não podem ficar dentro do Sistema_Nervoso_Central");
        }

        private static Transform CriarNo(Transform pai, string nome, out NoInteracaoAnatomica no, int nivel)
        {
            var objeto = new GameObject(nome) { hideFlags = HideFlags.HideAndDontSave };
            objeto.transform.SetParent(pai, false);
            no = objeto.AddComponent<NoInteracaoAnatomica>();
            return objeto.transform;
        }

        private static string Nome(NoInteracaoAnatomica no) => no != null ? no.NomeLegivel : "nada";

        private static bool CorIgual(Transform alvo, Color esperada)
        {
            var imagem = alvo != null ? alvo.GetComponent<Image>() : null;
            if (imagem == null)
                return false;
            var c = imagem.color;
            return Mathf.Abs(c.r - esperada.r) < 0.01f && Mathf.Abs(c.g - esperada.g) < 0.01f && Mathf.Abs(c.b - esperada.b) < 0.01f && Mathf.Abs(c.a - esperada.a) < 0.01f;
        }

        private static bool Perto(Vector3 a, Vector3 b) => (a - b).magnitude < 1e-4f;

        private static void Exigir(bool condicao, string mensagem)
        {
            if (!condicao)
                throw new InvalidOperationException("HUD: " + mensagem + ".");
        }
    }
}
