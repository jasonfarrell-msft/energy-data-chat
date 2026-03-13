using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Example.EnergyAnalyticsMcp.Entities;

[Table("energy-data-raw")]
public class EnergyDataRaw
{
    [Key]
    [Column("record_datetime")]
    public DateTime RecordDatetime { get; set; }

    [Column("megawatt_usage")]
    public decimal MegawattUsage { get; set; }
}
