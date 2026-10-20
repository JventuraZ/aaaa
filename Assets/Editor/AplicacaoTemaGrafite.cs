using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using VRSNC.Interface;

/// <summary>
/// Aplica o tema "Grafite suave" (<see cref="TemaVRSNC"/>) ao que fica salvo na cena: painel principal (fundo,
/// cabeçalho, cartões, contorno, botões, textos, aba recolhida, alça e bordas), oráculo do pulso e, nos controles, a
/// legenda com os comandos reais no lugar dos balões do template. É chamada no fim de
/// <see cref="ConfiguracaoPainelCompactoAbas.Aplicar"/>, registra tudo no Desfazer e não salva sozinha.
/// </summary>
public static class AplicacaoTemaGrafite
{
    private static readonly string[] Cartoes = { "InfoPanel", "NavigationPanel", "EEGPanel" };
    private static readonly string[] BaloesTemplate = { "Affordance Callouts Left", "Affordance Callouts Right" };

    public static void Aplicar(GameObject painel, GameObject pulso)
    {
        if (painel == null)
            return;
        var expandida = painel.transform.Find("ExpandedRoot");
        if (expandida != null)
        {
            Pintar(expandida.Find("Background"), TemaVRSNC.Painel);
            Pintar(expandida.Find("Moldura_Template_SpatialPanel"), TemaVRSNC.Contorno);
            Pintar(expandida.Find("Header"), TemaVRSNC.Cabecalho);
            foreach (var nome in Cartoes)
                Pintar(expandida.Find(nome), TemaVRSNC.Cartao);
            Pintar(expandida.Find("Barra_Manipulacao_Mundo"), TemaVRSNC.Alca);
            var bordas = expandida.Find("Bordas_Redimensionar_Janela");
            if (bordas != null)
            {
                foreach (Transform borda in bordas)
                    Pintar(borda, TemaVRSNC.BordaRedimensionar);
            }

            // O fundo do painel tinha um contorno do layout claro; com o tema, só a moldura desenha a borda.
            RemoverContorno(expandida.Find("Background"));
            PintarTextos(expandida);
        }

        Pintar(painel.transform.Find("CollapsedTab"), TemaVRSNC.Painel);
        PintarBotoes(painel.transform);
        if (pulso != null)
            AplicarPulso(pulso);
        InstalarLegendasControles();
    }

    private static void PintarBotoes(Transform raiz)
    {
        foreach (var botao in raiz.GetComponentsInChildren<Button>(true))
        {
            RemoverContorno(botao.transform);
            if (botao.targetGraphic != null)
                Undo.RecordObject(botao.targetGraphic, "Tema Grafite: botão");
            foreach (var rotulo in botao.GetComponentsInChildren<Text>(true))
                Undo.RecordObject(rotulo, "Tema Grafite: rótulo");
            Undo.RecordObject(botao, "Tema Grafite: cores do botão");
            TemaVRSNC.AplicarBotao(botao, botao.name == "Button_Reset" ? TemaVRSNC.Estilo.Perigo : TemaVRSNC.Estilo.Normal);
            // Rótulos TMP (oráculo do pulso) não são pintados por AplicarBotao.
            foreach (var rotuloTmp in botao.GetComponentsInChildren<TMP_Text>(true))
            {
                Undo.RecordObject(rotuloTmp, "Tema Grafite: rótulo TMP");
                rotuloTmp.color = TemaVRSNC.Texto;
            }
        }
    }

