namespace CarRentalApplication.Models;

public sealed class ModelFeatureDisplay
{
    public string Text { get; }
    public string IconGlyph { get; }

    public ModelFeatureDisplay(string text)
    {
        Text = text;
        var separator = text.IndexOf(':');
        var label = separator >= 0 ? text[..separator] : text;
        IconGlyph = label switch
        {
            "Model year" => "\uE787", // Calendar
            "Category" => "\uE804", // Car
            "Transmission" => "\uE713", // Settings
            "Engine capacity" => "\uE943", // Processing
            "Horsepower" => "\uE945", // Lightning bolt
            "Apple CarPlay" or "Android Auto" => "\uE8EA", // Phone
            "Roof" => "\uE80F", // Home
            "Color" => "\uE790", // Color
            "Fuel type" => "\uE7B8", // Drop
            "Seats" => "\uE716", // People
            "Doors" => "\uE8F4", // Open pane
            "Air conditioning" => "\uE9CA", // Snowflake
            "Parking sensors" => "\uE95A", // Radar
            "Rear camera" => "\uE722", // Camera
            "Rental price per day" => "\uE8C7", // Payment card
            _ => "\uE713"
        };
    }
}
