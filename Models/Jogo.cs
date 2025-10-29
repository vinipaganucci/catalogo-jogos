namespace catalogo_jogos.Models
{
    public class Jogo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Year { get; set; }
        public bool FinishedInThisYear { get; set; }
        public string Grade { get; set; }

        public bool EverCompleted { get; set; }

        public bool IsLastFinished { get; set; }
    }
}
