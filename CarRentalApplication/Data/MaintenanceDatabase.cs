using System;
using System.Collections.Generic;
using System.Globalization;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;

namespace CarRentalApplication.Data;

public static class MaintenanceDatabase
{
    public static long? Schedule(string databasePath, long carId, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("Maintenance end date must not precede its start date.");

        using var connection = Open(databasePath);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO VehicleMaintenance (CarId, StartDate, EndDate)
            SELECT c.Id, $startDate, $endDate
            FROM Cars c
            WHERE c.Id=$carId
              AND NOT EXISTS (
                  SELECT 1 FROM VehicleMaintenance m
                  WHERE m.CarId=c.Id AND m.StartDate <= $endDate AND m.EndDate >= $startDate
              )
              AND NOT EXISTS (
                  SELECT 1 FROM Reservations r
                  WHERE r.CarId=c.Id AND r.Status='Confirmed'
                    AND r.PickupDate <= $endDate AND r.ReturnDate >= $startDate
              );
            """;
        command.Parameters.AddWithValue("$carId", carId);
        command.Parameters.AddWithValue("$startDate", startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$endDate", endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (command.ExecuteNonQuery() != 1) return null;
        command.CommandText = "SELECT last_insert_rowid();";
        var id = (long)command.ExecuteScalar()!;
        transaction.Commit();
        return id;
    }

    public static List<MaintenanceRecord> GetAll(string databasePath)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.Id, m.CarId, c.Make, c.Model, c.LicensePlate, m.StartDate, m.EndDate
            FROM VehicleMaintenance m
            JOIN Cars c ON c.Id=m.CarId
            ORDER BY m.StartDate DESC, m.Id DESC;
            """;
        using var reader = command.ExecuteReader();
        var records = new List<MaintenanceRecord>();
        while (reader.Read())
            records.Add(new MaintenanceRecord
            {
                Id = reader.GetInt64(0), CarId = reader.GetInt64(1), Make = reader.GetString(2),
                Model = reader.GetString(3), LicensePlate = reader.GetString(4),
                StartDate = DateOnly.ParseExact(reader.GetString(5), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDate = DateOnly.ParseExact(reader.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
        return records;
    }

    public static bool Delete(string databasePath, long maintenanceId)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM VehicleMaintenance WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", maintenanceId);
        return command.ExecuteNonQuery() == 1;
    }

    private static SqliteConnection Open(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, ForeignKeys = true
        }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }
}
