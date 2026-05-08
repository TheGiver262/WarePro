import pandas as pd
import sys

sys.stdout.reconfigure(encoding='utf-8')
file_path = r"C:\WarePro\Database\WarePro_Export_5-5-2026.xlsx"

sheets_to_read = ["Sản phẩm", "Phiếu nhập kho", "Serial"]

for sheet in sheets_to_read:
    print(f"\n--- {sheet} ---")
    df = pd.read_excel(file_path, sheet_name=sheet)
    if sheet == "Sản phẩm":
        print(df[['id', 'ProductCode']].head(10))
    elif sheet == "Phiếu nhập kho":
        # Check available columns
        print(df.columns.tolist())
        # Try to find something like code or id
        cols = [c for c in df.columns if 'id' in c.lower() or 'code' in c.lower() or 'document' in c.lower()]
        print(df[cols].head(10))
    elif sheet == "Serial":
        print(df[['id', 'SerialCode', 'ProductId', 'StockInId']].head(10))
