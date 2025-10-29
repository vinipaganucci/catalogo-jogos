using System.Collections.Generic; // <--- ADICIONE ESTE 'using'

namespace catalogo_jogos.Models
{
    public class EstatisticasViewModel
    {
        public string LastGameFinished { get; set; }

        // MUDE AS DUAS LINHAS ABAIXO DE 'string' PARA 'List<string>'
        public List<string> MostFinishedGame { get; set; }
        public List<string> MostPlayedGame { get; set; }

        // Adicionamos um construtor para inicializar as listas
        public EstatisticasViewModel()
        {
            MostFinishedGame = new List<string>();
            MostPlayedGame = new List<string>();
        }
    }
}