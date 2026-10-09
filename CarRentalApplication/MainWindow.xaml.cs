using CarRentalApplication.Data;
using CarRentalApplication.Models;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Windows.Storage;

namespace CarRentalApplication;

public sealed partial class MainWindow : Window
{
    private Customer? _savedCustomer;
    private bool _reservationConfirmed;
    private List<CarCatalogItem> _catalog = [];
    private List<ReservationRecord> _reservations = [];
    private static string DatabasePath => Path.Combine(ApplicationData.Current.LocalFolder.Path, "carrental.db");

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 850));
        LoadFeatureBrands();
        LoadCatalog();
        LoadCustomers();
        LoadModels();
        LoadVehicles();
        LoadMaintenance();
        LoadReservations();
        ResetVehicleForm();
    }

    private void SaveVehicle_Click(object sender, RoutedEventArgs e)
    {
        VehicleRegistrationMessage.IsOpen = false;
        var make = VehicleMakeBox.Text.Trim();
        var model = VehicleModelBox.Text.Trim();
        var plate = VehiclePlateBox.Text.Trim().ToUpperInvariant();
        var roof = VehicleRoofBox.Text.Trim();
        var color = VehicleColorBox.Text.Trim();
        var fuel = VehicleFuelBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(make)) { ShowVehicleError("Enter the brand.", VehicleMakeBox); return; }
        if (string.IsNullOrWhiteSpace(model)) { ShowVehicleError("Enter the model.", VehicleModelBox); return; }
        if (!TryGetWholeNumber(VehicleYearBox, out var year) || year < 1900 || year > DateTime.Now.Year + 1)
        {
            ShowVehicleError($"Enter a model year between 1900 and {DateTime.Now.Year + 1}.", VehicleYearBox);
            return;
        }
        if (VehicleCategoryPicker.SelectedItem is not string category)
        {
            ShowVehicleError("Select the vehicle category.", VehicleCategoryPicker);
            return;
        }
        if (VehicleTransmissionPicker.SelectedItem is not string transmission)
        {
            ShowVehicleError("Select the transmission.", VehicleTransmissionPicker);
            return;
        }
        if (!TryGetPositiveWholeNumber(VehicleEngineBox, out var engineCc))
        {
            ShowVehicleError("Enter a positive engine capacity in cc.", VehicleEngineBox);
            return;
        }
        if (!TryGetPositiveWholeNumber(VehicleHorsepowerBox, out var horsepower))
        {
            ShowVehicleError("Enter positive horsepower.", VehicleHorsepowerBox);
            return;
        }
        if (plate.Length is < 2 or > 20 || !Regex.IsMatch(plate, "^[A-Z0-9]+(?:-[A-Z0-9]+)*$"))
        {
            ShowVehicleError("Enter a valid license plate using letters, numbers and hyphens.", VehiclePlateBox);
            return;
        }
        if (double.IsNaN(VehicleRateBox.Value) || VehicleRateBox.Value <= 0)
        {
            ShowVehicleError("Enter a rental price greater than zero.", VehicleRateBox);
            return;
        }
        if (string.IsNullOrWhiteSpace(roof)) { ShowVehicleError("Enter the roof type.", VehicleRoofBox); return; }
        if (string.IsNullOrWhiteSpace(color)) { ShowVehicleError("Enter the color.", VehicleColorBox); return; }
        if (string.IsNullOrWhiteSpace(fuel)) { ShowVehicleError("Enter the fuel type.", VehicleFuelBox); return; }
        if (!TryGetPositiveWholeNumber(VehicleSeatsBox, out var seats))
        {
            ShowVehicleError("Enter the number of seats.", VehicleSeatsBox);
            return;
        }
        if (!TryGetPositiveWholeNumber(VehicleDoorsBox, out var doors))
        {
            ShowVehicleError("Enter the number of doors.", VehicleDoorsBox);
            return;
        }

        var registration = new CarRegistration
        {
            Make = make, Model = model, ModelYear = year, Category = category,
            Transmission = transmission, EngineCc = engineCc, Horsepower = horsepower,
            LicensePlate = plate, DailyRate = (decimal)VehicleRateBox.Value,
            CarPlay = VehicleCarPlayToggle.IsOn, AndroidAuto = VehicleAndroidToggle.IsOn,
            Roof = roof, Color = color, FuelType = fuel, Seats = seats, Doors = doors,
            AirConditioning = VehicleAirConditioningToggle.IsOn,
            ParkingSensors = VehicleParkingSensorsToggle.IsOn,
            RearCamera = VehicleRearCameraToggle.IsOn,
            AdditionalFeatures = VehicleAdditionalFeaturesBox.Text.Trim()
        };

        SaveVehicleButton.IsEnabled = false;
        try
        {
            var carId = CarDatabase.SaveVehicle(DatabasePath, registration);
            VehicleRegistrationMessage.Severity = InfoBarSeverity.Success;
            VehicleRegistrationMessage.Title = "Vehicle registered";
            VehicleRegistrationMessage.Message = $"{make} {model}, plate {plate}, was saved with vehicle ID #{carId}.";
            VehicleRegistrationMessage.IsOpen = true;
            LoadCatalog();
            LoadFeatureBrands();
            LoadModels();
            LoadVehicles(carId);
            LoadMaintenance();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            Debug.WriteLine(exception);
            ShowVehicleError("The license plate already exists or one of the values is not accepted by the database.", VehiclePlateBox);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowVehicleError("The vehicle could not be saved. Your entries remain so you can try again.", VehicleMakeBox);
        }
        finally
        {
            SaveVehicleButton.IsEnabled = true;
        }
    }

    private static bool TryGetWholeNumber(NumberBox box, out int value)
    {
        value = 0;
        if (double.IsNaN(box.Value) || box.Value != Math.Truncate(box.Value) ||
            box.Value < int.MinValue || box.Value > int.MaxValue)
            return false;
        value = (int)box.Value;
        return true;
    }

    private static bool TryGetPositiveWholeNumber(NumberBox box, out int value) =>
        TryGetWholeNumber(box, out value) && value > 0;

    private void ShowVehicleError(string message, Control field)
    {
        VehicleRegistrationMessage.Severity = InfoBarSeverity.Error;
        VehicleRegistrationMessage.Title = "Vehicle registration failed";
        VehicleRegistrationMessage.Message = message;
        VehicleRegistrationMessage.IsOpen = true;
        field.Focus(FocusState.Programmatic);
    }

    private void ClearVehicleForm_Click(object sender, RoutedEventArgs e) => ResetVehicleForm();

    private void ResetVehicleForm()
    {
        VehicleMakeBox.Text = string.Empty;
        VehicleModelBox.Text = string.Empty;
        VehicleYearBox.Value = DateTime.Now.Year;
        VehicleCategoryPicker.SelectedIndex = -1;
        VehicleTransmissionPicker.SelectedIndex = -1;
        VehicleEngineBox.Value = double.NaN;
        VehicleHorsepowerBox.Value = double.NaN;
        VehiclePlateBox.Text = string.Empty;
        VehicleRateBox.Value = double.NaN;
        VehicleRoofBox.Text = string.Empty;
        VehicleColorBox.Text = string.Empty;
        VehicleFuelBox.Text = string.Empty;
        VehicleSeatsBox.Value = 5;
        VehicleDoorsBox.Value = 5;
        VehicleCarPlayToggle.IsOn = false;
        VehicleAndroidToggle.IsOn = false;
        VehicleAirConditioningToggle.IsOn = false;
        VehicleParkingSensorsToggle.IsOn = false;
        VehicleRearCameraToggle.IsOn = false;
        VehicleAdditionalFeaturesBox.Text = string.Empty;
        VehicleRegistrationMessage.IsOpen = false;
    }

    private void LoadReservations(long? selectId = null)
    {
        if (ReservationsTable is null) return;
        ReservationsMessage.IsOpen = false;
        try
        {
            var previousId = selectId ?? (ReservationsTable.SelectedItem as ReservationRecord)?.Id;
            AvailabilityDatabase.RefreshReservationTotals(DatabasePath);
            _reservations = AvailabilityDatabase.GetReservations(DatabasePath);
            ApplyReservationFilters(previousId);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            _reservations = [];
            ReservationsTable.ItemsSource = null;
            ReservationCountText.Text = "0 reservations";
            ShowReservationsMessage(InfoBarSeverity.Error, "Reservations could not be loaded. Select 'Reload reservations' to try again.");
        }
    }

    private void ReloadReservations_Click(object sender, RoutedEventArgs e) => LoadReservations();

    private void ReservationTextFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyReservationFilters();

    private void ReservationComboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyReservationFilters();

    private void ReservationDateFilter_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args) =>
        ApplyReservationFilters();

    private void ClearReservationFilters_Click(object sender, RoutedEventArgs e)
    {
        ReservationCustomerFilter.Text = string.Empty;
        ReservationPlateFilter.Text = string.Empty;
        ReservationDateFilter.Date = null;
        ReservationPeriodFilter.SelectedIndex = 0;
        ReservationStatusFilter.SelectedIndex = 0;
        ApplyReservationFilters();
    }

    private void ApplyReservationFilters(long? selectId = null)
    {
        if (ReservationsTable is null || ReservationCustomerFilter is null || ReservationPlateFilter is null ||
            ReservationDateFilter is null || ReservationPeriodFilter is null || ReservationStatusFilter is null ||
            ReservationCountText is null)
            return;

        var previousId = selectId ?? (ReservationsTable.SelectedItem as ReservationRecord)?.Id;
        var customerFilter = ReservationCustomerFilter.Text.Trim();
        var plateFilter = ReservationPlateFilter.Text.Trim();
        var selectedStatus = ReservationStatusFilter.SelectedItem as string;
        IEnumerable<ReservationRecord> filtered = _reservations;

        if (!string.IsNullOrWhiteSpace(customerFilter))
        {
            filtered = filtered.Where(reservation =>
                reservation.CustomerName.Contains(customerFilter, StringComparison.OrdinalIgnoreCase) ||
                reservation.CustomerEmail.Contains(customerFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(plateFilter))
        {
            filtered = filtered.Where(reservation =>
                reservation.LicensePlate.Contains(plateFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(selectedStatus) && selectedStatus != "All statuses")
        {
            filtered = filtered.Where(reservation =>
                string.Equals(reservation.Status, selectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (ReservationDateFilter.Date is DateTimeOffset selectedDateTime)
        {
            var selectedDate = DateOnly.FromDateTime(selectedDateTime.Date);
            var periodStart = selectedDate;
            var periodEnd = selectedDate;

            if (ReservationPeriodFilter.SelectedItem as string == "Week")
            {
                var daysSinceMonday = ((int)selectedDate.DayOfWeek + 6) % 7;
                periodStart = selectedDate.AddDays(-daysSinceMonday);
                periodEnd = periodStart.AddDays(6);
            }

            filtered = filtered.Where(reservation =>
                reservation.PickupDate <= periodEnd && reservation.ReturnDate >= periodStart);
        }

        var visibleReservations = filtered.ToList();
        ReservationsTable.ItemsSource = visibleReservations;
        ReservationsTable.SelectedItem = visibleReservations.FirstOrDefault(reservation => reservation.Id == previousId);

        var visibleLabel = visibleReservations.Count == 1 ? "reservation" : "reservations";
        ReservationCountText.Text = visibleReservations.Count == _reservations.Count
            ? $"{visibleReservations.Count} {visibleLabel}"
            : $"{visibleReservations.Count} of {_reservations.Count} {visibleLabel}";
    }

    private void ReservationsTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditReservationButton is null || DeleteReservationButton is null) return;
        var selected = ReservationsTable.SelectedItem is ReservationRecord;
        EditReservationButton.IsEnabled = selected;
        DeleteReservationButton.IsEnabled = selected;
    }

    private void ShowReservationsMessage(InfoBarSeverity severity, string message)
    {
        ReservationsMessage.Severity = severity;
        ReservationsMessage.Message = message;
        ReservationsMessage.IsOpen = true;
    }

    private async void EditReservation_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationsTable.SelectedItem is not ReservationRecord reservation) return;
        var customers = CustomerDatabase.GetAll(DatabasePath);
        var customerPicker = new ComboBox
        {
            Header = "Customer", ItemsSource = customers, DisplayMemberPath = "SelectionLabel",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        customerPicker.SelectedItem = customers.FirstOrDefault(customer => customer.Id == reservation.CustomerId);
        var vehiclePicker = new ComboBox
        {
            Header = "Available vehicle (all brands and models)", DisplayMemberPath = "SelectionLabel",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var pickupPicker = new CalendarDatePicker
        {
            Header = "Pickup date", HorizontalAlignment = HorizontalAlignment.Stretch,
            Date = new DateTimeOffset(reservation.PickupDate.ToDateTime(TimeOnly.MinValue))
        };
        var returnPicker = new CalendarDatePicker
        {
            Header = "Return date", HorizontalAlignment = HorizontalAlignment.Stretch,
            Date = new DateTimeOffset(reservation.ReturnDate.ToDateTime(TimeOnly.MinValue))
        };
        var availableHint = new TextBlock { TextWrapping = TextWrapping.Wrap };
        void ReloadAvailableVehicles()
        {
            var previousId = (vehiclePicker.SelectedItem as Car)?.Id ?? reservation.CarId;
            if (pickupPicker.Date is not DateTimeOffset pickupValue ||
                returnPicker.Date is not DateTimeOffset returnValue || returnValue.Date < pickupValue.Date)
            {
                vehiclePicker.ItemsSource = null;
                availableHint.Text = "Select a valid date range to load available vehicles.";
                return;
            }
            try
            {
                var available = AvailabilityDatabase.GetAvailableVehiclesForReservation(DatabasePath,
                    DateOnly.FromDateTime(pickupValue.Date), DateOnly.FromDateTime(returnValue.Date), reservation.Id);
                vehiclePicker.ItemsSource = available;
                vehiclePicker.SelectedItem = available.FirstOrDefault(vehicle => vehicle.Id == previousId);
                availableHint.Text = available.Count == 0
                    ? "No vehicle is available for this date range."
                    : $"{available.Count} available {(available.Count == 1 ? "vehicle" : "vehicles")}.";
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
                vehiclePicker.ItemsSource = null;
                availableHint.Text = "Available vehicles could not be loaded.";
            }
        }
        pickupPicker.DateChanged += (_, _) => ReloadAvailableVehicles();
        returnPicker.DateChanged += (_, _) => ReloadAvailableVehicles();
        ReloadAvailableVehicles();
        var statusPicker = new ComboBox
        {
            Header = "Status", ItemsSource = new[] { "Confirmed", "Cancelled" },
            HorizontalAlignment = HorizontalAlignment.Stretch, SelectedItem = reservation.Status
        };
        var error = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false };
        var fields = new StackPanel { Spacing = 10 };
        foreach (var field in new UIElement[] { customerPicker, pickupPicker, returnPicker, availableHint, vehiclePicker, statusPicker, error })
            fields.Children.Add(field);
        var dialog = new ContentDialog
        {
            XamlRoot = ReservationsTable.XamlRoot,
            Title = $"Edit reservation #{reservation.Id}",
            Content = fields,
            PrimaryButtonText = "Save changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (dialogSender, args) =>
        {
            string? validation = null;
            if (customerPicker.SelectedItem is not Customer)
                validation = "Select a customer.";
            else if (vehiclePicker.SelectedItem is not Car)
                validation = "Select a vehicle.";
            else if (pickupPicker.Date is not DateTimeOffset || returnPicker.Date is not DateTimeOffset)
                validation = "Select pickup and return dates.";
            else if (returnPicker.Date.Value.Date < pickupPicker.Date.Value.Date)
                validation = "The return date must be the same as or later than the pickup date.";
            else if (statusPicker.SelectedItem is not string)
                validation = "Select the reservation status.";

            if (validation is not null)
            {
                args.Cancel = true;
                error.Message = validation;
                error.IsOpen = true;
                return;
            }

            var customer = (Customer)customerPicker.SelectedItem!;
            var vehicle = (Car)vehiclePicker.SelectedItem!;
            var pickup = DateOnly.FromDateTime(pickupPicker.Date!.Value.Date);
            var dropoff = DateOnly.FromDateTime(returnPicker.Date!.Value.Date);
            var status = (string)statusPicker.SelectedItem!;
            try
            {
                if (!AvailabilityDatabase.UpdateReservation(DatabasePath, reservation.Id, customer.Id,
                    vehicle.Id, pickup, dropoff, status))
                {
                    args.Cancel = true;
                    error.Message = "The vehicle is not available for these dates, or the reservation no longer exists.";
                    error.IsOpen = true;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
                args.Cancel = true;
                error.Message = "The changes could not be saved. Try again.";
                error.IsOpen = true;
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            LoadReservations(reservation.Id);
            ResetAvailabilityResults();
            ShowReservationsMessage(InfoBarSeverity.Success, "Reservation details updated.");
        }
    }

    private async void DeleteReservation_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationsTable.SelectedItem is not ReservationRecord reservation) return;
        var dialog = new ContentDialog
        {
            XamlRoot = ReservationsTable.XamlRoot,
            Title = "Delete reservation?",
            Content = $"Delete reservation #{reservation.Id} for {reservation.CustomerName} and {reservation.LicensePlate}? This action cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            var deleted = AvailabilityDatabase.DeleteReservation(DatabasePath, reservation.Id);
            LoadReservations();
            ResetAvailabilityResults();
            ShowReservationsMessage(deleted ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                deleted ? "Reservation deleted." : "Reservation no longer exists. The list has been refreshed.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowReservationsMessage(InfoBarSeverity.Error, "The reservation could not be deleted. Try again.");
        }
    }

    private void LoadVehicles(long? selectId = null)
    {
        if (VehiclesTable is null) return;
        VehiclesMessage.IsOpen = false;
        try
        {
            var previousId = selectId ?? (VehiclesTable.SelectedItem as Car)?.Id;
            var vehicles = CarDatabase.GetAll(DatabasePath);
            VehiclesTable.ItemsSource = vehicles;
            VehiclesTable.SelectedItem = vehicles.FirstOrDefault(vehicle => vehicle.Id == previousId);
            VehicleCountText.Text = $"{vehicles.Count} {(vehicles.Count == 1 ? "vehicle" : "vehicles")}";
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            VehiclesTable.ItemsSource = null;
            ShowVehiclesMessage(InfoBarSeverity.Error, "Vehicles could not be loaded. Select 'Reload vehicles' to try again.");
        }
    }

    private void ReloadVehicles_Click(object sender, RoutedEventArgs e) => LoadVehicles();

    private void VehiclesTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditVehicleButton is null || DeleteVehicleButton is null) return;
        var selected = VehiclesTable.SelectedItem is Car;
        EditVehicleButton.IsEnabled = selected;
        DeleteVehicleButton.IsEnabled = selected;
    }

    private void ShowVehiclesMessage(InfoBarSeverity severity, string message)
    {
        VehiclesMessage.Severity = severity;
        VehiclesMessage.Message = message;
        VehiclesMessage.IsOpen = true;
    }

    private async void EditVehicle_Click(object sender, RoutedEventArgs e)
    {
        if (VehiclesTable.SelectedItem is not Car vehicle) return;

        var make = new TextBox { Header = "Brand", Text = vehicle.Make, MaxLength = 100 };
        var model = new TextBox { Header = "Model", Text = vehicle.Model, MaxLength = 100 };
        var year = new NumberBox { Header = "Model year", Minimum = 1900, Maximum = 2100, Value = vehicle.ModelYear };
        var category = new ComboBox { Header = "Category", ItemsSource = new[] { "Hatchback", "SUV", "Sedan" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        category.SelectedItem = vehicle.Category;
        var transmission = new ComboBox { Header = "Transmission", ItemsSource = new[] { "Manual", "Automatic" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        transmission.SelectedItem = vehicle.Transmission;
        var engine = new NumberBox { Header = "Engine capacity (cc)", Minimum = 1, Maximum = 10000, Value = vehicle.EngineCc };
        var horsepower = new NumberBox { Header = "Horsepower", Minimum = 1, Maximum = 3000, Value = vehicle.Horsepower };
        var plate = new TextBox { Header = "License plate", Text = vehicle.LicensePlate, MaxLength = 20, CharacterCasing = CharacterCasing.Upper };
        var price = new NumberBox { Header = "Rental price per day (€)", Minimum = 0.01, Maximum = 100000, SmallChange = 1, Value = (double)vehicle.DailyRate };
        var error = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false };
        var fields = new StackPanel { Spacing = 10 };
        foreach (var field in new UIElement[] { make, model, year, category, transmission, engine, horsepower, plate, price, error })
            fields.Children.Add(field);
        var dialog = new ContentDialog
        {
            XamlRoot = VehiclesTable.XamlRoot,
            Title = $"Edit vehicle #{vehicle.Id}",
            Content = new ScrollViewer { Content = fields, MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = "Save changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (dialogSender, args) =>
        {
            var normalizedPlate = plate.Text.Trim().ToUpperInvariant();
            string? validation = null;
            if (string.IsNullOrWhiteSpace(make.Text) || string.IsNullOrWhiteSpace(model.Text))
                validation = "Brand and model are required.";
            else if (!TryGetWholeNumber(year, out var parsedYear) || parsedYear < 1900 || parsedYear > DateTime.Now.Year + 1)
                validation = $"Enter a model year between 1900 and {DateTime.Now.Year + 1}.";
            else if (category.SelectedItem is not string || transmission.SelectedItem is not string)
                validation = "Select a category and transmission.";
            else if (!TryGetPositiveWholeNumber(engine, out _) || !TryGetPositiveWholeNumber(horsepower, out _))
                validation = "Engine capacity and horsepower must be positive whole numbers.";
            else if (normalizedPlate.Length is < 2 or > 20 || !Regex.IsMatch(normalizedPlate, "^[A-Z0-9]+(?:-[A-Z0-9]+)*$"))
                validation = "Enter a valid license plate using letters, numbers and hyphens.";
            else if (double.IsNaN(price.Value) || price.Value <= 0)
                validation = "Enter a rental price greater than zero.";

            if (validation is not null)
            {
                args.Cancel = true;
                error.Message = validation;
                error.IsOpen = true;
                return;
            }

            TryGetWholeNumber(year, out var validYear);
            TryGetPositiveWholeNumber(engine, out var validEngine);
            TryGetPositiveWholeNumber(horsepower, out var validHorsepower);
            var updated = new Car
            {
                Id = vehicle.Id, Make = make.Text.Trim(), Model = model.Text.Trim(),
                ModelYear = validYear, Category = (string)category.SelectedItem,
                Transmission = (string)transmission.SelectedItem, EngineCc = validEngine,
                Horsepower = validHorsepower, LicensePlate = normalizedPlate,
                DailyRate = (decimal)price.Value
            };
            try
            {
                if (!CarDatabase.UpdateVehicle(DatabasePath, updated))
                {
                    args.Cancel = true;
                    error.Message = "This vehicle no longer exists. Cancel and reload vehicles.";
                    error.IsOpen = true;
                }
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                Debug.WriteLine(exception);
                args.Cancel = true;
                error.Message = "The license plate already exists or a value is not accepted by the database.";
                error.IsOpen = true;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
                args.Cancel = true;
                error.Message = "The changes could not be saved. Try again.";
                error.IsOpen = true;
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            LoadVehicles(vehicle.Id);
            LoadMaintenance();
            LoadReservations();
            LoadCatalog();
            LoadFeatureBrands();
            LoadModels();
            ResetAvailabilityResults();
            ShowVehiclesMessage(InfoBarSeverity.Success, "Vehicle details updated.");
        }
    }

    private async void DeleteVehicle_Click(object sender, RoutedEventArgs e)
    {
        if (VehiclesTable.SelectedItem is not Car vehicle) return;
        var dialog = new ContentDialog
        {
            XamlRoot = VehiclesTable.XamlRoot,
            Title = "Delete vehicle?",
            Content = $"Delete {vehicle.Make} {vehicle.Model}, plate {vehicle.LicensePlate}? Its reservations will be retained without a vehicle so that a replacement can be selected.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var deleted = CarDatabase.DeleteVehicle(DatabasePath, vehicle.Id);
            LoadVehicles();
            LoadMaintenance();
            LoadReservations();
            LoadCatalog();
            LoadFeatureBrands();
            LoadModels();
            ResetAvailabilityResults();
            ShowVehiclesMessage(deleted ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                deleted ? "Vehicle deleted. Its reservations were retained and now require a replacement vehicle." : "Vehicle no longer exists. The list has been refreshed.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowVehiclesMessage(InfoBarSeverity.Error, "The vehicle could not be deleted. Try again.");
        }
    }

    private void LoadMaintenance(long? selectId = null)
    {
        if (MaintenanceTable is null || MaintenanceVehiclePicker is null) return;
        MaintenanceMessage.IsOpen = false;
        try
        {
            var selectedVehicleId = (MaintenanceVehiclePicker.SelectedItem as Car)?.Id;
            var vehicles = CarDatabase.GetAll(DatabasePath);
            MaintenanceVehiclePicker.ItemsSource = vehicles;
            MaintenanceVehiclePicker.SelectedItem = vehicles.FirstOrDefault(vehicle => vehicle.Id == selectedVehicleId);

            var previousId = selectId ?? (MaintenanceTable.SelectedItem as MaintenanceRecord)?.Id;
            var records = MaintenanceDatabase.GetAll(DatabasePath);
            MaintenanceTable.ItemsSource = records;
            MaintenanceTable.SelectedItem = records.FirstOrDefault(record => record.Id == previousId);
            MaintenanceCountText.Text = $"{records.Count} maintenance {(records.Count == 1 ? "period" : "periods")}";
            UpdateMaintenanceButton();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            MaintenanceTable.ItemsSource = null;
            ShowMaintenanceMessage(InfoBarSeverity.Error, "Maintenance periods could not be loaded. Select 'Reload maintenance' to try again.");
        }
    }

    private void ReloadMaintenance_Click(object sender, RoutedEventArgs e) => LoadMaintenance();

    private void MaintenanceInput_Changed(object sender, SelectionChangedEventArgs e) => UpdateMaintenanceButton();

    private void MaintenanceDate_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args) => UpdateMaintenanceButton();

    private void UpdateMaintenanceButton()
    {
        if (ScheduleMaintenanceButton is null || MaintenanceVehiclePicker is null ||
            MaintenanceStartPicker is null || MaintenanceEndPicker is null) return;
        ScheduleMaintenanceButton.IsEnabled = MaintenanceVehiclePicker.SelectedItem is Car &&
            MaintenanceStartPicker.Date is DateTimeOffset start &&
            MaintenanceEndPicker.Date is DateTimeOffset end && end.Date >= start.Date;
    }

    private void MaintenanceTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeleteMaintenanceButton is not null)
            DeleteMaintenanceButton.IsEnabled = MaintenanceTable.SelectedItem is MaintenanceRecord;
    }

    private void ShowMaintenanceMessage(InfoBarSeverity severity, string message)
    {
        MaintenanceMessage.Severity = severity;
        MaintenanceMessage.Message = message;
        MaintenanceMessage.IsOpen = true;
    }

    private void ScheduleMaintenance_Click(object sender, RoutedEventArgs e)
    {
        if (MaintenanceVehiclePicker.SelectedItem is not Car vehicle ||
            MaintenanceStartPicker.Date is not DateTimeOffset start ||
            MaintenanceEndPicker.Date is not DateTimeOffset end || end.Date < start.Date)
            return;

        ScheduleMaintenanceButton.IsEnabled = false;
        try
        {
            var maintenanceId = MaintenanceDatabase.Schedule(DatabasePath, vehicle.Id,
                DateOnly.FromDateTime(start.Date), DateOnly.FromDateTime(end.Date));
            if (maintenanceId is null)
            {
                ShowMaintenanceMessage(InfoBarSeverity.Warning,
                    "Maintenance was not scheduled because the vehicle has a confirmed reservation or another maintenance period in these dates.");
                return;
            }
            LoadMaintenance(maintenanceId.Value);
            ResetAvailabilityResults();
            ShowMaintenanceMessage(InfoBarSeverity.Success,
                $"{vehicle.Make} {vehicle.Model} ({vehicle.LicensePlate}) will be unavailable from {start:dd/MM/yyyy} to {end:dd/MM/yyyy}.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowMaintenanceMessage(InfoBarSeverity.Error, "Maintenance could not be scheduled. Try again.");
        }
        finally
        {
            UpdateMaintenanceButton();
        }
    }

    private async void DeleteMaintenance_Click(object sender, RoutedEventArgs e)
    {
        if (MaintenanceTable.SelectedItem is not MaintenanceRecord record) return;
        var dialog = new ContentDialog
        {
            XamlRoot = MaintenanceTable.XamlRoot,
            Title = "Delete maintenance period?",
            Content = $"Remove the maintenance period for {record.VehicleDisplay}, from {record.StartDateDisplay} to {record.EndDateDisplay}?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            var deleted = MaintenanceDatabase.Delete(DatabasePath, record.Id);
            LoadMaintenance();
            ResetAvailabilityResults();
            ShowMaintenanceMessage(deleted ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                deleted ? "Maintenance period deleted. The vehicle is available again for those dates." :
                "Maintenance period no longer exists. The list has been refreshed.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowMaintenanceMessage(InfoBarSeverity.Error, "Maintenance period could not be deleted. Try again.");
        }
    }

    private void LoadCatalog()
    {
        CatalogMessage.IsOpen = false;
        try
        {
            _catalog = CarModelFeaturesDatabase.GetCatalog(DatabasePath);
            CatalogBrandPicker.ItemsSource = new[] { "All brands" }
                .Concat(_catalog.Select(item => item.Make).Distinct().OrderBy(make => make))
                .ToList();
            CatalogBrandPicker.SelectedIndex = 0;
            UpdateCatalogModels();
            ApplyCatalogFilters();
            if (_catalog.Count == 0)
                ShowCatalogMessage(InfoBarSeverity.Warning, "There are no models in the database.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            CatalogList.ItemsSource = null;
            ShowCatalogMessage(InfoBarSeverity.Error, "Catalog could not be loaded. Select 'Reload' to try again.");
        }
    }

    private void UpdateCatalogModels()
    {
        var brand = CatalogBrandPicker.SelectedItem as string;
        CatalogModelPicker.ItemsSource = _catalog
            .Where(item => brand is null || brand == "All brands" || item.Make == brand)
            .Select(item => new CarModel(item.Make, item.Model))
            .ToList();
        CatalogModelPicker.SelectedIndex = -1;
    }

    private void CatalogBrandPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogModelPicker is null) return;
        UpdateCatalogModels();
        ApplyCatalogFilters();
    }

    private void CatalogModelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyCatalogFilters();

    private void ApplyCatalogFilters()
    {
        if (CatalogList is null || CatalogCount is null || CatalogBrandPicker is null || CatalogModelPicker is null) return;
        var brand = CatalogBrandPicker.SelectedItem as string;
        var model = CatalogModelPicker.SelectedItem as CarModel;
        var filtered = _catalog
            .Where(item => (brand is null || brand == "All brands" || item.Make == brand)
                && (model is null || item.Make == model.Make && item.Model == model.Model))
            .ToList();
        CatalogList.ItemsSource = filtered;
        CatalogCount.Text = $"{filtered.Count} {(filtered.Count == 1 ? "model" : "models")}";
    }

    private void CatalogList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditPriceButton is not null)
            EditPriceButton.IsEnabled = CatalogList.SelectedItem is CarCatalogItem;
    }

    private async void EditPrice_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogList.SelectedItem is not CarCatalogItem item) return;

        var priceBox = new NumberBox
        {
            Header = "Rental price per day (€)",
            Minimum = 0.01,
            Maximum = 100000,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = item.DailyRate is decimal rate ? (double)rate : double.NaN
        };
        var error = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock { Text = $"{item.Make} {item.Model}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(priceBox);
        content.Children.Add(error);
        var dialog = new ContentDialog
        {
            XamlRoot = CatalogList.XamlRoot,
            Title = "Edit rental price",
            Content = content,
            PrimaryButtonText = "Save price",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (double.IsNaN(priceBox.Value) || priceBox.Value <= 0)
            {
                args.Cancel = true;
                error.Message = "Enter a price greater than zero.";
                error.IsOpen = true;
                return;
            }
            try
            {
                if (!CarModelFeaturesDatabase.UpdateDailyRate(DatabasePath,
                    new CarModel(item.Make, item.Model), (decimal)priceBox.Value))
                {
                    args.Cancel = true;
                    error.Message = "This model no longer exists. Cancel and reload prices.";
                    error.IsOpen = true;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
                args.Cancel = true;
                error.Message = "The price could not be saved. Try again.";
                error.IsOpen = true;
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            LoadCatalog();
            LoadFeatureBrands();
            ResetAvailabilityResults();
            ShowCatalogMessage(InfoBarSeverity.Success,
                $"The daily price for {item.Make} {item.Model} was updated.");
        }
    }

    private void ReloadCatalog_Click(object sender, RoutedEventArgs e) => LoadCatalog();

    private void ShowCatalogMessage(InfoBarSeverity severity, string message)
    {
        CatalogMessage.Severity = severity;
        CatalogMessage.Message = message;
        CatalogMessage.IsOpen = true;
    }

    private void LoadFeatureBrands()
    {
        FeaturesMessage.IsOpen = false;
        FeatureBrandPicker.ItemsSource = null;
        ClearFeatureModelSelection();
        try
        {
            FeatureBrandPicker.ItemsSource = CarModelFeaturesDatabase.GetBrands(DatabasePath);
            if (FeatureBrandPicker.Items.Count == 0)
                ShowFeaturesMessage(InfoBarSeverity.Warning, "There are no models in the database.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowFeaturesMessage(InfoBarSeverity.Error, "Loading failed. Select 'Reload features' to try again.");
        }
    }

    private void ClearFeatureModelSelection()
    {
        if (FeatureModelPicker is null || FeaturesList is null || FeatureModelHeading is null) return;
        FeatureModelPicker.ItemsSource = null;
        FeatureModelPicker.IsEnabled = false;
        FeaturesList.ItemsSource = null;
        FeatureModelHeading.Visibility = Visibility.Collapsed;
    }

    private void FeatureBrandPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearFeatureModelSelection();
        if (FeaturesMessage is null || FeatureModelPicker is null) return;
        FeaturesMessage.IsOpen = false;
        if (FeatureBrandPicker.SelectedItem is not string make) return;
        try
        {
            FeatureModelPicker.ItemsSource = CarModelFeaturesDatabase.GetModels(DatabasePath, make);
            FeatureModelPicker.IsEnabled = FeatureModelPicker.Items.Count > 0;
            if (!FeatureModelPicker.IsEnabled)
                ShowFeaturesMessage(InfoBarSeverity.Warning, "There are no models for this brand.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowFeaturesMessage(InfoBarSeverity.Error, "Models could not be loaded. Try reloading.");
        }
    }

    private void FeatureModelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FeaturesList is null || FeatureModelHeading is null || FeaturesMessage is null) return;
        FeaturesList.ItemsSource = null;
        FeatureModelHeading.Visibility = Visibility.Collapsed;
        FeaturesMessage.IsOpen = false;
        if (FeatureModelPicker.SelectedItem is not CarModel model) return;
        try
        {
            var features = CarModelFeaturesDatabase.GetFeatures(DatabasePath, model);
            FeaturesList.ItemsSource = features.Select(feature => new ModelFeatureDisplay(feature)).ToList();
            FeatureModelHeading.Text = model.ToString();
            FeatureModelHeading.Visibility = features.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (features.Count == 0)
                ShowFeaturesMessage(InfoBarSeverity.Warning, "No features were found for this model.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowFeaturesMessage(InfoBarSeverity.Error, "Features could not be loaded. Try reloading.");
        }
    }

    private void ReloadFeatures_Click(object sender, RoutedEventArgs e) => LoadFeatureBrands();

    private void ShowFeaturesMessage(InfoBarSeverity severity, string message)
    {
        FeaturesMessage.Severity = severity;
        FeaturesMessage.Message = message;
        FeaturesMessage.IsOpen = true;
    }

    private void SaveCustomer_Click(object sender, RoutedEventArgs e)
    {
        FormMessage.IsOpen = false;
        var firstName = FirstNameBox.Text.Trim();
        var lastName = LastNameBox.Text.Trim();
        var phone = PhoneBox.Text.Trim();
        var email = EmailBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(firstName))
        {
            ShowError("Enter the first name.", FirstNameBox);
            return;
        }
        if (string.IsNullOrWhiteSpace(lastName))
        {
            ShowError("Enter the last name.", LastNameBox);
            return;
        }
        var phoneDigits = Regex.Replace(phone, "[^0-9]", string.Empty);
        if (!Regex.IsMatch(phone, @"^\+?[0-9 ()-]+$") || phoneDigits.Length < 7 || phoneDigits.Length > 15)
        {
            ShowError("Enter a valid phone number with 7 to 15 digits.", PhoneBox);
            return;
        }
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email || !address.Host.Contains('.'))
        {
            ShowError("Enter a valid email address.", EmailBox);
            return;
        }
        SaveButton.IsEnabled = false;
        try
        {
            var customer = new Customer
            {
                FirstName = firstName,
                LastName = lastName,
                Phone = phone,
                Email = email
            };
            var customerId = CustomerDatabase.Save(DatabasePath, customer);
            _savedCustomer = customer;
            SetCustomerFieldsEnabled(false);
            ResetButton.Content = "New customer";
            FormMessage.Severity = InfoBarSeverity.Success;
            FormMessage.Title = "Registration successful";
            FormMessage.Message = $"Customer {firstName} {lastName} was saved.";
            FormMessage.IsOpen = true;
            LoadCustomers(customerId);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowError("Saving failed. Your entries remain in the form so you can try again.", FirstNameBox);
        }
        finally
        {
            SaveButton.IsEnabled = _savedCustomer is null;
        }
    }

    private void ShowError(string message, Control field)
    {
        FormMessage.Severity = InfoBarSeverity.Error;
        FormMessage.Title = "Registration failed";
        FormMessage.Message = message;
        FormMessage.IsOpen = true;
        field.Focus(FocusState.Programmatic);
    }

    private void ClearForm_Click(object sender, RoutedEventArgs e)
    {
        _savedCustomer = null;
        SetCustomerFieldsEnabled(true);
        SaveButton.IsEnabled = true;
        ResetButton.Content = "Clear";
        ClearFields();
        FormMessage.IsOpen = false;
    }

    private void LoadModels()
    {
        _reservationConfirmed = false;
        ResetAvailabilityResults();
        try
        {
            ModelPicker.ItemsSource = AvailabilityDatabase.GetModels(DatabasePath);
            ModelPicker.SelectedIndex = -1;
            CheckAvailabilityButton.IsEnabled = false;
            if (ModelPicker.Items.Count == 0)
                ShowAvailabilityMessage(InfoBarSeverity.Warning, "No models", "There are no vehicles registered in the database.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ModelPicker.ItemsSource = null;
            CheckAvailabilityButton.IsEnabled = false;
            ShowAvailabilityMessage(InfoBarSeverity.Error, "Loading failed", "Select 'Reload models' to try again.");
        }
    }

    private void ReloadModels_Click(object sender, RoutedEventArgs e) => LoadModels();

    private void ReserveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_reservationConfirmed || ReservationCustomerPicker.SelectedItem is not Customer customer ||
            !TryGetRentalDates(out var pickup, out var dropoff) ||
            AvailablePlatesList.SelectedItem is not AvailableCar car)
            return;

        ReserveButton.IsEnabled = false;
        try
        {
            var reservationId = AvailabilityDatabase.CreateReservation(DatabasePath, customer.Id,
                car.LicensePlate, pickup, dropoff);
            if (reservationId is null)
            {
                ResetAvailabilityResults();
                ShowAvailabilityMessage(InfoBarSeverity.Warning, "Vehicle no longer available",
                    "Check availability again and select another vehicle.");
                return;
            }

            _reservationConfirmed = true;
            LoadReservations(reservationId.Value);
            CheckAvailabilityButton.IsEnabled = false;
            AvailablePlatesList.IsEnabled = false;
            ShowAvailabilityMessage(InfoBarSeverity.Success, $"Reservation #{reservationId} confirmed",
                $"{car.LicensePlate} · {customer.FirstName} {customer.LastName} · " +
                $"{pickup:dd/MM/yyyy} to {dropoff:dd/MM/yyyy}. Change the customer, dates or model to start another rental.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowAvailabilityMessage(InfoBarSeverity.Error, "Reservation failed",
                "The reservation could not be saved. Try again.");
            ReserveButton.IsEnabled = true;
        }
    }

    private void ModelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ReservationInputsChanged();
    }

    private void CheckAvailability_Click(object sender, RoutedEventArgs e)
    {
        if (ReservationCustomerPicker.SelectedItem is not Customer || ModelPicker.SelectedItem is not CarModel model ||
            !TryGetRentalDates(out var pickup, out var dropoff))
            return;

        ResetAvailabilityResults();
        CheckAvailabilityButton.IsEnabled = false;
        try
        {
            var cars = AvailabilityDatabase.GetAvailableCars(DatabasePath, model, pickup, dropoff);
            AvailablePlatesList.ItemsSource = cars;
            AvailablePlatesList.Visibility = cars.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            AvailablePlatesHeading.Visibility = AvailablePlatesList.Visibility;
            ShowAvailabilityMessage(cars.Count > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                cars.Count > 0 ? $"{cars.Count} available vehicles found — select one to see the total price" : "No vehicle is available",
                $"{model} · {pickup:dd/MM/yyyy} to {dropoff:dd/MM/yyyy}");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowAvailabilityMessage(InfoBarSeverity.Error, "Availability check failed", "Try checking availability again.");
        }
        finally
        {
            CheckAvailabilityButton.IsEnabled = true;
        }
    }

    private void AvailablePlatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RentalPricePanel.Visibility = Visibility.Collapsed;
        if (ReserveButton is not null) ReserveButton.IsEnabled = false;
        if (ReservationCustomerPicker.SelectedItem is not Customer || AvailablePlatesList.SelectedItem is not AvailableCar car ||
            !TryGetRentalDates(out var pickup, out var dropoff))
            return;

        var days = AvailabilityDatabase.GetRentalDays(pickup, dropoff);
        var total = AvailabilityDatabase.CalculateRentalTotal(car.DailyRate, pickup, dropoff);
        DailyRateText.Text = $"Daily rate: €{car.DailyRate:0.00}";
        RentalDaysText.Text = $"Rental period: {days} {(days == 1 ? "day" : "days")}";
        TotalRentalPriceText.Text = $"Total: €{total:0.00}";
        RentalPricePanel.Visibility = Visibility.Visible;
        if (ReserveButton is not null) ReserveButton.IsEnabled = !_reservationConfirmed;
    }

    private void ResetAvailabilityResults()
    {
        // SelectionChanged can fire while InitializeComponent is still creating controls.
        if (AvailabilityMessage is null || AvailablePlatesList is null || AvailablePlatesHeading is null || RentalPricePanel is null)
            return;
        AvailabilityMessage.IsOpen = false;
        AvailablePlatesList.ItemsSource = null;
        AvailablePlatesList.Visibility = Visibility.Collapsed;
        AvailablePlatesHeading.Visibility = Visibility.Collapsed;
        RentalPricePanel.Visibility = Visibility.Collapsed;
        if (ReserveButton is not null) ReserveButton.IsEnabled = false;
        AvailablePlatesList.IsEnabled = !_reservationConfirmed;
    }

    private void ShowAvailabilityMessage(InfoBarSeverity severity, string title, string message)
    {
        AvailabilityMessage.Severity = severity;
        AvailabilityMessage.Title = title;
        AvailabilityMessage.Message = message;
        AvailabilityMessage.IsOpen = true;
    }

    private void ClearFields()
    {
        FirstNameBox.Text = string.Empty;
        LastNameBox.Text = string.Empty;
        PhoneBox.Text = string.Empty;
        EmailBox.Text = string.Empty;
        FirstNameBox.Focus(FocusState.Programmatic);
    }

    private void SetCustomerFieldsEnabled(bool enabled)
    {
        foreach (var child in CustomerFields.Children)
            if (child is Control control)
                control.IsEnabled = enabled;
    }

    private void LoadCustomers(long? selectId = null)
    {
        try
        {
            var previousId = selectId ?? (ReservationCustomerPicker.SelectedItem as Customer)?.Id;
            var customers = CustomerDatabase.GetAll(DatabasePath);
            var tableSelectionId = (CustomersTable.SelectedItem as Customer)?.Id;
            CustomersTable.ItemsSource = customers;
            CustomersTable.SelectedItem = customers.FirstOrDefault(customer => customer.Id == tableSelectionId);
            CustomerCountText.Text = $"{customers.Count} customers";
            ReservationCustomerPicker.ItemsSource = customers;
            ReservationCustomerPicker.SelectedItem = customers.FirstOrDefault(customer => customer.Id == previousId);
            if (customers.Count == 0)
                ShowAvailabilityMessage(InfoBarSeverity.Warning, "No customers", "Register a customer to create a reservation.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowAvailabilityMessage(InfoBarSeverity.Error, "Customers could not be loaded", "Select 'Reload customers' to try again.");
            ShowCustomerTableMessage(InfoBarSeverity.Error, "Customers could not be loaded. Select 'Reload customers' to try again.");
        }
    }

    private void ReloadCustomers_Click(object sender, RoutedEventArgs e) => LoadCustomers();

    private void ReloadCustomerTable_Click(object sender, RoutedEventArgs e)
    {
        CustomerTableMessage.IsOpen = false;
        LoadCustomers();
    }

    private void CustomersTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditCustomerButton is null || DeleteCustomerButton is null) return;
        EditCustomerButton.IsEnabled = CustomersTable.SelectedItem is Customer;
        DeleteCustomerButton.IsEnabled = EditCustomerButton.IsEnabled;
    }

    private void ShowCustomerTableMessage(InfoBarSeverity severity, string message)
    {
        CustomerTableMessage.Severity = severity;
        CustomerTableMessage.Message = message;
        CustomerTableMessage.IsOpen = true;
    }

    private async void EditCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (CustomersTable.SelectedItem is not Customer customer) return;
        var first = new TextBox { Header = "First name", Text = customer.FirstName, MaxLength = 100 };
        var last = new TextBox { Header = "Last name", Text = customer.LastName, MaxLength = 100 };
        var phone = new TextBox { Header = "Phone", Text = customer.Phone, MaxLength = 30 };
        var email = new TextBox { Header = "Email", Text = customer.Email, MaxLength = 254 };
        var error = new InfoBar { Severity = InfoBarSeverity.Error, IsClosable = false };
        var fields = new StackPanel { Spacing = 10 };
        fields.Children.Add(first);
        fields.Children.Add(last);
        fields.Children.Add(phone);
        fields.Children.Add(email);
        fields.Children.Add(error);
        var dialog = new ContentDialog
        {
            XamlRoot = CustomersTable.XamlRoot, Title = $"Edit customer #{customer.Id}",
            Content = fields, PrimaryButtonText = "Save changes", CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var digits = Regex.Replace(phone.Text.Trim(), "[^0-9]", string.Empty);
            string? validation = null;
            if (string.IsNullOrWhiteSpace(first.Text) || string.IsNullOrWhiteSpace(last.Text))
                validation = "First and last name are required.";
            else if (!Regex.IsMatch(phone.Text.Trim(), @"^\+?[0-9 ()-]+$") || digits.Length < 7 || digits.Length > 15)
                validation = "Enter a valid phone number with 7 to 15 digits.";
            else if (!MailAddress.TryCreate(email.Text.Trim(), out var address) || address.Address != email.Text.Trim() || !address.Host.Contains('.'))
                validation = "Enter a valid email address.";
            if (validation is not null)
            {
                args.Cancel = true;
                error.Message = validation;
                error.IsOpen = true;
                return;
            }
            try
            {
                var updated = new Customer { Id = customer.Id, FirstName = first.Text.Trim(),
                    LastName = last.Text.Trim(), Phone = phone.Text.Trim(), Email = email.Text.Trim() };
                if (!CustomerDatabase.Update(DatabasePath, updated))
                {
                    args.Cancel = true;
                    error.Message = "This customer no longer exists. Cancel and reload customers.";
                    error.IsOpen = true;
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
                args.Cancel = true;
                error.Message = "Changes could not be saved. Try again.";
                error.IsOpen = true;
            }
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            LoadCustomers();
            LoadReservations();
            ShowCustomerTableMessage(InfoBarSeverity.Success, "Customer details updated.");
        }
    }

    private async void DeleteCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (CustomersTable.SelectedItem is not Customer customer) return;
        var dialog = new ContentDialog
        {
            XamlRoot = CustomersTable.XamlRoot, Title = "Delete customer?",
            Content = $"Delete {customer.FullName} (#{customer.Id}) and all their reservations, including cancelled reservations? This action cannot be undone.",
            PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            var deleted = CustomerDatabase.Delete(DatabasePath, customer.Id);
            LoadCustomers();
            LoadReservations();
            ShowCustomerTableMessage(deleted ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                deleted ? "Customer and all their reservations deleted." : "Customer no longer exists. The list has been refreshed.");
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ShowCustomerTableMessage(InfoBarSeverity.Error, "Customer could not be deleted. Try again.");
        }
    }

    private bool TryGetRentalDates(out DateOnly pickup, out DateOnly dropoff)
    {
        pickup = default;
        dropoff = default;
        if (PickupDatePicker?.Date is not DateTimeOffset start || ReturnDatePicker?.Date is not DateTimeOffset end)
            return false;
        pickup = DateOnly.FromDateTime(start.Date);
        dropoff = DateOnly.FromDateTime(end.Date);
        return dropoff >= pickup;
    }

    private void ReservationCustomerPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) => ReservationInputsChanged();

    private void ReservationDate_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args) => ReservationInputsChanged();

    private void ReservationInputsChanged()
    {
        _reservationConfirmed = false;
        ResetAvailabilityResults();
        if (CheckAvailabilityButton is null || ReservationCustomerPicker is null || ModelPicker is null) return;
        CheckAvailabilityButton.IsEnabled = ReservationCustomerPicker.SelectedItem is Customer &&
            ModelPicker.SelectedItem is CarModel && TryGetRentalDates(out _, out _);
        if (PickupDatePicker.Date is DateTimeOffset pickup && ReturnDatePicker.Date is DateTimeOffset dropoff && dropoff.Date < pickup.Date)
            ShowAvailabilityMessage(InfoBarSeverity.Warning, "Invalid dates", "The return date must be the same as or later than the pickup date.");
    }
}
