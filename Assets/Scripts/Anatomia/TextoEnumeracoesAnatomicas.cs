namespace VRSNC.Anatomia
{
    /// <summary>
    /// Nomes em português, prontos para leitura no painel, das categorias anatômicas serializadas.
    /// Os identificadores dos enums continuam sem acentos para preservar a serialização.
    /// </summary>
    public static class TextoEnumeracoesAnatomicas
    {
        public static string Exibir(DivisaoAnatomicaPrincipal divisao)
        {
            return divisao switch
            {
                DivisaoAnatomicaPrincipal.Cerebro => "Cérebro",
                DivisaoAnatomicaPrincipal.Telencefalo => "Telencéfalo",
                DivisaoAnatomicaPrincipal.Diencefalo => "Diencéfalo",
                DivisaoAnatomicaPrincipal.Cerebelo => "Cerebelo",
                DivisaoAnatomicaPrincipal.TroncoEncefalico => "Tronco encefálico",
                DivisaoAnatomicaPrincipal.SistemaVentricular => "Sistema ventricular",
                DivisaoAnatomicaPrincipal.OutrosRevisao => "Em revisão",
                _ => string.Empty,
            };
        }

        public static string Exibir(SubdivisaoAnatomica subdivisao)
        {
            return subdivisao switch
            {
                SubdivisaoAnatomica.CortexCerebral => "Córtex cerebral",
                SubdivisaoAnatomica.FormacaoHipocampal => "Formação hipocampal",
                SubdivisaoAnatomica.SistemaLimbico => "Sistema límbico",
                SubdivisaoAnatomica.GangliosDaBase => "Gânglios da base",
                SubdivisaoAnatomica.SistemaOlfatorio => "Sistema olfatório",
                SubdivisaoAnatomica.SubstanciaBrancaComissurasETratos => "Substância branca, comissuras e tratos",
                SubdivisaoAnatomica.AreaSeptalEProsencefaloBasal => "Área septal e prosencéfalo basal",
                SubdivisaoAnatomica.Talamo => "Tálamo",
                SubdivisaoAnatomica.Hipotalamo => "Hipotálamo",
                SubdivisaoAnatomica.Epitalamo => "Epitálamo",
                SubdivisaoAnatomica.Subtalamo => "Subtálamo",
                SubdivisaoAnatomica.Cerebelo => "Cerebelo",
                SubdivisaoAnatomica.Mesencefalo => "Mesencéfalo",
                SubdivisaoAnatomica.Ponte => "Ponte",
                SubdivisaoAnatomica.BulboMedulaOblonga => "Bulbo (medula oblonga)",
                SubdivisaoAnatomica.VentriculosLaterais => "Ventrículos laterais",
                SubdivisaoAnatomica.TerceiroVentriculo => "Terceiro ventrículo",
                SubdivisaoAnatomica.AquedutoCerebral => "Aqueduto cerebral",
                SubdivisaoAnatomica.QuartoVentriculo => "Quarto ventrículo",
                SubdivisaoAnatomica.PlexoCoroide => "Plexo coroide",
                SubdivisaoAnatomica.RevisaoAnatomicaNecessaria => "Em revisão anatômica",
                _ => string.Empty,
            };
        }

        public static string Exibir(LoboCortical lobo)
        {
            return lobo switch
            {
                LoboCortical.Frontal => "Lobo frontal",
                LoboCortical.Parietal => "Lobo parietal",
                LoboCortical.Temporal => "Lobo temporal",
                LoboCortical.Occipital => "Lobo occipital",
                LoboCortical.Insula => "Ínsula",
                LoboCortical.Limbico => "Lobo límbico",
                LoboCortical.Occipitotemporal => "Região occipitotemporal",
                _ => string.Empty,
            };
        }
    }
}
