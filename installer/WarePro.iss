#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif
#ifndef MySchemaRelease
  #define MySchemaRelease 7
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

; full tự cài SQL Express; app-only chỉ cấu hình kết nối tới SQL Server đã có.
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
Source: "{#SetupHelperDir}\*"; Flags: dontcopy recursesubdirs createallsubdirs

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
  PendingFullInstall = 'SOFTWARE\WarePro\Installer';

var
  ConnectionPage: TInputQueryWizardPage;
  AuthenticationPage: TInputOptionWizardPage;
  BootstrapPage: TInputQueryWizardPage;
  RemoveLocalDataCheckBox: TNewCheckBox;
  SqlRestartRequired: Boolean;
  InstallReady: Boolean;
  UpgradeMode: Boolean;
  ResumeFullMode: Boolean;
  DatabasePrepared: Boolean;
  DatabaseCutoverStarted: Boolean;
  DatabaseFinalized: Boolean;
  HelperExecutable: String;

// lần chạy tiếp sau restart giữ full mode; upgrade chỉ thay ứng dụng và nâng schema hiện có.
function IsFullMode: Boolean;
begin
  Result := ResumeFullMode or ((not UpgradeMode) and
    (CompareText(WizardSetupType(False), FullMode) = 0));
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

function ReadPendingFullInstall: Boolean;
var
  Value: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, PendingFullInstall, 'Pending', Value) and
    (Value = 1);
end;

procedure WritePendingFullInstall;
begin
  if not RegWriteDWordValue(HKLM64, PendingFullInstall, 'Pending', 1) then
    RaiseException('không lưu được trạng thái tiếp tục cài đặt sau khi khởi động lại.');
end;

procedure ClearPendingFullInstall;
begin
  RegDeleteValue(HKLM64, PendingFullInstall, 'Pending');
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
  ResumeFullMode := ReadPendingFullInstall;
  UpgradeMode := (not ResumeFullMode) and
    ((CompareText(ParameterOrDefault('WAREPROMODE', ''), 'upgrade') = 0) or
    PreviousInstallExists);

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

  BootstrapPage := CreateInputQueryPage(
    AuthenticationPage.ID,
    'tài khoản quản trị WarePro',
    'tạo mật khẩu ban đầu cho tài khoản admin',
    'mật khẩu cần ít nhất 12 ký tự và phải đổi sau lần đăng nhập đầu.');
  BootstrapPage.Add('mật khẩu admin:', True);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result :=
    (UpgradeMode and
      ((PageID = wpSelectComponents) or (PageID = ConnectionPage.ID) or
       (PageID = AuthenticationPage.ID) or (PageID = BootstrapPage.ID))) or
    (IsFullMode and
      ((PageID = ConnectionPage.ID) or (PageID = AuthenticationPage.ID)));
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = BootstrapPage.ID then
  begin
    if Length(BootstrapPage.Values[0]) < 12 then
    begin
      SuppressibleMsgBox('mật khẩu admin cần ít nhất 12 ký tự.', mbError, MB_OK, IDOK);
      Result := False;
    end;
    Exit;
  end;

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
    HelperExecutable,
    Arguments + ' --log ' + AddQuotes(HelperLogPath),
    ExtractFileDir(HelperExecutable),
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

function BootstrapSecretPath: String;
var
  SuppliedPath: String;
begin
  SuppliedPath := ExpandConstant('{param:WAREPROBOOTSTRAPSECRETFILE|}');
  if SuppliedPath <> '' then
    Result := SuppliedPath
  else
    Result := ExpandConstant('{tmp}\warepro-bootstrap.secret');
end;

procedure WriteProtectedBootstrapSecret;
var
  ExitCode: Integer;
  SecretPath: String;
  AclArguments: String;
begin
  // secret được ghi hoặc nhận qua file, sau đó siết ACL; command line chỉ chứa đường dẫn file.
  SecretPath := BootstrapSecretPath;
  if ExpandConstant('{param:WAREPROBOOTSTRAPSECRETFILE|}') = '' then
  begin
    if not SaveStringToFile(SecretPath, BootstrapPage.Values[0], False) then
      RaiseException('không ghi được mật khẩu admin tạm.');
    BootstrapPage.Values[0] := '';
  end
  else if not FileExists(SecretPath) then
    RaiseException('không tìm thấy file mật khẩu admin tạm đã cấp.');

  AclArguments :=
    AddQuotes(SecretPath) +
    ' /inheritance:r /grant:r *S-1-5-18:F *S-1-5-32-544:F ' +
    AddQuotes(GetUserNameString + ':F');
  if not Exec(ExpandConstant('{sys}\icacls.exe'), AclArguments, '', SW_HIDE,
      ewWaitUntilTerminated, ExitCode) or (ExitCode <> 0) then
  begin
    DeleteFile(SecretPath);
    RaiseException('không bảo vệ được file mật khẩu admin tạm.');
  end;
end;

function RollbackDatabaseCutover(var ExitCode: Integer): Boolean; forward;

