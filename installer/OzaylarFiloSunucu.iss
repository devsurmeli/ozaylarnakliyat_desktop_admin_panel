; Ozaylar Filo — Sunucu kurulum paketi (Inno Setup 6)
; Derleme: build\paketle.ps1 (ISCC /DSurum=x.y.z /DKaynak=<artifacts>)
; İçerik: sunucu servisi + istemci kurulumu (Velopack Setup.exe) + isteğe bağlı SQL Server 2025 Express kurulum dosyası.

#ifndef Surum
  #define Surum "1.0.0"
#endif
#ifndef Kaynak
  #define Kaynak "..\artifacts"
#endif

[Setup]
AppId={{6C1C2C2B-0F7E-4C55-9C0E-0A1F3D2B7A11}
AppName=Ozaylar Filo Sunucu
AppVersion={#Surum}
AppPublisher=Ozaylar
DefaultDirName={autopf}\OzaylarFilo\Sunucu
DisableProgramGroupPage=yes
OutputDir={#Kaynak}
OutputBaseFilename=OzaylarFiloSunucuKurulum-{#Surum}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
LanguageDetectionMethod=none

[Languages]
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"

[Files]
Source: "{#Kaynak}\sunucu\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion; Excludes: "appsettings.json"
Source: "{#Kaynak}\sunucu\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#Kaynak}\istemci\*"; DestDir: "{app}\Istemci"; Flags: recursesubdirs ignoreversion
; SQL Server 2025 Express kurulum dosyasını installer\prereq klasörüne koyarsanız pakete eklenir.
Source: "prereq\SQLEXPR*.exe"; DestDir: "{tmp}"; Flags: skipifsourcedoesntexist deleteafterinstall

[Run]
; Sunucuyu hazırla: SQL Express (gerekirse), port/güvenlik duvarı, yedek klasörü, veritabanı, Windows servisi
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\sunucu-hazirla.ps1"" {code:SqlParametresi}"; \
  StatusMsg: "Sunucu hazırlanıyor (SQL Server, veritabanı, servis)..."; Flags: waituntilterminated
; Güncelleme paylaşımına istemci paketini kopyala
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -Command ""Copy-Item -Path '{app}\Istemci\*' -Destination 'C:\OzaylarFilo\Guncelleme' -Recurse -Force"""; \
  StatusMsg: "İstemci güncelleme paketi yayınlanıyor..."; Flags: runhidden waituntilterminated
; Sunucu bilgisayara istemciyi de kur
Filename: "{app}\Istemci\OzaylarFilo-win-Setup.exe"; Description: "Ozaylar Filo programını bu bilgisayara da kur"; Flags: postinstall nowait skipifsilent

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop OzaylarFiloSunucu"; Flags: runhidden; RunOnceId: "ServisDurdur"
Filename: "sc.exe"; Parameters: "delete OzaylarFiloSunucu"; Flags: runhidden; RunOnceId: "ServisSil"

[Code]
function SqlParametresi(Param: string): string;
var
  Bulunan: TFindRec;
begin
  Result := '';
  if FindFirst(ExpandConstant('{tmp}\SQLEXPR*.exe'), Bulunan) then
  begin
    Result := '-SqlKurulum "' + ExpandConstant('{tmp}\') + Bulunan.Name + '"';
    FindClose(Bulunan);
  end;
end;
