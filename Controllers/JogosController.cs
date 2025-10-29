using catalogo_jogos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
            // ATUALIZAÇÃO: Usamos uma sub-query para obter o 'EverCompleted'
            command.CommandText = @"
            SELECT Id, Name, Year, FinishedInThisYear, Grade,
                   (SELECT MAX(g2.FinishedInThisYear) 
                    FROM Games g2 
                    WHERE g2.Name = Games.Name) AS EverCompleted
            FROM Games
            WHERE Id = $id";

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
                    Grade = reader.GetString(4),
                    // ATUALIZAÇÃO: Ler a 6ª coluna (índice 5)
                    EverCompleted = reader.GetBoolean(5)
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

        //Ação do botão de excluir
        [HttpPost]
        public IActionResult ExcluirJogo(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Games WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();

            // Mensagem de sucesso (igual você faz na atualização)
            TempData["Mensagem"] = "Jogo excluído com sucesso!";
            return RedirectToAction("ListaJogos");
        }

        //Filtragem universal E Ordenação (Com CTE para corrigir o EverCompleted)
        public IActionResult ListaJogos(string termoBusca, string sortOrder)
        {
            var listaJogos = new List<Jogo>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();

            // 1. Iniciar a query base com o CTE (Common Table Expression)
            // Isso força o cálculo do EverCompleted ANTES do filtro WHERE
            string sql = @"
                WITH AllGamesWithCompletion AS (
                    SELECT Id, Name, Year, FinishedInThisYear, Grade,
                           MAX(FinishedInThisYear) OVER (PARTITION BY Name) AS EverCompleted
                    FROM Games
                )
                SELECT Id, Name, Year, FinishedInThisYear, Grade, EverCompleted
                FROM AllGamesWithCompletion"; // Agora selecionamos da CTE

            // 2. Adicionar o filtro (WHERE), se existir
            // Esta lógica será aplicada APÓS o cálculo do EverCompleted
            if (!string.IsNullOrEmpty(termoBusca))
            {
                var whereClauses = new List<string>
                {
                    "Name LIKE $termoBusca",
                    "CAST(Year AS TEXT) LIKE $termoBusca",
                    "Grade LIKE $termoBusca"
                };

                command.Parameters.AddWithValue("$termoBusca", $"%{termoBusca}%");

                string termoLimpo = termoBusca.Trim();
                if (termoLimpo.Equals("sim", StringComparison.OrdinalIgnoreCase))
                {
                    whereClauses.Add("FinishedInThisYear = 1");
                }
                else if (termoLimpo.Equals("não", StringComparison.OrdinalIgnoreCase) ||
                         termoLimpo.Equals("nao", StringComparison.OrdinalIgnoreCase))
                {
                    whereClauses.Add("FinishedInThisYear = 0");
                }

                sql += $" WHERE {string.Join(" OR ", whereClauses)}";
                ViewData["CurrentFilter"] = termoBusca;
            }

            // 3. Adicionar a ordenação (ORDER BY)
            switch (sortOrder)
            {
                case "year":
                    sql += " ORDER BY Year";
                    break;
                case "name":
                    sql += " ORDER BY Name";
                    break;
                default:
                    sql += " ORDER BY Name";
                    break;
            }

            command.CommandText = sql;

            // 4. Executar a query
            // A ordem das colunas (índices 0 a 5) permanece a mesma
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                listaJogos.Add(new Jogo
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Year = reader.GetInt32(2),
                    FinishedInThisYear = reader.GetBoolean(3),
                    Grade = reader.GetString(4),
                    EverCompleted = reader.GetBoolean(5)
                });
            }

            return View(listaJogos);
        }

        public IActionResult Estatisticas()
        {
            // 1. Inicializar o ViewModel com valores padrão
            var viewModel = new EstatisticasViewModel
            {
                LastGameFinished = "Nenhum jogo zerado registrado",
                MostFinishedGame = "N/A",
                MostPlayedGame = "N/A"
            };

            // Usar a connection string que você já tem no topo do arquivo
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // ---
            // 2. QUERY 1: ÚLTIMO JOGO ZERADO
            // (Assumindo que FinishedInThisYear é 1 para 'true' e 0 para 'false')
            // ---
            var cmdLastGame = connection.CreateCommand();
            cmdLastGame.CommandText = @"
                SELECT Name, Year 
                FROM Games 
                WHERE FinishedInThisYear = 1 
                ORDER BY Year DESC 
                LIMIT 1";

            using (var reader = cmdLastGame.ExecuteReader())
            {
                if (reader.Read())
                {
                    viewModel.LastGameFinished = $"{reader.GetString(0)} (em {reader.GetInt32(1)})";
                }
            } // Primeiro reader é fechado aqui

            // ---
            // 3. QUERY 2: JOGO ZERADO MAIS VEZES
            // ---
            var cmdMostFinished = connection.CreateCommand();
            cmdMostFinished.CommandText = @"
                SELECT Name, COUNT(*) AS Count 
                FROM Games 
                WHERE FinishedInThisYear = 1 
                GROUP BY Name 
                ORDER BY Count DESC 
                LIMIT 1";

            using (var reader = cmdMostFinished.ExecuteReader())
            {
                if (reader.Read())
                {
                    viewModel.MostFinishedGame = $"{reader.GetString(0)} ({reader.GetInt32(1)} vezes)";
                }
            } // Segundo reader é fechado aqui

            // ---
            // 4. QUERY 3: JOGO JOGADO (REGISTRADO) MAIS VEZES
            // ---
            var cmdMostPlayed = connection.CreateCommand();
            cmdMostPlayed.CommandText = @"
                SELECT Name, COUNT(*) AS Count 
                FROM Games 
                GROUP BY Name 
                ORDER BY Count DESC 
                LIMIT 1";

            using (var reader = cmdMostPlayed.ExecuteReader())
            {
                if (reader.Read())
                {
                    viewModel.MostPlayedGame = $"{reader.GetString(0)} ({reader.GetInt32(1)} registros)";
                }
            } // Terceiro reader é fechado aqui

            // 5. Enviar o ViewModel preenchido para a View
            return View(viewModel);
        }

    }
}
