namespace CarRentalApplication.Models;

public sealed class CarRegistration
{
    public string Make { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int ModelYear { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Transmission { get; init; } = string.Empty;
    public int EngineCc { get; init; }
    public int Horsepower { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public decimal DailyRate { get; init; }
    public bool CarPlay { get; init; }
    public bool AndroidAuto { get; init; }
    public string Roof { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string FuelType { get; init; } = string.Empty;
    public int Seats { get; init; }
    public int Doors { get; init; }
    public bool AirConditioning { get; init; }
    public bool ParkingSensors { get; init; }
    public bool RearCamera { get; init; }
    public string AdditionalFeatures { get; init; } = string.Empty;
}
