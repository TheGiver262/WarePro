using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class WarrantyCoverage
    {
        public int Id { get; set; }

        public int ProductSerialId { get; set; }
        [ForeignKey("ProductSerialId")]
        public virtual ProductSerial? ProductSerial { get; set; }

        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        public int? SalesInvoiceId { get; set; }
        [ForeignKey("SalesInvoiceId")]
        public virtual SalesInvoice? SalesInvoice { get; set; }

        public DateTime WarrantyStartDate { get; set; }
        public DateTime WarrantyEndDate { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string CoverageStatus { get; set; } = "Active"; // Active, Expired, Voided

        public virtual ICollection<WarrantyClaim>? Claims { get; set; }
    }
}
