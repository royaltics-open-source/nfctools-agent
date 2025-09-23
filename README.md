# NFC Tools Agent

Este proyecto es una aplicación .NET para interactuar con dispositivos NFC (Near Field Communication). Permite leer, escribir y gestionar tarjetas NFC utilizando la librería PCSC.

## Características
- Lectura y escritura de tarjetas NFC
- Soporte para múltiples lectores NFC
- Integración con la librería PCSC (pcsc-sharp)
- Uso de Newtonsoft.Json para manejo de datos JSON
- Empaquetado con Fody y Costura para facilitar la distribución

## Requisitos
- .NET Framework 4.7.2 o superior
- Un lector NFC compatible con PCSC

## Instalación
1. Clona el repositorio:
   ```sh
   git clone https://github.com/royaltics-open-source/nfctools-agent.git
   ```
2. Abre la solución `nfcTools.sln` en Visual Studio.
3. Restaura los paquetes NuGet si es necesario.
4. Compila el proyecto.

## Uso
Ejecuta el archivo `NFCToolsAgent.exe` desde la carpeta `bin/Debug` o `bin/Release`.

## Estructura del proyecto
- `NfcService.cs`: Lógica principal para la interacción con dispositivos NFC
- `Program.cs`: Punto de entrada de la aplicación
- `App.config`: Configuración de la aplicación
- `Resources/`: Recursos gráficos
- `packages/`: Dependencias NuGet

## Dependencias principales
- [PCSC (pcsc-sharp)](https://github.com/danm-de/pcsc-sharp)
- [Newtonsoft.Json](https://www.newtonsoft.com/json)
- [Fody](https://github.com/Fody/Fody)
- [Costura.Fody](https://github.com/Fody/Costura)

## Licencia
Este proyecto está bajo la licencia MIT. Consulta el archivo `LICENSE` para más detalles.

## Autor
royaltics-open-source
@royaltics-solutions