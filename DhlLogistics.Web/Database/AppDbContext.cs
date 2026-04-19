namespace DhlLogistics.Web.Database
{
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using System.ComponentModel;

    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Container> Containers => Set<Container>();
        public DbSet<Collection> Collections => Set<Collection>();
        public DbSet<DhlClient> Clients => Set<DhlClient>();
        public DbSet<PickupJob> Jobs => Set<PickupJob>();
        public DbSet<GpsLocation> GpsLocations => Set<GpsLocation>();
        public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    }
}
