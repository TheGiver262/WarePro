#define SqlExpressUrl "https://download.microsoft.com/download/5/1/4/5145fe04-4d30-4b85-b0d1-39533663a2f1/SQL2022-SSEI-Expr.exe"
#define SqlExpressSha256 "36E0EC2AC3DD60F496C99CE44722C629209EA7302A2CE9CBFD1E42A73510D7B6"
#define SqlExpressBootstrapper "SQL2022-SSEI-Expr.exe"

[Code]

function SqlInstanceExists: Boolean;
begin
  Result :=
    RegValueExists(
      HKLM64,
      'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL',
      'SQLEXPRESS') or
    RegValueExists(
      HKLM32,
      'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL',
      'SQLEXPRESS');
end;

function ReadSqlInstanceId(var InstanceId: String): Boolean;
begin
  Result := RegQueryStringValue(
    HKLM64,
    'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL',
    'SQLEXPRESS',
    InstanceId);
  if not Result then
    Result := RegQueryStringValue(
      HKLM32,
      'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL',
      'SQLEXPRESS',
      InstanceId);
end;

function ReadSqlSetupValue(
  const InstanceId, ValueName: String;
  var Value: String): Boolean;
var
  SetupPath: String;
begin
  SetupPath :=
    'SOFTWARE\Microsoft\Microsoft SQL Server\' + InstanceId + '\Setup';
  Result := RegQueryStringValue(HKLM64, SetupPath, ValueName, Value);
  if not Result then
    Result := RegQueryStringValue(HKLM32, SetupPath, ValueName, Value);
end;

function SqlInstanceSupported: Boolean;
var
  InstanceId: String;
  VersionText: String;
  EditionText: String;
  DotPosition: Integer;
  MajorVersion: Integer;
begin
  Result := False;
  if not ReadSqlInstanceId(InstanceId) then
    Exit;
  if not ReadSqlSetupValue(InstanceId, 'Version', VersionText) then
    Exit;
  if not ReadSqlSetupValue(InstanceId, 'Edition', EditionText) then
    Exit;

  DotPosition := Pos('.', VersionText);
  if DotPosition = 0 then
    Exit;
  MajorVersion := StrToIntDef(Copy(VersionText, 1, DotPosition - 1), 0);
  Result := (MajorVersion >= 16) and
    (Pos('EXPRESS', Uppercase(EditionText)) > 0);
end;

function OnSqlDownloadProgress(
  const Url, FileName: String;
  const Progress, ProgressMax: Int64): Boolean;
begin
  if ProgressMax > 0 then
    WizardForm.StatusLabel.Caption := Format(
      'Đang tải SQL Server Express: %d%%', [Progress * 100 div ProgressMax])
  else
    WizardForm.StatusLabel.Caption := 'Đang tải SQL Server Express...';
  Result := True;
end;

function VerifyMicrosoftSignature(const FileName: String): Boolean;
var
  PowerShellPath: String;
  ScriptPath: String;
  ScriptText: String;
  ResultCode: Integer;
begin
  ScriptPath := ExpandConstant('{tmp}\warepro-verify-microsoft-signature.ps1');
  ScriptText :=
    'param([string]$Path)' + #13#10 +
    '$signature = Get-AuthenticodeSignature -LiteralPath $Path' + #13#10 +
    'if ($signature.Status -ne ''Valid'') { exit 1 }' + #13#10 +
    'if ($signature.SignerCertificate.Subject -notmatch ''O=Microsoft Corporation'') { exit 2 }' + #13#10 +
    'exit 0' + #13#10;
  SaveStringToFile(ScriptPath, ScriptText, False);
  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Result := Exec(
    PowerShellPath,
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ' +
      AddQuotes(ScriptPath) + ' ' + AddQuotes(FileName),
    ExpandConstant('{tmp}'),
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and (ResultCode = 0);
end;

function DownloadSqlMedia(const BootstrapperPath, MediaDirectory: String): Boolean;
var
  ResultCode: Integer;
begin
  ForceDirectories(MediaDirectory);
  Result := Exec(
    BootstrapperPath,
    '/Action=Download /MediaType=Core /MediaPath=' + AddQuotes(MediaDirectory) +
      ' /Quiet',
    ExpandConstant('{tmp}'),
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) and ((ResultCode = 0) or (ResultCode = 3010));
end;

function InstallSqlMedia(const MediaPackage: String; var RestartRequired: Boolean): Boolean;
var
  ResultCode: Integer;
  Parameters: String;
begin
  Parameters :=
    '/Q /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS ' +
    '/ADDCURRENTUSERASSQLADMIN=True ' +
    '/SQLSVCACCOUNT="NT AUTHORITY\NETWORK SERVICE" ' +
    '/SQLSVCSTARTUPTYPE=Automatic /TCPENABLED=0 /NPENABLED=0 ' +
    '/UpdateEnabled=True /SUPPRESSPRIVACYSTATEMENTNOTICE=True ' +
    '/IACCEPTSQLSERVERLICENSETERMS';
  Result := Exec(
    MediaPackage,
    Parameters,
    ExtractFileDir(MediaPackage),
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  RestartRequired := ResultCode = 3010;
  Result := Result and ((ResultCode = 0) or RestartRequired);
end;

function EnsureSqlExpress(var ErrorMessage: String): Boolean;
var
  BootstrapperPath: String;
  MediaDirectory: String;
  MediaPackage: String;
begin
  Result := True;
  if not IsFullMode then
    Exit;

  try
    if SqlInstanceExists then
    begin
      if not SqlInstanceSupported then
        RaiseException(
          'SQLEXPRESS hiện có không phải SQL Server 2022 Express hoặc mới hơn.');
      Log('SQLEXPRESS đã đúng phiên bản; bỏ qua cài SQL.');
      Exit;
    end;

    DownloadTemporaryFile(
      '{#SqlExpressUrl}',
      '{#SqlExpressBootstrapper}',
      '{#SqlExpressSha256}',
      @OnSqlDownloadProgress);
    BootstrapperPath := ExpandConstant('{tmp}\{#SqlExpressBootstrapper}');
    if not VerifyMicrosoftSignature(BootstrapperPath) then
      RaiseException('Chữ ký bootstrapper SQL Server không hợp lệ.');

    MediaDirectory := ExpandConstant('{tmp}\WareProSqlMedia');
    if not DownloadSqlMedia(BootstrapperPath, MediaDirectory) then
      RaiseException('Không tải được media SQL Server Express.');

    MediaPackage := MediaDirectory + '\SQLEXPR_x64_ENU.exe';
    if not FileExists(MediaPackage) then
      RaiseException('Không tìm thấy gói SQLEXPR_x64_ENU.exe đã tải.');
    if not VerifyMicrosoftSignature(MediaPackage) then
      RaiseException('Chữ ký gói SQL Server Express không hợp lệ.');

    if not InstallSqlMedia(MediaPackage, SqlRestartRequired) then
      RaiseException('SQL Server Express cài đặt không thành công.');
  except
    ErrorMessage := GetExceptionMessage;
    Result := False;
  end;
end;
