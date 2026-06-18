namespace DhlLogistics.Web.Database;

using DhlLogistics.Web.Model;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Standalone EF Core context for the navigation menu, backed by a LOCAL SQL Server
/// database (connection string "MenuConnection"). Deliberately separate from
/// <see cref="AppDbContext"/> (Postgres) — the menu is the only entity here, so the
/// schema is created with EnsureCreated rather than the app's migration pipeline.
///
/// Resolved through <c>IDbContextFactory&lt;MenuDbContext&gt;</c> so the interactive
/// NavMenu component can spin up a short-lived context off the render thread, the
/// same pattern CBM uses for its menu.
/// </summary>
public class MenuDbContext : DbContext
{
    public MenuDbContext(DbContextOptions<MenuDbContext> options) : base(options) { }

    public DbSet<Menu> Menus => Set<Menu>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<Menu>(e =>
        {
            e.ToTable("Menus");
            e.HasKey(m => m.MenuId);
            e.Property(m => m.MenuName).HasMaxLength(100).IsRequired();
            e.Property(m => m.Icon).HasMaxLength(16);
            e.Property(m => m.PageName).HasMaxLength(200);
            e.HasIndex(m => m.ParentId);
            e.HasIndex(m => m.ShowOrder);
        });
    }
}
