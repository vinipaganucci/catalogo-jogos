using catalogo_jogos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System.Diagnostics;

namespace catalogo_jogos.Controllers
{
    public class JogosController : Controller
    {

        private readonly string _connectionString = "Data Source=meubanco.db";

        //Tela Principal
        public IActionResult Index()
        {
            return View();
        }

        
        

        //Tela de edição dos jogos
        public IActionResult TelaEdicao(int id)
{
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, Year, FinishedInThisYear, Grade FROM Games WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var jogo = new Jogo
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Year = reader.GetInt32(2),
                    FinishedInThisYear = reader.GetBoolean(3),
                    Grade = reader.GetString(4)
                };
                return View(jogo);
            }

            return NotFound();
        }


        //Salva o objeto "Jogo" na tabela
        [HttpPost]
        public IActionResult SaveGame(Jogo model)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Games (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT,
                Year INTEGER,
                FinishedInThisYear TEXT,
                Grade TEXT
            );

            INSERT INTO Games (Name, Year, FinishedInThisYear, Grade)
            VALUES ($name, $year, $finished, $grade);
        ";

            command.Parameters.AddWithValue("$name", model.Name);
            command.Parameters.AddWithValue("$year", model.Year);
            command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
            command.Parameters.AddWithValue("$grade", model.Grade);

            command.ExecuteNonQuery();

            ViewBag.Mensagem = "Jogo salvo com sucesso!";
            return View("Index");
        }

        //Ação do botão de editar
        [HttpPost]
        public IActionResult AtualizarObjetoJogo(Jogo model)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Games
                SET Name = $name, Year = $year, FinishedInThisYear = $finishedinthisyear, Grade = $grade
                WHERE Id = $id";

            command.Parameters.AddWithValue("$id", model.Id);
            command.Parameters.AddWithValue("$name", model.Name);
            command.Parameters.AddWithValue("$year", model.Year);
            command.Parameters.AddWithValue("$finishedinthisyear", model.FinishedInThisYear);
            command.Parameters.AddWithValue("$grade", model.Grade);
            command.ExecuteNonQuery();

            TempData["Mensagem"] = "Jogo atualizado com sucesso!";
            return RedirectToAction("ListaJogos");
        }

        //Filtragem universal E Ordenação (Com lógica booleana)
        public IActionResult ListaJogos(string termoBusca, string sortOrder)
        {
            var listaJogos = new List<Jogo>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();

            // 1. Iniciar a query base
            string sql = "SELECT Id, Name, Year, FinishedInThisYear, Grade FROM Games";

            // 2. Adicionar o filtro (WHERE), se existir
            if (!string.IsNullOrEmpty(termoBusca))
            {
                // Lista dinâmica de condições
                var whereClauses = new List<string>
                {
                    "Name LIKE $termoBusca",
                    "CAST(Year AS TEXT) LIKE $termoBusca",
                    "Grade LIKE $termoBusca"
                };

                // Adiciona o parâmetro LIKE padrão
                command.Parameters.AddWithValue("$termoBusca", $"%{termoBusca}%");

                // --- INÍCIO DA NOVA LÓGICA ---
                string termoLimpo = termoBusca.Trim();

                // Verifica se a busca corresponde a "sim"
                if (termoLimpo.Equals("sim", StringComparison.OrdinalIgnoreCase))
                {
                    // Adiciona a condição booleana
                    whereClauses.Add("FinishedInThisYear = 1");
                }
                // Verifica se a busca corresponde a "não" (com e sem acento)
                else if (termoLimpo.Equals("não", StringComparison.OrdinalIgnoreCase) ||
                         termoLimpo.Equals("nao", StringComparison.OrdinalIgnoreCase))
                {
                    // Adiciona a condição booleana
                    whereClauses.Add("FinishedInThisYear = 0");
                }
                // --- FIM DA NOVA LÓGICA ---

                // Junta todas as condições com "OR"
                sql += $" WHERE {string.Join(" OR ", whereClauses)}";

                // Salva o filtro atual para ser usado pelos botões na View
                ViewData["CurrentFilter"] = termoBusca;
            }

            // 3. Adicionar a ordenação (ORDER BY)
            switch (sortOrder)
            {
                case "year":
                    sql += " ORDER BY Year"; // Ordena pelo ano (do menor para o maior)
                    break;
                case "name":
                    sql += " ORDER BY Name"; // Ordena por nome (A-Z)
                    break;
                default:
                    // Se não houver filtro, ordena por nome. Se houver filtro, 
                    // a ordem de relevância do LIKE é mantida (opcional, mas comum).
                    // Vamos manter o padrão de ordenar por nome.
                    sql += " ORDER BY Name";
                    break;
            }

            command.CommandText = sql;

            // 4. Executar a query
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                listaJogos.Add(new Jogo
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Year = reader.GetInt32(2),
                    FinishedInThisYear = reader.GetBoolean(3),
                    Grade = reader.GetString(4)
                });
            }

            return View(listaJogos);
        }


    }
}
