using Example.EnergyAnalyticsMcp.Entities;
using Microsoft.EntityFrameworkCore;

namespace Example.EnergyAnalyticsMcp.Data;

public class EnergyDbContext : DbContext
{
    public EnergyDbContext(DbContextOptions<EnergyDbContext> options) : base(options) { }

    public DbSet<EnergyDataRaw> RawData => Set<EnergyDataRaw>();
    public DbSet<EnergyDataDaily> DailyData => Set<EnergyDataDaily>();
    public DbSet<EnergyDataWeekly> WeeklyData => Set<EnergyDataWeekly>();
    public DbSet<EnergyDataMonthly> MonthlyData => Set<EnergyDataMonthly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnergyDataRaw>(e =>
        {
            e.Property(r => r.MegawattUsage).HasPrecision(18, 6);
        });

        modelBuilder.Entity<EnergyDataDaily>(e =>
        {
            e.Property(d => d.AverageMw).HasPrecision(18, 6);
            e.Property(d => d.MaxMw).HasPrecision(18, 6);
            e.Property(d => d.MinMw).HasPrecision(18, 6);
            e.Property(d => d.LoadFactor).HasPrecision(9, 6);
        });

        modelBuilder.Entity<EnergyDataWeekly>(e =>
        {
            e.Property(w => w.SystemId).HasDefaultValueSql("NEWID()");
            e.Property(w => w.AverageMw).HasPrecision(18, 6);
            e.Property(w => w.MaxMw).HasPrecision(18, 6);
            e.Property(w => w.MinMw).HasPrecision(18, 6);
            e.Property(w => w.LoadFactor).HasPrecision(9, 6);
        });

        modelBuilder.Entity<EnergyDataMonthly>(e =>
        {
            e.Property(m => m.SystemId).HasDefaultValueSql("NEWID()");
            e.Property(m => m.AverageMw).HasPrecision(18, 6);
            e.Property(m => m.MaxMw).HasPrecision(18, 6);
            e.Property(m => m.MinMw).HasPrecision(18, 6);
            e.Property(m => m.LoadFactor).HasPrecision(9, 6);
        });
    }
}
