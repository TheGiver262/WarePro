using System;

namespace QuanLyHangHoa.Services.DataImport
{
    // đánh dấu property dùng để nhận diện bản ghi cũ khi import lại cùng dữ liệu
    [AttributeUsage(AttributeTargets.Property)]
    public class ImportKeyAttribute : Attribute
    {
    }
}
