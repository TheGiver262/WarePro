using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class WarrantyService
    {
        public List<Warranty> GetAll()
        {
            using var db = new AppDbContext();
            return db.Warranties
                .Where(w => !w.IsDeleted)
                .Include(w => w.ProductSerial)
                    .ThenInclude(ps => ps!.Product)
                .ToList();
        }

        public void Add(Warranty w)
        {
            using var db = new AppDbContext();
            db.Warranties.Add(w);
            db.SaveChanges();
        }

        public void Update(Warranty updated)
        {
            using var db = new AppDbContext();
            var w = db.Warranties.Find(updated.Id);
            if (w == null) return;
            w.StartDate = updated.StartDate;
            w.EndDate   = updated.EndDate;
            w.Status    = updated.Status;
            w.ImageUrl  = updated.ImageUrl;
            db.SaveChanges();
        }

        public void SoftDelete(int id)
        {
            using var db = new AppDbContext();
            var w = db.Warranties.Find(id);
            if (w == null) return;
            w.IsDeleted = true;
            db.SaveChanges();
        }

        public Warranty? GetBySerial(string serialNumber)
        {
            using var db = new AppDbContext();
            return db.Warranties
                .Include(w => w.ProductSerial)
                    .ThenInclude(ps => ps!.Product)
                .FirstOrDefault(w => w.ProductSerial != null && w.ProductSerial.SerialNumber == serialNumber && !w.IsDeleted);
        }
    }
}
