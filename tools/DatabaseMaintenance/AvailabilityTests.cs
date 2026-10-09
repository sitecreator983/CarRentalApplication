using System;
using System.IO;
using System.Linq;
using CarRentalApplication.Data;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;

internal static class AvailabilityTests
{
    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "car-availability-" + Guid.NewGuid() + ".db");
        try
        {
            CarDatabase.Initialize(path);
            using (var legacy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                legacy.Open();
                using var command = legacy.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Customers (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, FirstName TEXT NOT NULL,
                        LastName TEXT NOT NULL, Phone TEXT NOT NULL, Email TEXT NOT NULL,
                        PickupDate TEXT NOT NULL, ReturnDate TEXT NOT NULL,
                        CHECK (ReturnDate >= PickupDate));
                    INSERT INTO Customers VALUES (42, 'Existing', 'Customer', '6912345678',
                        'existing@example.com', '2026-10-01', '2026-10-02');
                    CREATE TABLE Reservations (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT, CarId INTEGER NOT NULL REFERENCES Cars(Id),
                        CustomerId INTEGER NOT NULL REFERENCES Customers(Id), PickupDate TEXT NOT NULL,
                        ReturnDate TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'Confirmed');
                    INSERT INTO Reservations (CarId, CustomerId, PickupDate, ReturnDate)
                        SELECT Id, 42, '2026-09-01', '2026-09-02' FROM Cars LIMIT 1;
                    """;
                command.ExecuteNonQuery();
            }
            CustomerDatabase.Initialize(path);
            CustomerDatabase.Initialize(path);
            var existing = CustomerDatabase.GetAll(path).Single();
            Assert(existing.Id == 42 && existing.FullName == "Existing Customer", "Migration preserves existing customers and IDs");
            using (var check = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                check.Open();
                using var command = check.CreateCommand();
                command.CommandText = "PRAGMA foreign_key_check;";
                using var reader = command.ExecuteReader();
                Assert(!reader.Read(), "Migration preserves reservation references");
            }
            AvailabilityDatabase.Initialize(path);
            AvailabilityDatabase.Initialize(path);
            var migratedReservation = AvailabilityDatabase.GetReservations(path).Single();
            Assert(migratedReservation.TotalPrice == 96.00m, "Existing reservation total backfilled during migration");
            var models = AvailabilityDatabase.GetModels(path);
            Assert(models.Count == 5 && models.Distinct().Count() == 5, "Distinct models from database");
            var maintenanceModel = new CarModel("Toyota", "Corolla");
            var maintenancePlates = AvailabilityDatabase.GetAvailablePlates(path, maintenanceModel, Day(10), Day(15));
            var maintenanceCar = CarDatabase.GetAll(path).Single(car => car.LicensePlate == maintenancePlates[0]);
            var maintenanceId = MaintenanceDatabase.Schedule(path, maintenanceCar.Id, Day(10), Day(15));
            Assert(maintenanceId > 0 && MaintenanceDatabase.GetAll(path).Count == 1, "Maintenance period scheduled");
            Assert(!AvailabilityDatabase.GetAvailablePlates(path, maintenanceModel, Day(10), Day(15)).Contains(maintenanceCar.LicensePlate),
                "Vehicle under maintenance excluded from availability");
            Assert(AvailabilityDatabase.CreateReservation(path, 42, maintenanceCar.LicensePlate, Day(10), Day(15)) is null,
                "Reservation rejected during maintenance");
            Assert(MaintenanceDatabase.Schedule(path, maintenanceCar.Id, Day(12), Day(18)) is null,
                "Overlapping maintenance rejected");
            Assert(MaintenanceDatabase.Delete(path, maintenanceId.Value), "Maintenance period deleted");
            Assert(AvailabilityDatabase.GetAvailablePlates(path, maintenanceModel, Day(10), Day(15)).Contains(maintenanceCar.LicensePlate),
                "Vehicle available after maintenance deletion");
            Assert(!MaintenanceDatabase.Delete(path, maintenanceId.Value), "Repeated maintenance deletion returns false");
            var model = new CarModel("Skoda", "Kamiq");
            var all = AvailabilityDatabase.GetAvailablePlates(path, model, Day(1), Day(31));
            Assert(all.Count == 4, "No reservations: all four plates available");
            var pricedCars = AvailabilityDatabase.GetAvailableCars(path, model, Day(10), Day(15));
            Assert(pricedCars.Count == 4 && pricedCars.All(car => car.DailyRate == 48.00m), "Model daily rate returned with every available car");
            Assert(AvailabilityDatabase.GetRentalDays(Day(10), Day(15)) == 6, "Rental date range uses inclusive days");
            Assert(AvailabilityDatabase.CalculateRentalTotal(48.00m, Day(10), Day(15)) == 288.00m, "Total rental price");

            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO Customers (FirstName, LastName, Phone, Email)
                    VALUES ('Test', 'Customer', '6912345678', 'test@example.com');
                    INSERT INTO Reservations (CarId, CustomerId, PickupDate, ReturnDate)
                    SELECT Id, 42, '2026-10-10', '2026-10-15' FROM Cars WHERE LicensePlate = $plate;
                    """;
                command.Parameters.AddWithValue("$plate", all[0]);
                command.ExecuteNonQuery();
            }

            foreach (var dates in new[] { (10, 15), (11, 12), (1, 31), (8, 11), (14, 18), (5, 10), (15, 20), (10, 10) })
            {
                var available = AvailabilityDatabase.GetAvailablePlates(path, model, Day(dates.Item1), Day(dates.Item2));
                Assert(available.Count == 3 && !available.Contains(all[0]), $"Overlap {dates}: reserved plate excluded");
            }
            Assert(AvailabilityDatabase.GetAvailablePlates(path, model, Day(1), Day(9)).Count == 4, "Before reservation");
            Assert(AvailabilityDatabase.GetAvailablePlates(path, model, Day(16), Day(31)).Count == 4, "After reservation");
            Assert(AvailabilityDatabase.GetAvailablePlates(path, new CarModel("Kia", "Ceed"), Day(10), Day(15)).Count == 4, "Other model unaffected");
            Assert(AvailabilityDatabase.GetAvailablePlates(path, new CarModel("Unknown' OR 1=1 --", "Model"), Day(1), Day(31)).Count == 0, "Unknown model and parameterized filtering");
            try
            {
                AvailabilityDatabase.GetAvailablePlates(path, model, Day(15), Day(10));
                throw new Exception("Invalid date range accepted");
            }
            catch (ArgumentException) { }

            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE Reservations SET Status = 'Cancelled';";
                command.ExecuteNonQuery();
            }
            Assert(AvailabilityDatabase.GetAvailablePlates(path, model, Day(10), Day(15)).Count == 4, "Cancelled reservation releases car");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO Reservations (CarId, CustomerId, PickupDate, ReturnDate)
                    SELECT Id, 42, '2026-10-10', '2026-10-15' FROM Cars WHERE Make='Skoda' AND Model='Kamiq';
                    """;
                command.ExecuteNonQuery();
            }
            Assert(AvailabilityDatabase.GetAvailablePlates(path, model, Day(10), Day(15)).Count == 0, "Fully booked model");
            var customerId = CustomerDatabase.Save(path, new Customer
            {
                FirstName = "Booking", LastName = "Test", Phone = "6912345678",
                Email = "booking@example.com"
            });
            var kia = new CarModel("Kia", "Ceed");
            var plate = AvailabilityDatabase.GetAvailablePlates(path, kia, Day(10), Day(15))[0];
            var reservationId = AvailabilityDatabase.CreateReservation(path, customerId, plate, Day(10), Day(15));
            Assert(reservationId > 0, "Reservation saved with an ID");
            Assert(!AvailabilityDatabase.GetAvailablePlates(path, kia, Day(10), Day(15)).Contains(plate), "Saved booking removes car from availability");
            Assert(AvailabilityDatabase.CreateReservation(path, customerId, plate, Day(15), Day(20)) is null, "Overlapping booking rejected on save");
            Assert(AvailabilityDatabase.CreateReservation(path, customerId, "UNKNOWN", Day(10), Day(15)) is null, "Unknown plate cannot be reserved");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT CustomerId FROM Reservations WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", reservationId.Value);
                Assert((long)command.ExecuteScalar() == customerId, "Reservation linked to the saved customer");
            }
            Assert(AvailabilityDatabase.CreateReservation(path, customerId, plate, Day(16), Day(20)) > 0, "Non-overlapping booking accepted");
            var reservations = AvailabilityDatabase.GetReservations(path);
            Assert(reservations.Any(reservation => reservation.Id == reservationId), "Reservations list includes saved booking");
            Assert(reservations.Single(reservation => reservation.Id == reservationId).TotalPrice == 252.00m,
                "New reservation stores inclusive rental total");
            Assert(CarModelFeaturesDatabase.UpdateDailyRate(path, kia, 50.00m), "Rental price changed before reservations reload");
            AvailabilityDatabase.RefreshReservationTotals(path);
            Assert(AvailabilityDatabase.GetReservations(path).Single(reservation => reservation.Id == reservationId).TotalPrice == 300.00m,
                "Reservations reload applies current model price");
            Assert(CarModelFeaturesDatabase.UpdateDailyRate(path, kia, 42.00m), "Rental price restored for remaining tests");
            AvailabilityDatabase.RefreshReservationTotals(path);
            var reservedCarId = CarDatabase.GetAll(path).Single(car => car.LicensePlate == plate).Id;
            Assert(AvailabilityDatabase.UpdateReservation(path, reservationId.Value, customerId, reservedCarId,
                Day(21), Day(22), "Cancelled"), "Reservation details and status updated");
            var editedReservation = AvailabilityDatabase.GetReservations(path).Single(reservation => reservation.Id == reservationId);
            Assert(editedReservation.Status == "Cancelled" && editedReservation.PickupDate == Day(21) &&
                editedReservation.TotalPrice == 84.00m, "Edited reservation and recalculated total persisted");
            Assert(!AvailabilityDatabase.UpdateReservation(path, reservationId.Value, customerId, reservedCarId,
                Day(20), Day(21), "Confirmed"), "Overlapping reservation edit rejected");
            Assert(AvailabilityDatabase.UpdateReservation(path, reservationId.Value, customerId, reservedCarId,
                Day(21), Day(22), "Confirmed"), "Non-overlapping reservation confirmation accepted");
            Assert(AvailabilityDatabase.DeleteReservation(path, reservationId.Value), "Reservation deleted");
            Assert(AvailabilityDatabase.GetReservations(path).All(reservation => reservation.Id != reservationId), "Deleted reservation removed from list");
            Assert(!AvailabilityDatabase.DeleteReservation(path, reservationId.Value), "Repeated reservation deletion returns false");
            Assert(CustomerDatabase.Update(path, new Customer { Id = customerId, FirstName = "Updated",
                LastName = "Customer", Phone = "6912345678", Email = "updated@example.com" }), "Customer updated");
            var updated = CustomerDatabase.GetAll(path).Single(customer => customer.Id == customerId);
            Assert(updated.FirstName == "Updated" && updated.Email == "updated@example.com", "Updated fields persisted");
            Assert(CustomerDatabase.Delete(path, customerId), "Customer with reservations deleted");
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Reservations WHERE CustomerId = $id;";
                command.Parameters.AddWithValue("$id", customerId);
                Assert((long)command.ExecuteScalar() == 0, "All related reservations deleted");
                command.CommandText = "SELECT COUNT(*) FROM Reservations WHERE CustomerId = 42;";
                Assert((long)command.ExecuteScalar() > 0, "Other customers' reservations preserved");
            }
            Assert(AvailabilityDatabase.GetAvailablePlates(path, kia, Day(10), Day(15)).Contains(plate), "Deleting customer releases their booked car");
            Assert(!CustomerDatabase.Delete(path, customerId), "Repeated deletion returns false");
            var unbookedId = CustomerDatabase.Save(path, new Customer { FirstName = "Delete", LastName = "Test",
                Phone = "6912345678", Email = "delete@example.com" });
            Assert(CustomerDatabase.Delete(path, unbookedId), "Unbooked customer deleted");
            Assert(CustomerDatabase.GetAll(path).All(customer => customer.Id != unbookedId), "Deleted customer removed from list");
            Assert(!CustomerDatabase.Update(path, new Customer { Id = unbookedId }), "Update of missing customer rejected");
            var vehicles = CarDatabase.GetAll(path);
            Assert(vehicles.Count == 20 && vehicles.All(vehicle => vehicle.Id > 0), "All registered vehicles loaded with IDs");
            var editedVehicle = vehicles.First(vehicle => vehicle.Make == "Fiat");
            Assert(AvailabilityDatabase.CreateReservation(path, 42, editedVehicle.LicensePlate,
                new DateOnly(2027, 1, 10), new DateOnly(2027, 1, 12)) > 0, "Vehicle reservation created before edit");
            var updatedVehicle = new Car
            {
                Id = editedVehicle.Id, Make = editedVehicle.Make, Model = editedVehicle.Model,
                ModelYear = 2025, Category = editedVehicle.Category, Transmission = "Automatic",
                EngineCc = editedVehicle.EngineCc, Horsepower = 110,
                LicensePlate = "EDIT-0001", DailyRate = 45.50m
            };
            Assert(CarDatabase.UpdateVehicle(path, updatedVehicle), "Vehicle details updated");
            var persistedVehicle = CarDatabase.GetAll(path).Single(vehicle => vehicle.Id == editedVehicle.Id);
            Assert(persistedVehicle.LicensePlate == "EDIT-0001" && persistedVehicle.Horsepower == 110,
                "Edited vehicle fields persisted");
            Assert(CarDatabase.DeleteVehicle(path, editedVehicle.Id), "Vehicle deleted");
            Assert(CarDatabase.GetAll(path).All(vehicle => vehicle.Id != editedVehicle.Id), "Deleted vehicle removed from fleet");
            var orphanedReservation = AvailabilityDatabase.GetReservations(path)
                .Single(reservation => reservation.CarId is null && reservation.PickupDate == new DateOnly(2027, 1, 10));
            Assert(orphanedReservation.VehicleDisplay.Contains("select replacement"), "Deleted vehicle leaves visible replacement request");
            Assert(orphanedReservation.TotalPrice == 102.00m, "Deleting vehicle preserves reservation total");
            var replacements = AvailabilityDatabase.GetAvailableVehiclesForReservation(path,
                orphanedReservation.PickupDate, orphanedReservation.ReturnDate, orphanedReservation.Id);
            Assert(replacements.Count == 19 && replacements.Select(car => new CarModel(car.Make, car.Model)).Distinct().Count() == 5,
                "Replacement list includes available vehicles from all brands and models");
            Assert(AvailabilityDatabase.UpdateReservation(path, orphanedReservation.Id, 42, replacements[0].Id,
                orphanedReservation.PickupDate, orphanedReservation.ReturnDate, "Confirmed"), "Replacement vehicle assigned to orphaned reservation");
            Assert(CarModelFeaturesDatabase.GetFeatures(path, new CarModel("Fiat", "Tipo")).Count > 0,
                "Model features retained while other vehicles of model exist");
            Assert(!CarDatabase.DeleteVehicle(path, editedVehicle.Id), "Repeated vehicle deletion returns false");
            Console.WriteLine("PASS: model list, all overlap cases, inclusive boundaries, outside ranges, other models, cancelled reservations, fully booked model, invalid dates.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
            File.Delete(path + ".before-customer-date-migration.bak");
            File.Delete(path + ".before-nullable-reservation-car.bak");
        }
    }

    private static DateOnly Day(int day) => new(2026, 10, day);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
