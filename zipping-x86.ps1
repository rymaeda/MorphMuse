# Caminho para o DLL (ajuste conforme sua estrutura)
$dllPath = "C:\Users\rymae\Source\MorphMuse\bin\x86\Release\MorphMuse.dll"

# Obter nome base do DLL
$dllName = [System.IO.Path]::GetFileNameWithoutExtension($dllPath)

# Obter versão do arquivo sem carregar o assembly
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dllPath)
$version = $versionInfo.FileVersion

# Detectar arquitetura via leitura binária (sem carregar)
$arch = "unknown"
$fs = [System.IO.File]::OpenRead($dllPath)
$br = New-Object System.IO.BinaryReader($fs)

try {
    $fs.Seek(0x3C, 'Begin') | Out-Null
    $peOffset = $br.ReadInt32()
    $fs.Seek($peOffset + 4, 'Begin') | Out-Null
    $machine = $br.ReadUInt16()

    switch ($machine) {
        0x014C { $arch = "x86" }
        0x8664 { $arch = "x64" }
        default { $arch = "unknown" }
    }
}
finally {
    $br.Close()
    $fs.Close()
}

# Criar nome do ZIP
$zipName = "$dllName-v$version-$arch.zip"

# Definir pasta Release do projeto
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $projectDir "releases"

# Criar diretório se não existir
if (!(Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

# Caminho completo do ZIP
$zipPath = Join-Path $releaseDir $zipName

# Criar o ZIP no diretório Release
Compress-Archive -Path $dllPath -DestinationPath $zipPath -Force

Write-Host "✅ Arquivo gerado: $zipPath"