using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services.Lessons;

/// <summary>
/// קריאות היומן לכל אחד מהתפקידים. ההגבלה לדייר נעשית ע"י ה-Global Query Filter;
/// ההגבלה להורה/לתלמיד הספציפי — כאן.
/// </summary>
public class LessonQueries(TalmidonDbContext db, FamilyMembers family)
{
    public static readonly Expression<Func<Lesson, LessonDto>> ToDto = l => new LessonDto(
        l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.EndTime, l.Status, l.Origin,
        l.Homework, l.PaymentRequired, l.Amount, l.PaymentId != null, l.CompletedAt, l.SeriesId);

    private static readonly Expression<Func<LessonChangeRequest, ChangeRequestDto>> ToChangeRequestDto = c => new ChangeRequestDto(
        c.Id, c.LessonId, c.Lesson.StudentId, c.Lesson.Student.FullName, c.RequestedByParent.FullName,
        c.Type, c.Lesson.StartTime, c.Lesson.EndTime, c.ProposedStartTime, c.ProposedEndTime,
        c.Reason, c.Status, c.CreatedAt);

    public Task<List<LessonDto>> ListForTeacherAsync(
        DateTimeOffset? from, DateTimeOffset? to, Guid? studentId, LessonStatus? status)
    {
        var query = db.Lessons.AsQueryable();
        if (from is not null) query = query.Where(l => l.EndTime >= from);
        if (to is not null) query = query.Where(l => l.StartTime <= to);
        if (studentId is not null) query = query.Where(l => l.StudentId == studentId);
        if (status is not null) query = query.Where(l => l.Status == status);

        return query.OrderBy(l => l.StartTime).Select(ToDto).ToListAsync();
    }

    public Task<LessonDto?> GetAsync(Guid id) =>
        db.Lessons.Where(l => l.Id == id).Select(ToDto).FirstOrDefaultAsync();

    /// <summary>ברירת המחדל היא הממתינות — זה מה שהמורה צריכה לטפל בו.</summary>
    public Task<List<ChangeRequestDto>> ListChangeRequestsAsync(ChangeRequestStatus? status)
    {
        var wanted = status ?? ChangeRequestStatus.Pending;
        return db.LessonChangeRequests
            .Where(c => c.Status == wanted)
            .OrderBy(c => c.CreatedAt)
            .Select(ToChangeRequestDto)
            .ToListAsync();
    }

    public async Task<ServiceResult<List<LessonDto>>> ListForParentAsync(string userId, Guid? studentId)
    {
        var parent = await family.ParentAsync(userId);
        if (parent is null) return ServiceError.Forbidden;

        var childIds = family.ChildIdsOf(parent);
        var query = db.Lessons.Where(l => childIds.Contains(l.StudentId));
        if (studentId is not null) query = query.Where(l => l.StudentId == studentId);

        return await query.OrderBy(l => l.StartTime).Select(ToDto).ToListAsync();
    }

    /// <summary>
    /// בקשות השינוי של ילדי ההורה — כולל התשובה שהתקבלה עליהן.
    ///
    /// עד לשינוי הזה לא הייתה להורה שום דרך לראות בקשה ששלח: הוא יכול היה ליצור
    /// אחת, ומשם היא נעלמה מבחינתו. דחייה אינה משנה דבר במסכים, ולכן היא הייתה
    /// בלתי נראית לגמרי; וגם הכלל "כבר קיימת בקשה ממתינה לשיעור זה" נשמע שרירותי
    /// כשאי אפשר לראות את הבקשה שכבר קיימת.
    ///
    /// מוחזרות בקשות של כל ההורים המשויכים לאותו ילד ולא רק של המבקש: זו אותה
    /// משפחה, וזה בדיוק מה שמסביר בקשה ממתינה שההורה השני פתח.
    /// </summary>
    public async Task<ServiceResult<List<ChangeRequestDto>>> ListChangeRequestsForParentAsync(string userId)
    {
        var parent = await family.ParentAsync(userId);
        if (parent is null) return ServiceError.Forbidden;

        var childIds = family.ChildIdsOf(parent);
        return await db.LessonChangeRequests
            .Where(c => childIds.Contains(c.Lesson.StudentId))
            .OrderByDescending(c => c.CreatedAt)
            .Select(ToChangeRequestDto)
            .ToListAsync();
    }

    public async Task<ServiceResult<List<StudentLessonDto>>> ScheduleForStudentAsync(string userId)
    {
        var student = await family.StudentAsync(userId);
        if (student is null) return ServiceError.Forbidden;

        return await db.Lessons
            .Where(l => l.StudentId == student.Id)
            .OrderBy(l => l.StartTime)
            .Select(l => new StudentLessonDto(l.Id, l.StartTime, l.EndTime, l.Status, l.Homework))
            .ToListAsync();
    }
}
