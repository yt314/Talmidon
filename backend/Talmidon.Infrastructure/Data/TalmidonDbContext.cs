using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Talmidon.Domain.Common;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Infrastructure.Data;

/// <summary>
/// ה-DbContext הראשי. יורש מ-<see cref="IdentityDbContext{TUser}"/> כדי לכלול את טבלאות
/// ההתחברות (AspNetUsers וכו'), ומאכף בידוד רב-דיירות בשלוש שכבות:
/// <list type="number">
/// <item>קריאה — Global Query Filter על כל ישות <see cref="ITenantScoped"/>.</item>
/// <item>כתיבה — חתימה ואימות של TenantId ב-<see cref="SaveChanges()"/>.</item>
/// <item>מסד — מפתחות זרים מורכבים (Id, TenantId) שמונעים קישור חוצה-דייר פיזית.</item>
/// </list>
/// </summary>
public class TalmidonDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly Guid? _currentTenantId;

    public TalmidonDbContext(DbContextOptions<TalmidonDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenantId = currentTenant.TenantId;
    }

    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<StudentParent> StudentParents => Set<StudentParent>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonSeries> LessonSeries => Set<LessonSeries>();
    public DbSet<LessonChangeRequest> LessonChangeRequests => Set<LessonChangeRequest>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<StudentResource> StudentResources => Set<StudentResource>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<SubjectSuggestion> SubjectSuggestions => Set<SubjectSuggestion>();
    public DbSet<SiteFeedback> SiteFeedback => Set<SiteFeedback>();
    public DbSet<ErrorLogEntry> ErrorLog => Set<ErrorLogEntry>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // מבנה הטבלאות: מחלקה לכל ישות ב-Data/Configurations
        builder.ApplyConfigurationsFromAssembly(typeof(TalmidonDbContext).Assembly);

        ApplyTenantQueryFilters(builder);
    }

    /// <summary>
    /// מחיל Global Query Filter על כל ישות <see cref="ITenantScoped"/>:
    /// <c>e => e.TenantId == _currentTenantId</c>. EF מפרמטר את השדה ומעריך אותו
    /// מחדש בכל שאילתה לפי הדייר הנוכחי (fail-closed: דייר null → אפס שורות).
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var filter = (LambdaExpression)BuildTenantFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, null)!;

                builder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    private static readonly MethodInfo BuildTenantFilterMethod = typeof(TalmidonDbContext)
        .GetMethod(nameof(BuildTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private LambdaExpression BuildTenantFilter<TEntity>() where TEntity : class, ITenantScoped
    {
        Expression<Func<TEntity, bool>> filter = e => e.TenantId == _currentTenantId;
        return filter;
    }

    // ----- אכיפת דייר בכתיבה -----

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantOnSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceTenantOnSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// חותם ומאמת את TenantId לפני שמירה: חדש → נחתם בדייר הנוכחי (או נדרש להיות מפורש);
    /// עדכון → אסור לשנות TenantId ואסור לחרוג מהדייר הנוכחי. שכבת הגנה מעל מסנן הקריאה
    /// ומעל ה-FKs המורכבים.
    /// </summary>
    private void EnforceTenantOnSave()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (_currentTenantId is Guid tenantForAdd)
                    {
                        if (entry.Entity.TenantId == Guid.Empty)
                            entry.Entity.TenantId = tenantForAdd;
                        else if (entry.Entity.TenantId != tenantForAdd)
                            throw new InvalidOperationException(
                                $"Cross-tenant write blocked: {entry.Entity.GetType().Name}.TenantId does not match the current tenant.");
                    }
                    else if (entry.Entity.TenantId == Guid.Empty)
                    {
                        throw new InvalidOperationException(
                            $"Cannot persist {entry.Entity.GetType().Name}: no current tenant and TenantId is empty.");
                    }
                    break;

                case EntityState.Modified:
                    var originalTenant = entry.OriginalValues.GetValue<Guid>(nameof(ITenantScoped.TenantId));
                    if (originalTenant != entry.Entity.TenantId)
                        throw new InvalidOperationException(
                            $"Changing TenantId is not allowed ({entry.Entity.GetType().Name}).");
                    if (_currentTenantId is Guid tenantForUpdate && entry.Entity.TenantId != tenantForUpdate)
                        throw new InvalidOperationException(
                            $"Cross-tenant update blocked: {entry.Entity.GetType().Name}.TenantId does not match the current tenant.");
                    break;
            }
        }
    }
}
