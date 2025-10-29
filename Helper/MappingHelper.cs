namespace catalogo_jogos.Helper
{
    public static class MappingHelper
    {
        public static string MappingBoolean(bool value)
        {
            if (value) 
            {
                return "Sim";
            }
            else
            {
                return "Não";
            }
        }
    }
}
