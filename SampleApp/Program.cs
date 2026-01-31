using Microsoft.Extensions.DependencyInjection;
using MyDbLib.Api;
using MyDbLib.Api.Exceptions;
using MyDbLib.Core.Extensions;
using MyDbLib.Core.Helpers;
using MyDbLib.Providers.SqlServer;
using MyDbLib.Providers.MySql;
using SampleApp.Models;
using System;
using System.Threading.Tasks;

namespace SampleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // 1. DI setup
                var services = new ServiceCollection();

                services.AddMyDbLibCore();

                services.AddMyDbLibSqlServer(
                    name: "SQLServer",
                    connectionString: "Server=localhost;Database=ProjectDB;User Id=sa;Password=admin;Encrypt=False;"
                );

                services.AddMyDbLibMySql(
                    name: "MySQL",
                    connectionString: "Server=localhost;Port=3307;Database=myprojectdb;Uid=root;Pwd=admin"
                );

                var provider = services.BuildServiceProvider();
                var factory = provider.GetRequiredService<IDbDriverFactory>();

                var sql = factory.Get("SQLServer");
                var mysql = factory.Get("MySQL");

                Console.WriteLine($"SQLServer Instances: {SqlServerDriver.InstanceCount}");
                Console.WriteLine($"MySQL Instances: {MySqlDriver.InstanceCount}");

                // ------------------------------------------------
                // ASYNC CRUD (No Tx)
                // ------------------------------------------------
                Console.WriteLine("\n--- ASYNC CRUD (No Tx) ---");

                try
                {
                    await DbCrudHelper.InsertAsync(sql, "Users",
                        new { Username = "Jay", Email = "jay@gmail.com", PasswordHash = "hash" });

                    int newId = await DbCrudHelper.InsertAndGetIdAsync(sql, "Users",
                        new { Username = "Ajay", Email = "ajay@gmail.com", PasswordHash = "hash" });

                    Console.WriteLine($"Inserted ID: {newId}");

                    int up = await DbCrudHelper.UpdateAsync(sql, "Users",
                        new { Email = "ajay@new.com" },
                        new { Id = newId });

                    Console.WriteLine($"Updated rows: {up}");

                    int del = await DbCrudHelper.DeleteAsync(sql, "Users", new { Id = newId });
                    Console.WriteLine($"Deleted rows: {del}");
                }
                catch (DbLibException ex)
                {
                    Console.WriteLine("CRUD failed: " + ex.Message);
                }

                // ------------------------------------------------
                // ASYNC TRANSACTION
                // ------------------------------------------------
                Console.WriteLine("\n--- ASYNC TRANSACTION ---");

                using (var tx = await sql.BeginTransactionAsync())
                {
                    try
                    {
                        await DbCrudHelper.InsertAsync(tx, "Users",
                            new { Username = "TX1", Email = "tx1@gmail.com", PasswordHash = "hash" });

                        await DbCrudHelper.InsertAsync(tx, "Users",
                            new { Username = "TX2", Email = "tx2@gmail.com", PasswordHash = "hash" });

                        await tx.CommitAsync();
                        Console.WriteLine("Transaction committed");
                    }
                    catch (DbLibException ex)
                    {
                        await tx.RollbackAsync();
                        Console.WriteLine("Transaction rolled back: " + ex.Message);
                    }
                }

                // ------------------------------------------------
                // RAW SQL with DbCommandResult
                // ------------------------------------------------
                Console.WriteLine("\n--- RAW SQL (DbCommandResult) ---");

                var raw = await sql.ExecuteAsync(
                    "UPDATE Users SET Email = @e WHERE Username = @u",
                    new { e = "raw@mail.com", u = "Jay" });

                if (raw.Success)
                    Console.WriteLine($"RAW updated rows: {raw.AffectedRecords}");
                else
                    Console.WriteLine($"RAW error {raw.ErrorCode}: {raw.ErrorMessage}");


                var rows = await sql.QueryAsync("SELECT Id, Username, Email FROM Users WHERE Email LIKE @mail",
                                                new { mail = "%gmail.com" });
                foreach (var row in rows)
                {
                    Console.WriteLine($"{row["Id"]} | {row["Username"]} | {row["Email"]}");
                }

                // ------------------------------------------------
                // SYNC CRUD (No Tx)
                // ------------------------------------------------
                Console.WriteLine("\n--- SYNC CRUD (No Tx) ---");

                try
                {
                    DbCrudHelper.Insert(sql, "Users",
                        new { Username = "Sync1", Email = "sync1@gmail.com", PasswordHash = "hash" });

                    int syncId = DbCrudHelper.InsertAndGetId(sql, "Users",
                        new { Username = "Sync2", Email = "sync2@gmail.com", PasswordHash = "hash" });

                    Console.WriteLine($"Inserted ID: {syncId}");

                    int sup = DbCrudHelper.Update(sql, "Users",
                        new { Email = "sync2@new.com" },
                        new { Id = syncId });

                    Console.WriteLine($"Updated rows: {sup}");

                    int sdel = DbCrudHelper.Delete(sql, "Users", new { Id = syncId });
                    Console.WriteLine($"Deleted rows: {sdel}");
                }
                catch (DbLibException ex)
                {
                    Console.WriteLine("Sync CRUD failed: " + ex.Message);
                }

                // ------------------------------------------------
                // SYNC TRANSACTION
                // ------------------------------------------------
                Console.WriteLine("\n--- SYNC TRANSACTION ---");

                using (var tx = sql.BeginTransaction())
                {
                    try
                    {
                        DbCrudHelper.Insert(tx, "Users",
                            new { Username = "STX1", Email = "stx1@gmail.com", PasswordHash = "hash" });

                        DbCrudHelper.Insert(tx, "Users",
                            new { Username = "STX2", Email = "stx2@gmail.com", PasswordHash = "hash" });

                        tx.Commit();
                        Console.WriteLine("Transaction committed");
                    }
                    catch (DbLibException ex)
                    {
                        tx.Rollback();
                        Console.WriteLine("Transaction rolled back: " + ex.Message);
                    }
                }

                // ------------------------------------------------
                // QUERY
                // ------------------------------------------------
                Console.WriteLine("\n--- QUERY ---");

                try
                {
                    var users = await sql.QueryAsync<User>("SELECT * FROM Users");
                    foreach (var u in users)
                        Console.WriteLine($"{u.Id} - {u.Username} - {u.Email}");
                }
                catch (DbLibException ex)
                {
                    Console.WriteLine("Query failed: " + ex.Message);
                }

                Console.WriteLine("\nPress ENTER to exit...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal error: " + ex);
            }
        }
    }
}