    /// <summary>Textos fora de botões, pelo papel de cada um no painel.</summary>
    private static void PintarTextos(Transform expandida)
    {
        foreach (var texto in expandida.GetComponentsInChildren<Text>(true))
        {
            if (texto.GetComponentInParent<Button>(true) != null)
                continue;
            Undo.RecordObject(texto, "Tema Grafite: texto");
            switch (texto.name)
            {
                case "Title":
                    TemaVRSNC.AplicarTexto(texto, TemaVRSNC.Papel.Titulo);
                    break;
                case "Status":
                case "Path":
                case "DisplayName":
                case "NavigationTitle":
                    TemaVRSNC.AplicarTexto(texto, TemaVRSNC.Papel.Secundario);
                    break;
                case "Instrucao_Manipulacao_Mundo":
                    TemaVRSNC.AplicarTexto(texto, TemaVRSNC.Papel.Apagado);
                    break;
                default:
                    TemaVRSNC.AplicarTexto(texto, TemaVRSNC.Papel.Corpo);
                    break;
            }
        }

        foreach (var texto in expandida.GetComponentsInChildren<TMP_Text>(true))
        {
            Undo.RecordObject(texto, "Tema Grafite: texto TMP");
            texto.color = TemaVRSNC.Texto;
        }
    }

    // O oráculo já é escuro (estilo do template): só o fundo, o separador e os textos entram no tom do tema.
    private static void AplicarPulso(GameObject pulso)
    {
        foreach (var imagem in pulso.GetComponentsInChildren<Image>(true))
        {
            if (imagem.GetComponentInParent<Button>(true) != null)
                continue;
            switch (imagem.name)
            {
                case "Panel":
                    Pintar(imagem.transform, TemaVRSNC.Painel);
                    break;
                case "Top":
                    Pintar(imagem.transform, TemaVRSNC.Contorno);
                    break;
            }
        }

        foreach (var texto in pulso.GetComponentsInChildren<TMP_Text>(true))
        {
            if (texto.GetComponentInParent<Button>(true) != null)
                continue;
            Undo.RecordObject(texto, "Tema Grafite: texto do oráculo");
            texto.color = TemaVRSNC.Texto;
        }

        PintarBotoes(pulso.transform);
    }

    /// <summary>
    /// Desliga os balões do template nos controles e põe em cada controle a legenda com os comandos reais do VR SNC.
    /// </summary>
    public static void InstalarLegendasControles()
    {
        var modalidade = Object.FindFirstObjectByType<XRInputModalityManager>(FindObjectsInactive.Include);
        if (modalidade == null)
            return;
        InstalarLegenda(modalidade.leftController, true);
        InstalarLegenda(modalidade.rightController, false);
        var raiz = modalidade.transform;
        foreach (var transformacao in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (System.Array.IndexOf(BaloesTemplate, transformacao.name) < 0 || !transformacao.gameObject.activeSelf)
                continue;
            Undo.RecordObject(transformacao.gameObject, "Desligar balões do template (comandos que o app não usa)");
            transformacao.gameObject.SetActive(false);
        }
    }

    private static void InstalarLegenda(GameObject controle, bool esquerdo)
    {
        if (controle == null)
            return;
        var legenda = controle.GetComponent<LegendaControleVRSNC>();
        if (legenda == null)
            legenda = Undo.AddComponent<LegendaControleVRSNC>(controle);
        Undo.RecordObject(legenda, "Legenda do controle");
        legenda.Configurar(esquerdo, LegendaControleVRSNC.LinhasPadrao(esquerdo));
        EditorUtility.SetDirty(legenda);
    }

    private static void Pintar(Transform alvo, Color cor)
    {
        var imagem = alvo != null ? alvo.GetComponent<Image>() : null;
        if (imagem == null)
            return;
        Undo.RecordObject(imagem, "Tema Grafite: superfície");
        imagem.color = cor;
    }

    private static void RemoverContorno(Transform alvo)
    {
        if (alvo == null)
            return;
        var contornos = new List<Outline>(alvo.GetComponents<Outline>());
        foreach (var contorno in contornos)
        {
            // Shadow é a classe base de Outline: só os contornos (azuis) saem.
            if (contorno.GetType() == typeof(Outline))
                Undo.DestroyObjectImmediate(contorno);
        }
    }
}