procedure PrepareDatabaseCutover;
var
  StagingConfig: String;
  FinalConfig: String;
  ConfigToTest: String;
  Arguments: String;
  ExitCode: Integer;
  RollbackCode: Integer;
begin
  // kiểm tra đúng cấu hình sẽ dùng trước khi prepare để không nâng nhầm database.
  StagingConfig := ExpandConstant('{tmp}\warepro.settings.json');
  FinalConfig := ExpandConstant('{commonappdata}\WarePro\Config\warepro.settings.json');

  if UpgradeMode or ResumeFullMode then
  begin
    if not FileExists(FinalConfig) then
      RaiseException('không tìm thấy cấu hình WarePro hiện tại; không thể xác định database cần nâng cấp.');
    ConfigToTest := FinalConfig;
  end
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
  else if not TestConfiguration(ConfigToTest, '--mode app-only', ExitCode) then
    RaiseException(Format(
      'không kết nối được database; SQL credential phải được lưu sẵn trong Windows Credential Manager (mã %d).',
      { credential must already exist in Windows Credential Manager; bootstrap secret only creates admin } [ExitCode]));

  if CompareText(ConfigToTest, StagingConfig) = 0 then
    if not WriteConfiguration(FinalConfig, ExitCode) then
      RaiseException(Format('không lưu được cấu hình máy (mã %d).', [ExitCode]));

  Arguments :=
    'prepare-database --config ' + AddQuotes(FinalConfig) +
    ' --app-version {#MyAppVersion} --expected-schema {#MySchemaRelease}';
  if not UpgradeMode then
  begin
    WriteProtectedBootstrapSecret;
    Arguments := Arguments + ' --bootstrap-secret-file ' + AddQuotes(BootstrapSecretPath);
  end;

  // đặt cờ trước prepare để nhánh lỗi và DeinitializeSetup biết cần thử rollback.
  DatabaseCutoverStarted := True;
  try
    try
      if not RunSetupHelper(Arguments, ExitCode) or (ExitCode <> 0) then
        RaiseException(Format('không chuẩn bị được database (mã %d).', [ExitCode]));
    finally
      DeleteFile(BootstrapSecretPath);
    end;
    DatabasePrepared := True;
  except
    RollbackDatabaseCutover(RollbackCode);
    DatabaseCutoverStarted := False;
    RaiseException(Format('không chuẩn bị được database; đã thử khôi phục (mã %d).', [RollbackCode]));
  end;
end;

function FinalizeDatabaseCutover(var ExitCode: Integer): Boolean;
begin
  Result := RunSetupHelper(
    'finalize-database --config ' +
      AddQuotes(ExpandConstant('{commonappdata}\WarePro\Config\warepro.settings.json')) +
      ' --app-version {#MyAppVersion} --expected-schema {#MySchemaRelease}',
    ExitCode) and (ExitCode = 0);
end;

function RollbackDatabaseCutover(var ExitCode: Integer): Boolean;
begin
  Result := RunSetupHelper(
    'rollback-database --config ' +
      AddQuotes(ExpandConstant('{commonappdata}\WarePro\Config\warepro.settings.json')) +
      ' --app-version {#MyAppVersion} --expected-schema {#MySchemaRelease}',
    ExitCode) and (ExitCode = 0);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  ExtractTemporaryFiles('*');
  HelperExecutable := ExpandConstant('{tmp}\WarePro.SetupHelper.exe');

  if not EnsureSqlExpress(Result) then
    Exit;
  NeedsRestart := SqlRestartRequired;
  if not SqlRestartRequired then
    PrepareDatabaseCutover;
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
var
  ExitCode: Integer;
  RollbackCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    HelperExecutable := ExpandConstant('{app}\Setup\WarePro.SetupHelper.exe');
    if SqlRestartRequired then
    begin
      SaveConfigurationForRestart;
      WritePendingFullInstall;
    end
    else if DatabasePrepared then
    begin
      if not FinalizeDatabaseCutover(ExitCode) then
      begin
        RollbackDatabaseCutover(RollbackCode);
        RaiseException(Format('không hoàn tất được database (mã %d); đã thử khôi phục database.', [ExitCode]));
      end;
      DatabaseFinalized := True;
      DatabaseCutoverStarted := False;
      ClearPendingFullInstall;
      InstallReady := True;
    end;
  end;
end;

procedure DeinitializeSetup;
var
  ExitCode: Integer;
begin
  // khi setup kết thúc, thử xóa file secret đã dùng; nếu cutover chưa finalize thì thử rollback trước khi thoát.
  DeleteFile(BootstrapSecretPath);
  if (DatabasePrepared or DatabaseCutoverStarted) and not DatabaseFinalized then
  begin
    if FileExists(ExpandConstant('{app}\Setup\WarePro.SetupHelper.exe')) then
      HelperExecutable := ExpandConstant('{app}\Setup\WarePro.SetupHelper.exe')
    else
      HelperExecutable := ExpandConstant('{tmp}\WarePro.SetupHelper.exe');
    RollbackDatabaseCutover(ExitCode);
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
