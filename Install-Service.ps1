$serviceName = "DmtSftp"
$description = "SFTP file broker to support Wordpress/Netforum ETL integrations"
$binaryPath = "C:\Users\jcory\source\repos\dmt-sftp\src\DMT.SFTP.Service\bin\Release\netcoreapp3.0\DMT.SFTP.Service.exe"

if(Get-Service $serviceName -ErrorAction SilentlyContinue)
{
    $oldService = Get-WmiObject -Class Win32_Service -Filter "name=$serviceName"
    $oldService.delete()
    "Deleted old service"
}
else 
{
    $serviceName + " does not exist"    
}

"Installing new instance of " + $serviceName

New-Service -Name $serviceName -BinaryPathName $binaryPath -DisplayName "DMT SFTP" -StartupType Automatic -Description $description

"Install complete. Starting " + $serviceName

Start-Service $serviceName

$serviceName + " started!"