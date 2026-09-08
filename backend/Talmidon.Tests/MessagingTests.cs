using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// שיחות בין המורה לתלמידות ולהורים. הבדיקות כאן שומרות על שלושה דברים: שההודעה מגיעה
/// ליעדה, שאף אחד לא רואה שיחה שאינה שלו, ושסימון הנקרא/לא-נקרא נכון לשני הכיוונים —
/// עליו נשען התג שאומר למורה שמחכה לה משהו.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class MessagingTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Student_OpensAThread_TeacherSeesItUnread_AndTheReplyComesBack()
    {
        var (teacher, student, _) = await CreateStudentWithLoginAsync("msgFlow");

        var opened = await student.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = (Guid?)null,
            subject = "שאלה על שיעורי הבית",
            body = "לא הבנתי את תרגיל 4"
        });
        opened.EnsureSuccessStatusCode();
        var thread = await opened.Content.ReadFromJsonAsync<ThreadDetailDto>();
        Assert.Equal(MessageAuthor.Student, thread!.CounterpartRole);
        Assert.Single(thread.Messages);

        // אצל המורה: השיחה בתיבה, מסומנת כלא נקראה, והמונה עלה
        var inbox = await teacher.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages");
        var row = Assert.Single(inbox!, t => t.Id == thread.Id);
        Assert.True(row.HasUnread);
        Assert.Equal("לא הבנתי את תרגיל 4", row.LastMessagePreview);

        var count = await teacher.GetFromJsonAsync<UnreadCountDto>("/api/messages/unread-count");
        Assert.Equal(1, count!.Count);

        // ההודעה מגיעה גם למרכז ההתראות, עם התוכן עצמו ולא רק בשורה "יש הודעה"
        var notifications = await teacher.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.Contains(notifications!, n => n.Message.Contains("לא הבנתי את תרגיל 4") && n.LinkPath == "/app/messages");

        // פתיחת השיחה מסמנת אותה כנקראה, והמונה מתאפס
        await teacher.GetFromJsonAsync<ThreadDetailDto>($"/api/messages/{thread.Id}");
        var afterRead = await teacher.GetFromJsonAsync<UnreadCountDto>("/api/messages/unread-count");
        Assert.Equal(0, afterRead!.Count);

        var replied = await teacher.PostAsJsonAsync($"/api/messages/{thread.Id}/reply",
            new { body = "תרגיל 4 הוא בדיוק כמו תרגיל 2, רק עם שברים" });
        replied.EnsureSuccessStatusCode();

        // ואצל התלמידה: התשובה מופיעה, ומסומנת כחדשה עד שהיא פותחת
        var myThreads = await student.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages/mine");
        var myRow = Assert.Single(myThreads!);
        Assert.True(myRow.HasUnread);
        Assert.Equal(MessageAuthor.Teacher, myRow.LastSenderRole);

        var full = await student.GetFromJsonAsync<ThreadDetailDto>($"/api/messages/mine/{thread.Id}");
        Assert.Equal(2, full!.Messages.Count);
        Assert.Equal(MessageAuthor.Teacher, full.Messages[1].SenderRole);

        var studentCount = await student.GetFromJsonAsync<UnreadCountDto>("/api/messages/mine/unread-count");
        Assert.Equal(0, studentCount!.Count);
    }

    /// <summary>
    /// תגובה על הערה שהמורה כתבה. זו הסיבה שהמנגנון אחד ולא שניים: התגובה היא שיחה
    /// רגילה, ומה שמייחד אותה הוא רק הקישור להערה — כדי שהמורה תדע על מה מגיבים.
    /// </summary>
    [Fact]
    public async Task ReplyingToANote_CarriesTheNoteIntoTheThread()
    {
        var (teacher, student, studentId) = await CreateStudentWithLoginAsync("msgNote");

        var noteResponse = await teacher.PostAsJsonAsync("/api/notes", new
        {
            studentId,
            lessonId = (Guid?)null,
            content = "כדאי לחזור על לוח הכפל",
            visibleToStudent = true,
            visibleToParent = false
        });
        noteResponse.EnsureSuccessStatusCode();
        var note = await noteResponse.Content.ReadFromJsonAsync<NoteDto>();

        var opened = await student.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = note!.Id,
            subject = "תגובה להערה",
            body = "חזרתי, אפשר להיבחן?"
        });
        opened.EnsureSuccessStatusCode();
        var thread = await opened.Content.ReadFromJsonAsync<ThreadDetailDto>();

        Assert.Equal(note.Id, thread!.RelatedNoteId);

        // וגם אצל המורה ההערה מגיעה עם השיחה, לא רק המזהה שלה
        var asTeacher = await teacher.GetFromJsonAsync<ThreadDetailDto>($"/api/messages/{thread.Id}");
        Assert.Equal("כדאי לחזור על לוח הכפל", asTeacher!.RelatedNoteContent);
    }

    /// <summary>הערה של תלמידה אחרת אינה יכולה לשמש עוגן לפנייה — היא גם לא אמורה להיות ידועה.</summary>
    [Fact]
    public async Task CannotAnchorAThreadToAnotherStudentsNote()
    {
        var (teacher, student, _) = await CreateStudentWithLoginAsync("msgNoteIdor");
        var otherId = await AddStudentAsync(teacher, "תלמידה אחרת", null);

        var noteResponse = await teacher.PostAsJsonAsync("/api/notes", new
        {
            studentId = otherId,
            lessonId = (Guid?)null,
            content = "הערה על מישהי אחרת",
            visibleToStudent = true,
            visibleToParent = false
        });
        noteResponse.EnsureSuccessStatusCode();
        var note = await noteResponse.Content.ReadFromJsonAsync<NoteDto>();

        var response = await student.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = note!.Id,
            subject = "ניסיון",
            body = "לא שלי"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OneStudentCannotReadOrAnswerAnothersThread()
    {
        var (teacher, first, _) = await CreateStudentWithLoginAsync("msgIdorA");
        var second = await AddStudentWithLoginAsync(teacher, "תלמידה שנייה", "msgIdorB");

        var opened = await first.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = (Guid?)null,
            subject = "פרטי",
            body = "משהו אישי"
        });
        opened.EnsureSuccessStatusCode();
        var thread = await opened.Content.ReadFromJsonAsync<ThreadDetailDto>();

        var read = await second.GetAsync($"/api/messages/mine/{thread!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var reply = await second.PostAsJsonAsync($"/api/messages/mine/{thread.Id}/reply", new { body = "מציצה" });
        Assert.Equal(HttpStatusCode.NotFound, reply.StatusCode);

        var list = await second.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages/mine");
        Assert.Empty(list!);
    }

    /// <summary>מורה אחת אינה רואה ואינה עונה לשיחות של מורה אחרת.</summary>
    [Fact]
    public async Task OneTeacherCannotSeeAnothersThread()
    {
        var (_, student, _) = await CreateStudentWithLoginAsync("msgTenantA");
        var otherTeacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "msgTenantB");

        var opened = await student.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = (Guid?)null,
            subject = "פנייה",
            body = "שלום"
        });
        opened.EnsureSuccessStatusCode();
        var thread = await opened.Content.ReadFromJsonAsync<ThreadDetailDto>();

        var inbox = await otherTeacher.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages");
        Assert.DoesNotContain(inbox!, t => t.Id == thread!.Id);

        var read = await otherTeacher.GetAsync($"/api/messages/{thread!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    /// <summary>
    /// הודעה שנייה באותה שיחה, כשהראשונה עוד לא נקראה, אינה מייצרת התראה נוספת. בלי זה
    /// חמש שורות רצופות היו מציפות את מרכז ההתראות ואת תיבת המייל של המורה.
    /// </summary>
    [Fact]
    public async Task ASecondMessageWhileTheFirstIsUnreadDoesNotNotifyAgain()
    {
        var (teacher, student, _) = await CreateStudentWithLoginAsync("msgBurst");

        var opened = await student.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = (Guid?)null,
            subject = "רצף",
            body = "הודעה ראשונה"
        });
        opened.EnsureSuccessStatusCode();
        var thread = await opened.Content.ReadFromJsonAsync<ThreadDetailDto>();

        var second = await student.PostAsJsonAsync($"/api/messages/mine/{thread!.Id}/reply", new { body = "וגם זה" });
        second.EnsureSuccessStatusCode();

        var notifications = await teacher.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.Single(notifications!, n => n.Title.Contains("רצף"));

        // אבל השיחה עצמה כן התעדכנה
        var inbox = await teacher.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages");
        var row = Assert.Single(inbox!, t => t.Id == thread.Id);
        Assert.Equal("וגם זה", row.LastMessagePreview);
        Assert.True(row.HasUnread);

        // ואחרי שהמורה קראה, הודעה חדשה כן מתריעה שוב
        await teacher.GetFromJsonAsync<ThreadDetailDto>($"/api/messages/{thread.Id}");
        var third = await student.PostAsJsonAsync($"/api/messages/mine/{thread.Id}/reply", new { body = "ועוד משהו" });
        third.EnsureSuccessStatusCode();

        var after = await teacher.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.Equal(2, after!.Count(n => n.Title.Contains("רצף")));
    }

    [Fact]
    public async Task EmptyBodyIsRejected()
    {
        var (_, student, _) = await CreateStudentWithLoginAsync("msgEmpty");

        var response = await student.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = (Guid?)null,
            subject = "נושא",
            body = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>שיחה שנסגרה חוזרת לתיבה ברגע שמישהו כותב בה שוב.</summary>
    [Fact]
    public async Task AClosedThreadReopensOnANewMessage()
    {
        var (teacher, student, _) = await CreateStudentWithLoginAsync("msgClose");

        var opened = await student.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = (Guid?)null,
            relatedNoteId = (Guid?)null,
            subject = "טופל",
            body = "שאלה"
        });
        opened.EnsureSuccessStatusCode();
        var thread = await opened.Content.ReadFromJsonAsync<ThreadDetailDto>();

        var closed = await teacher.PostAsJsonAsync($"/api/messages/{thread!.Id}/close", new { });
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);

        var open = await teacher.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages?includeClosed=false");
        Assert.DoesNotContain(open!, t => t.Id == thread.Id);

        var again = await student.PostAsJsonAsync($"/api/messages/mine/{thread.Id}/reply", new { body = "עוד שאלה" });
        again.EnsureSuccessStatusCode();

        var reopened = await teacher.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages?includeClosed=false");
        Assert.Contains(reopened!, t => t.Id == thread.Id && !t.IsClosed);
    }

    /// <summary>
    /// המורה כותבת ראשונה. בלי ציון נמען היא מגיעה לתלמידה כשיש לה חשבון — טופס שדורש
    /// בחירה שאין בה ברירה הוא רק מכשול.
    /// </summary>
    [Fact]
    public async Task TeacherCanStartAThread_AndItDefaultsToTheStudent()
    {
        var (teacher, student, studentId) = await CreateStudentWithLoginAsync("msgTeacherFirst");

        var opened = await teacher.PostAsJsonAsync("/api/messages", new
        {
            studentId,
            counterpartRole = (MessageAuthor?)null,
            counterpartId = (Guid?)null,
            subject = "תזכורת",
            body = "אל תשכחי להביא מחברת"
        });
        opened.EnsureSuccessStatusCode();
        var thread = await opened.Content.ReadFromJsonAsync<ThreadDetailDto>();
        Assert.Equal(MessageAuthor.Student, thread!.CounterpartRole);

        var mine = await student.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages/mine");
        Assert.Contains(mine!, t => t.Id == thread.Id && t.HasUnread);

        // והמורה עצמה לא רואה את מה שהיא כתבה כ"חדש"
        var count = await teacher.GetFromJsonAsync<UnreadCountDto>("/api/messages/unread-count");
        Assert.Equal(0, count!.Count);
    }

    /// <summary>לתלמידה בלי חשבון אין למי לשלוח, ואומרים את זה במפורש במקום ליצור שיחה יתומה.</summary>
    [Fact]
    public async Task TeacherCannotStartAThreadWithNoOneToSendTo()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "msgNoTarget");
        var studentId = await AddStudentAsync(teacher, "תלמידה בלי חשבון", null);

        var response = await teacher.PostAsJsonAsync("/api/messages", new
        {
            studentId,
            counterpartRole = (MessageAuthor?)null,
            counterpartId = (Guid?)null,
            subject = "שלום",
            body = "יש מישהו?"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>הורה פונה בעניין ילדו — ולא בעניין ילד של הורה אחר.</summary>
    [Fact]
    public async Task ParentCanOpenAThreadForTheirChildOnly()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "msgParentT");
        var (parent, childId) = await CreateParentWithChildAsync(teacher, "אמא", "הילד שלי");
        var strangerId = await AddStudentAsync(teacher, "ילד של מישהו אחר", null);

        var mine = await parent.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = childId,
            relatedNoteId = (Guid?)null,
            subject = "בקשה",
            body = "אפשר להחליף יום?"
        });
        mine.EnsureSuccessStatusCode();
        var thread = await mine.Content.ReadFromJsonAsync<ThreadDetailDto>();
        Assert.Equal(MessageAuthor.Parent, thread!.CounterpartRole);
        Assert.Equal(childId, thread.StudentId);

        var stranger = await parent.PostAsJsonAsync("/api/messages/mine", new
        {
            studentId = strangerId,
            relatedNoteId = (Guid?)null,
            subject = "ניסיון",
            body = "לא הילד שלי"
        });
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);

        // ובתיבה של המורה השיחה מזוהה לפי שם ההורה, לא לפי שם הילד
        var inbox = await teacher.GetFromJsonAsync<List<ThreadSummaryDto>>("/api/messages");
        var row = Assert.Single(inbox!, t => t.Id == thread.Id);
        Assert.Equal("אמא", row.CounterpartName);
        Assert.Equal("הילד שלי", row.StudentName);
    }

    /// <summary>רשימת הנמענים היא בחירה אחת שטוחה: תלמידה עם חשבון, וכל הורה מקושר.</summary>
    [Fact]
    public async Task RecipientsListsStudentsWithLoginsAndLinkedParents()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "msgRecipients");
        var (_, childId) = await CreateParentWithChildAsync(teacher, "הורה ברשימה", "ילד עם הורה");
        var withLogin = await AddStudentAsync(teacher, "תלמידה עם חשבון", TestHelpers.UniqueEmail("msgRecipientS"));
        var withoutAnything = await AddStudentAsync(teacher, "תלמידה בלי כלום", null);

        var recipients = await teacher.GetFromJsonAsync<List<MessageRecipientDto>>("/api/messages/recipients");

        Assert.Contains(recipients!, r => r.Role == MessageAuthor.Student && r.Id == withLogin);
        Assert.Contains(recipients!, r => r.Role == MessageAuthor.Parent && r.StudentId == childId && r.Name == "הורה ברשימה");
        Assert.DoesNotContain(recipients!, r => r.StudentId == withoutAnything);
    }

    // ----- עזר -----

    private async Task<Guid> AddStudentAsync(HttpClient teacher, string name, string? loginEmail, Guid[]? parentIds = null)
    {
        var response = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = name,
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail,
            parentIds = parentIds ?? Array.Empty<Guid>()
        });
        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<StudentDetailDto>();
        return student!.Id;
    }

    private async Task<(HttpClient Teacher, HttpClient Student, Guid StudentId)> CreateStudentWithLoginAsync(string prefix)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, $"{prefix}T");
        var loginEmail = TestHelpers.UniqueEmail($"{prefix}S");
        var studentId = await AddStudentAsync(teacher, "תלמידה מתכתבת", loginEmail);
        return (teacher, await LoginAsAsync(loginEmail, "StudentPass123"), studentId);
    }

    private async Task<HttpClient> AddStudentWithLoginAsync(HttpClient teacher, string name, string prefix)
    {
        var loginEmail = TestHelpers.UniqueEmail(prefix);
        await AddStudentAsync(teacher, name, loginEmail);
        return await LoginAsAsync(loginEmail, "StudentPass123");
    }

    private async Task<(HttpClient Parent, Guid StudentId)> CreateParentWithChildAsync(
        HttpClient teacher, string parentName, string childName)
    {
        var parentEmail = TestHelpers.UniqueEmail("msgParent");
        var parentResponse = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName = parentName,
            gender = (int?)null,
            email = parentEmail,
            phone = (string?)null
        });
        parentResponse.EnsureSuccessStatusCode();
        var parent = await parentResponse.Content.ReadFromJsonAsync<ParentDto>();

        var childId = await AddStudentAsync(teacher, childName, null, [parent!.Id]);
        return (await LoginAsAsync(parentEmail, "ParentPass123"), childId);
    }

    /// <summary>קובעת סיסמה לחשבון שנוצר ע"י המורה ומחזירה קליינט מחובר כמותו.</summary>
    private async Task<HttpClient> LoginAsAsync(string email, string password)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException($"User {email} not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var anon = factory.CreateClient();
        return TestHelpers.AuthorizedClient(factory, await TestHelpers.LoginAsync(anon, email, password));
    }

    private record ParentDto(Guid Id, string FullName);
}
