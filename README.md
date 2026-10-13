# WinnetPort

A small, single-target TCP connectivity scanner for **Windows desktop**. It is intended for learning and for testing devices you own or have explicit permission to assess.

## Features

- GUI with target hostname/IP address, **Port range / Custom ports** modes, and per-port timeout.
- Custom port lists support individual ports and inclusive ranges such as `22, 80, 443, 8000-8005`.
- Invalid ports and ranges are rejected, and duplicate ports are scanned once.
- Asynchronous TCP checks so the desktop interface stays responsive.
- Up to 24 concurrent connections; up to 4,096 TCP ports per scan.
- Live results, progress bar, and totals for open/closed/timed out/other responses.
- Cancel in-progress scans, filter to open ports, and export all results to CSV.
- No NuGet packages; source is included.

## Run on Windows

1. Install **Visual Studio** with the **.NET desktop development** workload and the **.NET 10 SDK**.
2. Open `PortScanner.WinForms/PortScanner.WinForms.csproj` in Visual Studio.
3. Press **F5** or click **Start**.

Alternatively, on Windows with .NET 10 SDK installed:

```powershell
dotnet run --project .\PortScanner.WinForms\PortScanner.WinForms.csproj
```

## Publish a Windows executable

From the extracted project root, in PowerShell:

```powershell
dotnet publish .\PortScanner.WinForms\PortScanner.WinForms.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Look in `PortScanner.WinForms\bin\Release\net10.0-windows\win-x64\publish\` for the app.

## Basic use

1. Leave `127.0.0.1` to check your own computer or enter another authorized hostname/IP.
2. Select a scan mode:
   - **Port range**: Set the Start port and End port (for example, 1 through 1024).
   - **Custom ports**: Select the **Custom ports** radio button and enter a comma-separated list, for example `22, 80, 443, 8080` or `21-23, 80, 443, 8000-8005`.
3. Click **Start scan**. You can click **Cancel** at any time.
4. Enable **Show open ports only** to filter the table, or select **Export CSV** for all captured results.

Custom ports accept values from 1 to 65535. Repeated entries are removed automatically. Empty entries, backward ranges, and nonnumeric entries show a validation message. The 4,096 unique-port limit applies to both scan modes.

## Interpreting status

- **Open:** A TCP connection was accepted.
- **Closed:** The connection was refused.
- **Timed out:** No connection was established before the configured timeout.
- **Unreachable/Error:** The OS reported a connectivity or socket issue.

This checks TCP connections only; it does not scan UDP, identify services, assess vulnerabilities, or guarantee a port is closed when packets are filtered. Firewall behavior, routing, and the timeout can affect results.
- One host and at most 4,096 ports (consecutive or custom-selected) per scan.
- DNS names with both IPv4 and IPv6 addresses prefer IPv4; the app checks one resolved address.
- Results are not persisted unless exported.
