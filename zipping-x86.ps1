# Caminho para o DLL (ajuste conforme sua estrutura)
$dllPath = "C:\Users\rymae\Source\MorphMuse\bin\x86\Release\MorphMuse.dll"

# Obter nome base do DLL
$dllName = [System.IO.Path]::GetFileNameWithoutExtension($dllPath)

# Obter versão do assembly
$assembly = [System.Reflection.AssemblyName]::GetAssemblyName($dllPath)
$version = $assembly.Version.ToString()

# Detectar arquitetura
$arch = switch ($assembly.ProcessorArchitecture) {
    "MSIL" { "x86" }  # ou "AnyCPU", depende do seu build
    "Amd64" { "x64" }
    default { "unknown" }
}

# Criar nome do ZIP
$zipName = "$dllName-v$version-$arch.zip"

# Definir pasta Release do projeto
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseDir = Join-Path $projectDir "releases"

# Caminho completo do ZIP
$zipPath = Join-Path $releaseDir $zipName

# Criar o ZIP no diretório Release
Compress-Archive -Path $dllPath -DestinationPath $zipPath -Force

Write-Host "Arquivo gerado: $zipPath"