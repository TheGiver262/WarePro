# Bằng chứng kiểm thử WarePro

Ngày đối chiếu: 01/08/2026  
Commit: `41cc3a7fd45348c88835436d454814888d1311ee`

WarePro có hai bộ kiểm thử vì chúng phục vụ hai môi trường khác nhau:

- `QuanLyHangHoa.Tests` kiểm tra nghiệp vụ, dịch vụ, phân quyền và giao diện WPF bằng mock hoặc cơ sở dữ liệu SQLite cô lập.
- `WarePro.SqlServer.Tests` kiểm tra migration, ràng buộc, chỉ mục duy nhất và `rowversion` trên SQL Server 2022 tạm thời trong GitHub Actions.

| Bộ kiểm thử | Kết quả đạt |
|---|---:|
| `QuanLyHangHoa.Tests` | 886 |
| `WarePro.SqlServer.Tests` | 18 |
| **Tổng** | **904** |

Lệnh kiểm tra ứng dụng:

```powershell
dotnet test .\QuanLyHangHoa.Tests\QuanLyHangHoa.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RestoreBuildInParallel=false
```

Kiểm thử SQL Server được chạy bởi [workflow WarePro SQL Server](.github/workflows/warepro-sqlserver.yml). Kết quả tương ứng với commit trên: [18/18 kiểm thử đạt](https://github.com/TheGiver262/WarePro/actions/runs/30677989821).

Các nhóm đã kiểm tra gồm giao dịch và rollback, phân quyền, tồn kho, số sê-ri, kiểm kê, điều chuyển kho, bảo hành, migration và ràng buộc SQL Server. Kiểm thử tải và vận hành dài hạn không thuộc phạm vi kết quả này.

