<#
.SYNOPSIS
  Ozaylar Filo dağıtım paketlerini üretir.
  1) İstemci (WPF): self-contained win-x64 publish + Velopack paketi (Setup.exe + delta güncellemeler)  -> artifacts\istemci
  2) Sunucu servisi: self-contained win-x64 publish                                                    -> artifacts\sunucu
  3) (varsa) Inno Setup ile sunucu kurulum paketi                                                     -> artifacts\OzaylarFiloSunucuKurulum.exe

.EXAMPLE
  .\build\paketle.ps1 -Surum 1.0.1
#>
param(
    [Parameter(Mandatory)] [string]$Surum,
    [string]$Konfig = 'Release'
)
$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot
$cikti = Join-Path $kok 'artifacts'
New-Item -ItemType Directory -Force $cikti | Out-Null

Write-Host "== Testler" -ForegroundColor Cyan
dotnet test (Join-Path $kok 'Ozaylar.Filo.slnx') -c $Konfig
if ($LASTEXITCODE -ne 0) { throw 'Testler başarısız; paketleme durduruldu.' }

Write-Host "== İstemci publish" -ForegroundColor Cyan
$istemciPub = Join-Path $cikti 'istemci-publish'
dotnet publish (Join-Path $kok 'src\Ozaylar.Filo.Desktop') -c $Konfig -r win-x64 --self-contained true -p:Version=$Surum -o $istemciPub
if ($LASTEXITCODE -ne 0) { throw 'İstemci publish başarısız.' }

Write-Host "== Velopack paketi" -ForegroundColor Cyan
Push-Location $kok
dotnet vpk pack --packId OzaylarFilo --packVersion $Surum --packDir $istemciPub --mainExe OzaylarFilo.exe `
    --packTitle 'Ozaylar Filo' --outputDir (Join-Path $cikti 'istemci')
# İmzalama (önerilir, Smart App Control için): --signParams "/a /fd sha256 /tr http://timestamp.digicert.com /td sha256"
Pop-Location
if ($LASTEXITCODE -ne 0) { throw 'Velopack paketleme başarısız.' }

Write-Host "== Sunucu publish" -ForegroundColor Cyan
$sunucuPub = Join-Path $cikti 'sunucu'
dotnet publish (Join-Path $kok 'src\Ozaylar.Filo.Sunucu') -c $Konfig -r win-x64 --self-contained true -p:Version=$Surum -o $sunucuPub
if ($LASTEXITCODE -ne 0) { throw 'Sunucu publish başarısız.' }
Copy-Item (Join-Path $kok 'installer\sunucu-hazirla.ps1') $sunucuPub -Force

$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    Write-Host "== Inno Setup" -ForegroundColor Cyan
    & $iscc "/DSurum=$Surum" "/DKaynak=$cikti" (Join-Path $kok 'installer\OzaylarFiloSunucu.iss')
} else {
    Write-Warning 'Inno Setup 6 bulunamadı; sunucu kurulum .exe üretilmedi (artifacts\sunucu klasörü elle kurulabilir).'
}

Write-Host "`nTamam. Çıktılar: $cikti" -ForegroundColor Green
Write-Host "Güncelleme yayınlamak için artifacts\istemci içeriğini sunucudaki \\SUNUCU\OzaylarFiloGuncelleme paylaşımına kopyalayın."
