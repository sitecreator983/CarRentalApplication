namespace CarRentalApplication.Models;

public sealed class Customer
{
    public long Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string SelectionLabel => $"{FullName} · {Email} · #{Id}";
}
