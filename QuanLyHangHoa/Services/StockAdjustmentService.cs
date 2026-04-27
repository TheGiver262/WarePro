using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockAdjustmentService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockAdjustmentService()
            : this(() => new AppDbContext())
        {
        }

        public StockAdjustmentService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void Post(StockAdjustment adjustment)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            db.StockAdjustments.Add(adjustment);
            db.SaveChanges();

            var postingService = new InventoryAdjustmentService(
                new EfInventoryUnitOfWork(db),
                new FixedWarehouseProvider(adjustment.WarehouseId),
                new SystemClock());

            postingService.PostAdjustment(new PostStockAdjustmentCommand(
                Guid.NewGuid(),
                ParseStatus(adjustment.Status),
                adjustment.ReferenceDocumentCode,
                adjustment.ReasonCode,
                BuildLineCommands(db, adjustment.Lines),
                adjustment.PostedBy ?? adjustment.CreatedBy));

            adjustment.Status = StockDocumentStatus.Posted.ToString();
            adjustment.PostedAt = DateTime.Now;
            db.SaveChanges();
            transaction.Commit();
        }

        private static IReadOnlyCollection<StockAdjustmentLineCommand> BuildLineCommands(
            AppDbContext db,
            IEnumerable<StockAdjustmentLine> lines)
        {
            return lines.Select(line =>
            {
                var serialNumbers = Array.Empty<string>();
                if (line.ProductSerialId.HasValue)
                {
                    var serial = db.ProductSerials.Find(line.ProductSerialId.Value)
                        ?? throw new InventoryDomainException($"Serial id {line.ProductSerialId.Value} does not exist.");
                    serialNumbers = new[] { serial.SerialNumber };
                }

                return new StockAdjustmentLineCommand(
                    line.ProductId,
                    ParseDirection(line.Direction),
                    (int)Math.Abs(line.BaseQuantityDelta),
                    serialNumbers);
            }).ToArray();
        }

        private static StockDocumentStatus ParseStatus(string status)
        {
            return Enum.TryParse<StockDocumentStatus>(status, out var parsed)
                ? parsed
                : throw new InventoryDomainException($"Unsupported stock adjustment status {status}.");
        }

        private static StockLedgerDirection ParseDirection(string direction)
        {
            return Enum.TryParse<StockLedgerDirection>(direction, out var parsed)
                ? parsed
                : throw new InventoryDomainException($"Unsupported stock adjustment direction {direction}.");
        }

        private sealed class FixedWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly int _warehouseId;

            public FixedWarehouseProvider(int warehouseId)
            {
                _warehouseId = warehouseId;
            }

            public int GetDefaultWarehouseId()
            {
                return _warehouseId;
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
