using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Example.EnergyAnalyticsMcp.Entities;

[Table("energy-data-weekly")]
public class EnergyDataWeekly
{
    [Column("system_id")]
    public Guid SystemId { get; set; }

    [Key]
    [Column("week_start")]
    public DateTime WeekStart { get; set; }

    [Column("week_end")]
    public DateTime WeekEnd { get; set; }

    [Column("average_mw")]
    public decimal AverageMw { get; set; }

    [Column("max_mw")]
    public decimal MaxMw { get; set; }

    [Column("max_mw_time")]
    public DateTime MaxMwTime { get; set; }

    [Column("min_mw")]
    public decimal MinMw { get; set; }

    [Column("min_mw_time")]
    public DateTime MinMwTime { get; set; }

    [Column("load_factor")]
    public decimal LoadFactor { get; set; }
}
