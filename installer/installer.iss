; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "QPM"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Sc2ad"
#define MyAppURL "https://github.com/sc2ad/QuestPackageManager"
#define MyAppExeName "QPM.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{6CEE605F-9C59-4E33-A388-6415297B308D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
InfoAfterFile=.\information.txt
; Uncomment the following line to run in non administrative install mode (install for current user only.)
;PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.\
OutputBaseFilename=QPM-installer
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

; Taken from https://stackoverflow.com/a/46609047/11395424. Credit to author Wojciech Mleczek
; Adds QPM to PATH on installation and remove on uninstallation
[Code]
const SystemEnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
const UserEnvironmentKey = 'Environment';

function GetPathValue(var ResultStr: String): Boolean;
begin
    if IsAdmin()
    then Result := RegQueryStringValue(HKEY_LOCAL_MACHINE, SystemEnvironmentKey, 'Path', ResultStr)
    else Result := RegQueryStringValue(HKEY_CURRENT_USER, UserEnvironmentKey, 'Path', ResultStr);
end;

function SetPathValue(Paths: string): Boolean;
begin
    if IsAdmin()
    then Result := RegWriteStringValue(HKEY_LOCAL_MACHINE, SystemEnvironmentKey, 'Path', Paths)
    else Result := RegWriteStringValue(HKEY_CURRENT_USER, UserEnvironmentKey, 'Path', Paths)
end;

procedure EnvAddPath(Path: string);
var
    Paths: string;
begin
    { Retrieve current path (use empty string if entry not exists) }
    if not GetPathValue(Paths)
    then Paths := '';

    { Skip if string already found in path }
    if Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';') > 0 then exit;

    { App string to the end of the path variable }
    Paths := Paths + ';'+ Path +';'

    { Overwrite (or create if missing) path environment variable }
    if SetPathValue(Paths)
    then Log(Format('The [%s] added to PATH: [%s]', [Path, Paths]))
    else Log(Format('Error while adding the [%s] to PATH: [%s]', [Path, Paths]));
end;

procedure EnvRemovePath(Path: string);
var
    Paths: string;
    P: Integer;
begin
    { Skip if registry entry not exists }
    if not GetPathValue(Paths) then
        exit;

    { Skip if string not found in path }
    P := Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';');
    if P = 0 then exit;

    { Update path variable }
    Delete(Paths, P - 1, Length(Path) + 1);

    { Overwrite path environment variable }
    if SetPathValue(Paths)
    then Log(Format('The [%s] removed from PATH: [%s]', [Path, Paths]))
    else Log(Format('Error while removing the [%s] from PATH: [%s]', [Path, Paths]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
    if CurStep = ssPostInstall
     then EnvAddPath(ExpandConstant('{app}'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
    if CurUninstallStep = usPostUninstall
    then EnvRemovePath(ExpandConstant('{app}'));
end;

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\QPM\bin\Release\net5.0\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\QPM\bin\Release\net5.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[UninstallRun]

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
