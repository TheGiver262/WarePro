import pandas as pd
import numpy as np
import os

excel_path = r'C:\WarePro\Database\warepro_database_seed.xlsx'
sql_path = r'C:\WarePro\scratch\seed_database.sql'

sheets_order = [
    'AppUser', 'Category', 'Brand', 'Unit', 'Supplier', 'Customer', 'Warehouse', 
    'Product', 'ProductUnit', 'StockBalance', 'StockIn', 'StockOut', 
    'StockInLine', 'StockOutLine', 'ProductSerial', 
    'StockAdjustment', 'StockAdjustmentLine', 'StockCountSession', 'StockCountLine',
    'StockLedger', 'PurchaseInvoice', 'PurchaseInvoiceLine', 'SalesInvoice', 'SalesInvoiceLine',
    'WarrantyCoverage', 'WarrantyClaim', 'AuditLog'
]

if not os.path.exists(os.path.dirname(sql_path)):
    os.makedirs(os.path.dirname(sql_path))

xls = pd.ExcelFile(excel_path)
sql_lines = ["USE ProductManagementDb;", "GO", "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';", "GO"]

for sheet in sheets_order:
    if sheet not in xls.sheet_names:
        print(f"Skipping {sheet}: Not found in Excel.")
        continue
    df = pd.read_excel(xls, sheet)
    if df.empty:
        print(f"Skipping {sheet}: Sheet is empty.")
        continue
    
    sql_lines.append(f"PRINT 'Seeding {sheet}...';")
    sql_lines.append(f"SET IDENTITY_INSERT [{sheet}] ON;")
    
    cols = [c for c in df.columns if not c.startswith('Unnamed')]
    for _, row in df.iterrows():
        vals = []
        for col in cols:
            val = row[col]
            if pd.isna(val):
                vals.append("NULL")
            elif isinstance(val, (bool, np.bool_)):
                vals.append("1" if val else "0")
            elif isinstance(val, (str)):
                # Escape single quotes and use N prefix for unicode
                escaped = val.replace("'", "''")
                vals.append(f"N'{escaped}'")
            elif isinstance(val, (int, float, np.integer, np.floating)):
                if isinstance(val, float) and val.is_integer():
                    vals.append(str(int(val)))
                else:
                    vals.append(str(val))
            elif isinstance(val, (pd.Timestamp)):
                vals.append(f"'{val.strftime('%Y-%m-%d %H:%M:%S')}'")
            else:
                txt = str(val).replace("'", "''")
                vals.append(f"N'{txt}'")
        
        col_str = ", ".join([f"[{c}]" for c in cols])
        val_str = ", ".join(vals)
        
        # Check if ID exists to avoid PK violation
        if 'Id' in cols:
            id_val = row['Id']
            if pd.isna(id_val):
                 sql_lines.append(f"INSERT INTO [{sheet}] ({col_str}) VALUES ({val_str});")
            else:
                 sql_lines.append(f"IF NOT EXISTS (SELECT 1 FROM [{sheet}] WHERE Id = {int(id_val)})")
                 sql_lines.append(f"INSERT INTO [{sheet}] ({col_str}) VALUES ({val_str});")
        else:
            sql_lines.append(f"INSERT INTO [{sheet}] ({col_str}) VALUES ({val_str});")
    
    sql_lines.append(f"SET IDENTITY_INSERT [{sheet}] OFF;")
    sql_lines.append("GO")

sql_lines.append("EXEC sp_MSforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL';")
sql_lines.append("GO")

with open(sql_path, 'w', encoding='utf-8-sig') as f:
    f.write("\n".join(sql_lines))

print(f"Generated SQL script at {sql_path}")
