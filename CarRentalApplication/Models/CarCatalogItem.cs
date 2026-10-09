namespace CarRentalApplication.Models;

public sealed record CarCatalogItem(string Make, string Model, decimal? DailyRate)
{
    public string DailyRateDisplay => DailyRate is decimal rate ? $"€{rate:0.00} / day" : "Price not set";
}