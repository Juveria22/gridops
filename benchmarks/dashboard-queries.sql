-- Dashboard query benchmark. Run against GridOps_Bench (see benchmarks/README.md).
-- Each query runs 50x, parameterized like EF Core sends it.
-- Results come from sys.dm_exec_query_stats: avg server elapsed time + logical reads (8 KB pages).
SET NOCOUNT ON;
ALTER DATABASE SCOPED CONFIGURATION CLEAR PROCEDURE_CACHE;

DECLARE @runs int = 50, @i int = 0;
DECLARE @since datetimeoffset = DATEADD(day, -30, SYSDATETIMEOFFSET());

WHILE @i < @runs
BEGIN
    -- 1. active outages in one borough, newest first, page 1
    EXEC sp_executesql
        N'/*bench:active_by_borough*/ SELECT TOP(@take) Id, Title, Borough, Neighborhood, Status, Priority, CustomersAffected, ReportedAt
          FROM Outages WHERE Borough = @borough AND Status IN (N''Reported'', N''Investigating'', N''Restoring'')
          ORDER BY ReportedAt DESC',
        N'@borough nvarchar(20), @take int', @borough = N'Brooklyn', @take = 25;

    EXEC sp_executesql
        N'/*bench:active_by_borough_count*/ SELECT COUNT(*) FROM Outages
          WHERE Borough = @borough AND Status IN (N''Reported'', N''Investigating'', N''Restoring'')',
        N'@borough nvarchar(20)', @borough = N'Brooklyn';

    -- 2. high/critical outages in the last 30 days
    EXEC sp_executesql
        N'/*bench:recent_high_priority*/ SELECT TOP(@take) Id, Title, Borough, Neighborhood, Status, Priority, CustomersAffected, ReportedAt
          FROM Outages WHERE ReportedAt >= @since AND Priority IN (N''High'', N''Critical'')
          ORDER BY ReportedAt DESC',
        N'@since datetimeoffset, @take int', @since = @since, @take = 25;

    EXEC sp_executesql
        N'/*bench:recent_high_priority_count*/ SELECT COUNT(*) FROM Outages
          WHERE ReportedAt >= @since AND Priority IN (N''High'', N''Critical'')',
        N'@since datetimeoffset', @since = @since;

    -- 3. default dashboard view: everything, newest first
    EXEC sp_executesql
        N'/*bench:all_newest*/ SELECT TOP(@take) Id, Title, Borough, Neighborhood, Status, Priority, CustomersAffected, ReportedAt
          FROM Outages ORDER BY ReportedAt DESC',
        N'@take int', @take = 25;

    SET @i += 1;
END;

SELECT
    SUBSTRING(t.text, CHARINDEX('/*bench:', t.text) + 8, CHARINDEX('*/', t.text) - CHARINDEX('/*bench:', t.text) - 8) AS query,
    qs.execution_count AS runs,
    CAST(qs.total_elapsed_time / qs.execution_count / 1000.0 AS decimal(10, 2)) AS avg_ms,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) t
WHERE t.text LIKE '%/*bench:%' AND t.text NOT LIKE '%dm_exec_query_stats%' AND t.text NOT LIKE '%WHILE @i%'
ORDER BY query;
