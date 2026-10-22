using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services.Students;

/// <summary>קריאות התלמידים: הרשימה והכרטיס של המורה, ומה שהמשפחה רואה על עצמה.</summary>
public class StudentQueries(TalmidonDbContext db, FamilyMembers family)
{
    private static readonly Expression<Func<Student, StudentDetailDto>> ToDetail = s => new StudentDetailDto(
        s.Id, s.FullName, s.Gender, s.GradeLevel, s.BirthDate, s.GeneralInfo,
        s.DefaultPricePerLesson, s.DefaultDurationMinutes, s.IsActive, s.UserId != null,
        s.StudentParents.Select(sp => new ParentSummaryDto(
            sp.Parent.Id, sp.Parent.FullName, sp.Parent.Email, sp.Parent.Phone)).ToList());

    public Task<List<StudentListItemDto>> ListAsync() =>
        db.Students
            .OrderBy(s => s.FullName)
            .Select(s => new StudentListItemDto(
                s.Id, s.FullName, s.GradeLevel, s.IsActive, s.UserId != null, s.StudentParents.Count,
                s.DefaultPricePerLesson, s.DefaultDurationMinutes,
                db.Users.Any(u => u.Id == s.UserId && u.EmailConfirmed)))
            .ToListAsync();

    public Task<StudentDetailDto?> GetAsync(Guid id) =>
        db.Students.Where(s => s.Id == id).Select(ToDetail).FirstOrDefaultAsync();

    public async Task<ServiceResult<List<MyChildDto>>> ChildrenOfParentAsync(string userId)
    {
        var parent = await family.ParentAsync(userId);
        if (parent is null) return ServiceError.Forbidden;

        return await db.StudentParents
            .Where(sp => sp.ParentId == parent.Id)
            .OrderBy(sp => sp.Student.FullName)
            .Select(sp => new MyChildDto(sp.Student.Id, sp.Student.FullName))
            .ToListAsync();
    }

    public Task<MyStudentProfileDto?> ProfileOfStudentAsync(string userId) =>
        db.Students
            .Where(s => s.UserId == userId)
            .Select(s => new MyStudentProfileDto(s.FullName, s.Gender))
            .FirstOrDefaultAsync();
}
