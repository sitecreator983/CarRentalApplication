using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;

namespace CarRentalApplication.Data;

public static class AvailabilityDatabase
{
    public static void Initialize(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Reservations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CarId INTEGER REFERENCES Cars(Id) ON DELETE SET NULL,
                CustomerId INTEGER NOT NULL REFERENCES Customers(Id),
                PickupDate TEXT NOT NULL,
                ReturnDate TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'Confirmed' CHECK (Status IN ('Confirmed', 'Cancelled')),
                TotalPrice REAL CHECK (TotalPrice >= 0),
                CHECK (ReturnDate >= PickupDate)
            );
            CREATE INDEX IF NOT EXISTS IX_Reservations_Car_Dates
                ON Reservations (CarId, PickupDate, ReturnDate);
            CREATE TABLE IF NOT EXISTS VehicleMaintenance (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CarId INTEGER NOT NULL REFERENCES Cars(Id) ON DELETE CASCADE,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                CHECK (EndDate >= StartDate)
            );
            CREATE INDEX IF NOT EXISTS IX_VehicleMaintenance_Car_Dates
                ON VehicleMaintenance (CarId, StartDate, EndDate);
            """;
        command.ExecuteNonQuery();

        command.CommandText = "PRAGMA table_info(Reservations);";
        var carIdIsRequired = false;
        var hasTotalPrice = false;
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var columnName = reader.GetString(1);
                if (columnName == "CarId") carIdIsRequired = reader.GetInt32(3) == 1;
                if (columnName == "TotalPrice") hasTotalPrice = true;
            }
        }
        if (carIdIsRequired)
        {
            var backupPath = databasePath + ".before-nullable-reservation-car.bak";
            if (!File.Exists(backupPath))
            {
                using var backup = OpenConnection(backupPath);
                connection.BackupDatabase(backup);
            }
            command.CommandText = "PRAGMA foreign_keys=OFF;";
            command.ExecuteNonQuery();
            using var transaction = connection.BeginTransaction();
            command.Transaction = transaction;
            var copiedTotalPrice = hasTotalPrice ? "TotalPrice" : "NULL";
            command.CommandText = $"""
                CREATE TABLE Reservations_New (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CarId INTEGER REFERENCES Cars(Id) ON DELETE SET NULL,
                    CustomerId INTEGER NOT NULL REFERENCES Customers(Id),
                    PickupDate TEXT NOT NULL,
                    ReturnDate TEXT NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'Confirmed' CHECK (Status IN ('Confirmed', 'Cancelled')),
                    TotalPrice REAL CHECK (TotalPrice >= 0),
                    CHECK (ReturnDate >= PickupDate)
                );
                INSERT INTO Reservations_New (Id, CarId, CustomerId, PickupDate, ReturnDate, Status, TotalPrice)
                    SELECT Id, CarId, CustomerId, PickupDate, ReturnDate, Status, {copiedTotalPrice} FROM Reservations;
                DROP TABLE Reservations;
                ALTER TABLE Reservations_New RENAME TO Reservations;
                CREATE INDEX IX_Reservations_Car_Dates ON Reservations (CarId, PickupDate, ReturnDate);
                """;
            command.ExecuteNonQuery();
            transaction.Commit();
            command.Transaction = null;
            command.CommandText = "PRAGMA foreign_keys=ON;";
            command.ExecuteNonQuery();
            hasTotalPrice = true;
        }
        if (!hasTotalPrice)
        {
            command.CommandText = "ALTER TABLE Reservations ADD COLUMN TotalPrice REAL CHECK (TotalPrice >= 0);";
            command.ExecuteNonQuery();
        }

        // Backfill existing reservations while the related vehicle still exists.
        command.CommandText = """
            UPDATE Reservations
            SET TotalPrice = (
                SELECT ROUND(COALESCE(f.DailyRate, c.DailyRate) *
                    (CAST(julianday(Reservations.ReturnDate) - julianday(Reservations.PickupDate) AS INTEGER) + 1), 2)
                FROM Cars c
                LEFT JOIN CarModelFeatures f ON f.Make=c.Make AND f.Model=c.Model
                WHERE c.Id=Reservations.CarId
            )
            WHERE TotalPrice IS NULL AND CarId IS NOT NULL;
            """;
        command.ExecuteNonQuery();
    }

    public static List<CarModel> GetModels(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Make, Model FROM Cars ORDER BY Make, Model;";
        using var reader = command.ExecuteReader();
        var models = new List<CarModel>();
        while (reader.Read())
            models.Add(new CarModel(reader.GetString(0), reader.GetString(1)));
        return models;
    }

    public static List<string> GetAvailablePlates(string databasePath, CarModel model, DateOnly pickup, DateOnly dropoff)
    {
        var cars = GetAvailableCars(databasePath, model, pickup, dropoff);
        var plates = new List<string>(cars.Count);
        foreach (var car in cars)
            plates.Add(car.LicensePlate);
        return plates;
    }

    public static List<AvailableCar> GetAvailableCars(string databasePath, CarModel model, DateOnly pickup, DateOnly dropoff)
    {
        if (dropoff < pickup)
            throw new ArgumentException("Return date must not precede pickup date.");

        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        // Both endpoints are included: a car returning that day is still occupied.
        command.CommandText = """
            SELECT c.LicensePlate, COALESCE(f.DailyRate, c.DailyRate) FROM Cars c
            LEFT JOIN CarModelFeatures f ON f.Make = c.Make AND f.Model = c.Model
            WHERE c.Make = $make AND c.Model = $model
              AND NOT EXISTS (
                SELECT 1 FROM Reservations r
                WHERE r.CarId = c.Id AND r.Status = 'Confirmed'
                  AND r.PickupDate <= $returnDate AND r.ReturnDate >= $pickupDate
              )
              AND NOT EXISTS (
                SELECT 1 FROM VehicleMaintenance m
                WHERE m.CarId = c.Id
                  AND m.StartDate <= $returnDate AND m.EndDate >= $pickupDate
              )
            ORDER BY c.LicensePlate;
            """;
        command.Parameters.AddWithValue("$make", model.Make);
        command.Parameters.AddWithValue("$model", model.Model);
        command.Parameters.AddWithValue("$pickupDate", pickup.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$returnDate", dropoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        var cars = new List<AvailableCar>();
        while (reader.Read())
            cars.Add(new AvailableCar(reader.GetString(0), reader.GetDecimal(1)));
        return cars;
    }

    public static long? CreateReservation(string databasePath, long customerId, string licensePlate, DateOnly pickup, DateOnly dropoff)
    {
        if (dropoff < pickup)
            throw new ArgumentException("Return date must not precede pickup date.");

        using var connection = OpenConnection(databasePath);
        // Take the write lock before checking availability to prevent competing bookings.
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Reservations (CarId, CustomerId, PickupDate, ReturnDate, Status, TotalPrice)
            SELECT c.Id, $customerId, $pickupDate, $returnDate, 'Confirmed',
                   ROUND(COALESCE(f.DailyRate, c.DailyRate) * $days, 2)
            FROM Cars c
            LEFT JOIN CarModelFeatures f ON f.Make=c.Make AND f.Model=c.Model
            WHERE c.LicensePlate = $plate
              AND NOT EXISTS (
                SELECT 1 FROM Reservations r
                WHERE r.CarId = c.Id AND r.Status = 'Confirmed'
                  AND r.PickupDate <= $returnDate AND r.ReturnDate >= $pickupDate
              )
              AND NOT EXISTS (
                SELECT 1 FROM VehicleMaintenance m
                WHERE m.CarId = c.Id
                  AND m.StartDate <= $returnDate AND m.EndDate >= $pickupDate
              );
            """;
        command.Parameters.AddWithValue("$customerId", customerId);
        command.Parameters.AddWithValue("$plate", licensePlate);
        command.Parameters.AddWithValue("$pickupDate", pickup.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$returnDate", dropoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$days", GetRentalDays(pickup, dropoff));
        if (command.ExecuteNonQuery() == 0)
            return null;
        command.CommandText = "SELECT last_insert_rowid();";
        var id = (long)command.ExecuteScalar()!;
        transaction.Commit();
        return id;
    }

    public static List<ReservationRecord> GetReservations(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.Id, r.CustomerId, cu.FirstName || ' ' || cu.LastName, cu.Email,
                   r.CarId, c.Make, c.Model, c.LicensePlate,
                   r.PickupDate, r.ReturnDate, r.Status, r.TotalPrice
            FROM Reservations r
            JOIN Customers cu ON cu.Id = r.CustomerId
            LEFT JOIN Cars c ON c.Id = r.CarId
            ORDER BY r.PickupDate DESC, r.Id DESC;
            """;
        using var reader = command.ExecuteReader();
        var reservations = new List<ReservationRecord>();
        while (reader.Read())
            reservations.Add(new ReservationRecord
            {
                Id = reader.GetInt64(0), CustomerId = reader.GetInt64(1),
                CustomerName = reader.GetString(2), CustomerEmail = reader.GetString(3),
                CarId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                Make = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Model = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                LicensePlate = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                PickupDate = DateOnly.ParseExact(reader.GetString(8), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReturnDate = DateOnly.ParseExact(reader.GetString(9), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Status = reader.GetString(10),
                TotalPrice = reader.IsDBNull(11) ? null : reader.GetDecimal(11)
            });
        return reservations;
    }

    public static void RefreshReservationTotals(string databasePath)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Reservations
            SET TotalPrice = (
                SELECT ROUND(COALESCE(f.DailyRate, c.DailyRate) *
                    (CAST(julianday(Reservations.ReturnDate) - julianday(Reservations.PickupDate) AS INTEGER) + 1), 2)
                FROM Cars c
                LEFT JOIN CarModelFeatures f ON f.Make=c.Make AND f.Model=c.Model
                WHERE c.Id=Reservations.CarId
            )
            WHERE CarId IS NOT NULL
              AND EXISTS (SELECT 1 FROM Cars WHERE Id=Reservations.CarId);
            """;
        command.ExecuteNonQuery();
    }

    public static List<Car> GetAvailableVehiclesForReservation(string databasePath, DateOnly pickup,
        DateOnly dropoff, long reservationId)
    {
        if (dropoff < pickup)
            return [];
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.Id, c.Make, c.Model, c.ModelYear, c.Category, c.Transmission,
                   c.EngineCc, c.Horsepower, c.LicensePlate, COALESCE(f.DailyRate, c.DailyRate)
            FROM Cars c
            LEFT JOIN CarModelFeatures f ON f.Make=c.Make AND f.Model=c.Model
            WHERE NOT EXISTS (
                SELECT 1 FROM Reservations r
                WHERE r.CarId=c.Id AND r.Id<>$reservationId AND r.Status='Confirmed'
                  AND r.PickupDate <= $returnDate AND r.ReturnDate >= $pickupDate
            )
              AND NOT EXISTS (
                SELECT 1 FROM VehicleMaintenance m
                WHERE m.CarId=c.Id
                  AND m.StartDate <= $returnDate AND m.EndDate >= $pickupDate
              )
            ORDER BY c.Make, c.Model, c.LicensePlate;
            """;
        command.Parameters.AddWithValue("$reservationId", reservationId);
        command.Parameters.AddWithValue("$pickupDate", pickup.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$returnDate", dropoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        var vehicles = new List<Car>();
        while (reader.Read())
            vehicles.Add(new Car
            {
                Id = reader.GetInt64(0), Make = reader.GetString(1), Model = reader.GetString(2),
                ModelYear = reader.GetInt32(3), Category = reader.GetString(4),
                Transmission = reader.GetString(5), EngineCc = reader.GetInt32(6),
                Horsepower = reader.GetInt32(7), LicensePlate = reader.GetString(8),
                DailyRate = reader.GetDecimal(9)
            });
        return vehicles;
    }

    public static bool UpdateReservation(string databasePath, long reservationId, long customerId, long carId,
        DateOnly pickup, DateOnly dropoff, string status)
    {
        if (dropoff < pickup)
            throw new ArgumentException("Return date must not precede pickup date.");
        if (status is not ("Confirmed" or "Cancelled"))
            throw new ArgumentException("Unknown reservation status.", nameof(status));

        using var connection = OpenConnection(databasePath);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Reservations
            SET CustomerId=$customerId, CarId=$carId, PickupDate=$pickupDate,
                ReturnDate=$returnDate, Status=$status,
                TotalPrice=(
                    SELECT ROUND(COALESCE(f.DailyRate, c.DailyRate) * $days, 2)
                    FROM Cars c
                    LEFT JOIN CarModelFeatures f ON f.Make=c.Make AND f.Model=c.Model
                    WHERE c.Id=$carId
                )
            WHERE Id=$id
              AND EXISTS (SELECT 1 FROM Customers WHERE Id=$customerId)
              AND EXISTS (SELECT 1 FROM Cars WHERE Id=$carId)
              AND ($status='Cancelled' OR (
                    NOT EXISTS (
                        SELECT 1 FROM Reservations other
                        WHERE other.CarId=$carId AND other.Id<>$id AND other.Status='Confirmed'
                          AND other.PickupDate <= $returnDate AND other.ReturnDate >= $pickupDate
                    )
                    AND NOT EXISTS (
                        SELECT 1 FROM VehicleMaintenance m
                        WHERE m.CarId=$carId
                          AND m.StartDate <= $returnDate AND m.EndDate >= $pickupDate
                    )
              ));
            """;
        command.Parameters.AddWithValue("$id", reservationId);
        command.Parameters.AddWithValue("$customerId", customerId);
        command.Parameters.AddWithValue("$carId", carId);
        command.Parameters.AddWithValue("$pickupDate", pickup.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$returnDate", dropoff.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$days", GetRentalDays(pickup, dropoff));
        if (command.ExecuteNonQuery() != 1)
            return false;
        transaction.Commit();
        return true;
    }

    public static bool DeleteReservation(string databasePath, long reservationId)
    {
        using var connection = OpenConnection(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Reservations WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", reservationId);
        return command.ExecuteNonQuery() == 1;
    }

    public static int GetRentalDays(DateOnly pickup, DateOnly dropoff)
    {
        if (dropoff < pickup)
            throw new ArgumentException("Return date must not precede pickup date.");
        return dropoff.DayNumber - pickup.DayNumber + 1;
    }

    public static decimal CalculateRentalTotal(decimal dailyRate, DateOnly pickup, DateOnly dropoff) =>
        dailyRate * GetRentalDays(pickup, dropoff);

    private static SqliteConnection OpenConnection(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString());
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
