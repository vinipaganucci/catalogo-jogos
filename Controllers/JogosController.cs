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
            // Garante que a coluna existe (igual ao SaveGame)
            using var connectionCheck = new SqliteConnection(_connectionString);
            connectionCheck.Open();
            try
            {
                var alterCommand = connectionCheck.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN IsLastFinished INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                if (!ex.Message.Contains("duplicate column name")) throw;
            }
            connectionCheck.Close();


            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Id, Name, Year, FinishedInThisYear, Grade,
                   (SELECT MAX(g2.FinishedInThisYear) 
                    FROM Games g2 
                    WHERE g2.Name = Games.Name) AS EverCompleted,
                   IsLastFinished
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
                    EverCompleted = reader.GetBoolean(5),
                    IsLastFinished = reader.GetBoolean(6) // Lendo o novo campo
                };
                return View(jogo);
            }

            return NotFound();
        }

        //Exclui o jogo
        [HttpPost]
        public IActionResult ExcluirJogo(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Games WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            command.ExecuteNonQuery();

            TempData["Mensagem"] = "Jogo excluído com sucesso!";
            return RedirectToAction("ListaJogos");
        }


        //Salva o objeto "Jogo" na tabela
        [HttpPost]
        public IActionResult SaveGame(Jogo model)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // --- ETAPA 1: Garantir que a tabela e a coluna existem ---
            var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Games (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT,
                    Year INTEGER,
                    FinishedInThisYear INTEGER,
                    Grade TEXT,
                    IsLastFinished INTEGER DEFAULT 0 
                );";
            createCommand.ExecuteNonQuery();

            // --- ETAPA 2: Adicionar a coluna 'IsLastFinished' se ela não existir ---
            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN IsLastFinished INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                if (!ex.Message.Contains("duplicate column name"))
                {
                    throw; // Lança outros erros
                }
                // Se a coluna já existe, ignora o erro e continua.
            }

            // --- ETAPA 3: Iniciar a transação para o UPDATE e INSERT ---
            var transaction = connection.BeginTransaction();
            try
            {
                // LÓGICA DE RESET
                if (model.IsLastFinished)
                {
                    var resetCommand = connection.CreateCommand();
                    resetCommand.Transaction = transaction;
                    resetCommand.CommandText = "UPDATE Games SET IsLastFinished = 0 WHERE IsLastFinished = 1";
                    resetCommand.ExecuteNonQuery();
                }

                // COMANDO DE INSERÇÃO ATUALIZADO
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO Games (Name, Year, FinishedInThisYear, Grade, IsLastFinished)
                    VALUES ($name, $year, $finished, $grade, $islastfinished);
                    ";

                command.Parameters.AddWithValue("$name", model.Name);
                command.Parameters.AddWithValue("$year", model.Year);
                command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
                command.Parameters.AddWithValue("$grade", model.Grade);
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished); // Novo campo

                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            ViewBag.Mensagem = "Jogo salvo com sucesso!";
            return View("Index");
        }

        //Ação do botão de editar
        [HttpPost]
        public IActionResult AtualizarObjetoJogo(Jogo model)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var transaction = connection.BeginTransaction();

            try
            {
                // LÓGICA DE RESET (Igual ao SaveGame)
                if (model.IsLastFinished)
                {
                    var resetCommand = connection.CreateCommand();
                    resetCommand.Transaction = transaction;
                    resetCommand.CommandText = "UPDATE Games SET IsLastFinished = 0 WHERE IsLastFinished = 1";
                    resetCommand.ExecuteNonQuery();
                }

                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE Games
                    SET Name = $name, 
                        Year = $year, 
                        FinishedInThisYear = $finished, 
                        Grade = $grade,
                        IsLastFinished = $islastfinished 
                    WHERE Id = $id";

                command.Parameters.AddWithValue("$id", model.Id);
                command.Parameters.AddWithValue("$name", model.Name);
                command.Parameters.AddWithValue("$year", model.Year);
                command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
                command.Parameters.AddWithValue("$grade", model.Grade);
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished); // Adicionado

                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            TempData["Mensagem"] = "Jogo atualizado com sucesso!";
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
            var viewModel = new EstatisticasViewModel
            {
                LastGameFinished = "Nenhum jogo zerado registrado",
                MostFinishedGame = "N/A",
                MostPlayedGame = "N/A"
            };

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // ---
            // 2. QUERY 1: ÚLTIMO JOGO ZERADO (LOGICA ATUALIZADA)
            // Busca o único jogo marcado com IsLastFinished = 1
            // ---
            var cmdLastGame = connection.CreateCommand();
            cmdLastGame.CommandText = @"
                SELECT Name, Year 
                FROM Games 
                WHERE IsLastFinished = 1 
                LIMIT 1";

            using (var reader = cmdLastGame.ExecuteReader())
            {
                if (reader.Read())
                {
                    viewModel.LastGameFinished = $"{reader.GetString(0)} (em {reader.GetInt32(1)})";
                }
            }

            // ---
            // 3. QUERY 2: JOGO ZERADO MAIS VEZES (Sem alteração)
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
            }

            // ---
            // 4. QUERY 3: JOGO JOGADO (REGISTRADO) MAIS VEZES (Sem alteração)
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
            }

            return View(viewModel);
        }

    }
}
