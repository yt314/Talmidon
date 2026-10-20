using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

/// <summary>
/// שיחות והודעות. שיחה שייכת למורה (דייר) ותמיד עוסקת בתלמידה אחת, גם כשהצד השני
/// הוא ההורה — כך התיבה של המורה מסודרת לפי תלמידות ולא לפי אנשים.
/// </summary>
internal sealed class MessageThreadConfiguration : IEntityTypeConfiguration<MessageThread>
{
    public void Configure(EntityTypeBuilder<MessageThread> e)
    {
        e.Property(t => t.Subject).HasMaxLength(200).IsRequired();
        e.Property(t => t.CounterpartRole).HasConversion<string>().HasMaxLength(20);
        e.Property(t => t.LastSenderRole).HasConversion<string>().HasMaxLength(20);
        e.Property(t => t.LastMessagePreview).HasMaxLength(200).IsRequired();

        e.HasAlternateKey(t => new { t.Id, t.TenantId });

        e.HasOne(t => t.Teacher)
            .WithMany()
            .HasForeignKey(t => t.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // קישור לתלמידה — באותו דייר בלבד, ומחיקת תלמידה מוחקת את שיחותיה
        e.HasOne(t => t.Student)
            .WithMany()
            .HasForeignKey(t => new { t.StudentId, t.TenantId })
            .HasPrincipalKey(s => new { s.Id, s.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        // ההערה שממנה נפתחה השיחה — קישור נוח בלבד. אם ההערה נמחקת, השיחה נשארת:
        // מה שנכתב בה כבר נאמר, ומחיקת הערה אינה סיבה למחוק דיון שלם.
        e.HasOne(t => t.RelatedNote)
            .WithMany()
            .HasForeignKey(t => t.RelatedNoteId)
            .OnDelete(DeleteBehavior.SetNull);

        // התיבה נטענת תמיד כדייר אחד מסודר לפי ההודעה האחרונה
        e.HasIndex(t => new { t.TenantId, t.LastMessageAt });
        e.HasIndex(t => t.StudentId);
        // הצד השני טוען את השיחות שלו בלבד
        e.HasIndex(t => new { t.TenantId, t.CounterpartRole, t.CounterpartId });
    }
}
