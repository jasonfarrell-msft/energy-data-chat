"""
DataLoader: Reads energy data from an Excel file and upserts it into
the energy-data-raw table in Azure SQL Database.

Idempotent — safe to run multiple times. Creates the table and index
if they do not already exist, then merges (upserts) rows by record_datetime.
"""

import io
import os
import sys
from pathlib import Path

import pandas as pd
import pyodbc
from dotenv import load_dotenv

# ──────────────────────────────────────────────
# Configuration
# ──────────────────────────────────────────────

load_dotenv(Path(__file__).parent / ".env")

SQL_SERVER = os.environ["SQL_SERVER"]
SQL_DATABASE = os.environ["SQL_DATABASE"]
SQL_USERNAME = os.environ["SQL_USERNAME"]
SQL_PASSWORD = os.environ["SQL_PASSWORD"]

EXCEL_FILE = Path(__file__).parent / "AI System Load Value 2025 NEW.xlsx"
TABLE_NAME = "[energy-data-raw]"
BATCH_SIZE = 500

# ──────────────────────────────────────────────
# SQL Statements
# ──────────────────────────────────────────────

CREATE_TABLE_SQL = f"""
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'energy-data-raw'
)
BEGIN
    CREATE TABLE {TABLE_NAME} (
        record_datetime DATETIME2(0) NOT NULL,
        megawatt_usage  DECIMAL(18, 6) NOT NULL,
        CONSTRAINT PK_energy_data_raw PRIMARY KEY (record_datetime)
    );
END
"""

CREATE_INDEX_SQL = f"""
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'ix-datetime'
      AND object_id = OBJECT_ID('{TABLE_NAME}')
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix-datetime]
    ON {TABLE_NAME} (record_datetime);
END
"""

MERGE_SQL = f"""
MERGE {TABLE_NAME} AS target
USING (VALUES (?, ?)) AS source (record_datetime, megawatt_usage)
ON target.record_datetime = source.record_datetime
WHEN MATCHED THEN
    UPDATE SET megawatt_usage = source.megawatt_usage
WHEN NOT MATCHED THEN
    INSERT (record_datetime, megawatt_usage)
    VALUES (source.record_datetime, source.megawatt_usage);
"""


def get_connection() -> pyodbc.Connection:
    conn_str = (
        "DRIVER={ODBC Driver 18 for SQL Server};"
        f"SERVER={SQL_SERVER};"
        f"DATABASE={SQL_DATABASE};"
        f"UID={SQL_USERNAME};"
        f"PWD={SQL_PASSWORD};"
        "Encrypt=yes;"
        "TrustServerCertificate=no;"
    )
    return pyodbc.connect(conn_str)


def ensure_schema(cursor: pyodbc.Cursor) -> None:
    """Create the table and index if they don't exist."""
    cursor.execute(CREATE_TABLE_SQL)
    cursor.execute(CREATE_INDEX_SQL)
    cursor.commit()
    print("Schema verified (table + index).")


def read_excel_data() -> pd.DataFrame:
    """Read Column B (datetime) and Column C (megawatt) from the Excel file."""
    try:
        df = pd.read_excel(EXCEL_FILE, header=None, engine="openpyxl")
    except Exception:
        # Fallback: file may be encrypted OLE (DRM label)
        import msoffcrypto

        with open(EXCEL_FILE, "rb") as f:
            ms = msoffcrypto.OfficeFile(f)
            ms.load_key(password="")
            decrypted = io.BytesIO()
            ms.decrypt(decrypted)
            decrypted.seek(0)
        df = pd.read_excel(decrypted, header=None, engine="openpyxl")

    # Columns: A=0, B=1, C=2 — keep B and C only
    df = df[[1, 2]].copy()
    df.columns = ["record_datetime", "megawatt_usage"]

    # Drop the header row and any rows where datetime is missing
    df = df.dropna(subset=["record_datetime"])

    # Convert to proper types
    df["record_datetime"] = pd.to_datetime(df["record_datetime"], errors="coerce")
    df["megawatt_usage"] = pd.to_numeric(df["megawatt_usage"], errors="coerce")

    # Drop any rows that failed conversion
    df = df.dropna()

    print(f"Read {len(df)} rows from Excel.")
    return df


def upsert_data(cursor: pyodbc.Cursor, df: pd.DataFrame) -> None:
    """Upsert data into the table in batches using MERGE."""
    rows = [
        (row.record_datetime.strftime("%Y-%m-%d %H:%M:%S"), float(row.megawatt_usage))
        for row in df.itertuples(index=False)
    ]

    total = len(rows)
    for i in range(0, total, BATCH_SIZE):
        batch = rows[i : i + BATCH_SIZE]
        cursor.executemany(MERGE_SQL, batch)
        cursor.commit()
        loaded = min(i + BATCH_SIZE, total)
        print(f"  Upserted {loaded}/{total} rows...", end="\r")

    print(f"\nUpsert complete: {total} rows processed.")


def main() -> None:
    print(f"Connecting to {SQL_SERVER}/{SQL_DATABASE}...")
    conn = get_connection()
    cursor = conn.cursor()

    try:
        ensure_schema(cursor)
        df = read_excel_data()
        upsert_data(cursor, df)
        print("Done.")
    finally:
        cursor.close()
        conn.close()


if __name__ == "__main__":
    main()
