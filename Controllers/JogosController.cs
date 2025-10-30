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
            // Garante que as colunas existem
            using var connectionCheck = new SqliteConnection(_connectionString);
            connectionCheck.Open();
            try
            {
                var alterCommand = connectionCheck.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN IsLastFinished INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            try
            {
                var alterCommand = connectionCheck.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN Ordem INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            try
            {
                var alterCommand = connectionCheck.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN CoverUrl TEXT DEFAULT ''";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            try
            {
                var alterCommand = connectionCheck.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN ScreenshotUrl1 TEXT DEFAULT ''";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            try
            {
                var alterCommand = connectionCheck.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN ScreenshotUrl2 TEXT DEFAULT ''";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            connectionCheck.Close();


            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            // ADICIONA AS NOVAS COLUNAS AO SELECT
            command.CommandText = @"
            SELECT Id, Name, Year, FinishedInThisYear, Grade,
                   (SELECT MAX(g2.FinishedInThisYear) 
                    FROM Games g2 
                    WHERE g2.Name = Games.Name) AS EverCompleted,
                   IsLastFinished,
                   Ordem,
                   CoverUrl,
                   ScreenshotUrl1,
                   ScreenshotUrl2
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
                    // LENDO AS NOVAS COLUNAS
                    CoverUrl = reader.GetString(8),
                    ScreenshotUrl1 = reader.GetString(9),
                    ScreenshotUrl2 = reader.GetString(10)
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

            // --- ETAPA 1 e 2 (Garantir colunas) ---
            var createCommand = connection.CreateCommand();
            createCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS Games (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT,
                    Year INTEGER,
                    FinishedInThisYear INTEGER,
                    Grade TEXT,
                    IsLastFinished INTEGER DEFAULT 0,
                    Ordem INTEGER DEFAULT 0,
                    CoverUrl TEXT DEFAULT '',
                    ScreenshotUrl1 TEXT DEFAULT '',
                    ScreenshotUrl2 TEXT DEFAULT ''
                );";
            createCommand.ExecuteNonQuery();

            // Checagens 'ALTER TABLE' (incluindo as novas colunas)
            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN IsLastFinished INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN Ordem INTEGER DEFAULT 0";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            // --- NOVAS CHECAGENS DE COLUNA ---
            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN CoverUrl TEXT DEFAULT ''";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN ScreenshotUrl1 TEXT DEFAULT ''";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            try
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN ScreenshotUrl2 TEXT DEFAULT ''";
                alterCommand.ExecuteNonQuery();
            }
            catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }

            // --- FIM DAS NOVAS CHECAGENS ---


            // --- ETAPA 3: Iniciar a transação ---
            var transaction = connection.BeginTransaction();
            try
            {
                // LÓGICA DE RESET (IsLastFinished)
                if (model.IsLastFinished)
                {
                    var resetCommand = connection.CreateCommand();
                    resetCommand.Transaction = transaction;
                    resetCommand.CommandText = "UPDATE Games SET IsLastFinished = 0 WHERE IsLastFinished = 1";
                    resetCommand.ExecuteNonQuery();
                }

                // COMANDO DE INSERÇÃO
                // (Não precisamos adicionar as URLs aqui, elas começam vazias)
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
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished);

                command.ExecuteNonQuery();

                // --- ETAPA 4: DEFINIR A ORDEM PADRÃO ---
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

        //Ação do botão de editar
        [HttpPost]
        public IActionResult AtualizarObjetoJogo(Jogo model)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var transaction = connection.BeginTransaction();

            try
            {
                // LÓGICA DE RESET (IsLastFinished)
                if (model.IsLastFinished)
                {
                    var resetCommand = connection.CreateCommand();
                    resetCommand.Transaction = transaction;
                    resetCommand.CommandText = "UPDATE Games SET IsLastFinished = 0 WHERE IsLastFinished = 1";
                    resetCommand.ExecuteNonQuery();
                }

                // =======================================
                // MUDANÇA 1: UPDATE PRINCIPAL (Atualiza o registro específico)
                // =======================================
                var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
                    UPDATE Games
                    SET Name = $name, 
                        Year = $year, 
                        FinishedInThisYear = $finished, 
                        Grade = $grade,
                        IsLastFinished = $islastfinished,
                        Ordem = $ordem,
                        CoverUrl = $coverurl,
                        ScreenshotUrl1 = $ss1,
                        ScreenshotUrl2 = $ss2
                    WHERE Id = $id";

                command.Parameters.AddWithValue("$id", model.Id);
                command.Parameters.AddWithValue("$name", model.Name);
                command.Parameters.AddWithValue("$year", model.Year);
                command.Parameters.AddWithValue("$finished", model.FinishedInThisYear);
                command.Parameters.AddWithValue("$grade", model.Grade);
                command.Parameters.AddWithValue("$islastfinished", model.IsLastFinished);
                command.Parameters.AddWithValue("$ordem", model.Ordem);
                command.Parameters.AddWithValue("$coverurl", model.CoverUrl ?? "");
                command.Parameters.AddWithValue("$ss1", model.ScreenshotUrl1 ?? "");
                command.Parameters.AddWithValue("$ss2", model.ScreenshotUrl2 ?? "");
                command.ExecuteNonQuery();

                // =======================================
                // MUDANÇA 2: PROPAGAR IMAGENS (Atualiza todos os outros com o mesmo nome)
                // =======================================
                var propagateCommand = connection.CreateCommand();
                propagateCommand.Transaction = transaction;
                propagateCommand.CommandText = @"
                    UPDATE Games
                    SET CoverUrl = $coverurl,
                        ScreenshotUrl1 = $ss1,
                        ScreenshotUrl2 = $ss2
                    WHERE Name = $name 
                      AND Id != $id"; // Onde o nome é igual, mas o ID é diferente (para não re-trabalhar)

                // Adicionamos os parâmetros de novo para este comando
                propagateCommand.Parameters.AddWithValue("$coverurl", model.CoverUrl ?? "");
                propagateCommand.Parameters.AddWithValue("$ss1", model.ScreenshotUrl1 ?? "");
                propagateCommand.Parameters.AddWithValue("$ss2", model.ScreenshotUrl2 ?? "");
                propagateCommand.Parameters.AddWithValue("$name", model.Name);
                propagateCommand.Parameters.AddWithValue("$id", model.Id);

                propagateCommand.ExecuteNonQuery();


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


        //Filtragem universal E Ordenação (Atualizado para filtro 'Nunca Zerados')
        public IActionResult ListaJogos(string termoBusca, string sortOrder, int? filtroAno, bool? filtroNaoZerados)
        {
            var listaJogos = new List<Jogo>();

            // --- ETAPA 1: Buscar todos os anos únicos para os botões ---
            var distinctYears = new List<int>();
            using (var connectionYears = new SqliteConnection(_connectionString))
            {
                connectionYears.Open();
                var yearsCommand = connectionYears.CreateCommand();
                yearsCommand.CommandText = "SELECT DISTINCT Year FROM Games ORDER BY Year DESC";
                using (var yearsReader = yearsCommand.ExecuteReader())
                {
                    while (yearsReader.Read())
                    {
                        distinctYears.Add(yearsReader.GetInt32(0));
                    }
                }
            }
            ViewData["DistinctYears"] = distinctYears;


            // --- ETAPA 2: Garantir que a coluna Ordem exista ---
            using (var connectionCheck = new SqliteConnection(_connectionString))
            {
                connectionCheck.Open();
                try
                {
                    var alterCommand = connectionCheck.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN Ordem INTEGER DEFAULT 0";
                    alterCommand.ExecuteNonQuery();
                }
                catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            }


            // --- ETAPA 3: Montar a Query Principal ---
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            var whereClauses = new List<string>();

            // 1. Iniciar a query base com o CTE
            string sql = @"
                WITH AllGamesWithCompletion AS (
                    SELECT Id, Name, Year, FinishedInThisYear, Grade, Ordem,
                           MAX(CASE WHEN FinishedInThisYear = 1 OR FinishedInThisYear = 'true' THEN 1 ELSE 0 END) OVER (PARTITION BY Name) AS EverCompleted
                    FROM Games
                )
                SELECT Id, Name, Year, FinishedInThisYear, Grade, EverCompleted, Ordem
                FROM AllGamesWithCompletion";

            // 2. Adicionar o filtro de BUSCA (termoBusca)
            if (!string.IsNullOrEmpty(termoBusca))
            {
                var searchClauses = new List<string>
                {
                    "Name LIKE $termoBusca",
                    "CAST(Year AS TEXT) LIKE $termoBusca",
                    "Grade LIKE $termoBusca"
                };
                command.Parameters.AddWithValue("$termoBusca", $"%{termoBusca}%");

                string termoLimpo = termoBusca.Trim();
                if (termoLimpo.Equals("sim", StringComparison.OrdinalIgnoreCase))
                {
                    searchClauses.Add("FinishedInThisYear = 1");
                }
                else if (termoLimpo.Equals("não", StringComparison.OrdinalIgnoreCase) ||
                         termoLimpo.Equals("nao", StringComparison.OrdinalIgnoreCase))
                {
                    searchClauses.Add("FinishedInThisYear = 0");
                }

                whereClauses.Add($"({string.Join(" OR ", searchClauses)})");
            }

            // 3. Adicionar o filtro de ANO (filtroAno)
            if (filtroAno.HasValue)
            {
                whereClauses.Add("Year = $filtroAno");
                command.Parameters.AddWithValue("$filtroAno", filtroAno.Value);
            }

            // 4. ADICIONAR O FILTRO DE 'NUNCA ZERADOS' (filtroNaoZerados)
            if (filtroNaoZerados == true)
            {
                // A coluna 'EverCompleted' é 0 (false) ou 1 (true)
                whereClauses.Add("EverCompleted = 0");
            }

            // 5. Juntar todos os filtros com "AND"
            if (whereClauses.Count > 0)
            {
                sql += $" WHERE {string.Join(" AND ", whereClauses)}";
            }

            // 6. Adicionar a ordenação
            switch (sortOrder)
            {
                case "name":
                    sql += " ORDER BY Name";
                    break;
                case "year":
                default:
                    sql += " ORDER BY Year, Ordem";
                    break;
            }

            command.CommandText = sql;

            // 7. Passar os filtros atuais de volta para a View
            ViewData["CurrentFilter"] = termoBusca;
            ViewData["CurrentYearFilter"] = filtroAno;
            ViewData["CurrentSortOrder"] = sortOrder;
            ViewData["CurrentNaoZeradosFilter"] = filtroNaoZerados; // <-- NOVO

            // 8. Executar a query
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
                    Ordem = reader.GetInt32(6)
                });
            }

            return View(listaJogos);
        }

        public IActionResult Estatisticas()
        {
            // O ViewModel (EstatisticasViewModel.cs) inicializa as listas
            var viewModel = new EstatisticasViewModel
            {
                LastGameFinished = "Nenhum jogo zerado registrado"
            };

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // ---
            // 1. QUERY 1: ÚLTIMO JOGO ZERADO
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
            // 2. QUERY 2: JOGO(S) ZERADO(S) MAIS VEZES
            // ---
            var cmdMostFinished = connection.CreateCommand();
            cmdMostFinished.CommandText = @"
                WITH GameCounts AS (
                    SELECT Name, COUNT(*) AS Count
                    FROM Games
                    WHERE FinishedInThisYear = 1
                    GROUP BY Name
                )
                SELECT Name, Count
                FROM GameCounts
                WHERE Count = (SELECT MAX(Count) FROM GameCounts)
                ORDER BY Name;
            ";

            using (var reader = cmdMostFinished.ExecuteReader())
            {
                while (reader.Read())
                {
                    viewModel.MostFinishedGame.Add($"{reader.GetString(0)} ({reader.GetInt32(1)} vezes)");
                }
            }
            if (viewModel.MostFinishedGame.Count == 0)
            {
                viewModel.MostFinishedGame.Add("N/A");
            }

            // ---
            // 3. QUERY 3: JOGO(S) REGISTRADO(S) MAIS VEZES
            // ---
            var cmdMostPlayed = connection.CreateCommand();
            cmdMostPlayed.CommandText = @"
                WITH GameCounts AS (
                    SELECT Name, COUNT(*) AS Count
                    FROM Games
                    GROUP BY Name
                )
                SELECT Name, Count
                FROM GameCounts
                WHERE Count = (SELECT MAX(Count) FROM GameCounts)
                ORDER BY Name;
            ";

            using (var reader = cmdMostPlayed.ExecuteReader())
            {
                while (reader.Read())
                {
                    viewModel.MostPlayedGame.Add($"{reader.GetString(0)} ({reader.GetInt32(1)} registros)");
                }
            }
            if (viewModel.MostPlayedGame.Count == 0)
            {
                viewModel.MostPlayedGame.Add("N/A");
            }

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult SalvarOrdemLote(Dictionary<int, int> ordemValores)
        {
            // 'ordemValores' é um dicionário onde:
            // Key   = O Id do jogo (ex: 118)
            // Value = O novo valor de 'Ordem' que você digitou (ex: 124)

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            // Usamos uma Transação para garantir que ou TODOS os updates
            // funcionam, ou NENHUM funciona. É mais seguro.
            using var transaction = connection.BeginTransaction();

            try
            {
                // Loop por cada par (Id, Ordem) que recebemos da View
                foreach (var par in ordemValores)
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "UPDATE Games SET Ordem = $ordem WHERE Id = $id";
                    command.Parameters.AddWithValue("$ordem", par.Value); // O novo valor da Ordem
                    command.Parameters.AddWithValue("$id", par.Key);       // O Id do Jogo
                    command.ExecuteNonQuery();
                }

                // Se o loop terminou sem erros, salva tudo
                transaction.Commit();
            }
            catch (Exception)
            {
                // Se deu qualquer erro, desfaz tudo
                transaction.Rollback();
                throw; // Lança o erro para a tela de erro
            }

            TempData["Mensagem"] = "Ordem dos jogos atualizada com sucesso!";
            // Redireciona de volta para a ListaJogos (para vermos o resultado)
            return RedirectToAction("ListaJogos");
        }

        // ... (Pode ser depois do método 'AtualizarObjetoJogo') ...

        // =======================================
        // NOVA ACTION PARA A PÁGINA DE DETALHES
        // =======================================
        public IActionResult Detalhar(int id)
        {
            // Garante que as colunas existem (é bom ter a checagem aqui também)
            using (var connectionCheck = new SqliteConnection(_connectionString))
            {
                connectionCheck.Open();
                try
                {
                    var alterCommand = connectionCheck.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN CoverUrl TEXT DEFAULT ''";
                    alterCommand.ExecuteNonQuery();
                }
                catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
                try
                {
                    var alterCommand = connectionCheck.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN ScreenshotUrl1 TEXT DEFAULT ''";
                    alterCommand.ExecuteNonQuery();
                }
                catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
                try
                {
                    var alterCommand = connectionCheck.CreateCommand();
                    alterCommand.CommandText = "ALTER TABLE Games ADD COLUMN ScreenshotUrl2 TEXT DEFAULT ''";
                    alterCommand.ExecuteNonQuery();
                }
                catch (SqliteException ex) { if (!ex.Message.Contains("duplicate column name")) throw; }
            }


            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            // Mesma query da 'TelaEdicao'
            command.CommandText = @"
            SELECT Id, Name, Year, FinishedInThisYear, Grade,
                   (SELECT MAX(g2.FinishedInThisYear) 
                    FROM Games g2 
                    WHERE g2.Name = Games.Name) AS EverCompleted,
                   IsLastFinished,
                   Ordem,
                   CoverUrl,
                   ScreenshotUrl1,
                   ScreenshotUrl2
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
                    CoverUrl = reader.GetString(8),
                    ScreenshotUrl1 = reader.GetString(9),
                    ScreenshotUrl2 = reader.GetString(10)
                };
                // Envia o jogo para uma View chamada 'Detalhar.cshtml'
                return View(jogo);
            }

            return NotFound();
        }

    }
}
