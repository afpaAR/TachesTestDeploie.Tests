using System;
using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using TachesTestDeploie;
using Microsoft.Extensions.Configuration;

namespace TachesTestDeploie.Tests
{
    [TestFixture]
    public class TacheRepositoryTests
    {
        private string _masterCs = default!;
        private string _dbName = default!;
        private string _dbCs = default!;
        private TacheRepository repo;


        /// <summary>
        /// Préparation générale des tests. On instancie TacheRepository pour pouvoir effectuer les tests. Pour des tests successifs, on DELETE dans tache pour être sur d'avoir une table vierge
        /// </summary>
        [SetUp]
        public void Setup()
        {
            repo = new TacheRepository();

            using var cn = new SqlConnection(Environment.GetEnvironmentVariable("TACHES_DB"));
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM TACHES";
            cmd.ExecuteNonQuery();

        }

        /// <summary>
        /// On initialise l'ensemble des chaînes de connection
        /// </summary>
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // Charger appsettings.json du projet test
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();


            //On récupère la chaîne de connection concernant Master pour la création et la suppression de la BDD
            _masterCs = config.GetConnectionString("Master");

            //On construit la chaîne de connection pour la base simulée avec un nom unique (Guid)
            _dbName = "taches_test_" + new Guid();
            _dbCs = _masterCs.Replace("Initial Catalog=master", $"Initial Catalog={_dbName}");

            //Création de la base et de la table
            CreateDatabase();
            CreateSchema();

            // Diriger le repo vers la base de test
            Environment.SetEnvironmentVariable("TACHES_DB", _dbCs);
        }


        /// <summary>
        /// On garde l'ensemble clean en supprimant la base de test et en suppriment l'environnement de test
        /// </summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            DropDatabase();
            Environment.SetEnvironmentVariable("TACHES_DB", null);
        }

        /// <summary>
        /// Création de la BDD avec une connection master
        /// </summary>
        private void CreateDatabase()
        {
            using var cn = new SqlConnection(_masterCs);
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_dbName}]";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Création de la table de tâche avec une connection sur la BDD
        /// </summary>
        private void CreateSchema()
        {
            using var cn = new SqlConnection(_dbCs);
            cn.Open();

            var sql = @"
                IF OBJECT_ID('dbo.TACHES') IS NULL
                BEGIN
                    CREATE TABLE dbo.TACHES (
                        Id            INT           NOT NULL PRIMARY KEY,
                        Nom           NCHAR(100)    NOT NULL,
                        Description   VARCHAR(255)  NULL,
                        DateCreation  DATE          NOT NULL,
                        DateFermeture DATE          NULL
                    );
                END;
            ";
            using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// suppression de la BDD simulée
        /// </summary>
        private void DropDatabase()
        {
            using var cn = new SqlConnection(_masterCs);
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = $@"
                IF DB_ID('{_dbName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{_dbName}];
                END";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Test de l'ajout avec confirmation du résultat avec GetTaches pour avoir la liste
        /// </summary>
        [Test]
        public void AddTache_Should_Insert_Row_And_Appear_In_GetTaches()
        {
            var t = new Taches
            {
                Nom = "Acheter du pain",
                Description = "Boulangerie",
                DateCreation = DateTime.Now.Date
            };

            repo.AddTache(t);

            var all = repo.GetTaches();

            //La fonctionnalité a bien testé qu'il n'y a qu'une et une seule entrée et que chaque valeur correspond à ce qui a été inséré
            Assert.That(all, Has.Count.EqualTo(1));
            Assert.That(all[0].Nom.Trim(), Is.EqualTo("Acheter du pain"));
            Assert.That(all[0].DateFermeture, Is.EqualTo(DateTime.MinValue));
        }

        /// <summary>
        /// Test de la fermeture d'une tache à la date du jour
        /// </summary>
        [Test]
        public void MarkCompleted_Should_Set_DateFermeture_Today()
        {

            repo.AddTache(new Taches { Nom = "Appeler maman", Description = "☎", DateCreation = DateTime.Now.Date });
            var all = repo.GetTaches();
            var id = all[0].Id;

            repo.MarkCompleted(id);

            var after = repo.GetTaches();
            Assert.That(after[0].DateFermeture, Is.EqualTo(DateTime.Now.Date));
        }

        /// <summary>
        /// Test de la suppression d'une tache
        /// </summary>
        [Test]
        public void DeleteTache_Should_Remove_Row()
        {

            repo.AddTache(new Taches { Nom = "Lire un livre", Description = "Polar", DateCreation = DateTime.Now.Date });
            var all = repo.GetTaches();
            Assert.That(all, Has.Count.EqualTo(1));
            var id = all[0].Id;

            repo.DeleteTache(id);

            var after = repo.GetTaches();
            Assert.That(after, Is.Empty);
        }


        /// <summary>
        /// Test du dernier Id
        /// </summary>
        [Test]
        public void GetLastId_Should_Return_1_When_Table_Empty()
        {
            var nextId = repo.getLastId();
            Assert.That(nextId, Is.EqualTo(1));
        }

        /// <summary>
        /// Test que les ajouts successifs donnent les bons Id (c'est indirectement un test de GetLastId)
        /// </summary>
        [Test]
        public void AddTache_Should_AutoIncrement_Id()
        {
            repo.AddTache(new Taches { Nom = "T1", Description = "", DateCreation = DateTime.Now.Date });
            repo.AddTache(new Taches { Nom = "T2", Description = "", DateCreation = DateTime.Now.Date });

            var all = repo.GetTaches();
            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all[0].Id + 1, Is.EqualTo(all[1].Id));
        }
    }
}
