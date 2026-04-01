using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    // Dịch vụ quản lý Hàng hoá: Chịu trách nhiệm tương tác với SQL Server
    public class ProductService
    {
        // Lấy danh sách toàn bộ hàng hoá
        public List<Product> GetAllProducts()
        {
            using (var db = new AppDbContext())
            {
                // Truy vấn và trả về List
                return db.Products.ToList();
            }
        }

        // Thêm hàng hoá mới vào Database
        public void AddProduct(Product p)
        {
            using (var db = new AppDbContext())
            {
                db.Products.Add(p);
                db.SaveChanges(); // Lệnh này sẽ thực thi (commit) lên SQL Server
            }
        }

        // Cập nhật thông tin hàng hoá
        public void UpdateProduct(Product updatedProduct)
        {
            using (var db = new AppDbContext())
            {
                var p = db.Products.Find(updatedProduct.Id);
                if (p != null)
                {
                    // Copy dữ liệu mới đè lên cái cũ
                    p.Name = updatedProduct.Name;
                    p.Category = updatedProduct.Category;
                    p.Quantity = updatedProduct.Quantity;
                    p.UnitPrice = updatedProduct.UnitPrice;
                    p.Origin = updatedProduct.Origin;
                    p.WarrantyMonths = updatedProduct.WarrantyMonths;
                    p.Notes = updatedProduct.Notes;
                    
                    db.SaveChanges();
                }
            }
        }

        // Xoá hàng hoá
        public void DeleteProduct(int id)
        {
            using (var db = new AppDbContext())
            {
                var p = db.Products.Find(id);
                if (p != null)
                {
                    db.Products.Remove(p);
                    db.SaveChanges();
                }
            }
        }
    }
}
