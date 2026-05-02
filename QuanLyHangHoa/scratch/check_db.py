import sqlite3
import os

db_path = r'C:\WarePro\QuanLyHangHoa\bin\Debug\net8.0-windows\Database\QuanLyHangHoa_v2.db'
if not os.path.exists(db_path):
    # try common locations
    db_path = r'C:\WarePro\QuanLyHangHoa\Database\QuanLyHangHoa_v2.db'

print(f"Checking DB at: {db_path}")
conn = sqlite3.connect(db_path)
cursor = conn.cursor()

cursor.execute("PRAGMA table_info(SalesInvoices)")
columns = cursor.fetchall()
for col in columns:
    print(col)

conn.close()
