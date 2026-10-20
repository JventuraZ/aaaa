using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using VRSNC.Interacao;

namespace VRSNC.EEG
{
    /// <summary>
    /// Região Desikan-Killiany (atlas do fsaverage, onde o Protobrain calcula o córtex sob cada eletrodo)
    /// → estrutura equivalente do modelo BodyParts3D do VR, pelo nome em português e pelo lado.
    /// Os dois atlas dividem o córtex de formas diferentes; as equivalências "aproximadas" apontam o giro
    /// do modelo que contém a maior parte da região, e a interface avisa isso.
    /// </summary>
    public static class CorrespondenciaGirosEEG
    {
        private static readonly Dictionary<string, (string nome, bool aproximada)> Tabela = new()
        {
            ["superiorfrontal"] = ("giro frontal superior", false),
            ["rostralmiddlefrontal"] = ("giro frontal médio", false),
            ["caudalmiddlefrontal"] = ("giro frontal médio", false),
            ["parsopercularis"] = ("giro frontal inferior", false),
            ["parstriangularis"] = ("giro frontal inferior", false),
            ["parsorbitalis"] = ("giro frontal inferior", false),
            ["lateralorbitofrontal"] = ("giro orbital lateral", false),
            ["medialorbitofrontal"] = ("giro orbital medial", false),
            ["frontalpole"] = ("giro frontal superior", true),
            ["precentral"] = ("giro pré-central", false),
            ["paracentral"] = ("giro pré-central", true),
            ["postcentral"] = ("giro pós-central", false),
            ["supramarginal"] = ("giro supramarginal", false),
            ["superiorparietal"] = ("lóbulo parietal superior", false),
            ["inferiorparietal"] = ("giro angular", true),
            ["precuneus"] = ("precúneo", false),
            ["superiortemporal"] = ("giro temporal superior", false),
            ["bankssts"] = ("giro temporal superior", true),
            ["temporalpole"] = ("giro temporal superior", true),
            ["transversetemporal"] = ("giro temporal transverso anterior", false),
            ["middletemporal"] = ("giro temporal médio", false),
            ["inferiortemporal"] = ("giro temporal inferior", false),
            ["fusiform"] = ("giro fusiforme", false),
            ["entorhinal"] = ("giro parahipocampal posterior", true),
            ["parahippocampal"] = ("giro parahipocampal posterior", false),
            ["lateraloccipital"] = ("giro occipital lateral", false),
            ["lingual"] = ("giro lingual", false),
            ["cuneus"] = ("cúneo", false),
            ["pericalcarine"] = ("cúneo", true),
            ["rostralanteriorcingulate"] = ("giro do cíngulo", false),
            ["caudalanteriorcingulate"] = ("giro do cíngulo", false),
            ["posteriorcingulate"] = ("giro do cíngulo", false),
            ["isthmuscingulate"] = ("giro do cíngulo", false),
            ["insula"] = ("primeiro giro curto da ínsula", true),
        };

        public static bool TentarObter(string chaveDesikan, out string nomeBase, out bool aproximada)
        {
            if (chaveDesikan != null && Tabela.TryGetValue(chaveDesikan, out var entrada))
            {
                nomeBase = entrada.nome;
                aproximada = entrada.aproximada;
                return true;
            }

            nomeBase = null;
            aproximada = false;
            return false;
        }

        /// <summary>
        /// Estrutura final do modelo para uma região e um lado. Partes repetidas da mesma estrutura
        /// (malhas divididas) devolvem a primeira, pela ordem do código do arquivo.
        /// </summary>
        public static NoInteracaoAnatomica Resolver(Transform raiz, string chaveDesikan, bool esquerdo, out bool aproximada)
        {
            aproximada = false;
            if (raiz == null || !TentarObter(chaveDesikan, out var nomeBase, out aproximada))
                return null;
            var padrao = new Regex("^" + Regex.Escape(nomeBase) + (esquerdo ? " esquerd[oa]$" : " direit[oa]$"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            NoInteracaoAnatomica melhor = null;
            var melhorCodigo = string.Empty;
            foreach (var no in raiz.GetComponentsInChildren<NoInteracaoAnatomica>(true))
            {
                if (no.NosFilhos.Count > 0 || !padrao.IsMatch(no.NomeLegivel ?? string.Empty))
                    continue;
                var codigo = NoInteracaoAnatomica.ObterCodigoArquivo(no.NomeExibicao) ?? string.Empty;
                if (melhor == null || string.CompareOrdinal(codigo, melhorCodigo) < 0)
                {
                    melhor = no;
                    melhorCodigo = codigo;
                }
            }

            return melhor;
        }

        /// <summary>Caminho de um nó a partir da raiz, para guardar na cena e reencontrar em execução.</summary>
        public static string Caminho(Transform raiz, Transform alvo)
        {
            var partes = new List<string>();
            for (var atual = alvo; atual != null && atual != raiz; atual = atual.parent)
                partes.Insert(0, atual.name);
            return string.Join("/", partes);
        }
    }
}
