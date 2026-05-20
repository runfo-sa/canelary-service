# Instalador para el Servicio controlador de etiquetas de Canelary
# Autor: Agustin Marco <agustin.marco@runfo.com.ar>

$ServiceName = "VSTC"
$DisplayName = "Canelary - Controlador de Etiquetas"
$FolderPath = "C:\soft\Canelary\Service\"
$FilePath = "C:\soft\Canelary\Service\Client.exe"
$Uri = "http://localhost:5262/get-client?key={0}" -f $env:Auth__ClaveDescarga

New-Item -ItemType Directory -Force -Path $FolderPath
Invoke-WebRequest -Uri $Uri -OutFile $FilePath

$Service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($Service -eq $null)
{
	$params = @{
	  Name = $ServiceName
	  BinaryPathName = $FilePath
	  DisplayName = $DisplayName
	  Description = "Controla el estado de etiquetas en este equipo."
	}

	New-Service @params
	sc.exe config $ServiceName start= delayed-auto
	Start-Service -Name $ServiceName
}
else
{
	Get-Service -Name $ServiceName
	Restart-Service -Name $ServiceName
}
