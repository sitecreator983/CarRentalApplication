
# Car Rental Application

Desktop car rental management application developed using WinUI 3 and .NET 8.

## Requirements

- Windows 10 (version 17763 or later) or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with the **.NET desktop development** and **Windows application development** workloads
- Windows **Developer Mode** enabled to run the application from Visual Studio

## Launching the GUI

1. Download or clone the repository to a Windows computer.
2. Open `CarRentalApplication.slnx` with Visual Studio 2022.
3. Set `CarRentalApplication` as the startup project.
4. Select the `Debug` configuration and `x64` platform.
5. Press **F5** to build and launch the GUI.

On the first launch, the application automatically creates the SQLite database in the local application data directory. No separate database file is required in the repository.

## Installation from GitHub Releases

Prebuilt installation packages are available on the [Releases](https://github.com/sitecreator983/CarRentalApplication/releases/latest) page.

For installation outside the Microsoft Store:

1. Download `CarRentalApplication.cer` and `CarRentalApplication_x64.appinstaller` from the latest release.
2. Trust the public certificate for the current Windows user account:

   ```powershell
   Import-Certificate -FilePath .\CarRentalApplication.cer -CertStoreLocation Cert:\CurrentUser\TrustedPeople
   ```

3. Open `CarRentalApplication_x64.appinstaller` and select **Install**.

The installation package is signed with a self-signed test certificate. This requires manually trusting the certificate on each computer. For public distribution, a certificate issued by a trusted Certificate Authority (CA) or distribution through the Microsoft Store is recommended.

The certificate's private key is not distributed and must never be uploaded to GitHub.

## Project Structure

-`CarRentalApplication/` - WinUI 3 desktop application and source code
-`tools/DatabaseMaintenance/` - Utility tool for database maintenance and validation
