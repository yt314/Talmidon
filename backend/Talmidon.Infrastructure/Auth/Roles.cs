namespace Talmidon.Infrastructure.Auth;

/// <summary>שמות התפקידים במערכת.</summary>
public static class Roles
{
    public const string Teacher = "Teacher";
    public const string Parent = "Parent";
    public const string Student = "Student";
    public const string Admin = "Admin";

    public static readonly string[] All = [Teacher, Parent, Student, Admin];
}
