using Talmidon.Api.Multitenancy;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Auth;
using Talmidon.Api.Services.Lessons;
using Talmidon.Api.Services.Messages;
using Talmidon.Api.Services.Parents;
using Talmidon.Api.Services.Payments;
using Talmidon.Api.Services.Public;
using Talmidon.Api.Services.Students;
using Talmidon.Api.Services.Teachers;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Startup;

public static class ApiServices
{
    /// <summary>שירותי שכבת ה-API: הדייר הנוכחי והשירותים שהבקרים נשענים עליהם.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        // ספק דייר אמיתי מתוך טוקן ה-JWT (מחליף את NullCurrentTenant)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant, HttpContextCurrentTenant>();

        // הודעות למשפחה של תלמיד/ה — משותף לבקרי השיעורים והסדרות
        services.AddScoped<FamilyNotifier>();
        services.AddScoped<TeacherNotifier>();
        services.AddScoped<FamilyMembers>();

        // חשבונות: הרשמה, מיילים, אסימונים
        services.AddScoped<TenantResolver>();
        services.AddScoped<RefreshTokens>();
        services.AddScoped<AuthEmails>();
        services.AddScoped<TeacherRegistration>();

        // היומן: קריאות, פעולות המורה, ובקשות מהמשפחה
        services.AddScoped<LessonQueries>();
        services.AddScoped<LessonService>();
        services.AddScoped<LessonRequestService>();
        services.AddScoped<LessonSeriesService>();

        // תשלומים: קריאות, ורישום/ביטול תשלום
        services.AddScoped<PaymentQueries>();
        services.AddScoped<PaymentService>();

        // תלמידים
        services.AddScoped<StudentQueries>();
        services.AddScoped<StudentService>();
        services.AddScoped<ParentService>();
        services.AddScoped<NoteService>();

        // הספרייה הציבורית והפניות ממנה
        services.AddScoped<PublicLibrary>();
        services.AddScoped<ContactRequestIntake>();

        // ניהול: חשבונות מורות והצעות תחומי לימוד
        services.AddScoped<TeacherAccounts>();
        services.AddScoped<SubjectSuggestions>();

        // הפרופיל ושעות הזמינות של המורה
        services.AddScoped<TeacherProfileService>();
        services.AddScoped<TeacherAvailabilityService>();

        // דוחות
        services.AddScoped<ReportService>();
        services.AddScoped<ExportService>();

        // שיחות בין המורה למשפחה
        services.AddScoped<MessageThreads>();
        services.AddScoped<MessageNotifier>();
        services.AddScoped<MessageQueries>();
        services.AddScoped<TeacherMessageService>();
        services.AddScoped<FamilyMessageService>();

        return services;
    }
}
