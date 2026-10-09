using System;

namespace CarRentalApplication.Models;

public sealed class ReservationRecord
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public long? CarId { get; init; }
    public string Make { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string LicensePlate { get; init; } = string.Empty;
    public DateOnly PickupDate { get; init; }
    public DateOnly ReturnDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal? TotalPrice { get; init; }
    public string CustomerDisplay => $"{CustomerName} · {CustomerEmail}";
    public string VehicleDisplay => CarId is null
        ? "Vehicle deleted — select replacement"
        : $"{Make} {Model} · {LicensePlate}";
    public string PickupDateDisplay => PickupDate.ToString("dd/MM/yyyy");
    public string ReturnDateDisplay => ReturnDate.ToString("dd/MM/yyyy");
    public string TotalPriceDisplay => TotalPrice is decimal total ? $"€{total:0.00}" : "Not available";
}
