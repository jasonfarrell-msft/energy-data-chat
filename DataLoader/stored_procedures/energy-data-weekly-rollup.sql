/*
    Stored Procedure: usp_RollupEnergyDataWeekly
    Purpose:          Aggregates daily energy data from [energy-data-daily]
                      into weekly summaries in [energy-data-weekly].
    Idempotent:       Yes — uses MERGE to upsert; safe to re-run at any time.
    Source Table:     [energy-data-daily] (day, average_mw, max_mw, max_mw_time, min_mw, min_mw_time, load_factor)
    Target Table:     [energy-data-weekly] (created if not exists)
*/

-- Create target table if it does not already exist
IF OBJECT_ID(N'[energy-data-weekly]', N'U') IS NULL
BEGIN
    CREATE TABLE [energy-data-weekly]
    (
        system_id       UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWID(),
        week_start      DATETIME2(0)        NOT NULL,
        week_end        DATETIME2(0)        NOT NULL,
        average_mw      DECIMAL(18,6)       NOT NULL,
        max_mw          DECIMAL(18,6)       NOT NULL,
        max_mw_time     DATETIME2(0)        NOT NULL,
        min_mw          DECIMAL(18,6)       NOT NULL,
        min_mw_time     DATETIME2(0)        NOT NULL,
        load_factor     DECIMAL(9,6)        NOT NULL,   -- average_mw / max_mw

        CONSTRAINT PK_load_weekly PRIMARY KEY CLUSTERED (week_start)
    );
END;

-- Create index on week_start if it does not already exist
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'[energy-data-weekly]')
      AND  name      = N'ix-energy-data-weekly-week-start'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix-energy-data-weekly-week-start]
        ON [energy-data-weekly] (week_start);
END;

-- Create index on week_end if it does not already exist
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'[energy-data-weekly]')
      AND  name      = N'ix-energy-data-weekly-week-end'
)
BEGIN
    CREATE NONCLUSTERED INDEX [ix-energy-data-weekly-week-end]
        ON [energy-data-weekly] (week_end);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_RollupEnergyDataWeekly
AS
BEGIN
    SET NOCOUNT ON;

    /*
        CTE: WeeklyAgg
        Aggregate daily data into ISO weeks (Monday–Sunday).
        DATETRUNC(WEEK, [day]) gives the Monday of each week.
        week_end is computed as week_start + 6 days (Sunday).
    */
    ;WITH WeeklyAgg AS
    (
        SELECT
            CAST(DATETRUNC(WEEK, [day]) AS DATETIME2(0))                AS week_start,
            CAST(DATEADD(DAY, 6, DATETRUNC(WEEK, [day])) AS DATETIME2(0)) AS week_end,
            AVG(average_mw)                                             AS average_mw,
            MAX(max_mw)                                                 AS max_mw,
            MIN(min_mw)                                                 AS min_mw
        FROM [energy-data-daily]
        GROUP BY DATETRUNC(WEEK, [day])
    ),

    /*
        CTE: WithTimestamps
        Resolve the exact timestamps for the weekly max and min readings
        by joining back to the daily table.
    */
    WithTimestamps AS
    (
        SELECT
            w.week_start,
            w.week_end,
            w.average_mw,
            w.max_mw,
            w.min_mw,
            -- Timestamp of the daily max that matches the weekly max (earliest day if tied)
            (
                SELECT TOP 1 d.max_mw_time
                FROM   [energy-data-daily] d
                WHERE  DATETRUNC(WEEK, d.[day]) = w.week_start
                  AND  d.max_mw = w.max_mw
                ORDER BY d.[day]
            ) AS max_mw_time,
            -- Timestamp of the daily min that matches the weekly min (earliest day if tied)
            (
                SELECT TOP 1 d.min_mw_time
                FROM   [energy-data-daily] d
                WHERE  DATETRUNC(WEEK, d.[day]) = w.week_start
                  AND  d.min_mw = w.min_mw
                ORDER BY d.[day]
            ) AS min_mw_time
        FROM WeeklyAgg w
    )

    /*
        MERGE: Upsert weekly rows into [energy-data-weekly].
        - Matched rows are updated with recalculated values.
        - Unmatched rows are inserted.
        This guarantees idempotency — re-running always reflects current daily data.
    */
    MERGE [energy-data-weekly] AS tgt
    USING (
        SELECT
            week_start,
            week_end,
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
        ON  tgt.week_start = src.week_start
        AND tgt.week_end   = src.week_end

    WHEN MATCHED THEN
        UPDATE SET
            tgt.average_mw   = src.average_mw,
            tgt.max_mw       = src.max_mw,
            tgt.max_mw_time  = src.max_mw_time,
            tgt.min_mw       = src.min_mw,
            tgt.min_mw_time  = src.min_mw_time,
            tgt.load_factor  = src.load_factor

    WHEN NOT MATCHED BY TARGET THEN
        INSERT (system_id, week_start, week_end, average_mw, max_mw, max_mw_time, min_mw, min_mw_time, load_factor)
        VALUES (
            NEWID(),
            src.week_start,
            src.week_end,
            src.average_mw,
            src.max_mw,
            src.max_mw_time,
            src.min_mw,
            src.min_mw_time,
            src.load_factor
        );
END;
GO
