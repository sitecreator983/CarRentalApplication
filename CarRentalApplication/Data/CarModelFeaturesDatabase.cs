using System;
using System.Collections.Generic;
using System.Globalization;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;

namespace CarRentalApplication.Data;

public static class CarModelFeaturesDatabase
{
    // Called within the fleet initialization transaction. Existing feature values are preserved.
    internal static void Initialize(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS CarModelFeatures (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Make TEXT NOT NULL,
                Model TEXT NOT NULL,
                ModelYear INTEGER,
                Category TEXT,
                Transmission TEXT,
                EngineCc INTEGER CHECK (EngineCc > 0),
                Horsepower INTEGER CHECK (Horsepower > 0),
                DailyRate REAL CHECK (DailyRate >= 0),
                CarPlay INTEGER CHECK (CarPlay IN (0, 1)),
                AndroidAuto INTEGER CHECK (AndroidAuto IN (0, 1)),
                Roof TEXT,
                Color TEXT,
                FuelType TEXT,
                Seats INTEGER CHECK (Seats > 0),
                Doors INTEGER CHECK (Doors > 0),
                AirConditioning INTEGER CHECK (AirConditioning IN (0, 1)),
                ParkingSensors INTEGER CHECK (ParkingSensors IN (0, 1)),
                RearCamera INTEGER CHECK (RearCamera IN (0, 1)),
                AdditionalFeatures TEXT,
                UNIQUE (Make, Model)
            );
            INSERT INTO CarModelFeatures (Make, Model, ModelYear, Category, Transmission, EngineCc, Horsepower)
            SELECT Make, Model,
                CASE WHEN COUNT(DISTINCT ModelYear) = 1 THEN MIN(ModelYear) END,
                CASE WHEN COUNT(DISTINCT Category) = 1 THEN MIN(Category) END,
                CASE WHEN COUNT(DISTINCT Transmission) = 1 THEN MIN(Transmission) END,
                CASE WHEN COUNT(DISTINCT EngineCc) = 1 THEN MIN(EngineCc) END,
                CASE WHEN COUNT(DISTINCT Horsepower) = 1 THEN MIN(Horsepower) END
            FROM Cars GROUP BY Make, Model
            ON CONFLICT (Make, Model) DO NOTHING;

            INSERT INTO CarModelFeatures
                (Make, Model, CarPlay, AndroidAuto, Roof, Color, FuelType, Seats, Doors,
                 AirConditioning, ParkingSensors, RearCamera, AdditionalFeatures)
            VALUES
                ('Skoda', 'Kamiq', 1, 1, 'Panoramic glass roof', 'Moon White', 'Petrol', 5, 5, 1, 1, 1,
                 'Bluetooth, cruise control, keyless start, lane assist'),
                ('Kia', 'Ceed', 1, 1, 'Standard roof', 'Lunar Silver', 'Petrol', 5, 5, 1, 1, 1,
                 'Bluetooth, cruise control, lane keeping assist'),
                ('Toyota', 'Corolla', 1, 1, 'Standard roof', 'Pearl White', 'Hybrid petrol', 5, 4, 1, 1, 1,
                 'Bluetooth, adaptive cruise control, lane assist'),
                ('Peugeot', '208', 1, 1, 'Standard roof', 'Faro Yellow', 'Petrol', 5, 5, 1, 1, 0,
                 'Bluetooth, cruise control, speed limiter'),
                ('Fiat', 'Tipo', 1, 1, 'Standard roof', 'Cinema Black', 'Petrol', 5, 4, 1, 1, 0,
                 'Bluetooth, cruise control, speed limiter')
            ON CONFLICT (Make, Model) DO UPDATE SET
                CarPlay = COALESCE(CarModelFeatures.CarPlay, excluded.CarPlay),
                AndroidAuto = COALESCE(CarModelFeatures.AndroidAuto, excluded.AndroidAuto),
                Roof = COALESCE(CarModelFeatures.Roof, excluded.Roof),
                Color = COALESCE(CarModelFeatures.Color, excluded.Color),
                FuelType = COALESCE(CarModelFeatures.FuelType, excluded.FuelType),
                Seats = COALESCE(CarModelFeatures.Seats, excluded.Seats),
                Doors = COALESCE(CarModelFeatures.Doors, excluded.Doors),
                AirConditioning = COALESCE(CarModelFeatures.AirConditioning, excluded.AirConditioning),
                ParkingSensors = COALESCE(CarModelFeatures.ParkingSensors, excluded.ParkingSensors),
                RearCamera = COALESCE(CarModelFeatures.RearCamera, excluded.RearCamera),
                AdditionalFeatures = COALESCE(CarModelFeatures.AdditionalFeatures, excluded.AdditionalFeatures);
            """;
        command.ExecuteNonQuery();

        command.CommandText = "PRAGMA table_info(CarModelFeatures);";
        var hasDailyRate = false;
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                hasDailyRate |= reader.GetString(1) == "DailyRate";
        }
        if (!hasDailyRate)
        {
            command.CommandText = "ALTER TABLE CarModelFeatures ADD COLUMN DailyRate REAL CHECK (DailyRate >= 0);";
            command.ExecuteNonQuery();
        }

        // Prices are model-level values. Preserve a price if it has already been edited in the database.
        command.CommandText = """
            UPDATE CarModelFeatures
            SET DailyRate = CASE
                WHEN Make = 'Skoda' AND Model = 'Kamiq' THEN 48.00
                WHEN Make = 'Kia' AND Model = 'Ceed' THEN 42.00
                WHEN Make = 'Toyota' AND Model = 'Corolla' THEN 52.00
                WHEN Make = 'Peugeot' AND Model = '208' THEN 38.00
                WHEN Make = 'Fiat' AND Model = 'Tipo' THEN 34.00
            END
            WHERE DailyRate IS NULL AND (Make, Model) IN (
                ('Skoda', 'Kamiq'), ('Kia', 'Ceed'), ('Toyota', 'Corolla'),
                ('Peugeot', '208'), ('Fiat', 'Tipo')
            );
            """;
        command.ExecuteNonQuery();
    }

    public static List<string> GetBrands(string databasePath)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Make FROM CarModelFeatures ORDER BY Make;";
        using var reader = command.ExecuteReader();
        var brands = new List<string>();
        while (reader.Read()) brands.Add(reader.GetString(0));
        return brands;
    }

    public static List<CarCatalogItem> GetCatalog(string databasePath)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Make, Model, DailyRate FROM CarModelFeatures ORDER BY Make, Model;";
        using var reader = command.ExecuteReader();
        var items = new List<CarCatalogItem>();
        while (reader.Read())
            items.Add(new CarCatalogItem(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetDecimal(2)));
        return items;
    }

    public static bool UpdateDailyRate(string databasePath, CarModel model, decimal dailyRate)
    {
        if (dailyRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(dailyRate), "Daily rate must be greater than zero.");

        using var connection = Open(databasePath);
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE CarModelFeatures SET DailyRate = $rate
            WHERE Make = $make AND Model = $model;
            """;
        command.Parameters.AddWithValue("$rate", dailyRate);
        command.Parameters.AddWithValue("$make", model.Make);
        command.Parameters.AddWithValue("$model", model.Model);
        if (command.ExecuteNonQuery() != 1)
            return false;

        command.CommandText = """
            UPDATE Cars SET DailyRate = $rate
            WHERE Make = $make AND Model = $model;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }

    public static List<CarModel> GetModels(string databasePath, string make)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Make, Model FROM CarModelFeatures WHERE Make = $make ORDER BY Model;";
        command.Parameters.AddWithValue("$make", make);
        using var reader = command.ExecuteReader();
        var models = new List<CarModel>();
        while (reader.Read()) models.Add(new CarModel(reader.GetString(0), reader.GetString(1)));
        return models;
    }

    public static List<string> GetFeatures(string databasePath, CarModel model)
    {
        using var connection = Open(databasePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ModelYear, Category, Transmission, EngineCc, Horsepower,
                CarPlay, AndroidAuto, Roof, Color, FuelType, Seats, Doors,
                AirConditioning, ParkingSensors, RearCamera, AdditionalFeatures, DailyRate
            FROM CarModelFeatures WHERE Make = $make AND Model = $model;
            """;
        command.Parameters.AddWithValue("$make", model.Make);
        command.Parameters.AddWithValue("$model", model.Model);
        using var reader = command.ExecuteReader();
        var features = new List<string>();
        if (!reader.Read()) return features;
        string[] labels = ["Model year", "Category", "Transmission", "Engine capacity", "Horsepower",
            "Apple CarPlay", "Android Auto", "Roof", "Color", "Fuel type", "Seats", "Doors",
            "Air conditioning", "Parking sensors", "Rear camera", "Additional features", "Rental price per day"];
        for (var i = 0; i < labels.Length; i++)
        {
            var value = reader.IsDBNull(i) ? "Not registered" : Convert.ToString(reader.GetValue(i))!;
            if (!reader.IsDBNull(i))
            {
                if (i is 5 or 6 or 12 or 13 or 14) value = reader.GetInt32(i) == 1 ? "Yes" : "No";
                if (i == 3) value += " cc";
                if (i == 4) value += " hp";
                if (i == 16) value = "€" + reader.GetDecimal(i).ToString("0.00", CultureInfo.InvariantCulture);
            }
            features.Add($"{labels[i]}: {value}");
        }
        return features;
    }

    private static SqliteConnection Open(string databasePath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite
        }.ToString());
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }
}
