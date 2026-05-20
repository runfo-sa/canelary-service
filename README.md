# Canelary Service (VSTS)

#### Es una solución para controlar en qué estado están las etiquetas que utiliza PiQuatro.

Este proyecto está dividido en tres proyectos de .NET 10:

- **Client** — Windows Service (`Microsoft.NET.Sdk.Worker`) que corre en las máquinas de producción.
- **Server** — Web API (`Microsoft.NET.Sdk.Web`, Minimal API) que recibe los reportes de los clientes y los persiste en SQL Server.
- **Core** — Biblioteca compartida con los DTOs y helpers comunes (HMAC, escaner de etiquetas, IP).
- **Tests/Canelary.Tests** — Tests unitarios (xUnit) que cubren los componentes críticos.

Para detalles internos del codigo (DI, options pattern, typed HTTP client, source-gen JSON, file logger, semaforos, DPAPI, etc.) ver [ARCHITECTURE.md](ARCHITECTURE.md).

### Ejemplo:

| Cliente        | Descripcion                | UltimaConexion      |
| -------------- | -------------------------- | ------------------- |
| 192.168.10.102 | Archivos Sobrantes         | 2024-04-22 11:24:00 |
| 192.168.42.25  | Okay                       | 2024-04-22 11:49:00 |
| 192.168.78.25  | Desactualizado             | 2024-04-22 11:50:00 |
| 192.168.78.40  | Multiples Instalaciones    | 2024-04-22 11:50:00 |
| 192.168.28.4   | Desactualizado y Sobrantes | 2024-04-22 11:50:00 |
| 192.168.10.25  | Desactualizado             | 2024-04-19 16:26:00 |
| 192.168.56.1   | Archivos Sobrantes         | 2024-04-15 11:36:00 |

---

# Instalación

### • Servidor

Para poder instalar el servidor es necesario contar con:

