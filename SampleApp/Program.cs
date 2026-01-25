using Microsoft.Extensions.DependencyInjection;
using MyDbLib.Api;
using MyDbLib.Api.Exceptions;
using MyDbLib.Core;
using MyDbLib.Core.Extensions;        // AddMyDbLibCore
using MyDbLib.Providers.MySql;        // AddMyDbLibMySql
using MyDbLib.Providers.SqlServer;    // AddMyDbLibSqlServer
using SampleApp.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SampleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // v.imp
            //object data = new { Username = "Sai1", Email = "sai12@gmail.com", PasswordHash = "sdsaddddadadad" };
            //var columns = GetPropertyNames(data, "Insert data cannot be empty.");

            //object data = new { Username = "Sai1", Email = "sai12@gmail.com", PasswordHash = "sdsaddddadadad" };
            //var props = data.GetType().GetProperties();
            //var columns = props.Select(p => p.Name).ToList();
            //foreach (var prop in props)
            //{
            //    Console.WriteLine(prop.Name + " - " + prop.GetValue(data));
            //}
            //return;
            //

            // 1️. Create DI container, this creates Empty box, Setup DI (composition root)
            var services = new ServiceCollection();

            // Core is registered
            // Only ONE IDbDriverFactory is registered, No ambiguity. DI resolves by type, not by name.
            services.AddMyDbLibCore();

            // SQL Server provider is registered
            // Adds a driver registration entry into DI
            services.AddMyDbLibSqlServer(
                name: "SQLServer",
                connectionString: "Server=localhost;Database=ProjectDB;User Id=sa;Password=admin;Encrypt=False;"
            );

            // Adds another entry into a shared dictionary:
            services.AddMyDbLibMySql(
                name: "MySQL",
                connectionString: "Server=localhost;Port=3307;Database=myprojectdb;Uid=root;Pwd=admin"
            );

            // Now the dictionary looks like: 
            // "SQLServer" → SqlServerDriver factory
            // "MySQL"     → MySqlDriver factory
            // Still: No driver created, No driver chosen, Just registrations

            // 3️. Build service provider
            // With this, DI container is frozen and dictionary is complete and Factory knows all possible drivers
            // still no objects created...
            var provider = services.BuildServiceProvider();

            // 4️. Resolve IDbDriverFactory (program to interface)
            // Instantiates DbDriverFactory, Injects the dictionary into it
            // IDbDriverFactory → DbDriverFactory (DbDriverFactory object is created, Stored as singleton)
            var factory = provider.GetRequiredService<IDbDriverFactory>();

            // ONLY when you call this below happens
            // Name → lookup in dictionary → invoke factory → create driver
            // Now DI must create SqlServerDriver.
            var driverSQLServer = factory.Get("SQLServer");
            var driverSQLServer1 = factory.Get("SQLServer");
            var driverSQLServer2 = factory.Get("SQLServer");
            var driverSQLServer3 = factory.Get("SQLServer");

            // below returning TRUE and 'InstanceCount' returning 1 shows the Singleton is working perfect
            // to test this we add code in constructor of the SQL Server driver class
            Console.WriteLine(ReferenceEquals(driverSQLServer, driverSQLServer1)); // True
            Console.WriteLine(ReferenceEquals(driverSQLServer2, driverSQLServer3)); // True
            Console.WriteLine($"No. of SQL Server Driver Instance: {SqlServerDriver.InstanceCount}");

            // Now DI must create MySQLDriver.
            var driverMySQL = factory.Get("MySQL");
            var driverMySQL1 = factory.Get("MySQL");
            var driverMySQL2 = factory.Get("MySQL");
            var driverMySQL3 = factory.Get("MySQL");

            // below returning TRUE and 'InstanceCount' returning 1 shows the Singleton is working perfect
            // to test this we add code in constructor of the MySQL driver class
            Console.WriteLine(ReferenceEquals(driverMySQL, driverMySQL1)); // True
            Console.WriteLine(ReferenceEquals(driverMySQL2, driverMySQL3)); // True
            Console.WriteLine($"No. of MySQL Driver Instance: {MySqlDriver.InstanceCount}");


            #region Call method to return SINGLE TYPED data
            //var singleResultTyped = await driverSQLServer.QuerySingleAsync<User>("Select * From Users WHERE Username = @Name", new { Name = "Sai" });

            //if (singleResultTyped != null)
            //    Console.WriteLine($"{singleResultTyped.Id} - {singleResultTyped.Username} - {singleResultTyped.Email}");
            //else
            //    Console.WriteLine("User not found");
            #endregion
            //return;

            #region Using Transaction
            /*
            using (var tx = await driverSQLServer.BeginTransactionAsync())
            {
                try
                {
                    // Insert user
                    await tx.InsertAsync(
                        "Users",
                        new { Username = "Sai123", Email = "sai123@gmail.com", PasswordHash = "sdsaddddadadad" }
                    );
                    // Insert user and get Id back
                    int id = await tx.InsertAndGetIdAsync("Users",
                        new { Username = "Sai123456", Email = "sai123456@gmail.com", PasswordHash = "sdsaddddadadad" }
                        );

                    Console.WriteLine($"Id generated:{id}");
                    await tx.CommitAsync();
                }
                catch (Exception ex)
                {
                    // Any failure → rollback
                    await tx.RollbackAsync();

                    Console.WriteLine("Transaction rolled back.");
                    Console.WriteLine(ex.Message);
                }
            }
            return;
            */
            #endregion

            // 5️. Test connection
            //bool ok = driver.TestConnectionAsync().GetAwaiter().GetResult();
            //Console.WriteLine(ok ? "Connection successful" : "Connection failed");

            // 6️. Execute SQL command
            //var result = await driverSQLServer.ExecuteAsync("UPDATE Users SET Username = @NewName, UpdatedAt = @updateAt WHERE Username = @OldName"
            //    , new { NewName = "Jayandra", OldName = "Jay", updateAt = DateTime.UtcNow });

            // 7️. Handle execution result
            //if (result.Success) Console.WriteLine($"Updated {result.AffectedRecords} rows");
            //else Console.WriteLine($"Error {result.ErrorCode}: {result.ErrorMessage}");

            #region Execute SQL command
            //var result1 = await driverSQLServer.ExecuteAsync("INSERT INTO Users([Username],[Email],[PasswordHash],[CreatedAt]) VALUES(@UserName,@Email,@Pwd,@CreatedAt)",
            //   new
            //   {
            //       UserName = "shravani battu",
            //       Email = "shravani.battu@gmail.com",
            //       Pwd = "hash",
            //       CreatedAt = DateTime.UtcNow
            //   });

            //if (result1.Success) Console.WriteLine($"Inserted {result1.AffectedRecords} rows");
            //else Console.WriteLine($"Error {result1.ErrorCode}: {result1.ErrorMessage}");
            #endregion

            #region Call method to return data as Dictionary of string and object (i.e. Column Name and Data)
            //var result2 = driver.QueryAsync("Select * From Users WHERE Username = @Name", new { Name = "Sai" }).GetAwaiter().GetResult();
            //var result2 = await driverSQLServer.QueryAsync("Select * From Users");

            //foreach (var row in result2)
            //{
            //    Console.WriteLine($"{row["Id"]} - {row["Username"]} - {row["Email"]}");
            //}
            #endregion

            #region Call method to return TYPED data
            //var resultTyped = await driverSQLServer.QueryAsync<User>("Select * From Users");

            //foreach (var row in resultTyped)
            //{
            //    Console.WriteLine($"{row.Id} - {row.Username} - {row.Email}");
            //}
            #endregion

            #region INSERT example
            //try
            //{
            //    int userId = await driverSQLServer.InsertAndGetIdAsync(
            //        table: "Users",
            //        data: new { Username = "Sai1", Email = "sai12@gmail.com", PasswordHash = "sdsaddddadadad" }
            //        );
            //    Console.WriteLine($"UserId returned: {userId}");
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("InsertAndGetIdAsync: " + ex.Message);
            //}
            #endregion

            #region UPDATE EXAMPLE...
            //try
            //{
            //    int updatedRows = await driverSQLServer.UpdateAsync(
            //        table: "Users",
            //        data: new { Username = "Jayandra", Email = "jayandra@gmail.com", UpdatedAt = DateTime.UtcNow },
            //        where: new { Id = 10 }
            //    );
            //    Console.WriteLine($"Updated rows: {updatedRows}");

            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("UpdateAsync: " + ex.Message);
            //}
            #endregion

            #region DELETE EXAMPLE...
            //try
            //{
            //    int deletedRows = await driverSQLServer.DeleteAsync(
            //        table: "Users",
            //        where: new { Id = 26 }
            //    );
            //    Console.WriteLine($"Deleted rows: {deletedRows}");
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("DeleteAsync: " + ex.Message);
            //}
            #endregion

            #region Testing for MySQL database
            Console.WriteLine("Commands executed against MySQL database");

            #region Call method to return TYPED data
            var resultMySQL = await driverMySQL.QueryAsync<Dept>("Select * From dept");
            Console.WriteLine("Type Department rows from MySQL");
            foreach (var row in resultMySQL)
            {
                Console.WriteLine($"{row.DeptId} - {row.DeptName} - {row.Location}");
            }
            #endregion

            #region Call method to return data as Dictionary of string and object (i.e. Column Name and Data)
            Console.WriteLine();
            Console.WriteLine();
            var resultMySQL2 = await driverMySQL.QueryAsync("Select * From dept WHERE DeptId > @deptid", new { deptid = 3 });
            //var resultMySQL2 = await driverSQLServer.QueryAsync("Select * From dept");

            foreach (var row in resultMySQL2)
            {
                Console.WriteLine($"{row["deptid"]} - {row["deptname"]} - {row["location"]}");
            }
            #endregion
            #endregion

            Console.WriteLine();
            Console.WriteLine("Press ENTER to exit...");
            Console.ReadLine();
        }
    }
}
