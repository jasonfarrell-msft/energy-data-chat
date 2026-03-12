/*
    Stored Procedure: usp_RollupEnergyDataMonthly
    Purpose:          Aggregates daily energy data from [energy-data-daily]
                      into monthly summaries in [energy-data-monthly].
    Idempotent:       Yes — uses MERGE to upsert; safe to re-run at any time.
    Source Table:     [energy-data-daily] (day, average_mw, max_mw, max_mw_time, min_mw, min_mw_time, load_factor)
    Target Table:     [energy-data-monthly] (created if not exists)
*/

-- Create target table if it does not already exist
IF OBJECT_ID(N'[energy-data-monthly]', N'U') IS NULL
BEGIN
    CREATE TABLE [energy-data-monthly]
    (
        system_id       UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID(),
        month_start     DATETIME2(0)        NOT NULL,
        month_end       DATETIME2(0)        NOT NULL,
        average_mw      DECIMAL(18,6)       NOT NULL,
        max_mw          DECIMAL(18,6)       NOT NULL,
        max_mw_time     DATETIME2(0)        NOT NULL,
        min_mw          DECIMAL(18,6)       NOT NULL,
        min_mw_time     DATETIME2(0)        NOT NULL,
        load_factor     DECIMAL(9,6)        NOT NULL,   -- average_mw / max_mw

        CONSTRAINT PK_load_monthly PRIMARY KEY CLUSTERED (month_start)
    );
END;

-- Create index on month_start if it does not already exist
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'[energy-data-monthly]')
      AND  name      = N'ix-energy-data-monthly-month-start'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix-energy-data-monthly-month-start]
        ON [energy-data-monthly] (month_start);
END;

-- Create index on month_end if it does not already exist
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'[energy-data-monthly]')
      AND  name      = N'ix-energy-data-monthly-month-end'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix-energy-data-monthly-month-end]
        ON [energy-data-monthly] (month_end);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_RollupEnergyDataMonthly
AS
BEGIN
    SET NOCOUNT ON;

    /*
        CTE: MonthlyAgg
        Aggregate daily data into calendar months.
        DATETRUNC(MONTH, [day]) gives the first day of each month.
        month_end is computed as the last day of the month.
    */
    ;WITH MonthlyAgg AS
    (
        SELECT
            CAST(DATETRUNC(MONTH, [day]) AS DATETIME2(0))                                       AS month_start,
            CAST(EOMONTH([day]) AS DATETIME2(0))                                                AS month_end,
            AVG(average_mw)                                                                     AS average_mw,
            MAX(max_mw)                                                                         AS max_mw,
            MIN(min_mw)                                                                         AS min_mw
        FROM [energy-data-daily]
        GROUP BY DATETRUNC(MONTH, [day]), EOMONTH([day])
    ),

    /*
        CTE: WithTimestamps
        Resolve the exact timestamps for the monthly max and min readings
        by joining back to the daily table.
    */
    WithTimestamps AS
    (
        SELECT
            m.month_start,
            m.month_end,
            m.average_mw,
            m.max_mw,
            m.min_mw,
            -- Timestamp of the daily max that matches the monthly max (earliest day if tied)
            (
                SELECT TOP 1 d.max_mw_time
                FROM   [energy-data-daily] d
                WHERE  DATETRUNC(MONTH, d.[day]) = m.month_start
                  AND  d.max_mw = m.max_mw
                ORDER BY d.[day]
            ) AS max_mw_time,
            -- Timestamp of the daily min that matches the monthly min (earliest day if tied)
            (
                SELECT TOP 1 d.min_mw_time
                FROM   [energy-data-daily] d
                WHERE  DATETRUNC(MONTH, d.[day]) = m.month_start
                  AND  d.min_mw = m.min_mw
                ORDER BY d.[day]
            ) AS min_mw_time
        FROM MonthlyAgg m
    )

    /*
        MERGE: Upsert monthly rows into [energy-data-monthly].
        - Matched rows are updated with recalculated values.
        - Unmatched rows are inserted.
        This guarantees idempotency — re-running always reflects current daily data.
    */
    MERGE [energy-data-monthly] AS tgt
    USING (
        SELECT
            month_start,
            month_end,
            average_mw,
            max_mw,
            max_mw_time,
            min_mw,
            min_mw_time,
            CASE
                WHEN max_mw = 0 THEN 0
                ELSE average_mw / max_mw
            END             AS load_factor
        FROM WithTimestamps
    ) AS src
        ON  tgt.month_start = src.month_start
        AND tgt.month_end   = src.month_end

    WHEN MATCHED THEN
        UPDATE SET
            tgt.average_mw   = src.average_mw,
            tgt.max_mw       = src.max_mw,
            tgt.max_mw_time  = src.max_mw_time,
            tgt.min_mw       = src.min_mw,
            tgt.min_mw_time  = src.min_mw_time,
            tgt.load_factor  = src.load_factor

    WHEN NOT MATCHED BY TARGET THEN
        INSERT (system_id, month_start, month_end, average_mw, max_mw, max_mw_time, min_mw, min_mw_time, load_factor)
        VALUES (
            NEWID(),
            src.month_start,
            src.month_end,
            src.average_mw,
            src.max_mw,
            src.max_mw_time,
            src.min_mw,
            src.min_mw_time,
            src.load_factor
        );
END;
GO
