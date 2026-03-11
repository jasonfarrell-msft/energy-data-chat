/*
    Stored Procedure: usp_RollupEnergyDataDaily
    Purpose:          Aggregates 5-minute interval energy data from [energy-data-raw]
                      into daily summaries in [energy-data-daily].
    Idempotent:       Yes — uses MERGE to upsert; safe to re-run at any time.
    Source Table:     [energy-data-raw] (record_datetime DATETIME2(0), megawatt_usage DECIMAL(18,6))
    Target Table:     [energy-data-daily] (created if not exists)
*/

-- Create target table if it does not already exist
IF OBJECT_ID(N'[energy-data-daily]', N'U') IS NULL
BEGIN
    CREATE TABLE [energy-data-daily]
    (
        [day]           DATE            NOT NULL,
        average_mw      DECIMAL(18,6)   NOT NULL,
        max_mw          DECIMAL(18,6)   NOT NULL,
        max_mw_time     DATETIME2(0)    NOT NULL,
        min_mw          DECIMAL(18,6)   NOT NULL,
        min_mw_time     DATETIME2(0)    NOT NULL,
        load_factor     DECIMAL(9,6)    NOT NULL,   -- average_mw / max_mw

        CONSTRAINT [pk-energy-data-daily] PRIMARY KEY CLUSTERED ([day])
    );
END;

-- Create index on [day] column if it does not already exist
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'[energy-data-daily]')
      AND  name      = N'ix-energy-data-daily-day'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix-energy-data-daily-day]
        ON [energy-data-daily] ([day]);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_RollupEnergyDataDaily
AS
BEGIN
    SET NOCOUNT ON;

    /*
        CTE: DailyAgg
        Compute daily aggregates from the raw 5-minute interval data.
        Uses FIRST_VALUE with a window ordered by megawatt_usage to resolve
        the exact timestamp of the daily max and min readings.
    */
    ;WITH DailyAgg AS
    (
        SELECT
            CAST(record_datetime AS DATE)                       AS [day],
            AVG(megawatt_usage)                                 AS average_mw,
            MAX(megawatt_usage)                                 AS max_mw,
            MIN(megawatt_usage)                                 AS min_mw,
            -- Timestamp of the maximum reading (earliest if tied)
            MIN(CASE
                WHEN megawatt_usage = max_day.max_mw
                THEN record_datetime
            END)                                                AS max_mw_time,
            -- Timestamp of the minimum reading (earliest if tied)
            MIN(CASE
                WHEN megawatt_usage = min_day.min_mw
                THEN record_datetime
            END)                                                AS min_mw_time
        FROM [energy-data-raw] r
        CROSS APPLY (
            SELECT MAX(megawatt_usage) AS max_mw
            FROM   [energy-data-raw]
            WHERE  CAST(record_datetime AS DATE) = CAST(r.record_datetime AS DATE)
        ) max_day
        CROSS APPLY (
            SELECT MIN(megawatt_usage) AS min_mw
            FROM   [energy-data-raw]
            WHERE  CAST(record_datetime AS DATE) = CAST(r.record_datetime AS DATE)
        ) min_day
        GROUP BY CAST(record_datetime AS DATE)
    )

    /*
        MERGE: Upsert daily rows into [energy-data-daily].
        - Matched rows are updated with recalculated values.
        - Unmatched rows are inserted.
        This guarantees idempotency — re-running always reflects current raw data.
    */
    MERGE [energy-data-daily] AS tgt
    USING DailyAgg             AS src
        ON tgt.[day] = src.[day]

    WHEN MATCHED THEN
        UPDATE SET
            tgt.average_mw   = src.average_mw,
            tgt.max_mw       = src.max_mw,
            tgt.max_mw_time  = src.max_mw_time,
            tgt.min_mw       = src.min_mw,
            tgt.min_mw_time  = src.min_mw_time,
            tgt.load_factor  = CASE
                                   WHEN src.max_mw = 0 THEN 0
                                   ELSE src.average_mw / src.max_mw
                               END

    WHEN NOT MATCHED BY TARGET THEN
        INSERT ([day], average_mw, max_mw, max_mw_time, min_mw, min_mw_time, load_factor)
        VALUES (
            src.[day],
            src.average_mw,
            src.max_mw,
            src.max_mw_time,
            src.min_mw,
            src.min_mw_time,
            CASE
                WHEN src.max_mw = 0 THEN 0
                ELSE src.average_mw / src.max_mw
            END
        );
END;
GO
