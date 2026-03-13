using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Example.EnergyAnalyticsMcp.Entities;

[Table("energy-data-daily")]
public class EnergyDataDaily
{
    [Key]
    [Column("day")]
    public DateOnly Day { get; set; }

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
