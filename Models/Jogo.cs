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
        public int Ordem { get; set; }
        public string CoverUrl { get; set; }

        
        public string DlcUrl1 { get; set; } 
        public string DlcUrl2 { get; set; } 
        public string DlcUrl3 { get; set; }
        public string DlcUrl4 { get; set; } 
       
        public string YoutubeUrl { get; set; }
    }
}