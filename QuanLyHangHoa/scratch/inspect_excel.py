import pandas as pd
import os
import sys

# Set output encoding to UTF-8
sys.stdout.reconfigure(encoding='utf-8')

file_path = r"C:\WarePro\Database\WarePro_Export_5-5-2026.xlsx"

if os.path.exists(file_path):
    xl = pd.ExcelFile(file_path)
    print(f"Sheets: {xl.sheet_names}")
    for sheet in xl.sheet_names:
        df = pd.read_excel(file_path, sheet_name=sheet, nrows=5)
        print(f"\nSheet: {sheet}")
        print(df.columns.tolist())
        print(df.head())
else:
    print("File not found")
