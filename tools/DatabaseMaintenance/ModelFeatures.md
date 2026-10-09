# Χαρακτηριστικά μοντέλων

Η εφαρμογή δημιουργεί αυτόματα τον πίνακα `CarModelFeatures` στην υπάρχουσα
`carrental.db` κατά την επόμενη εκκίνηση. Υπάρχει μία εγγραφή για κάθε συνδυασμό
`Make` / `Model`, ανεξάρτητα από τους αριθμούς κυκλοφορίας του πίνακα `Cars`.
Η καρτέλα «Χαρακτηριστικά μοντέλων» διαβάζει από αυτόν τον πίνακα.

Τα υπάρχοντα πεδία `ModelYear`, `Category`, `Transmission`, `EngineCc` και
`Horsepower` αντιγράφονται μόνο όταν συμφωνούν όλα τα αυτοκίνητα του μοντέλου.
Τα νέα πεδία παραμένουν `NULL` μέχρι να καταχωρηθούν πραγματικές τιμές.
Οι υπάρχουσες εγγραφές χαρακτηριστικών δεν αντικαθίστανται κατά την εκκίνηση.

Πεδία εξοπλισμού με τιμές `1` (ναι), `0` (όχι), `NULL` (άγνωστο):
`CarPlay`, `AndroidAuto`, `AirConditioning`, `ParkingSensors`, `RearCamera`.
Πεδία κειμένου: `Roof`, `Color`, `FuelType`, `AdditionalFeatures`.
Θετικοί ακέραιοι: `Seats`, `Doors`.
Το χρώμα και ο εξοπλισμός περιγράφουν το μοντέλο του καταλόγου· αν τα φυσικά
αυτοκίνητα διαφέρουν, μπορεί να καταχωρηθεί σχετική περιγραφή.

Για καταχώριση μέσω SQLite, προσαρμόστε το παρακάτω παράδειγμα στις πραγματικές
τιμές του στόλου (οι τιμές εδώ είναι ενδεικτικές):

```sql
UPDATE CarModelFeatures
SET CarPlay = 1,
    AndroidAuto = 1,
    Roof = 'Συμβατική',
    Color = 'Λευκό',
    FuelType = 'Βενζίνη',
    Seats = 5,
    Doors = 5,
    AirConditioning = 1,
    ParkingSensors = 1,
    RearCamera = 0,
    AdditionalFeatures = 'Bluetooth'
WHERE Make = 'Skoda' AND Model = 'Kamiq';
```

Στη συνέχεια πατήστε «Ανανέωση χαρακτηριστικών» και επιλέξτε ξανά brand/μοντέλο.
Η καρτέλα προβάλλει στοιχεία· η καταχώριση γίνεται στη βάση.

Έλεγχοι:

```powershell
dotnet run --project tools/DatabaseMaintenance -- --test-features
dotnet run --project tools/DatabaseMaintenance -- --test-availability
```
