using catalogo_jogos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace catalogo_jogos.Controllers
{
    public class JogosController : Controller
    {
        private readonly string _connectionString = "Data Source=meubanco.db";

        // Tela Principal
        public IActionResult Index()
        {
            return View();
        }

        // Tela de edição dos jogos
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

            try
            {
                var alterAnalise = connectionCheck.CreateCommand();
                alterAnalise.CommandText = "ALTER TABLE Games ADD COLUMN Analise TEXT DEFAULT ''";
                alterAnalise.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            connectionCheck.Close();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Id, Name, Year, FinishedInThisYear, Grade,
                   (SELECT MAX(g2.FinishedInThisYear) FROM Games g2 WHERE g2.Name = Games.Name) AS EverCompleted,
                   IsLastFinished, Ordem, CoverUrl, DlcUrl1, DlcUrl2, DlcUrl3, DlcUrl4, YoutubeUrl, Platinado,
                   COALESCE(
                       NULLIF(Analise, ''), 
                       (SELECT g2.Analise FROM Games g2 WHERE g2.Name = Games.Name AND g2.Analise IS NOT NULL AND g2.Analise != '' ORDER BY g2.Id DESC LIMIT 1),
                       ''
                   ) AS AnaliseHerdada
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
                    IsLastFinished = reader.GetBoolean(6),
                    Ordem = reader.GetInt32(7),
                    CoverUrl = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    DlcUrl1 = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    DlcUrl2 = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    DlcUrl3 = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    DlcUrl4 = reader.IsDBNull(12) ? "" : reader.GetString(12),
                    YoutubeUrl = reader.IsDBNull(13) ? "" : reader.GetString(13),
                    Platinado = reader.GetBoolean(14),
                    Analise = reader.IsDBNull(15) ? "" : reader.GetString(15)
                };
                return View(jogo);
            }

            return NotFound();
        }

        // Exclui o jogo e atualiza o antecessor como último se necessário
        [HttpPost]
        public IActionResult ExcluirJogo(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var transaction = connection.BeginTransaction();
            try
            {
                // Verifica se o jogo excluído era o último zerado
                var checkCmd = connection.CreateCommand();
                checkCmd.Transaction = transaction;
                checkCmd.CommandText = "SELECT IsLastFinished FROM Games WHERE Id = $id";
                checkCmd.Parameters.AddWithValue("$id", id);
                var wasLastFinishedObj = checkCmd.ExecuteScalar();
                bool wasLastFinished = wasLastFinishedObj != null && Convert.ToBoolean(wasLastFinishedObj);

                // Exclui o jogo
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM Games WHERE Id = $id";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();

                // Se era o último zerado, define o antecessor mais recente (zerado) como último zerado
                if (wasLastFinished)
                {
                    var updatePrevCmd = connection.CreateCommand();
                    updatePrevCmd.Transaction = transaction;
                    updatePrevCmd.CommandText = @"
                        UPDATE Games 
                        SET IsLastFinished = 1 
                        WHERE Id = (
                            SELECT Id FROM Games 
                            WHERE FinishedInThisYear = 1 
                            ORDER BY Year DESC, Ordem DESC, Id DESC 
                            LIMIT 1
                        )";
                    updatePrevCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            TempData["Mensagem"] = "Jogo excluído com sucesso!";
            return RedirectToAction("ListaJogos");
        }

        // SALVA O JOGO NO INDEX
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
                    YoutubeUrl TEXT DEFAULT '', Platinado INTEGER DEFAULT 0, Analise TEXT DEFAULT ''
                );";
            createCommand.ExecuteNonQuery();

            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN Platinado INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            try
            {
                var alterAnalise = connection.CreateCommand();
                alterAnalise.CommandText = "ALTER TABLE Games ADD COLUMN Analise TEXT DEFAULT ''";
                alterAnalise.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

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

                string coverUrlHerdada = "";
                string dlc1Herdada = "", dlc2Herdada = "", dlc3Herdada = "", dlc4Herdada = "";
                string youtubeHerdada = "";
                string analiseHerdada = "";

                var searchExisting = connection.CreateCommand();
                searchExisting.Transaction = transaction;
                searchExisting.CommandText = @"
                    SELECT CoverUrl, DlcUrl1, DlcUrl2, DlcUrl3, DlcUrl4, YoutubeUrl, Analise 
                    FROM Games 
                    WHERE Name = $name 
                    ORDER BY Id DESC LIMIT 1";
                searchExisting.Parameters.AddWithValue("$name", model.Name ?? "");

                using (var readerExisting = searchExisting.ExecuteReader())
                {
                    if (readerExisting.Read())
                    {
                        coverUrlHerdada = readerExisting.IsDBNull(0) ? "" : readerExisting.GetString(0);
                        dlc1Herdada = readerExisting.IsDBNull(1) ? "" : readerExisting.GetString(1);
                        dlc2Herdada = readerExisting.IsDBNull(2) ? "" : readerExisting.GetString(2);
                        dlc3Herdada = readerExisting.IsDBNull(3) ? "" : readerExisting.GetString(3);
                        dlc4Herdada = readerExisting.IsDBNull(4) ? "" : readerExisting.GetString(4);
                        youtubeHerdada = readerExisting.IsDBNull(5) ? "" : readerExisting.GetString(5);
                        analiseHerdada = readerExisting.IsDBNull(6) ? "" : readerExisting.GetString(6);
                    }
                }

                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO Games (Name, Year, FinishedInThisYear, Grade, IsLastFinished, Platinado, CoverUrl, DlcUrl1, DlcUrl2, DlcUrl3, DlcUrl4, YoutubeUrl, Analise)
                    VALUES ($name, $year, $finished, $grade, $islastfinished, $platinado, $cover, $dlc1, $dlc2, $dlc3, $dlc4, $youtube, $analise);";

                command.Parameters.AddWithValue("$name", model.Name ?? "");
                command.Parameters.AddWithValue("$year", model.Year);
                command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
                command.Parameters.AddWithValue("$grade", model.Grade ?? "");
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished);
                command.Parameters.AddWithValue("$platinado", model.Platinado);
                command.Parameters.AddWithValue("$cover", coverUrlHerdada);
                command.Parameters.AddWithValue("$dlc1", dlc1Herdada);
                command.Parameters.AddWithValue("$dlc2", dlc2Herdada);
                command.Parameters.AddWithValue("$dlc3", dlc3Herdada);
                command.Parameters.AddWithValue("$dlc4", dlc4Herdada);
                command.Parameters.AddWithValue("$youtube", youtubeHerdada);
                command.Parameters.AddWithValue("$analise", analiseHerdada);

                command.ExecuteNonQuery();

                var getLastIdCommand = connection.CreateCommand();
                getLastIdCommand.Transaction = transaction;
                getLastIdCommand.CommandText = "SELECT last_insert_rowid()";

                long lastId = (long)getLastIdCommand.ExecuteScalar();

                var updateOrdemCommand = connection.CreateCommand();
                updateOrdemCommand.Transaction = transaction;
                updateOrdemCommand.CommandText = "UPDATE Games SET Ordem = $id WHERE Id = $id";
                updateOrdemCommand.Parameters.AddWithValue("$id", lastId);
                updateOrdemCommand.ExecuteNonQuery();

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

        // EDITAR JOGO
        [HttpPost]
        public IActionResult AtualizarObjetoJogo(Jogo model)
        {
            var termoBusca = Request.Query["termoBusca"];
            var sortOrder = Request.Query["sortOrder"];
            var filtroAno = Request.Query["filtroAno"];
            var filtroNaoZerados = Request.Query["filtroNaoZerados"];

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
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
                command.CommandText = @"
                    UPDATE Games
                    SET Name = $name, Year = $year, FinishedInThisYear = $finished, 
                        Grade = $grade, IsLastFinished = $islastfinished, Ordem = $ordem,
                        CoverUrl = $coverurl, DlcUrl1 = $dlc1, DlcUrl2 = $dlc2, 
                        DlcUrl3 = $dlc3, DlcUrl4 = $dlc4, YoutubeUrl = $youtubeurl,
                        Platinado = $platinado, Analise = $analise
                    WHERE Id = $id";

                command.Parameters.AddWithValue("$id", model.Id);
                command.Parameters.AddWithValue("$name", model.Name ?? "");
                command.Parameters.AddWithValue("$year", model.Year);
                command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
                command.Parameters.AddWithValue("$grade", model.Grade ?? "");
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished);
                command.Parameters.AddWithValue("$ordem", model.Ordem);
                command.Parameters.AddWithValue("$coverurl", model.CoverUrl ?? "");
                command.Parameters.AddWithValue("$dlc1", model.DlcUrl1 ?? "");
                command.Parameters.AddWithValue("$dlc2", model.DlcUrl2 ?? "");
                command.Parameters.AddWithValue("$dlc3", model.DlcUrl3 ?? "");
                command.Parameters.AddWithValue("$dlc4", model.DlcUrl4 ?? "");
                command.Parameters.AddWithValue("$youtubeurl", model.YoutubeUrl ?? "");
                command.Parameters.AddWithValue("$platinado", model.Platinado);
                command.Parameters.AddWithValue("$analise", model.Analise ?? "");

                command.ExecuteNonQuery();

                var propagateCommand = connection.CreateCommand();
                propagateCommand.Transaction = transaction;
                propagateCommand.CommandText = @"
                    UPDATE Games
                    SET CoverUrl = $coverurl, DlcUrl1 = $dlc1, DlcUrl2 = $dlc2, 
                        DlcUrl3 = $dlc3, DlcUrl4 = $dlc4, YoutubeUrl = $youtubeurl, Analise = $analise
                    WHERE Name = $name";

                propagateCommand.Parameters.AddWithValue("$coverurl", model.CoverUrl ?? "");
                propagateCommand.Parameters.AddWithValue("$dlc1", model.DlcUrl1 ?? "");
                propagateCommand.Parameters.AddWithValue("$dlc2", model.DlcUrl2 ?? "");
                propagateCommand.Parameters.AddWithValue("$dlc3", model.DlcUrl3 ?? "");
                propagateCommand.Parameters.AddWithValue("$dlc4", model.DlcUrl4 ?? "");
                propagateCommand.Parameters.AddWithValue("$youtubeurl", model.YoutubeUrl ?? "");
                propagateCommand.Parameters.AddWithValue("$analise", model.Analise ?? "");
                propagateCommand.Parameters.AddWithValue("$name", model.Name ?? "");

                propagateCommand.ExecuteNonQuery();

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            TempData["Mensagem"] = "Jogo atualizado com sucesso!";

            return RedirectToAction("Detalhar", new
            {
                id = model.Id,
                termoBusca = termoBusca,
                sortOrder = sortOrder,
                filtroAno = filtroAno,
                filtroNaoZerados = filtroNaoZerados
            });
        }

        // SALVAR ANÁLISE
        [HttpPost]
        public IActionResult SalvarAnalise(int id, string analise)
        {
            var termoBusca = Request.Query["termoBusca"];
            var sortOrder = Request.Query["sortOrder"];
            var filtroAno = Request.Query["filtroAno"];
            var filtroNaoZerados = Request.Query["filtroNaoZerados"];

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var getNameCmd = connection.CreateCommand();
                getNameCmd.Transaction = transaction;
                getNameCmd.CommandText = "SELECT Name FROM Games WHERE Id = $id";
                getNameCmd.Parameters.AddWithValue("$id", id);
                string gameName = Convert.ToString(getNameCmd.ExecuteScalar());

                var updateCmd = connection.CreateCommand();
                updateCmd.Transaction = transaction;
                updateCmd.CommandText = "UPDATE Games SET Analise = $analise WHERE Name = $name";
                updateCmd.Parameters.AddWithValue("$analise", analise ?? "");
                updateCmd.Parameters.AddWithValue("$name", gameName);
                updateCmd.ExecuteNonQuery();

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            return RedirectToAction("Detalhar", new { id, termoBusca, sortOrder, filtroAno, filtroNaoZerados });
        }

        // MÉTODO 'ESTATISTICAS' CORRIGIDO PARA BUSCAR O ANTECESSOR SE O ÚLTIMO FOR EXCLUÍDO
        public IActionResult Estatisticas()
        {
            int quantidadeJogosZerados = ConsultarQuantidadeDeJogosZerados();
            ViewBag.QuantidadeJogosZerados = quantidadeJogosZerados;

            var viewModel = new EstatisticasViewModel
            {
                LastGameFinished = "Nenhum jogo zerado registrado"
            };

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Tenta buscar por IsLastFinished = 1 ou cai de volta para o antecessor mais recente que esteja zerado
            var cmdLastGame = connection.CreateCommand();
            cmdLastGame.CommandText = @"
                SELECT Name, Year 
                FROM Games 
                WHERE FinishedInThisYear = 1 
                ORDER BY IsLastFinished DESC, Year DESC, Ordem DESC, Id DESC 
                LIMIT 1";

            using (var reader = cmdLastGame.ExecuteReader())
            {
                if (reader.Read())
                {
                    viewModel.LastGameFinished = $"{reader.GetString(0)} (em {reader.GetInt32(1)})";
                }
            }

            var cmdMostFinished = connection.CreateCommand();
            cmdMostFinished.CommandText = @"
                WITH GameCounts AS (SELECT Name, COUNT(*) AS Count FROM Games WHERE FinishedInThisYear = 1 GROUP BY Name)
                SELECT Name, Count FROM GameCounts WHERE Count = (SELECT MAX(Count) FROM GameCounts) ORDER BY Name;";
            using (var reader = cmdMostFinished.ExecuteReader())
            {
                while (reader.Read())
                {
                    viewModel.MostFinishedGame.Add($"{reader.GetString(0)} ({reader.GetInt32(1)} vezes)");
                }
            }
            if (viewModel.MostFinishedGame.Count == 0) viewModel.MostFinishedGame.Add("N/A");

            var cmdMostPlayed = connection.CreateCommand();
            cmdMostPlayed.CommandText = @"
                WITH GameCounts AS (SELECT Name, COUNT(*) AS Count FROM Games GROUP BY Name)
                SELECT Name, Count FROM GameCounts WHERE Count = (SELECT MAX(Count) FROM GameCounts) ORDER BY Name;";
            using (var reader = cmdMostPlayed.ExecuteReader())
            {
                while (reader.Read())
                {
                    viewModel.MostPlayedGame.Add($"{reader.GetString(0)} ({reader.GetInt32(1)} registros)");
                }
            }
            if (viewModel.MostPlayedGame.Count == 0) viewModel.MostPlayedGame.Add("N/A");

            return View(viewModel);
        }

        // MÉTODO 'DETALHAR'
        public IActionResult Detalhar(int id)
        {
            using (var connectionCheck = new SqliteConnection(_connectionString))
            {
                connectionCheck.Open();
                try
                {
                    var alterCommand = connectionCheck.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN Platinado INTEGER DEFAULT 0";
                    alterCommand.ExecuteNonQuery();
                }
                catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

                try
                {
                    var alterAnalise = connectionCheck.CreateCommand();
                    alterAnalise.CommandText = "ALTER TABLE Games ADD COLUMN Analise TEXT DEFAULT ''";
                    alterAnalise.ExecuteNonQuery();
                }
                catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Id, Name, Year, FinishedInThisYear, Grade,
                   (SELECT MAX(g2.FinishedInThisYear) FROM Games g2 WHERE g2.Name = Games.Name) AS EverCompleted,
                   IsLastFinished, Ordem, CoverUrl, DlcUrl1, DlcUrl2, DlcUrl3, DlcUrl4, YoutubeUrl, Platinado,
                   COALESCE(
                       NULLIF(Analise, ''), 
                       (SELECT g2.Analise FROM Games g2 WHERE g2.Name = Games.Name AND g2.Analise IS NOT NULL AND g2.Analise != '' ORDER BY g2.Id DESC LIMIT 1),
                       ''
                   ) AS AnaliseHerdada
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
                    IsLastFinished = reader.GetBoolean(6),
                    Ordem = reader.GetInt32(7),
                    CoverUrl = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    DlcUrl1 = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    DlcUrl2 = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    DlcUrl3 = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    DlcUrl4 = reader.IsDBNull(12) ? "" : reader.GetString(12),
                    YoutubeUrl = reader.IsDBNull(13) ? "" : reader.GetString(13),
                    Platinado = reader.GetBoolean(14),
                    Analise = reader.IsDBNull(15) ? "" : reader.GetString(15)
                };
                return View(jogo);
            }

            return NotFound();
        }

        // MÉTODO 'SALVAR ORDEM LOTE'
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
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            TempData["Mensagem"] = "Ordem dos jogos atualizada com sucesso!";
            return RedirectToAction("ListaJogos");
        }

        // MÉTODO 'LISTAJOGOS'
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

            string sql = @"
        WITH AllGamesWithCompletion AS (
            SELECT Id, Name, Year, FinishedInThisYear, Grade, Ordem, Platinado, CoverUrl,
                   MAX(CASE WHEN FinishedInThisYear = 1 OR FinishedInThisYear = 'true' THEN 1 ELSE 0 END) OVER (PARTITION BY Name) AS EverCompleted
            FROM Games
        )
        SELECT Id, Name, Year, FinishedInThisYear, Grade, EverCompleted, Ordem, Platinado, CoverUrl
        FROM AllGamesWithCompletion";

            if (!string.IsNullOrEmpty(termoBusca))
            {
                command.Parameters.AddWithValue("$termoBusca", $"%{termoBusca}%");
                whereClauses.Add("(Name LIKE $termoBusca OR CAST(Year AS TEXT) LIKE $termoBusca OR Grade LIKE $termoBusca)");
            }
            if (filtroAno.HasValue)
            {
                whereClauses.Add("Year = $filtroAno");
                command.Parameters.AddWithValue("$filtroAno", filtroAno.Value);
            }
            if (filtroNaoZerados == true) whereClauses.Add("EverCompleted = 0");
            if (filtroPlatinados == true) whereClauses.Add("Platinado = 1");

            if (whereClauses.Count > 0) sql += $" WHERE {string.Join(" AND ", whereClauses)}";

            if (unicos == true) sql += " GROUP BY Name";

            switch (sortOrder)
            {
                case "name": sql += " ORDER BY Name ASC"; break;
                default: sql += " ORDER BY Year DESC, Ordem ASC"; break;
            }

            command.CommandText = sql;

            ViewData["CurrentFilter"] = termoBusca;
            ViewData["CurrentYearFilter"] = filtroAno;
            ViewData["CurrentUnicos"] = unicos;
            ViewData["CurrentNaoZeradosFilter"] = filtroNaoZerados;
            ViewData["CurrentPlatinadosFilter"] = filtroPlatinados;

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
                    Platinado = reader.GetBoolean(7),
                    CoverUrl = reader.IsDBNull(8) ? "" : reader.GetString(8)
                });
            }
            return View(listaJogos);
        }

        public int ConsultarQuantidadeDeJogosZerados()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                SELECT COUNT (DISTINCT Name) FROM Games Where FinishedInThisYear = 1;
                ";

            var resultado = createCommand.ExecuteScalar();
            int quantidade = Convert.ToInt32(resultado);

            return quantidade;
        }
    }
}