namespace CarRentalApplication.Models;

public sealed class Car
{
    public long Id { get; init; }
    public string Make { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int ModelYear { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Transmission { get; init; } = string.Empty;
    public int EngineCc { get; init; }
    public int Horsepower { get; init; }
    public string LicensePlate { get; init; } = string.Empty;
    public decimal DailyRate { get; init; }
    public string DailyRateDisplay => $"€{DailyRate:0.00}";
    public string SelectionLabel => $"{Make} {Model} · {LicensePlate} · #{Id}";
}
