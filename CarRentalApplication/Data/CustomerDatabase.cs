using System.Collections.Generic;
using System.IO;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;

namespace CarRentalApplication.Data;

public static class CustomerDatabase
{
    public static void Initialize(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                Phone TEXT NOT NULL,
                Email TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA table_info(Customers);";
        var legacyDates = false;
        using (var reader = command.ExecuteReader())
            while (reader.Read()) legacyDates |= reader.GetString(1) == "PickupDate";
        if (!legacyDates) return;

        var backupPath = databasePath + ".before-customer-date-migration.bak";
        if (!File.Exists(backupPath))
        {
            using var backup = OpenConnection(backupPath);
            connection.BackupDatabase(backup);
        }

        // Preserve customer IDs referenced by existing reservations when upgrading.
        command.CommandText = "PRAGMA foreign_keys = OFF;";
        command.ExecuteNonQuery();
        using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE Customers_New (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstName TEXT NOT NULL, LastName TEXT NOT NULL,
                Phone TEXT NOT NULL, Email TEXT NOT NULL
            );
            INSERT INTO Customers_New (Id, FirstName, LastName, Phone, Email)
                SELECT Id, FirstName, LastName, Phone, Email FROM Customers;
            DROP TABLE Customers;
            ALTER TABLE Customers_New RENAME TO Customers;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public static long Save(string databasePath, Customer customer)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Customers (FirstName, LastName, Phone, Email)
            VALUES ($firstName, $lastName, $phone, $email);
            """;
        command.Parameters.AddWithValue("$firstName", customer.FirstName);
        command.Parameters.AddWithValue("$lastName", customer.LastName);
        command.Parameters.AddWithValue("$phone", customer.Phone);
        command.Parameters.AddWithValue("$email", customer.Email);
        command.ExecuteNonQuery();
        command.CommandText = "SELECT last_insert_rowid();";
        return (long)command.ExecuteScalar()!;
    }

    public static List<Customer> GetAll(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FirstName, LastName, Phone, Email FROM Customers ORDER BY FirstName, LastName, Id;";
        using var reader = command.ExecuteReader();
        var customers = new List<Customer>();
        while (reader.Read())
            customers.Add(new Customer { Id = reader.GetInt64(0), FirstName = reader.GetString(1),
                LastName = reader.GetString(2), Phone = reader.GetString(3), Email = reader.GetString(4) });
        return customers;
    }

    public static bool Update(string databasePath, Customer customer)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Customers SET FirstName = $first, LastName = $last, Phone = $phone, Email = $email
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", customer.Id);
        command.Parameters.AddWithValue("$first", customer.FirstName);
        command.Parameters.AddWithValue("$last", customer.LastName);
        command.Parameters.AddWithValue("$phone", customer.Phone);
        command.Parameters.AddWithValue("$email", customer.Email);
        return command.ExecuteNonQuery() == 1;
    }

    public static bool Delete(string databasePath, long customerId)
    {
        using var connection = OpenConnection(databasePath);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Remove confirmed and cancelled bookings before deleting their customer.
        command.CommandText = "DELETE FROM Reservations WHERE CustomerId = $id;";
        command.Parameters.AddWithValue("$id", customerId);
        command.ExecuteNonQuery();
        command.CommandText = "DELETE FROM Customers WHERE Id = $id;";
        var deleted = command.ExecuteNonQuery() == 1;
        transaction.Commit();
        return deleted;
    }

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
