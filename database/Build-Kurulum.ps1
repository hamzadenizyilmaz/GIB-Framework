[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$sources = @('01-Users-Tenants-Invoices-Schema.sql', '02-Invoice-Audit-Protection-Triggers.sql', '03-Tenant-Row-Security.sql',
    '04-System-Seed-Data.sql', '05-Tax-Office-Seed-Data.sql', '06-Upgrade-Schema.sql', '07-Upgrade-Schema.sql',
    '08-Upgrade-Schema.sql')

function Read-SqlFile([string]$Name) {
    ([IO.File]::ReadAllText((Join-Path $PSScriptRoot $Name)) -replace '\r\n', "`n").TrimEnd()
}

$body = ($sources | ForEach-Object { Read-SqlFile $_ }) -join "`n`n"
$outputs = [ordered]@{
    'GIBFramework-Plesk-Install.sql' = $body
    'GIBFramework-Local-Install.sql' = (Read-SqlFile '00-Local-Database.sql') + "`n`n" + $body
}

foreach ($name in $outputs.Keys) {
    $path = Join-Path $PSScriptRoot $name
    if ($Check) {
        if (!(Test-Path -LiteralPath $path) -or (Read-SqlFile $name) -cne $outputs[$name]) {
            throw "$name güncel değil. Build-Kurulum.ps1 betiğini çalıştırın."
        }
    } else {
        [IO.File]::WriteAllText($path, $outputs[$name] + "`n", [Text.UTF8Encoding]::new($true))
    }
    "$name hazır."
}
