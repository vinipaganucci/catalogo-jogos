using Microsoft.EntityFrameworkCore;
using catalogo_jogos.Models; // muda pro namespace certo

namespace catalogo_jogos.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Jogo> Jogos { get; set; } // exemplo
    }
}
