#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef SetupHelperDir
  #define SetupHelperDir "..\artifacts\setup-helper"
#endif

#define MyAppName "WarePro"
#define MyAppPublisher "WarePro"
#define MyAppExeName "WarePro.exe"

[Setup]
AppId={{47F3016C-70E3-4BEE-A4AF-6934F7CB7626}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppUpdatesURL=https://github.com/TheGiver262/WarePro-Releases/releases
DefaultDirName={autopf64}\WarePro
DefaultGroupName=WarePro
DisableProgramGroupPage=auto
OutputDir=..\artifacts\installer
OutputBaseFilename=WarePro-Setup
SetupIconFile=..\QuanLyHangHoa\Assets\WarePro.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
AppMutex=WarePro.MainWindow
UsePreviousAppDir=yes
UsePreviousGroup=yes
SetupLogging=yes
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductVersion={#MyAppVersion}
MinVersion=10.0.17763

[Types]
Name: "full"; Description: "cài đầy đủ một-click (WarePro + SQL Server Express)"
Name: "app-only"; Description: "chỉ cài WarePro (dùng SQL Server có sẵn)"

[Components]
Name: "application"; Description: "phần mềm WarePro"; Types: full app-only; Flags: fixed
Name: "sql"; Description: "SQL Server Express 2022"; Types: full

[Tasks]
Name: "desktopicon"; Description: "tạo biểu tượng ngoài màn hình"; GroupDescription: "tùy chọn:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\Database\warepro_database_seed.xlsx"; DestDir: "{app}\Database"; Flags: ignoreversion
Source: "{#SetupHelperDir}\*"; DestDir: "{app}\Setup"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\WarePro"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\WarePro"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "mở WarePro"; Flags: postinstall nowait skipifsilent; Check: CanLaunchWarePro

[Code]
const
  FullMode = 'full';
  AppOnlyMode = 'app-only';
  DefaultServer = '.\SQLEXPRESS';
  DefaultDatabase = 'ProductManagementDb';
  MachineLogDirectory = '{commonappdata}\WarePro\InstallerLogs';

var
  ConnectionPage: TInputQueryWizardPage;
  AuthenticationPage: TInputOptionWizardPage;
  RemoveLocalDataCheckBox: TNewCheckBox;
  SqlRestartRequired: Boolean;
  InstallReady: Boolean;
  UpgradeMode: Boolean;

function IsFullMode: Boolean;
begin
  Result := (not UpgradeMode) and
    (CompareText(WizardSetupType(False), FullMode) = 0);
end;

function IsAppOnlyMode: Boolean;
begin
  Result := UpgradeMode or
    (CompareText(WizardSetupType(False), AppOnlyMode) = 0);
end;

#include "includes\SqlExpress2022.iss"

function ParameterOrDefault(const Name, DefaultValue: String): String;
var
  Index: Integer;
  Prefix: String;
begin
  Prefix := '/' + Uppercase(Name) + '=';
  Result := DefaultValue;
  for Index := 1 to ParamCount do
    if Pos(Prefix, Uppercase(ParamStr(Index))) = 1 then
    begin
      Result := Copy(ParamStr(Index), Length(Prefix) + 1, MaxInt);
      Exit;
    end;
end;

function PreviousInstallExists: Boolean;
var
  InstallLocation: String;
begin
  Result := RegQueryStringValue(
    HKLM64,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{47F3016C-70E3-4BEE-A4AF-6934F7CB7626}_is1',
    'InstallLocation',
    InstallLocation);
  if not Result then
    Result := FileExists(ExpandConstant('{autopf64}\WarePro\WarePro.exe'));
end;

procedure InitializeWizard;
var
  Authentication: String;
begin
  UpgradeMode :=
    (CompareText(ParameterOrDefault('WAREPROMODE', ''), 'upgrade') = 0) or
    PreviousInstallExists;

  ConnectionPage := CreateInputQueryPage(
    wpSelectTasks,
    'kết nối SQL Server',
    'nhập máy chủ và cơ sở dữ liệu WarePro',
    'trình cài đặt sẽ kiểm tra kết nối trước khi lưu cấu hình.');
  ConnectionPage.Add('máy chủ:', False);
  ConnectionPage.Add('cơ sở dữ liệu:', False);
  ConnectionPage.Values[0] := ParameterOrDefault('WAREPROSERVER', DefaultServer);
  ConnectionPage.Values[1] := ParameterOrDefault('WAREPRODATABASE', DefaultDatabase);

  AuthenticationPage := CreateInputOptionPage(
    ConnectionPage.ID,
    'kiểu đăng nhập SQL Server',
    'chọn cách WarePro kết nối cơ sở dữ liệu',
    'mật khẩu SQL không được ghi vào bộ cài hoặc file cấu hình.',
    True,
    False);
  AuthenticationPage.Add('Windows Authentication');
  AuthenticationPage.Add('SQL Server Authentication (nhập credential ở lần chạy đầu)');
  Authentication := ParameterOrDefault('WAREPROAUTH', 'Windows');
  AuthenticationPage.SelectedValueIndex := 0;
  if CompareText(Authentication, 'SqlPassword') = 0 then
    AuthenticationPage.SelectedValueIndex := 1;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result :=
    (UpgradeMode and
      ((PageID = wpSelectComponents) or (PageID = ConnectionPage.ID) or
       (PageID = AuthenticationPage.ID))) or
    (IsFullMode and
      ((PageID = ConnectionPage.ID) or (PageID = AuthenticationPage.ID)));
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ConnectionPage.ID then
  begin
    if Trim(ConnectionPage.Values[0]) = '' then
    begin
      SuppressibleMsgBox('hãy nhập máy chủ SQL Server.', mbError, MB_OK, IDOK);
      Result := False;
    end
    else if Trim(ConnectionPage.Values[1]) = '' then
    begin
      SuppressibleMsgBox('hãy nhập tên cơ sở dữ liệu.', mbError, MB_OK, IDOK);
      Result := False;
    end;
  end;
end;

function SelectedServer: String;
begin
  if IsFullMode then
    Result := DefaultServer
  else
    Result := Trim(ConnectionPage.Values[0]);
end;

function SelectedDatabase: String;
begin
  if IsFullMode then
    Result := DefaultDatabase
  else
    Result := Trim(ConnectionPage.Values[1]);
end;

function SelectedAuthentication: String;
begin
  if IsFullMode or (AuthenticationPage.SelectedValueIndex = 0) then
    Result := 'Windows'
  else
    Result := 'SqlPassword';
end;

function HelperLogPath: String;
begin
  Result := ExpandConstant(MachineLogDirectory + '\setup-helper.log');
end;

function RunSetupHelper(const Arguments: String; var ExitCode: Integer): Boolean;
begin
  ForceDirectories(ExpandConstant(MachineLogDirectory));
  Result := Exec(
    ExpandConstant('{app}\Setup\WarePro.SetupHelper.exe'),
    Arguments + ' --log ' + AddQuotes(HelperLogPath),
    ExpandConstant('{app}\Setup'),
    SW_HIDE,
    ewWaitUntilTerminated,
    ExitCode);
end;

function WriteConfiguration(const Path: String; var ExitCode: Integer): Boolean;
var
  Arguments: String;
begin
  Arguments :=
    'write-config --server ' + AddQuotes(SelectedServer) +
    ' --database ' + AddQuotes(SelectedDatabase) +
    ' --auth ' + SelectedAuthentication +
    ' --config ' + AddQuotes(Path);
  Result := RunSetupHelper(Arguments, ExitCode) and (ExitCode = 0);
end;

function TestConfiguration(const Path, ModeSwitch: String; var ExitCode: Integer): Boolean;
begin
  Result := RunSetupHelper(
    'test-connection --config ' + AddQuotes(Path) + ' ' + ModeSwitch,
    ExitCode) and (ExitCode = 0);
end;

procedure ConfigureAndCheckDatabase;
var
  StagingConfig: String;
  FinalConfig: String;
  ConfigToTest: String;
  ExitCode: Integer;
begin
  StagingConfig := ExpandConstant('{tmp}\warepro.settings.json');
  FinalConfig := ExpandConstant('{commonappdata}\WarePro\Config\warepro.settings.json');

  if UpgradeMode and FileExists(FinalConfig) then
  begin
    { bản cập nhật chỉ thay file ứng dụng, giữ nguyên cấu hình kết nối đang dùng. }
    InstallReady := True;
    Exit;
  end;

  if IsFullMode and FileExists(FinalConfig) then
    ConfigToTest := FinalConfig
  else
  begin
    if not WriteConfiguration(StagingConfig, ExitCode) then
      RaiseException(Format('không ghi được cấu hình tạm (mã %d).', [ExitCode]));
    ConfigToTest := StagingConfig;
  end;

  if IsFullMode then
  begin
    if not RunSetupHelper(
        'detect-sql --instance ' + AddQuotes(DefaultServer),
        ExitCode) or (ExitCode <> 0) then
      RaiseException(Format('không tìm thấy SQLEXPRESS đang chạy (mã %d).', [ExitCode]));

    if not TestConfiguration(ConfigToTest, '--mode full', ExitCode) then
      RaiseException(Format('SQL Server chưa sẵn sàng (mã %d).', [ExitCode]));
  end
  else if CompareText(SelectedAuthentication, 'SqlPassword') <> 0 then
  begin
    if not TestConfiguration(ConfigToTest, '--mode app-only', ExitCode) then
      RaiseException(Format('không kết nối được cơ sở dữ liệu đã chọn (mã %d).', [ExitCode]));
  end;
  { SQL Authentication is checked after first-run credential entry. }

  if CompareText(ConfigToTest, StagingConfig) = 0 then
    if not WriteConfiguration(FinalConfig, ExitCode) then
      RaiseException(Format('không lưu được cấu hình máy (mã %d).', [ExitCode]));

  InstallReady := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not EnsureSqlExpress(Result) then
    Exit;
  NeedsRestart := SqlRestartRequired;
end;

procedure SaveConfigurationForRestart;
var
  FinalConfig: String;
  ExitCode: Integer;
begin
  FinalConfig := ExpandConstant(
    '{commonappdata}\WarePro\Config\warepro.settings.json');
  if not FileExists(FinalConfig) then
    if not WriteConfiguration(FinalConfig, ExitCode) then
      RaiseException(Format(
        'không lưu được cấu hình trước khi khởi động lại (mã %d).', [ExitCode]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if SqlRestartRequired then
      SaveConfigurationForRestart
    else
      ConfigureAndCheckDatabase;
  end;
end;

function NeedRestart: Boolean;
begin
  Result := SqlRestartRequired;
end;

function CanLaunchWarePro: Boolean;
begin
  Result := InstallReady and not SqlRestartRequired;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  RemoveLocalDataCheckBox := TNewCheckBox.Create(UninstallProgressForm);
  RemoveLocalDataCheckBox.Parent := UninstallProgressForm;
  RemoveLocalDataCheckBox.Left := UninstallProgressForm.StatusLabel.Left;
  RemoveLocalDataCheckBox.Top := UninstallProgressForm.ProgressBar.Top +
    UninstallProgressForm.ProgressBar.Height + ScaleY(16);
  RemoveLocalDataCheckBox.Width := UninstallProgressForm.StatusLabel.Width;
  RemoveLocalDataCheckBox.Caption :=
    'xóa cấu hình và cache cục bộ (vẫn keep database và keep credentials)';
  RemoveLocalDataCheckBox.Checked := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and RemoveLocalDataCheckBox.Checked then
  begin
    DelTree(ExpandConstant('{commonappdata}\WarePro\Config'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\WarePro\Updates'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\WarePro\State'), True, True, True);
  end;
end;
