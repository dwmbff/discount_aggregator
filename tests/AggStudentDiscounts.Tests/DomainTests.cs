using AggStudentDiscounts.Domain.Entities;

namespace AggStudentDiscounts.Tests;

public class ApplicationStateMachineTests
{
    [Fact]
    public void Approve_FromOnModeration_PublishesAndStampsDate()
    {
        var app = new Application { RejectionReason = "stale" };

        app.Approve();

        Assert.Equal(ApplicationStatus.Published, app.Status);
        Assert.Null(app.RejectionReason);
        Assert.NotNull(app.ModeratedAt);
    }

    [Fact]
    public void Reject_FromOnModeration_StoresReason()
    {
        var app = new Application();

        app.Reject("Нет подтверждения");

        Assert.Equal(ApplicationStatus.Rejected, app.Status);
        Assert.Equal("Нет подтверждения", app.RejectionReason);
    }

    [Theory]
    [InlineData(ApplicationStatus.Published)]
    [InlineData(ApplicationStatus.Rejected)]
    public void Approve_And_Reject_FromFinalStatus_Throw(ApplicationStatus status)
    {
        var app = new Application { Status = status };

        Assert.Throws<InvalidOperationException>(() => app.Approve());
        Assert.Throws<InvalidOperationException>(() => app.Reject("reason"));
    }
}

public class UserCourseTests
{
    private static User UserUpdatedAt(DateTime updated, int course) =>
        new() { Course = course, LastProfileUpdate = updated };

    [Fact]
    public void RefreshCourse_AfterOctoberFirst_IncrementsOnce()
    {
        var user = UserUpdatedAt(new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), 1);

        user.RefreshCourse(new DateTime(2025, 10, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, user.Course);
    }

    [Fact]
    public void RefreshCourse_BeforeOctoberFirst_DoesNothing()
    {
        var user = UserUpdatedAt(new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), 1);

        user.RefreshCourse(new DateTime(2025, 9, 30, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, user.Course);
    }

    [Fact]
    public void RefreshCourse_IsIdempotentWithinSameYear()
    {
        var user = UserUpdatedAt(new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), 1);
        var now = new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        user.RefreshCourse(now);
        user.RefreshCourse(now.AddDays(5));

        Assert.Equal(2, user.Course);
    }

    [Fact]
    public void RefreshCourse_AfterSeveralYears_CatchesUpAllMissedOctobers()
    {
        var user = UserUpdatedAt(new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc), 1);

        user.RefreshCourse(new DateTime(2025, 10, 5, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(4, user.Course); // октябрь 2023, 2024, 2025
    }
}
