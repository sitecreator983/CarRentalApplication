# Car Rental Application

Εφαρμογή desktop διαχείρισης ενοικίασης αυτοκινήτων, υλοποιημένη με WinUI 3 και .NET 8.

## Απαιτήσεις

- Windows 10 (έκδοση 17763 ή νεότερη) ή Windows 11
- .NET 8 SDK
- Visual Studio 2022 με τα workloads **.NET desktop development** και **Windows application development**
- Ενεργοποιημένο το **Developer Mode** των Windows για εκτέλεση της εφαρμογής από το Visual Studio

## Άνοιγμα του GUI

1. Κατεβάστε ή κάντε clone το repository σε υπολογιστή Windows.
2. Ανοίξτε το `CarRentalApplication.slnx` με το Visual Studio 2022.
3. Επιλέξτε `CarRentalApplication` ως startup project.
4. Επιλέξτε διαμόρφωση `Debug` και πλατφόρμα `x64`.
5. Πατήστε **F5** για build και εκκίνηση του GUI.

Στην πρώτη εκτέλεση, η εφαρμογή δημιουργεί τη βάση SQLite στον τοπικό φάκελο δεδομένων της εφαρμογής. Δεν απαιτείται ξεχωριστό αρχείο βάσης δεδομένων στο repository.

## Δομή

- `CarRentalApplication/` — WinUI 3 desktop εφαρμογή και κώδικας
- `tools/DatabaseMaintenance/` — βοηθητικό εργαλείο συντήρησης και ελέγχου βάσης δεδομένων
