namespace CarRentalApplication.Models;

public sealed record AvailableCar(string LicensePlate, decimal DailyRate)
{
    public override string ToString() => $"{LicensePlate} — €{DailyRate:0.00} / day";
}
