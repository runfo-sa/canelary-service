# Instalacion del Cliente en Windows

El Cliente de Canelary se instala como un **servicio de Windows** que controla el estado de las etiquetas en el equipo. El script [`installer.ps1`](Client/installer.ps1) descarga el ejecutable desde el Server y lo registra como servicio.

## Requisitos previos

- Windows con PowerShell 5.1 o superior.
- Permisos de **Administrador** en el equipo.
- Conectividad de red con el Server de Canelary (por defecto `http://localhost:5262`).

## Pasos

1. Abrir **PowerShell como Administrador**.

2. Si el Server no esta en `localhost`, editar la variable `$Uri` en [`installer.ps1`](Client/installer.ps1) apuntando al Server correcto. Por ejemplo:

   ```powershell
   $Uri = "http://<ip-o-host-del-server>:5262/get-client"
   ```

3. Permitir la ejecucion del script en la sesion actual:

   ```powershell
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   ```

4. Ejecutar el instalador:

   ```powershell
   .\installer.ps1
   ```

El script va a:

- Crear la carpeta `C:\soft\Canelary\Service\`.
- Descargar `Client.exe` desde el Server.
- Registrar el servicio **VSTC** (`Canelary - Controlador de Etiquetas`) con arranque automatico retrasado.
- Iniciar el servicio (o reiniciarlo si ya estaba instalado).

## Verificar la instalacion

```powershell
Get-Service -Name VSTC
```

El estado debe figurar como `Running`.

## Ajustar la configuracion (opcional)

En el primer arranque el servicio crea `C:\ProgramData\Canelary Service\appsettings.json` con valores por defecto. Solo hace falta editarlo si el Server **no** esta en `localhost:5262`:

1. Detener el servicio:

   ```powershell
   Stop-Service -Name VSTC
   ```

2. Editar `C:\ProgramData\Canelary Service\appsettings.json` ajustando `Server.Ip` / `Server.Port`:

   ```json
   {
     "Server": {
       "Ip": "<ip-o-host-del-server>",
       "Port": "5262"
     }
   }
   ```

3. Reiniciar el servicio:

   ```powershell
   Start-Service -Name VSTC
   ```

## Reinstalar / actualizar

Volver a ejecutar `.\installer.ps1` con permisos de Administrador. Si el servicio ya existe, el script descarga la version nueva del ejecutable y reinicia el servicio.

## Desinstalar

```powershell
Stop-Service -Name VSTC
sc.exe delete VSTC
Remove-Item -Recurse -Force C:\soft\Canelary\Service
```
