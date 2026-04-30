using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Models
{
    public class Product
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        [ImportKey]
        public string ProductCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        public int BrandId { get; set; }
        [ForeignKey("BrandId")]
        public virtual Brand? Brand { get; set; }

        public int DefaultUnitId { get; set; }
        [ForeignKey("DefaultUnitId")]
        public virtual Unit? DefaultUnit { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultPrice { get; set; } = 0;
        
        [MaxLength(100)]
        public string? OriginCountry { get; set; }
        
        public int WarrantyPeriodMonths { get; set; } = 0;
        public bool IsSerialTracked { get; set; } = false;
        public bool IsActive { get; set; } = true;
        
        [NotMapped]
        public decimal TotalQuantity => StockBalances?.Sum(sb => sb.OnHandQuantity) ?? 0;

        // Navigation properties
        public virtual ICollection<ProductUnit>? ProductUnits { get; set; }
        public virtual ICollection<StockBalance>? StockBalances { get; set; }
        public virtual ICollection<StockLedger>? StockLedgers { get; set; }
        public virtual ICollection<ProductSerial>? ProductSerials { get; set; }
    }
}
