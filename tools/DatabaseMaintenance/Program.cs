using System;
using System.IO;
using CarRentalApplication.Data;
using Microsoft.Data.Sqlite;

if (args.Length == 1 && args[0] == "--test-features")
{
    FeatureTests.Run();
    return;
}

if (args.Length == 1 && args[0] == "--test-availability")
{
    AvailabilityTests.Run();
    return;
}

if (args.Length != 1)
    throw new ArgumentException("Provide the path to carrental.db.");

var path = Path.GetFullPath(args[0]);
if (File.Exists(path) && !File.Exists(path + ".before-license-plates.bak"))
{
    // SQLite backup includes committed changes even when WAL mode is enabled.
    using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
    using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path + ".before-license-plates.bak" }.ToString());
    source.Open();
    backup.Open();
    source.BackupDatabase(backup);
}

CarDatabase.Initialize(path);
using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
connection.Open();
using var command = connection.CreateCommand();
command.CommandText = "SELECT Make, Model, LicensePlate FROM Cars ORDER BY Make, Model, LicensePlate;";
using var reader = command.ExecuteReader();
while (reader.Read())
    Console.WriteLine($"{reader.GetString(0)} {reader.GetString(1)} | {reader.GetString(2)}");
