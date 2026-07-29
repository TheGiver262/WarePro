# WarePro database concurrency contract

## Authority and write path

WarePro is a LAN multi-client application. SQL Server is the authoritative shared database; client app-only machines install WarePro and connect to that server, but never install a separate local WarePro database. Independent local databases would split stock, serial, warranty and audit truth.

Every business mutation uses `DatabaseWriteExecutor.cs`: a fresh EF context, transaction, authorization, validation and recalculation per attempt. It retries transient failures at most three total attempts, including SQL Server deadlock 1205. A `rowversion` conflict is `DB-WRITE-CONFLICT`; it is never retried or overwritten. The UI must reload and ask the operator to decide again.

Each command carries a stable operation ID. If commit acknowledgement is uncertain, the executor returns success only after natural-state or marker verification with that operation ID. Retry exhaustion is `DB-WRITE-RETRY-EXHAUSTED`; maintenance denial is surfaced as the maintenance error, not as a silent retry.

Structured logs may contain operation ID, operation name, attempt number, safe entity key, result/error code and correlation time. Never log a password, token, full connection string, SQL credential or raw exception detail containing a secret.

## Concurrent access and deadlocks

Concurrent edits use first-writer-wins. Each screen sends the `rowversion` it originally read; after one client commits, another client with the old value is rejected and must reload. WarePro never merges or silently overwrites stale stock, serial, invoice, warranty or master-data changes.

A SQL Server deadlock is different: error 1205 means SQL Server ended one transaction to release locks. WarePro retries that transaction with a fresh context, fresh authorization and recalculated data, for at most three total attempts. If it still cannot commit, the UI reports failure and asks the operator to retry.

Every attempt is atomic: document state, balance, serial, ledger and audit either commit together or roll back together. Unique database constraints remain the final guard against duplicate document codes, serial numbers, active warranty coverage and open warranty claims.

## Exact direct-write allowlist

No directory is exempt. The boundary contract scans all production projects and permits `SaveChanges`, `SaveChangesAsync`, `BeginTransaction` and `BeginTransactionAsync` only in these individually reviewed files:

- `QuanLyHangHoa/Data/DatabaseWriteExecutor.cs` — common transaction, retry, conflict and uncertain-commit executor.
- `WarePro.SetupHelper/SetupCommands.cs` — installer-owned migration, maintenance and backup infrastructure.
- `QuanLyHangHoa/Inventory/EfInventoryUnitOfWork.cs` — executor-internal aggregate commit infrastructure; every production construction site is executor-wrapped.
- `QuanLyHangHoa/Services/AppUserService.cs` — generated identity before audit write.
- `QuanLyHangHoa/Services/BrandService.cs` — generated identity before audit write.
- `QuanLyHangHoa/Services/CategoryService.cs` — generated identity before audit write.
- `QuanLyHangHoa/Services/CustomerService.cs` — generated identity before audit write.
- `QuanLyHangHoa/Services/InvoiceService.Integrity.cs` — generated document keys during integrity repair.
- `QuanLyHangHoa/Services/OpeningBalanceImportService.cs` — generated import document keys before dependent rows.
- `QuanLyHangHoa/Services/ProductSerialImportService.cs` — generated stock-in key before posting imported serials and ledger rows.
- `QuanLyHangHoa/Services/ProductService.cs` — generated identity before audit write.
- `QuanLyHangHoa/Services/StockAdjustmentService.cs` — generated adjustment key before posting rows.
- `QuanLyHangHoa/Services/StockCountService.cs` — generated count and adjustment keys before posting rows.
- `QuanLyHangHoa/Services/StockInService.cs` — generated stock-in key before posting rows.
- `QuanLyHangHoa/Services/StockOutService.cs` — generated stock-out key before posting rows.
- `QuanLyHangHoa/Services/StockReversalService.cs` — generated reversal key before posting rows.
- `QuanLyHangHoa/Services/StockTransferService.cs` — generated transfer key before posting rows.
- `QuanLyHangHoa/Services/SupplierService.cs` — generated identity before audit write.
- `QuanLyHangHoa/Services/UnitService.cs` — generated identity before audit write.
- `QuanLyHangHoa/Services/WarrantyClaimService.Writes.cs` — generated claim and replacement keys before dependent rows.
- `QuanLyHangHoa/Services/DataImport/DatabaseSeeder.cs` — generated seed keys before dependent seed rows.
- `QuanLyHangHoa/Services/DataImport/DynamicImportService.cs` — generated import keys before dependent import rows.

### Reviewed raw DML infrastructure

The same Roslyn contract also scans `ExecuteNonQuery` and `ExecuteNonQueryAsync`, including conditional access. These eight calls are exact, method-scoped exceptions; any other raw DML call fails the boundary test.

- `QuanLyHangHoa/Startup/FirstInstallDemoSeeder.cs`: `AcquireSeedLockAsync` acquires the SQL Server session lock before rechecking and writing first-install demo data.
- `WarePro.SetupHelper/SetupCommands.cs`: `OpenConnectionWithCreationAsync` creates the target database once; two `ExecuteAsync` overload calls run installer-owned schema/maintenance commands.
- `QuanLyHangHoa/Services/ClientSessionLease.cs`: `ExecuteAsync` is session-lease infrastructure only.
- `QuanLyHangHoa/Services/DatabaseBackupService.cs`: `BackupWithChecksum` and `VerifyWithChecksum` are explicit SQL Server backup/verification infrastructure.
- `QuanLyHangHoa/Services/SchemaUpgradeLock.cs`: `Dispose` releases the SQL Server schema-maintenance lock.

No production `ExecuteSqlRaw` or `ExecuteSqlInterpolated` calls are permitted or present.
## Schema, startup and operations

Installer infrastructure owns schema changes. Startup is read/check-only: it checks compatibility and must not migrate a shared database. Schema 9 enables RCSI, uses the maintenance lock, takes and verifies backup before upgrade, validates the resulting shape and rejects an old client that does not meet the compatibility floor. During maintenance, clients wait or receive the maintenance error; they must not write around the lock.

Before a release migration, retain backup evidence including `RESTORE VERIFYONLY`, SQL Server version, schema-8 upgrade result and old-client rejection. A backup failure stops migration. Rollback is a DBA-controlled restore procedure, never a client-side destructive shortcut.

## Acceptance gate

Acceptance requires the boundary contract, non-real-database tests, disposable SQL Server tests, Release SQL tests, Release solution build, clean diff, write-call inventory and recorded evidence. Plan 2 remains blocked until the user reviews this gate and gives approval.
