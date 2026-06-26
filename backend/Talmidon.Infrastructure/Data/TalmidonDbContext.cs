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
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<StudentParent> StudentParents => Set<StudentParent>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonChangeRequest> LessonChangeRequests => Set<LessonChangeRequest>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureTeacher(builder);
        ConfigureTeacherSubject(builder);
        ConfigureStudent(builder);
        ConfigureParent(builder);
        ConfigureStudentParent(builder);
        ConfigureLesson(builder);
        ConfigureLessonChangeRequest(builder);
        ConfigurePayment(builder);
        ConfigureNote(builder);
        ConfigureRefreshToken(builder);
        ConfigureIdentityOverrides(builder);

        ApplyTenantQueryFilters(builder);
    }

    private static void ConfigureTeacher(ModelBuilder builder)
    {
        builder.Entity<Teacher>(e =>
        {
            e.Property(t => t.FullName).HasMaxLength(200).IsRequired();
            e.Property(t => t.Phone).HasMaxLength(40);
            e.Property(t => t.Bio).HasMaxLength(2000);
            e.Property(t => t.RulesText).HasMaxLength(4000);
            e.Property(t => t.ContactInfo).HasMaxLength(1000);
            e.Property(t => t.DefaultPricePerLesson).HasPrecision(10, 2);

            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(t => t.UserId).IsUnique();
            e.HasIndex(t => t.IsPublic);

            e.ToTable(t => t.HasCheckConstraint(
                "CK_Teachers_DefaultPricePerLesson_NonNegative",
                "\"DefaultPricePerLesson\" >= 0"));
        });
    }

    private static void ConfigureTeacherSubject(ModelBuilder builder)
    {
        builder.Entity<TeacherSubject>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(100).IsRequired();

            e.HasOne(s => s.Teacher)
                .WithMany(t => t.Subjects)
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            // תחום ייחודי לכל מורה (מונע כפילויות בספרייה הציבורית)
            e.HasIndex(s => new { s.TeacherId, s.Name }).IsUnique();
        });
    }

    private static void ConfigureStudent(ModelBuilder builder)
    {
        builder.Entity<Student>(e =>
        {
            e.Property(s => s.FullName).HasMaxLength(200).IsRequired();
            e.Property(s => s.GradeLevel).HasMaxLength(50);
            e.Property(s => s.GeneralInfo).HasMaxLength(4000);

            // מפתח חלופי לשמש כיעד למפתחות זרים מורכבים (Id, TenantId)
            e.HasAlternateKey(s => new { s.Id, s.TenantId });

            e.HasOne(s => s.Teacher)
                .WithMany(t => t.Students)
                .HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(s => s.TenantId);
            e.HasIndex(s => s.UserId).IsUnique();
        });
    }

    private static void ConfigureParent(ModelBuilder builder)
    {
        builder.Entity<Parent>(e =>
        {
            e.Property(p => p.FullName).HasMaxLength(200).IsRequired();
            e.Property(p => p.Email).HasMaxLength(256).IsRequired();
            e.Property(p => p.Phone).HasMaxLength(40);

            e.HasAlternateKey(p => new { p.Id, p.TenantId });

            e.HasOne(p => p.Teacher)
                .WithMany(t => t.Parents)
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // אימייל ייחודי לכל מורה (יעד תזכורות/אישורים)
            e.HasIndex(p => new { p.TenantId, p.Email }).IsUnique();
            e.HasIndex(p => p.UserId).IsUnique();
        });
    }

    private static void ConfigureStudentParent(ModelBuilder builder)
    {
        builder.Entity<StudentParent>(e =>
        {
            e.HasKey(sp => new { sp.StudentId, sp.ParentId });

            // קישור באותו דייר בלבד — מפתחות זרים מורכבים
            e.HasOne(sp => sp.Student)
                .WithMany(s => s.StudentParents)
                .HasForeignKey(sp => new { sp.StudentId, sp.TenantId })
                .HasPrincipalKey(s => new { s.Id, s.TenantId })
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(sp => sp.Parent)
                .WithMany(p => p.StudentParents)
                .HasForeignKey(sp => new { sp.ParentId, sp.TenantId })
                .HasPrincipalKey(p => new { p.Id, p.TenantId })
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(sp => sp.TenantId);
        });
    }

    private static void ConfigureLesson(ModelBuilder builder)
    {
        builder.Entity<Lesson>(e =>
        {
            e.Property(l => l.Homework).HasMaxLength(2000);
            e.Property(l => l.Amount).HasPrecision(10, 2);
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.Origin).HasConversion<string>().HasMaxLength(20);

            e.HasAlternateKey(l => new { l.Id, l.TenantId });

            e.HasOne(l => l.Teacher)
                .WithMany(t => t.Lessons)
                .HasForeignKey(l => l.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // קישור לתלמיד — באותו דייר בלבד (מפתח זר מורכב)
            e.HasOne(l => l.Student)
                .WithMany(s => s.Lessons)
                .HasForeignKey(l => new { l.StudentId, l.TenantId })
                .HasPrincipalKey(s => new { s.Id, s.TenantId })
                .OnDelete(DeleteBehavior.Cascade);

            // הפניות אופציונליות (audit/link) — נשארות חד-עמודה עם SetNull,
            // ובידודן נאכף נוסף ב-SaveChanges (לא ניתן SetNull לעמודת TenantId שאינה nullable).
            e.HasOne(l => l.RequestedByParent)
                .WithMany()
                .HasForeignKey(l => l.RequestedByParentId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(l => l.Payment)
                .WithMany(p => p.CoveredLessons)
                .HasForeignKey(l => l.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(l => l.StudentId);
            e.HasIndex(l => new { l.TenantId, l.StartTime });
            e.HasIndex(l => l.PaymentId);
            // נתיב חם: שיעורים הפתוחים לתשלום (אינדקס חלקי)
            e.HasIndex(l => new { l.TenantId, l.PaymentRequired })
                .HasFilter("\"PaymentId\" IS NULL");

            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Lessons_Amount_NonNegative", "\"Amount\" >= 0");
                t.HasCheckConstraint("CK_Lessons_Time_Order", "\"EndTime\" > \"StartTime\"");
            });
        });
    }

    private static void ConfigureLessonChangeRequest(ModelBuilder builder)
    {
        builder.Entity<LessonChangeRequest>(e =>
        {
            e.Property(c => c.Reason).HasMaxLength(1000);
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(c => c.Lesson)
                .WithMany(l => l.ChangeRequests)
                .HasForeignKey(c => new { c.LessonId, c.TenantId })
                .HasPrincipalKey(l => new { l.Id, l.TenantId })
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(c => c.RequestedByParent)
                .WithMany()
                .HasForeignKey(c => new { c.RequestedByParentId, c.TenantId })
                .HasPrincipalKey(p => new { p.Id, p.TenantId })
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(c => c.TenantId);
            e.HasIndex(c => c.LessonId);

            e.ToTable(t => t.HasCheckConstraint(
                "CK_LessonChangeRequests_ProposedTime_Order",
                "\"ProposedStartTime\" IS NULL OR \"ProposedEndTime\" IS NULL OR \"ProposedEndTime\" > \"ProposedStartTime\""));
        });
    }

    private static void ConfigurePayment(ModelBuilder builder)
    {
        builder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.Property(p => p.Method).HasMaxLength(50);
            e.Property(p => p.Note).HasMaxLength(1000);

            e.HasAlternateKey(p => new { p.Id, p.TenantId });

            e.HasOne(p => p.Teacher)
                .WithMany(t => t.Payments)
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Parent)
                .WithMany(par => par.Payments)
                .HasForeignKey(p => new { p.ParentId, p.TenantId })
                .HasPrincipalKey(par => new { par.Id, par.TenantId })
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => p.ParentId);

            e.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount_NonNegative", "\"Amount\" >= 0"));
        });
    }

    private static void ConfigureNote(ModelBuilder builder)
    {
        builder.Entity<Note>(e =>
        {
            e.Property(n => n.Content).HasMaxLength(4000).IsRequired();

            e.HasOne(n => n.Teacher)
                .WithMany(t => t.Notes)
                .HasForeignKey(n => n.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(n => n.Student)
                .WithMany(s => s.Notes)
                .HasForeignKey(n => new { n.StudentId, n.TenantId })
                .HasPrincipalKey(s => new { s.Id, s.TenantId })
                .OnDelete(DeleteBehavior.Cascade);

            // קישור אופציונלי לשיעור — חד-עמודה עם SetNull
            e.HasOne(n => n.Lesson)
                .WithMany(l => l.Notes)
                .HasForeignKey(n => n.LessonId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(n => n.StudentId);
        });
    }

    private static void ConfigureRefreshToken(ModelBuilder builder)
    {
        builder.Entity<RefreshToken>(e =>
        {
            e.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(r => r.ReplacedByTokenHash).HasMaxLength(128);

            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(r => r.TokenHash).IsUnique();
            e.HasIndex(r => r.UserId);
        });
    }

    /// <summary>אכיפת ייחודיות אימייל ברמת המסד (Identity אוכף ברמת ה-UserManager בלבד).</summary>
    private static void ConfigureIdentityOverrides(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(e =>
            e.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique());
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
