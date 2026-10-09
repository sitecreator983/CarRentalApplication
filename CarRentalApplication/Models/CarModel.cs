namespace CarRentalApplication.Models;

public sealed record CarModel(string Make, string Model)
{
    public override string ToString() => $"{Make} {Model}";
}
