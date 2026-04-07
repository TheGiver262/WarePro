using System;

namespace QuanLyHangHoa.Models
{
    public class Warranty
    {
        public int Id { get; set; }
        
        public int ProductSerialId { get; set; }
        public virtual ProductSerial? ProductSerial { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        public string Status { get; set; } = "Active"; // Active, Expired
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
    }
}
