<#
.SYNOPSIS
  Sunucu bilgisayarı Ozaylar Filo için hazırlar (yönetici olarak çalıştırılmalı).
  - SQL Server 2025 Express'i (yoksa) karma kimlik doğrulama + Türkçe collation + TCP 1433 ile kurar
  - Güvenlik duvarında yalnızca özel/etki alanı ağları için 1433/TCP açar
  - Yedek klasörünü oluşturur ve SQL servis hesabına yazma izni verir
  - Güncelleme paylaşımını (\\BILGISAYAR\OzaylarFiloGuncelleme) oluşturur
  - Veritabanını kurar (Ozaylar.Filo.Sunucu.exe kur) ve Windows servisini kaydeder/başlatır
.PARAMETER SqlKurulum
  SQL Server 2025 Express kurulum dosyası (SQLEXPR_x64_TRK.exe / SQLEXPR_x64_ENU.exe). Verilmezse SQL'in kurulu olduğu varsayılır.
#>
param(
    [string]$SqlKurulum,
    [string]$Ornek = 'SQLEXPRESS',
    [string]$YedekKlasoru = 'C:\OzaylarFilo\Yedek',
    [string]$GuncellemeKlasoru = 'C:\OzaylarFilo\Guncelleme',
    [string]$UygulamaSifresi
)
$ErrorActionPreference = 'Stop'
$burasi = Split-Path -Parent $MyInvocation.MyCommand.Path
function Adim($m) { Write-Host "`n== $m" -ForegroundColor Cyan }

# ---------------------------------------------------------------- SQL Server Express
$servisAdi = "MSSQL`$$Ornek"
if (-not (Get-Service $servisAdi -ErrorAction SilentlyContinue)) {
    if (-not $SqlKurulum) { throw "SQL Server '$Ornek' örneği bulunamadı ve -SqlKurulum verilmedi." }
    Adim 'SQL Server 2025 Express kuruluyor (birkaç dakika sürebilir)'
    $saSifre = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 20 | ForEach-Object { [char]$_ }) + '!7a'
    & $SqlKurulum /Q /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=$Ornek /SECURITYMODE=SQL /SAPWD=$saSifre `
        /SQLSYSADMINACCOUNTS="BUILTIN\Administrators" /TCPENABLED=1 /NPENABLED=0 /SQLCOLLATION=Turkish_100_CI_AS `
        /UPDATEENABLED=0 /IACCEPTSQLSERVERLICENSETERMS | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "SQL Server kurulumu başarısız (kod $LASTEXITCODE)." }
    Write-Host "sa şifresi yalnızca acil durum içindir; uygulama Windows yöneticisi hesabıyla yönetilir." -ForegroundColor Yellow
}

Adim 'SQL Server TCP/IP sabit port 1433'
$kayit = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server' | Where-Object { $_.PSChildName -like "MSSQL*.$Ornek" } | Select-Object -First 1
if ($kayit) {
    $tcp = Join-Path $kayit.PSPath 'MSSQLServer\SuperSocketNetLib\Tcp'
    Set-ItemProperty $tcp -Name Enabled -Value 1
    Set-ItemProperty (Join-Path $tcp 'IPAll') -Name TcpPort -Value '1433'
    Set-ItemProperty (Join-Path $tcp 'IPAll') -Name TcpDynamicPorts -Value ''
    # Karma kimlik doğrulama (LoginMode=2) — istemciler filo_app SQL login'i ile bağlanır
    Set-ItemProperty (Join-Path $kayit.PSPath 'MSSQLServer') -Name LoginMode -Value 2
    Restart-Service $servisAdi -Force
}

Adim 'Güvenlik duvarı (yalnızca özel/etki alanı ağı)'
if (-not (Get-NetFirewallRule -DisplayName 'Ozaylar Filo SQL' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'Ozaylar Filo SQL' -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow -Profile Private, Domain | Out-Null
}

Adim 'Yedek klasörü'
New-Item -ItemType Directory -Force $YedekKlasoru | Out-Null
icacls $YedekKlasoru /grant "NT SERVICE\${servisAdi}:(OI)(CI)M" | Out-Null
icacls $YedekKlasoru /grant "SYSTEM:(OI)(CI)F" | Out-Null

Adim 'Güncelleme paylaşımı'
New-Item -ItemType Directory -Force $GuncellemeKlasoru | Out-Null
if (-not (Get-SmbShare -Name 'OzaylarFiloGuncelleme' -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name 'OzaylarFiloGuncelleme' -Path $GuncellemeKlasoru -ReadAccess 'Everyone' | Out-Null
}

Adim 'Veritabanı kurulumu'
$exe = Join-Path $burasi 'Ozaylar.Filo.Sunucu.exe'
$arglar = @('kur', '--sql', "localhost\$Ornek", '--windows')
if ($UygulamaSifresi) { $arglar += @('--app-sifre', $UygulamaSifresi) }
& $exe @arglar
if ($LASTEXITCODE -ne 0) { throw 'Veritabanı kurulumu başarısız.' }

# appsettings.json'daki yedek klasörünü ayarla
$ayarYol = Join-Path $burasi 'appsettings.json'
$ayar = Get-Content $ayarYol -Raw -Encoding UTF8 | ConvertFrom-Json
$ayar.Yedek.YerelKlasor = $YedekKlasoru
$ayar | ConvertTo-Json -Depth 5 | Set-Content $ayarYol -Encoding UTF8

Adim 'Windows servisi'
if (-not (Get-Service 'OzaylarFiloSunucu' -ErrorAction SilentlyContinue)) {
    New-Service -Name 'OzaylarFiloSunucu' -BinaryPathName "`"$exe`"" -DisplayName 'Ozaylar Filo Sunucu' `
        -Description 'Ozaylar Filo: veritabanı güncelleme, otomatik yedekleme, TCMB kurları' -StartupType Automatic | Out-Null
    sc.exe failure OzaylarFiloSunucu reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
}
Start-Service 'OzaylarFiloSunucu'

Adim 'Güç ayarı: uyku kapalı (sunucu sürekli açık olmalı)'
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0

Write-Host "`nSunucu hazır." -ForegroundColor Green
Write-Host "İstemci bağlantı bilgisi: $env:ProgramData\OzaylarFilo\istemci-baglanti-bilgisi.txt"
Write-Host "Yedeklerin ikinci kopyası için appsettings.json > Yedek > IkinciHedef ve SifrelemeParolasi alanlarını doldurup servisi yeniden başlatın."
