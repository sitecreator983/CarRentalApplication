using System;
using System.Collections.Generic;
using System.Globalization;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;

namespace CarRentalApplication.Data;

public static class CarDatabase
{
    public static void Initialize(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Cars';";
        var isNewDatabase = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Cars (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Make TEXT NOT NULL,
                Model TEXT NOT NULL,
                ModelYear INTEGER NOT NULL,
                Category TEXT NOT NULL CHECK (Category IN ('Hatchback', 'SUV', 'Sedan')),
                Transmission TEXT NOT NULL CHECK (Transmission IN ('Manual', 'Automatic')),
                EngineCc INTEGER NOT NULL,
                Horsepower INTEGER NOT NULL,
                LicensePlate TEXT NOT NULL UNIQUE,
                DailyRate REAL NOT NULL CHECK (DailyRate >= 0)
            );
            """;
        command.ExecuteNonQuery();

        command.CommandText = "PRAGMA table_info(Cars);";
        var hasRentalCode = false;
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                hasRentalCode |= reader.GetString(1) == "RentalCode";
        }
        if (hasRentalCode)
        {
            // Renaming retains the existing IDs, data and UNIQUE constraint.
            command.CommandText = "ALTER TABLE Cars RENAME COLUMN RentalCode TO LicensePlate;";
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE Cars SET LicensePlate = printf('IKA-%04d', 1000 + Id);";
            command.ExecuteNonQuery();
        }

        // Demo vehicles are inserted only when the Cars table is first created.
        // Later startups must never manufacture extra vehicles for user-created models.
        if (isNewDatabase)
        {
            command.CommandText = """
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Skoda', 'Kamiq', 2024, 'SUV', 'Automatic', 1498, 150, 'IKA-1001', 48.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Skoda', 'Kamiq', 2024, 'SUV', 'Automatic', 1498, 150, 'IKA-1002', 48.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Skoda', 'Kamiq', 2024, 'SUV', 'Automatic', 1498, 150, 'IKA-1003', 48.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Skoda', 'Kamiq', 2024, 'SUV', 'Automatic', 1498, 150, 'IKA-1004', 48.00);

                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Kia', 'Ceed', 2023, 'Hatchback', 'Manual', 1482, 160, 'IKA-1005', 42.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Kia', 'Ceed', 2023, 'Hatchback', 'Manual', 1482, 160, 'IKA-1006', 42.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Kia', 'Ceed', 2023, 'Hatchback', 'Manual', 1482, 160, 'IKA-1007', 42.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Kia', 'Ceed', 2023, 'Hatchback', 'Manual', 1482, 160, 'IKA-1008', 42.00);

                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Toyota', 'Corolla', 2024, 'Sedan', 'Automatic', 1798, 140, 'IKA-1009', 52.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Toyota', 'Corolla', 2024, 'Sedan', 'Automatic', 1798, 140, 'IKA-1010', 52.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Toyota', 'Corolla', 2024, 'Sedan', 'Automatic', 1798, 140, 'IKA-1011', 52.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Toyota', 'Corolla', 2024, 'Sedan', 'Automatic', 1798, 140, 'IKA-1012', 52.00);

                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Peugeot', '208', 2023, 'Hatchback', 'Manual', 1199, 100, 'IKA-1013', 38.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Peugeot', '208', 2023, 'Hatchback', 'Manual', 1199, 100, 'IKA-1014', 38.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Peugeot', '208', 2023, 'Hatchback', 'Manual', 1199, 100, 'IKA-1015', 38.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Peugeot', '208', 2023, 'Hatchback', 'Manual', 1199, 100, 'IKA-1016', 38.00);

                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Fiat', 'Tipo', 2022, 'Sedan', 'Manual', 1368, 95, 'IKA-1017', 34.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Fiat', 'Tipo', 2022, 'Sedan', 'Manual', 1368, 95, 'IKA-1018', 34.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Fiat', 'Tipo', 2022, 'Sedan', 'Manual', 1368, 95, 'IKA-1019', 34.00);
                INSERT INTO Cars (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
                VALUES ('Fiat', 'Tipo', 2022, 'Sedan', 'Manual', 1368, 95, 'IKA-1020', 34.00);
                """;
            command.ExecuteNonQuery();
        }
        CarModelFeaturesDatabase.Initialize(connection, transaction);
        transaction.Commit();
    }

    public static long SaveVehicle(string databasePath, CarRegistration vehicle)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        // Reuse the spelling of an existing model so one model cannot be duplicated by letter casing.
        command.CommandText = """
            SELECT Make, Model FROM CarModelFeatures
            WHERE Make = $make COLLATE NOCASE AND Model = $model COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$make", vehicle.Make);
        command.Parameters.AddWithValue("$model", vehicle.Model);
        var make = vehicle.Make;
        var model = vehicle.Model;
        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                make = reader.GetString(0);
                model = reader.GetString(1);
            }
        }

        command.Parameters.Clear();
        command.CommandText = """
            INSERT INTO Cars
                (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, LicensePlate, DailyRate)
            VALUES
                ($make, $model, $year, $category, $transmission, $engineCc, $horsepower, $plate, $rate);
            """;
        command.Parameters.AddWithValue("$make", make);
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$year", vehicle.ModelYear);
        command.Parameters.AddWithValue("$category", vehicle.Category);
        command.Parameters.AddWithValue("$transmission", vehicle.Transmission);
        command.Parameters.AddWithValue("$engineCc", vehicle.EngineCc);
        command.Parameters.AddWithValue("$horsepower", vehicle.Horsepower);
        command.Parameters.AddWithValue("$plate", vehicle.LicensePlate.ToUpperInvariant());
        command.Parameters.AddWithValue("$rate", vehicle.DailyRate);
        command.ExecuteNonQuery();
        command.CommandText = "SELECT last_insert_rowid();";
        var carId = (long)command.ExecuteScalar()!;

        command.Parameters.Clear();
        command.CommandText = """
            INSERT INTO CarModelFeatures
                (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, DailyRate,
                 CarPlay, AndroidAuto, Roof, Color, FuelType, Seats, Doors,
                 AirConditioning, ParkingSensors, RearCamera, AdditionalFeatures)
            VALUES
                ($make, $model, $year, $category, $transmission, $engineCc, $horsepower, $rate,
                 $carPlay, $androidAuto, $roof, $color, $fuel, $seats, $doors,
                 $airConditioning, $parkingSensors, $rearCamera, $additional)
            ON CONFLICT (Make, Model) DO UPDATE SET
                ModelYear = excluded.ModelYear, Category = excluded.Category,
                Transmission = excluded.Transmission, EngineCc = excluded.EngineCc,
                Horsepower = excluded.Horsepower, DailyRate = excluded.DailyRate,
                CarPlay = excluded.CarPlay, AndroidAuto = excluded.AndroidAuto,
                Roof = excluded.Roof, Color = excluded.Color, FuelType = excluded.FuelType,
                Seats = excluded.Seats, Doors = excluded.Doors,
                AirConditioning = excluded.AirConditioning,
                ParkingSensors = excluded.ParkingSensors, RearCamera = excluded.RearCamera,
                AdditionalFeatures = excluded.AdditionalFeatures;
            """;
        command.Parameters.AddWithValue("$make", make);
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$year", vehicle.ModelYear);
        command.Parameters.AddWithValue("$category", vehicle.Category);
        command.Parameters.AddWithValue("$transmission", vehicle.Transmission);
        command.Parameters.AddWithValue("$engineCc", vehicle.EngineCc);
        command.Parameters.AddWithValue("$horsepower", vehicle.Horsepower);
        command.Parameters.AddWithValue("$rate", vehicle.DailyRate);
        command.Parameters.AddWithValue("$carPlay", vehicle.CarPlay);
        command.Parameters.AddWithValue("$androidAuto", vehicle.AndroidAuto);
        command.Parameters.AddWithValue("$roof", vehicle.Roof);
        command.Parameters.AddWithValue("$color", vehicle.Color);
        command.Parameters.AddWithValue("$fuel", vehicle.FuelType);
        command.Parameters.AddWithValue("$seats", vehicle.Seats);
        command.Parameters.AddWithValue("$doors", vehicle.Doors);
        command.Parameters.AddWithValue("$airConditioning", vehicle.AirConditioning);
        command.Parameters.AddWithValue("$parkingSensors", vehicle.ParkingSensors);
        command.Parameters.AddWithValue("$rearCamera", vehicle.RearCamera);
        command.Parameters.AddWithValue("$additional", vehicle.AdditionalFeatures);
        command.ExecuteNonQuery();

        transaction.Commit();
        return carId;
    }

    public static List<Car> GetAll(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Make, Model, ModelYear, Category, Transmission,
                   EngineCc, Horsepower, LicensePlate, DailyRate
            FROM Cars ORDER BY Make, Model, LicensePlate;
            """;
        using var reader = command.ExecuteReader();
        var cars = new List<Car>();
        while (reader.Read())
            cars.Add(new Car
            {
                Id = reader.GetInt64(0), Make = reader.GetString(1), Model = reader.GetString(2),
                ModelYear = reader.GetInt32(3), Category = reader.GetString(4),
                Transmission = reader.GetString(5), EngineCc = reader.GetInt32(6),
                Horsepower = reader.GetInt32(7), LicensePlate = reader.GetString(8),
                DailyRate = reader.GetDecimal(9)
            });
        return cars;
    }

    public static bool UpdateVehicle(string databasePath, Car vehicle)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, ForeignKeys = true
        }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Make, Model FROM Cars WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", vehicle.Id);
        string oldMake;
        string oldModel;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) return false;
            oldMake = reader.GetString(0);
            oldModel = reader.GetString(1);
        }

        command.Parameters.Clear();
        command.CommandText = """
            SELECT Make, Model FROM CarModelFeatures
            WHERE Make = $make COLLATE NOCASE AND Model = $model COLLATE NOCASE
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$make", vehicle.Make);
        command.Parameters.AddWithValue("$model", vehicle.Model);
        var make = vehicle.Make;
        var model = vehicle.Model;
        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                make = reader.GetString(0);
                model = reader.GetString(1);
            }
        }

        command.Parameters.Clear();
        command.CommandText = """
            UPDATE Cars SET Make=$make, Model=$model, ModelYear=$year, Category=$category,
                Transmission=$transmission, EngineCc=$engineCc, Horsepower=$horsepower,
                LicensePlate=$plate, DailyRate=$rate
            WHERE Id=$id;
            """;
        command.Parameters.AddWithValue("$id", vehicle.Id);
        command.Parameters.AddWithValue("$make", make);
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$year", vehicle.ModelYear);
        command.Parameters.AddWithValue("$category", vehicle.Category);
        command.Parameters.AddWithValue("$transmission", vehicle.Transmission);
        command.Parameters.AddWithValue("$engineCc", vehicle.EngineCc);
        command.Parameters.AddWithValue("$horsepower", vehicle.Horsepower);
        command.Parameters.AddWithValue("$plate", vehicle.LicensePlate.ToUpperInvariant());
        command.Parameters.AddWithValue("$rate", vehicle.DailyRate);
        command.ExecuteNonQuery();

        command.CommandText = """
            INSERT INTO CarModelFeatures
                (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower, DailyRate)
            VALUES ($make, $model, $year, $category, $transmission, $engineCc, $horsepower, $rate)
            ON CONFLICT (Make, Model) DO UPDATE SET
                ModelYear=excluded.ModelYear, Category=excluded.Category,
                Transmission=excluded.Transmission, EngineCc=excluded.EngineCc,
                Horsepower=excluded.Horsepower, DailyRate=excluded.DailyRate;
            UPDATE Cars SET DailyRate=$rate WHERE Make=$make AND Model=$model;
            """;
        command.ExecuteNonQuery();

        if (!string.Equals(oldMake, make, StringComparison.Ordinal) ||
            !string.Equals(oldModel, model, StringComparison.Ordinal))
        {
            command.Parameters.Clear();
            command.CommandText = """
                DELETE FROM CarModelFeatures
                WHERE Make=$oldMake AND Model=$oldModel
                  AND NOT EXISTS (SELECT 1 FROM Cars WHERE Make=$oldMake AND Model=$oldModel);
                """;
            command.Parameters.AddWithValue("$oldMake", oldMake);
            command.Parameters.AddWithValue("$oldModel", oldModel);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        return true;
    }

    public static bool DeleteVehicle(string databasePath, long carId)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, ForeignKeys = true
        }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Make, Model FROM Cars WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", carId);
        string make;
        string model;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) return false;
            make = reader.GetString(0);
            model = reader.GetString(1);
        }

        command.CommandText = "UPDATE Reservations SET CarId=NULL WHERE CarId=$id; DELETE FROM Cars WHERE Id=$id;";
        command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText = """
            DELETE FROM CarModelFeatures
            WHERE Make=$make AND Model=$model
              AND NOT EXISTS (SELECT 1 FROM Cars WHERE Make=$make AND Model=$model);
            """;
        command.Parameters.AddWithValue("$make", make);
        command.Parameters.AddWithValue("$model", model);
        command.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }
}
