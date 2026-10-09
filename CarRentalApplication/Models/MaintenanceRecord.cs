using System;

namespace CarRentalApplication.Models;

public sealed class MaintenanceRecord
{
    public long Id { get; init; }
    public long CarId { get; init; }
    public string Make { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string LicensePlate { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public string VehicleDisplay => $"{Make} {Model} · {LicensePlate}";
    public string StartDateDisplay => StartDate.ToString("dd/MM/yyyy");
    public string EndDateDisplay => EndDate.ToString("dd/MM/yyyy");
}
