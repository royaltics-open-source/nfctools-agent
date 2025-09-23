# NFC Tools Agent

This project is a .NET application for interacting with NFC (Near Field Communication) devices. It allows you to read, write, and manage NFC cards using the PCSC library.

## Features
- Reading and writing NFC cards
- Support for multiple NFC readers
- Integration with the PCSC library (pcsc-sharp)
- Use of Newtonsoft.Json for handling JSON data
- Packaged with Fody and Costura for easy distribution

## Requirements
- .NET Framework 4.7.2 or higher
- A PCSC-compatible NFC reader

## Installation
1. Clone the repository:
```sh
git clone https://github.com/royaltics-open-source/nfctools-agent.git
```
2. Open the `nfcTools.sln` solution in Visual Studio.
3. Restore the NuGet packages if necessary.
4. Build the project.

## Usage
Run the `NFCToolsAgent.exe` file from the `bin/Debug` or `bin/Release` folder.

## Project Structure
- `NfcService.cs`: Main logic for interacting with NFC devices
- `Program.cs`: Application entry point
- `App.config`: Application configuration
- `Resources/`: Graphic resources
- `packages/`: NuGet dependencies

## Main Dependencies
- [PCSC (pcsc-sharp)](https://github.com/danm-de/pcsc-sharp)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [Fody](https://github.com/Fody/Fody)
- [Costura.Fody](https://github.com/Fody/Costura)

## License
This project is licensed under the MIT License. See the `LICENSE` file for more details.

## Author
royaltics-open-source
@royaltics-solutions