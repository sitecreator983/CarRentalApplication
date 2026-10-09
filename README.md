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

## Εγκατάσταση από GitHub Releases

Τα έτοιμα πακέτα εγκατάστασης διατίθενται στη σελίδα [Releases](https://github.com/sitecreator983/CarRentalApplication/releases/latest). Για δοκιμαστική εγκατάσταση εκτός Microsoft Store:

1. Κατεβάστε τα `CarRentalApplication.cer` και `CarRentalApplication_x64.appinstaller` από το τελευταίο Release.
2. Εμπιστευτείτε το δημόσιο πιστοποιητικό για τον τρέχοντα λογαριασμό Windows:

   ```powershell
   Import-Certificate -FilePath .\CarRentalApplication.cer -CertStoreLocation Cert:\CurrentUser\TrustedPeople
   ```

3. Ανοίξτε το `CarRentalApplication_x64.appinstaller` και επιλέξτε **Install**.

Το πακέτο υπογράφεται με αυτοϋπογεγραμμένο πιστοποιητικό δοκιμών. Αυτό απαιτεί χειροκίνητη εμπιστοσύνη του πιστοποιητικού σε κάθε υπολογιστή· για ευρεία διανομή προτιμάται πιστοποιητικό από έμπιστη αρχή ή το Microsoft Store. Το ιδιωτικό κλειδί του πιστοποιητικού δεν διανέμεται και δεν πρέπει να ανέβει στο GitHub.

## Δομή

- `CarRentalApplication/` — WinUI 3 desktop εφαρμογή και κώδικας
- `tools/DatabaseMaintenance/` — βοηθητικό εργαλείο συντήρησης και ελέγχου βάσης δεδομένων
