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

        //Lista os jogos com possibilidade de filtro
        //Filtragem universal (Nome, Ano, Nota)
        public IActionResult ListaJogos(string termoBusca)
        {
            var listaJogos = new List<Jogo>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();

            // Verifica se o usuário digitou algo para buscar
            if (!string.IsNullOrEmpty(termoBusca))
            {
                command.CommandText = @"
                    SELECT Id, Name, Year, FinishedInThisYear, Grade 
                    FROM Games 
                    WHERE Name LIKE $termoBusca 
                       OR CAST(Year AS TEXT) LIKE $termoBusca
                       OR Grade LIKE $termoBusca";

                // Adiciona os '%' para a consulta LIKE (procurar "contém")
                command.Parameters.AddWithValue("$termoBusca", $"%{termoBusca}%");
            }
            else
            {
                // Se não houver termo de busca, seleciona todos os jogos
                command.CommandText = "SELECT Id, Name, Year, FinishedInThisYear, Grade FROM Games";
            }

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
