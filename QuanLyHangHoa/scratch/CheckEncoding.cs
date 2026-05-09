using System;
using System.IO;
using System.Text;

class Program {
    static void Main(string[] args) {
        string path = @"C:\WarePro\QuanLyHangHoa\Themes\Tables.xaml";
        using (var reader = new StreamReader(path, true)) {
            reader.Peek(); // Trigger encoding detection
            Console.WriteLine($"Encoding: {reader.CurrentEncoding.EncodingName}");
            
            // Read first few lines to see if there are any weird characters
            for (int i = 0; i < 200; i++) {
                string line = reader.ReadLine();
                if (line == null) break;
                if (line.Contains("Value=\"Đã bán\"") || line.Contains("Value=\"Đã đặt\"")) {
                    Console.WriteLine($"Line {i+1}: {line}");
                    foreach (char c in line) {
                        if (c > 127) {
                            Console.WriteLine($"Char: {c} (U+{(int)c:X4})");
                        }
                    }
                }
            }
        }
    }
}
