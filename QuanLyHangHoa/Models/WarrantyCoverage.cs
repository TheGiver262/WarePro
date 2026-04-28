using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class WarrantyCoverage
    {
        public int Id { get; set; }

        public int ProductSerialId { get; set; }
        public virtual ProductSerial? ProductSerial { get; set; }

        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        public int? SalesInvoiceId { get; set; }
        public virtual SalesInvoice? SalesInvoice { get; set; }

        public DateTime WarrantyStartDate { get; set; }
        public DateTime WarrantyEndDate { get; set; }
        public string CoverageStatus { get; set; } = string.Empty;

        public virtual ICollection<WarrantyClaim> Claims { get; set; } = new List<WarrantyClaim>();
    }
}
