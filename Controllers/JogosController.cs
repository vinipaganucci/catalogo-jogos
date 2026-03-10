using catalogo_jogos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace catalogo_jogos.Controllers
{
    public class JogosController : Controller
    {
        private readonly string _connectionString = "Data Source=meubanco.db";

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult TelaEdicao(int id)
        {
            using var connectionCheck = new SqliteConnection(_connectionString);
            connectionCheck.Open();
            try
            {
                var alterCommand = connectionCheck.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN Platinado INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Id, Name, Year, FinishedInThisYear, Grade,
                   (SELECT MAX(g2.FinishedInThisYear) FROM Games g2 WHERE g2.Name = Games.Name) AS EverCompleted,
                   IsLastFinished, Ordem, CoverUrl, DlcUrl1, DlcUrl2, DlcUrl3, DlcUrl4, YoutubeUrl, Platinado
            FROM Games WHERE Id = $id";
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
                    IsLastFinished = reader.GetBoolean(6),
                    Ordem = reader.GetInt32(7),
                    CoverUrl = reader.GetString(8),
                    DlcUrl1 = reader.GetString(9),
                    DlcUrl2 = reader.GetString(10),
                    DlcUrl3 = reader.GetString(11),
                    DlcUrl4 = reader.GetString(12),
                    YoutubeUrl = reader.GetString(13),
                    Platinado = reader.GetBoolean(14)
                };
                return View(jogo);
            }
            return NotFound();
        }

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

        [HttpPost]
        public IActionResult SaveGame(Jogo model)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Games (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, Year INTEGER,
                    FinishedInThisYear INTEGER, Grade TEXT, IsLastFinished INTEGER DEFAULT 0,
                    Ordem INTEGER DEFAULT 0, CoverUrl TEXT DEFAULT '', DlcUrl1 TEXT DEFAULT '', 
                    DlcUrl2 TEXT DEFAULT '', DlcUrl3 TEXT DEFAULT '', DlcUrl4 TEXT DEFAULT '', 
                    YoutubeUrl TEXT DEFAULT '', Platinado INTEGER DEFAULT 0
                );";
            createCommand.ExecuteNonQuery();

            var transaction = connection.BeginTransaction();
            try
            {
                if (model.IsLastFinished)
                {
                    var resetCommand = connection.CreateCommand();
                    resetCommand.Transaction = transaction;
                    resetCommand.CommandText = "UPDATE Games SET IsLastFinished = 0 WHERE IsLastFinished = 1";
                    resetCommand.ExecuteNonQuery();
                }
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO Games (Name, Year, FinishedInThisYear, Grade, IsLastFinished, Platinado) VALUES ($name, $year, $finished, $grade, $islastfinished, $platinado);";
                command.Parameters.AddWithValue("$name", model.Name);
                command.Parameters.AddWithValue("$year", model.Year);
                command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
                command.Parameters.AddWithValue("$grade", model.Grade);
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished);
                command.Parameters.AddWithValue("$platinado", model.Platinado);
                command.ExecuteNonQuery();

                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }

            ViewBag.Mensagem = "Jogo saved com sucesso!";
            return View("Index");
        }

        [HttpPost]
        public IActionResult AtualizarObjetoJogo(Jogo model)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var transaction = connection.BeginTransaction();
            try
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"UPDATE Games SET Name=$name, Year=$year, FinishedInThisYear=$finished, Grade=$grade, IsLastFinished=$islastfinished, Ordem=$ordem, CoverUrl=$cover, Platinado=$platinado WHERE Id=$id";
                command.Parameters.AddWithValue("$id", model.Id);
                command.Parameters.AddWithValue("$name", model.Name);
                command.Parameters.AddWithValue("$year", model.Year);
                command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
                command.Parameters.AddWithValue("$grade", model.Grade);
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished);
                command.Parameters.AddWithValue("$ordem", model.Ordem);
                command.Parameters.AddWithValue("$cover", model.CoverUrl ?? "");
                command.Parameters.AddWithValue("$platinado", model.Platinado);
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }

            return RedirectToAction("ListaJogos");
        }

        public IActionResult Estatisticas()
        {
            var viewModel = new EstatisticasViewModel();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            // Lógica simplificada para exemplo
            return View(viewModel);
        }

        public IActionResult Detalhar(int id)
        {
            return TelaEdicao(id);
        }

        [HttpPost]
        public IActionResult SalvarOrdemLote(Dictionary<int, int> ordemValores)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var par in ordemValores)
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE Games SET Ordem = $ordem WHERE Id = $id";
                    command.Parameters.AddWithValue("$ordem", par.Value);
                    command.Parameters.AddWithValue("$id", par.Key);
                    command.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
            return RedirectToAction("ListaJogos");
        }

        // MÉTODO LISTAJOGOS MODIFICADO COM O BOTÃO ÚNICOS
        public IActionResult ListaJogos(string termoBusca, string sortOrder, int? filtroAno, bool? filtroNaoZerados, bool? filtroPlatinados, bool? unicos)
        {
            var listaJogos = new List<Jogo>();
            var distinctYears = new List<int>();

            using (var connectionYears = new SqliteConnection(_connectionString))
            {
                connectionYears.Open();
                var yearsCommand = connectionYears.CreateCommand();
                yearsCommand.CommandText = "SELECT DISTINCT Year FROM Games ORDER BY Year DESC";
                using var yearsReader = yearsCommand.ExecuteReader();
                while (yearsReader.Read()) distinctYears.Add(yearsReader.GetInt32(0));
            }
            ViewData["DistinctYears"] = distinctYears;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            var whereClauses = new List<string>();

            // Query base com EverCompleted calculado por nome
            string sql = @"
                WITH AllGamesWithCompletion AS (
                    SELECT Id, Name, Year, FinishedInThisYear, Grade, Ordem, Platinado,
                           MAX(CASE WHEN FinishedInThisYear = 1 OR FinishedInThisYear = 'true' THEN 1 ELSE 0 END) OVER (PARTITION BY Name) AS EverCompleted
                    FROM Games
                )
                SELECT Id, Name, Year, FinishedInThisYear, Grade, EverCompleted, Ordem, Platinado
                FROM AllGamesWithCompletion";

            // Filtros de busca
            if (!string.IsNullOrEmpty(termoBusca))
            {
                var searchClauses = new List<string> { "Name LIKE $termoBusca", "CAST(Year AS TEXT) LIKE $termoBusca", "Grade LIKE $termoBusca" };
                command.Parameters.AddWithValue("$termoBusca", $"%{termoBusca}%");
                whereClauses.Add($"({string.Join(" OR ", searchClauses)})");
            }

            if (filtroAno.HasValue)
            {
                whereClauses.Add("Year = $filtroAno");
                command.Parameters.AddWithValue("$filtroAno", filtroAno.Value);
            }

            if (filtroNaoZerados == true) whereClauses.Add("EverCompleted = 0");
            if (filtroPlatinados == true) whereClauses.Add("Platinado = 1");

            if (whereClauses.Count > 0) sql += $" WHERE {string.Join(" AND ", whereClauses)}";

            // LÓGICA DO BOTÃO ÚNICOS: Agrupa pelo nome para remover duplicatas
            if (unicos == true) sql += " GROUP BY Name";

            // Ordenação
            switch (sortOrder)
            {
                case "name": sql += " ORDER BY Name"; break;
                default: sql += " ORDER BY Year DESC, Ordem ASC"; break;
            }

            command.CommandText = sql;

            // Mantém os estados dos filtros para a View
            ViewData["CurrentFilter"] = termoBusca;
            ViewData["CurrentYearFilter"] = filtroAno;
            ViewData["CurrentSortOrder"] = sortOrder;
            ViewData["CurrentNaoZeradosFilter"] = filtroNaoZerados;
            ViewData["CurrentPlatinadosFilter"] = filtroPlatinados;
            ViewData["CurrentUnicos"] = unicos; // Novo estado

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
                    EverCompleted = reader.GetBoolean(5),
                    Ordem = reader.GetInt32(6),
                    Platinado = reader.GetBoolean(7)
                });
            }
            return View(listaJogos);
        }
    }
}