- Un servidor http capaz de hospedar [aplicaciones ASP.NET](https://learn.microsoft.com/es-mx/aspnet/core/host-and-deploy/iis/?view=aspnetcore-8.0).
- Una instancia de SQL Server capaz de utilizar [tablas en memoria](https://learn.microsoft.com/es-mx/sql/relational-databases/in-memory-oltp/requirements-for-using-memory-optimized-tables?view=sql-server-ver16#requirements).

Para que el servidor pueda comunicarse con la base de datos:

1. Preparar la base de datos ejecutando el SQL script **[deploy_VSTS.sql](deploy_VSTS.sql)** (raíz del repo).
2. Modificar la cadena de conexión **DefaultConnection** dentro de [Server/appsettings.json](Server/appsettings.json) para que apunte a la base.
3. Asignar el rol **vst_server** al usuario correspondiente.

#### Configurar secretos sin tocar appsettings.json

`Auth__ClavePublica`, `Auth__ClavePrivada` y `Auth__ClaveDescarga` pueden setearse como variables de entorno y sobreescriben los valores del `appsettings.json` (el host de ASP.NET aplica esa precedencia por defecto). En entornos productivos se recomienda dejar el bloque `"Auth"` vacio en el JSON y proveerlo via env vars.

#### Healthcheck

El servidor expone `GET /healthz` para monitoreo (no requiere auth, no cuenta para el rate limiter). Devuelve 200 si la conexion a la base de datos esta viva, 503 si no.

---

### • Cliente

Para poder instalar el cliente es necesario contar con:

- Una versión de Windows 10 1607+ o Windows 11 22000+, 64 bits.

Para instalar, ejecutar el script **[Client/installer.ps1](Client/installer.ps1)** como administrador.

#### Encriptado de secretos en reposo

A partir de la primera ejecucion el archivo `C:\ProgramData\Canelary Service\appsettings.json` se reescribe con la seccion `Auth` cifrada via Windows DPAPI (`DataProtectionScope.LocalMachine`). Los tres campos quedan como base64 ciphertext y el flag `"Encrypted": true` marca el estado.

Si se edita el archivo manualmente y se ponen las claves en plaintext (con `"Encrypted": false`), el servicio las leera y las migrara automaticamente al siguiente `Save()`.

> :warning: Si se reinstala Windows o se cambia la maquina, las claves cifradas no se podran leer y hay que regenerar el `appsettings.json` (borrarlo y dejar que el servicio escriba el default plaintext, o restaurar desde un backup que estuviera en plaintext).

---

# Funcionamiento

### • Cliente

> :warning: La primera vez que se ejecuta el servicio realiza un análisis para encontrar PiQuatro. Si no encuentra ninguna instalación, o encuentra múltiples, envia un reporte al servidor y entra en estado degradado (sigue corriendo, pero no enviara etiquetas hasta que se resuelva).

El servicio realiza tres tareas en paralelo dentro de un mismo `BackgroundService`:

1. **CheckEtiquetas** — envia al servidor la lista de archivos `.e01` encontrados en el directorio de PiQuatro, con nombre y hash SHA-256. Cada N minutos (default `IntervaloMins=5`).
2. **CheckPiQuatro** — analiza si hay multiples o ninguna instalacion de PiQuatro en la unidad configurada (default `C:`). Una vez al dia a la hora `PiquatroTime` (default 02:00).
3. **CheckUpdates** — consulta `/client-version`; si el hash del `Client.exe` local difiere, descarga `installer.ps1` y se auto-actualiza. Una vez al dia a la hora `UpdateTime` (default 00:00).

Las tres tareas comparten dos `SemaphoreSlim` para evitar pisarse durante la auto-actualizacion. Los errores transitorios en cualquier iteracion se loguean y el loop continua.

> El archivo de configuración esta en `C:\ProgramData\Canelary Service\appsettings.json`. Los logs en `C:\ProgramData\Canelary Service\Logs\yyyy_MM_dd.log`.

---

### • Servidor

Una vez iniciado se puede consultar la API en `http://{hostname}:{port}/swagger` y el healthcheck en `/healthz`.

El servidor escucha y responde a las peticiones de los clientes via _API REST_ (Minimal API). Cada reporte se persiste en la tabla `[service].[EstadoCliente]` (memory-optimized) y se detalla en archivos log.

> Logs en `C:\ProgramData\Canelary Server\{cliente}\yyyy_MM_dd.log`.

> Errores internos -> Visor de Eventos de Windows.

Esta limitado a **100 peticiones cada 10 minutos** por la fixed-window rate limiter. El endpoint `/healthz` esta exento.

---

### • Autenticación

**Reportes (POST `/validate-client`, `/multiple-installations`)**: el cliente firma cada request con HMAC-SHA256 usando `ClavePublica` como mensaje y `ClavePrivada` como clave, y envia los headers:

```
request-key:  <ClavePublica>
request-hash: <base64(HMAC-SHA256(ClavePrivada, ClavePublica))>
```

El servidor recomputa el hash y lo compara en **tiempo constante** (`CryptographicOperations.FixedTimeEquals`). El POST `/not-installed` no requiere auth (es solo telemetria).

**Descargas (GET `/get-client`, `/installer`)**: la clave de descarga se manda en `Authorization: Bearer <ClaveDescarga>`. El servidor sigue aceptando `?key=<ClaveDescarga>` como fallback transitorio para no romper clientes ya desplegados (el `installer.ps1` actual todavia usa ese esquema).

Las tres claves se configuran en:

- Server: `appsettings.json` seccion `"Auth"` o env vars `Auth__ClavePublica` etc.
- Client: `C:\ProgramData\Canelary Service\appsettings.json` seccion `"Auth"` (cifrada via DPAPI a partir del primer arranque).

#### HTTPS

El esquema HTTP/HTTPS del cliente es configurable via `"Server.Scheme"` en el JSON del cliente (default `"http"`). Para migrar a HTTPS:

1. En el server: configurar un binding TLS en Kestrel (via `appsettings.json` -> `Kestrel:Endpoints:Https`) y descomentar `app.UseHttpsRedirection()` en [Server/Server.cs](Server/Server.cs).
2. En cada cliente: setear `"Server": { "Scheme": "https", ... }` en el JSON.
3. Eventualmente: cerrar el binding HTTP del server una vez que todos los clientes hayan migrado.

---

# Desarrollo

### Requisitos

- .NET SDK 10.0.201+ (verificar con `dotnet --version`).
- Visual Studio 2022 17.13+, Rider 2025.1+ o VS Code con extension C#.

### Build + tests

```powershell
# Compilar todo en Release
dotnet build Service.sln -c Release

# Compilar tratando warnings de async como errores (igual que CI)
dotnet build Service.sln -c Release /warnaserror:CS4014,CS8618,CS1998

# Correr la suite de tests (36 tests, ~100ms)
dotnet test Service.sln -c Release
```

### CI

[.github/workflows/ci.yml](.github/workflows/ci.yml) corre dos jobs en paralelo:

- **build-and-test-linux** (`ubuntu-latest`) — job primario, porque el Server productivo corre en Linux. El Client targetea `net10.0-windows` pero compila gracias a `<EnableWindowsTargeting>true</EnableWindowsTargeting>` en su csproj. Los tests de DPAPI (`SecretProtectorTests`) se auto-saltean en non-Windows.
- **build-and-test-windows** (`windows-latest`) — valida que el Client y los tests DPAPI corran sobre su target real.

Ambos jobs hacen: `dotnet restore` → `dotnet build -c Release /warnaserror:CS4014,CS8618,CS1998` → `dotnet test` con `XPlat Code Coverage` → upload de `TestResults/` como artifact. Ambos deben pasar para que un PR sea mergeable.

Para que `Analysis.CheckClient` no intente escribir en `/usr/share` en Linux, el job de Linux setea la env var `CANELARY_SERVER_LOG_BASE=${{ runner.temp }}/canelary-server-logs` que redirige los logs por-cliente a una carpeta del runner.
