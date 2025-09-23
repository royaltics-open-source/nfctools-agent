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

[HTTP Listener & API Routes]
The application starts an HTTP server at http://localhost:1616/pcsc/ by default. This allows external applications to interact with the NFC reader via HTTP requests.

## Available Routes

**GET /pcsc/verifydevice**

Checks if an NFC reader is available.
Response: `{ status: true/false, code: 200, message: "Reader active"/"Not available" }`

**GET /pcsc/getuidcard**

Reads the UID of the NFC card.
Response: `{ status: true/false, code: 200, message: "OK"/"Read failed", data: <hexUID> }`

**POST /pcsc/readcard**

Reads a sector from the card.
Body: `{ Sector: <int>, AuthKey: <string> }`
Response: `{ status: true/false, code: 200, message: "OK"/"Read failed", data: <hex> }`

**POST /pcsc/writecard**

Writes data to a sector.
Body: `{ Sector: <int>, AuthKey: <string>, Data: <string> }`
Response: `{ status: true, code: 200, message: "Write OK" }`

**POST /pcsc/encodingcard**

Encodes the UID and last digits in a sector, and changes the keys.
Body: `{ Uid: <string>, LastDigits: <string>, Sector: <int>, AuthKey: <string>, NewKeyA: <string>, NewKeyB: <string>, AccessBits: <int> }`
Response: `{ status: true/false, code: 200/400/500, message: <string>, data: {...} }`

All responses are in JSON format. For errors or unknown routes, the server returns a 404 or 500 code with a descriptive message.

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