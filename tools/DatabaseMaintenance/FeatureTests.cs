using System;
using System.IO;
using System.Linq;
using CarRentalApplication.Data;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;

internal static class FeatureTests
{
    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "car-features-" + Guid.NewGuid() + ".db");
        try
        {
            CarDatabase.Initialize(path);
            var seededCars = CarDatabase.GetAll(path);
            Assert(seededCars.Count == 20 && seededCars.Select(car => car.LicensePlate).Distinct().Count() == 20,
                "Twenty explicit seed vehicles have unique license plates");
            Assert(seededCars.Any(car => car.LicensePlate == "IKA-1001") &&
                seededCars.Any(car => car.LicensePlate == "IKA-1020"), "Explicit seed plate range loaded");
            var model = new CarModel("Skoda", "Kamiq");
            Assert(CarModelFeaturesDatabase.GetBrands(path).Count == 5, "Five brands");
            Assert(CarModelFeaturesDatabase.GetModels(path, "Skoda").SequenceEqual(new[] { model }), "Brand filtering");
            Assert(CarModelFeaturesDatabase.GetModels(path, "Skoda' OR 1=1 --").Count == 0, "Parameterized brand");
            var features = CarModelFeaturesDatabase.GetFeatures(path, model);
            Assert(features.Count == 17 && features.Contains("Engine capacity: 1498 cc"), "Existing specifications migrated");
            Assert(features.Contains("Apple CarPlay: Yes") && features.Contains("Roof: Panoramic glass roof"), "Seed equipment loaded");
            Assert(features.Contains("Rental price per day: €48.00"), "Model rental price loaded");
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE CarModelFeatures SET CarPlay=1, AndroidAuto=0, Roof='Retractable roof', Color='Blue', Seats=5
                WHERE Make='Skoda' AND Model='Kamiq';
                INSERT INTO CarModelFeatures (Make, Model, Color) VALUES ('Other brand', 'Kamiq', 'Red');
                """;
            command.ExecuteNonQuery();
            CarDatabase.Initialize(path);
            features = CarModelFeaturesDatabase.GetFeatures(path, model);
            Assert(features.Contains("Apple CarPlay: Yes") && features.Contains("Android Auto: No"), "Boolean feature values");
            Assert(features.Contains("Roof: Retractable roof") && features.Contains("Color: Blue"), "Values survive reinitialization");
            Assert(CarModelFeaturesDatabase.GetFeatures(path, new CarModel("Other brand", "Kamiq")).Contains("Color: Red"), "Same model name under another brand");
            var registration = new CarRegistration
            {
                Make = "Mazda", Model = "3", ModelYear = 2026, Category = "Hatchback",
                Transmission = "Automatic", EngineCc = 1998, Horsepower = 186,
                LicensePlate = "NEX-2026", DailyRate = 59.50m,
                CarPlay = true, AndroidAuto = true, Roof = "Standard roof", Color = "Soul Red",
                FuelType = "Petrol", Seats = 5, Doors = 5, AirConditioning = true,
                ParkingSensors = true, RearCamera = true, AdditionalFeatures = "Bluetooth, lane assist"
            };
            var carId = CarDatabase.SaveVehicle(path, registration);
            Assert(carId > 0, "New vehicle returns its database ID");
            var registeredFeatures = CarModelFeaturesDatabase.GetFeatures(path, new CarModel("Mazda", "3"));
            Assert(registeredFeatures.Contains("Color: Soul Red") &&
                registeredFeatures.Contains("Rental price per day: €59.50"), "New model features saved");
            command.CommandText = "SELECT COUNT(*) FROM Cars WHERE LicensePlate = 'NEX-2026';";
            Assert(Convert.ToInt32(command.ExecuteScalar()) == 1, "New car saved in fleet");
            CarDatabase.Initialize(path);
            command.CommandText = "SELECT COUNT(*) FROM Cars WHERE Make = 'Mazda' AND Model = '3';";
            Assert(Convert.ToInt32(command.ExecuteScalar()) == 1, "Restart does not create extra cars for a user model");
            Assert(CarModelFeaturesDatabase.UpdateDailyRate(path, new CarModel("Mazda", "3"), 65.25m), "Model price updated");
            registeredFeatures = CarModelFeaturesDatabase.GetFeatures(path, new CarModel("Mazda", "3"));
            Assert(registeredFeatures.Contains("Rental price per day: €65.25"), "Updated model price displayed");
            command.CommandText = "SELECT DailyRate FROM Cars WHERE LicensePlate = 'NEX-2026';";
            Assert(Convert.ToDecimal(command.ExecuteScalar()) == 65.25m, "Updated price applied to fleet cars");
            Assert(!CarModelFeaturesDatabase.UpdateDailyRate(path, new CarModel("Missing", "Model"), 50m), "Missing model price update rejected");
            try
            {
                CarModelFeaturesDatabase.UpdateDailyRate(path, new CarModel("Mazda", "3"), 0m);
                throw new Exception("Zero rental price accepted");
            }
            catch (ArgumentOutOfRangeException) { }
            try
            {
                CarDatabase.SaveVehicle(path, registration);
                throw new Exception("Duplicate license plate accepted");
            }
            catch (SqliteException) { }
            command.CommandText = "SELECT COUNT(*) FROM CarModelFeatures;";
            Assert(Convert.ToInt32(command.ExecuteScalar()) == 7, "One row per brand/model after vehicle registration");
            command.CommandText = "SELECT COUNT(*) FROM Cars;";
            Assert(Convert.ToInt32(command.ExecuteScalar()) == 21, "Vehicle saved once and duplicate rolled back");
            foreach (var sql in new[] {
                "INSERT INTO CarModelFeatures (Make, Model) VALUES ('Skoda', 'Kamiq');",
                "UPDATE CarModelFeatures SET CarPlay=2;",
                "UPDATE CarModelFeatures SET Seats=0;",
                "UPDATE CarModelFeatures SET DailyRate=-1;" })
            {
                command.CommandText = sql;
                try { command.ExecuteNonQuery(); throw new Exception("Invalid feature data accepted"); }
                catch (SqliteException) { }
            }
            Console.WriteLine("PASS: feature migration, brand filtering, unknown/boolean values, persistence, model uniqueness, fleet preservation and constraints.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
