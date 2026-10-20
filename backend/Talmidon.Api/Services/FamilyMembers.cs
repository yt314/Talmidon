using Microsoft.EntityFrameworkCore;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services;

/// <summary>
/// מי המשתמש המחובר בתוך המשפחה, ולמי מותר לו לגשת — הרשאה ברמת שורה להורה ולתלמיד.
/// </summary>
public class FamilyMembers(TalmidonDbContext db)
{
    public Task<Parent?> ParentAsync(string userId) => db.Parents.FirstOrDefaultAsync(p => p.UserId == userId);

    public Task<Student?> StudentAsync(string userId) => db.Students.FirstOrDefaultAsync(s => s.UserId == userId);

    /// <summary>תת-שאילתה (לא רשימה): נשלחת ל-Postgres כחלק מהשאילתה שמשתמשת בה.</summary>
    public IQueryable<Guid> ChildIdsOf(Parent parent) =>
        db.StudentParents.Where(sp => sp.ParentId == parent.Id).Select(sp => sp.StudentId);

    public Task<bool> IsChildOfAsync(Parent parent, Guid studentId) =>
        db.StudentParents.AnyAsync(sp => sp.ParentId == parent.Id && sp.StudentId == studentId);
}
