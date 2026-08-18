; Sentinela — instalador do agente Windows (Inno Setup 6)
;
; Assistente: Boas-vindas → Licença → URL da API → Pasta → Instalar
; Saída: dist\installer\Sentinela.exe
;
; Silencioso (GPO / script):
;   Sentinela.exe /VERYSILENT /NORESTART /APIURL=http://192.168.0.116:5002

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef AgentPublishDir
  #define AgentPublishDir "..\..\dist\agent-setup"
#endif

#ifndef DefaultApiUrl
  #define DefaultApiUrl "http://192.168.0.116:5002"
#endif

#define MyAppName "Sentinela"
#define MyAppPublisher "Mobi"
#define MyAppExeName "Sentinela.Agent.exe"
#define MyServiceName "SentinelaAgent"
#define MyServiceDisplay "Agente Sentinela"

[Setup]
AppId={{E8B3C4A1-7D2F-4E91-9B6A-1F2E3D4C5B6A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=© {#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Instalador do agente Sentinela
DefaultDirName={autopf}\Sentinela\Agent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=LICENSE.txt
OutputDir=..\..\dist\installer
OutputBaseFilename=Sentinela
SetupIconFile=setup.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
WizardStyle=modern
WizardSizePercent=120
WizardImageFile=wizard-side.bmp
WizardSmallImageFile=wizard-small.bmp
Compression=lzma2
SolidCompression=yes
LZMAUseSeparateProcess=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
AllowNoIcons=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
DisableWelcomePage=no
DisableReadyPage=no
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
SetupLogging=yes
Uninstallable=yes
ChangesEnvironment=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
WelcomeLabel1=Bem-vindo ao instalador do [name]
WelcomeLabel2=Este assistente instalará o agente Sentinela neste computador.%n%nO agente roda como serviço do Windows, inicia com a máquina e se conecta ao servidor informado no próximo passo.%n%nRecomenda-se fechar os demais aplicativos antes de continuar.
FinishedHeadingLabel=Instalação concluída
FinishedLabel=O Sentinela foi instalado neste computador. O serviço do agente já está em execução e iniciará automaticamente com o Windows.
ClickFinish=Clique em Concluir para sair do instalador.

[CustomMessages]
ServerPageCaption=Servidor Sentinela
ServerPageDescription=Informe o endereço da API usada pelo agente.
ServerPageSubCaption=O agente usará esta URL para enviar telemetria, alertas e receber comandos. Use o IP ou o nome do servidor na rede interna.
ServerPageApiLabel=URL da API:
ServerPageInvalid=Informe a URL da API começando com http:// ou https://.%n%nExemplo: http://192.168.0.116:5002

[Files]
Source: "{#AgentPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,appsettings.Development.json,dist,dist\*,createdump.exe"

[Dirs]
Name: "{commonappdata}\Sentinela\Agent\logs"; Flags: uninsneveruninstall
Name: "{commonappdata}\Sentinela\Agent\recordings"; Flags: uninsneveruninstall

[Icons]
Name: "{autoprograms}\{#MyAppName}\Logs do agente"; Filename: "{commonappdata}\Sentinela\Agent\logs"
Name: "{autoprograms}\{#MyAppName}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

[UninstallDelete]
Type: files; Name: "{app}\appsettings.Production.json"

[Code]
var
  ServerPage: TInputQueryWizardPage;

function StripSpaces(const S: String): String;
var
  I, J: Integer;
begin
  I := 1;
  J := Length(S);
  while (I <= J) and (S[I] = ' ') do
    I := I + 1;
  while (J >= I) and (S[J] = ' ') do
    J := J - 1;
  if J < I then
    Result := ''
  else
    Result := Copy(S, I, J - I + 1);
end;

function NormalizeApiUrl(const Raw: String): String;
var
  Url: String;
begin
  Url := StripSpaces(Raw);
  while (Length(Url) > 0) and (Url[Length(Url)] = '/') do
    Delete(Url, Length(Url), 1);
  Result := Url;
end;

function GetApiUrl(): String;
begin
  Result := NormalizeApiUrl(ServerPage.Values[0]);
end;

procedure InitializeWizard;
var
  DefaultUrl: String;
begin
  DefaultUrl := ExpandConstant('{param:APIURL|{#DefaultApiUrl}}');
  DefaultUrl := NormalizeApiUrl(DefaultUrl);
  if DefaultUrl = '' then
    DefaultUrl := '{#DefaultApiUrl}';

  ServerPage := CreateInputQueryPage(
    wpLicense,
    CustomMessage('ServerPageCaption'),
    CustomMessage('ServerPageDescription'),
    CustomMessage('ServerPageSubCaption'));
  ServerPage.Add(CustomMessage('ServerPageApiLabel'), False);
  ServerPage.Values[0] := DefaultUrl;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Url, LowerUrl: String;
begin
  Result := True;
  if CurPageID = ServerPage.ID then
  begin
    Url := GetApiUrl();
    LowerUrl := LowerCase(Url);
    if (Url = '') or
       ((Pos('http://', LowerUrl) <> 1) and (Pos('https://', LowerUrl) <> 1)) then
    begin
      MsgBox(CustomMessage('ServerPageInvalid'), mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure StopAgent;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1500);
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName} /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

procedure WriteProductionConfig;
var
  ApiUrl, Json, Path: String;
begin
  ApiUrl := GetApiUrl();
  Json :=
    '{' + #13#10 +
    '  "ServerConnection": {' + #13#10 +
    '    "ApiUrl": "' + ApiUrl + '",' + #13#10 +
    '    "SignalRUrl": "' + ApiUrl + '/hubs/agent"' + #13#10 +
    '  },' + #13#10 +
    '  "ScreenCapture": {' + #13#10 +
    '    "ApiBaseUrl": "' + ApiUrl + '"' + #13#10 +
    '  }' + #13#10 +
    '}' + #13#10;
  Path := ExpandConstant('{app}\appsettings.Production.json');
  SaveStringToFile(Path, Json, False);
end;

procedure InstallService;
var
  ResultCode: Integer;
  BinPath, Sc: String;
begin
  Sc := ExpandConstant('{sys}\sc.exe');
  BinPath := ExpandConstant('{app}\{#MyAppExeName}');

  Exec(Sc, 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1500);
  Exec(Sc, 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);

  Exec(Sc,
    'create {#MyServiceName} binPath= "' + BinPath + '" start= delayed-auto obj= LocalSystem DisplayName= "{#MyServiceDisplay}"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Exec(Sc,
    'description {#MyServiceName} "Monitoramento, segurança e gravação local do Sentinela."',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  Exec(Sc,
    'failure {#MyServiceName} reset= 86400 actions= restart/10000/restart/30000/restart/60000',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  RegWriteMultiStringValue(HKLM, 'SYSTEM\CurrentControlSet\Services\{#MyServiceName}',
    'Environment', 'DOTNET_ENVIRONMENT=Production');

  Exec(Sc, 'start {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    StopAgent;
  if CurStep = ssPostInstall then
  begin
    WriteProductionConfig;
    InstallService;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    StopAgent;
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1500);
  end;
end;
