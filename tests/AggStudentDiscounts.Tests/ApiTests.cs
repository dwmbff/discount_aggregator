using System.Net;
using System.Net.Http.Json;
using AggStudentDiscounts.Api.DTOs;

namespace AggStudentDiscounts.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static RegisterRequest NewRegistration(string email = "a@test.com", string password = "Password123") => new()
    {
        Email = email, Password = password, Name = "Анна", University = "МГУ", Department = "ВМК", Course = 2
    };

    [Fact]
    public async Task Register_ReturnsJwt_AndDoesNotStorePlainPassword()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration("hash@test.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(3, auth!.AccessToken.Split('.').Length); // header.payload.signature
        Assert.Equal("User", auth.User.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRegistration("dup@test.com"));

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration("DUP@test.com"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/auth/register", NewRegistration("short@test.com", "123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRegistration("login@test.com"));

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "login@test.com", Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401_WithToken_ReturnsProfile()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/auth/me")).StatusCode);

        var client = await factory.CreateUserClientAsync("me@test.com");
        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");

        Assert.Equal("me@test.com", me!.Email);
    }
}

public class ApplicationFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CreateApplication_WithoutPhoto_Returns400()
    {
        var client = await factory.CreateUserClientAsync("nophoto@test.com");

        var response = await client.PostAsync("/api/applications", ApiFactory.NewApplicationForm(withPhoto: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateApplication_FifthInADay_Returns429()
    {
        var client = await factory.CreateUserClientAsync("limit@test.com");
        for (var i = 0; i < 4; i++)
        {
            await ApiFactory.SubmitApplicationAsync(client, $"Place {i}");
        }

        var response = await client.PostAsync("/api/applications", ApiFactory.NewApplicationForm("Place 5"));

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
    }

    [Fact]
    public async Task User_CannotSeeOrCancelSomeoneElsesApplication()
    {
        var owner = await factory.CreateUserClientAsync("owner@test.com");
        var stranger = await factory.CreateUserClientAsync("stranger@test.com");
        var id = await ApiFactory.SubmitApplicationAsync(owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.DeleteAsync($"/api/applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/applications/{id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_Endpoints_RequireModeratorRole()
    {
        var user = await factory.CreateUserClientAsync("plain@test.com");

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/admin/applications")).StatusCode);
    }

    [Fact]
    public async Task Reject_RequiresReason_AndSecondDecisionReturns409()
    {
        var user = await factory.CreateUserClientAsync("rej@test.com");
        var moderator = await factory.CreateModeratorClientAsync();
        var id = await ApiFactory.SubmitApplicationAsync(user);

        var noReason = await moderator.PutAsJsonAsync($"/api/admin/applications/{id}/reject", new RejectApplicationRequest());
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var rejected = await moderator.PutAsJsonAsync($"/api/admin/applications/{id}/reject",
            new RejectApplicationRequest { Reason = "Нет подтверждения" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        var approveAfter = await moderator.PutAsync($"/api/admin/applications/{id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, approveAfter.StatusCode);

        var mine = await user.GetFromJsonAsync<ApplicationResponse>($"/api/applications/{id}");
        Assert.Equal("Rejected", mine!.Status);
        Assert.Equal("Нет подтверждения", mine.RejectionReason);
    }

    [Fact]
    public async Task FullLifecycle_Submit_Approve_Publish_Vote()
    {
        var user = await factory.CreateUserClientAsync("flow@test.com");
        var moderator = await factory.CreateModeratorClientAsync();
        var anonymous = factory.CreateClient();

        var id = await ApiFactory.SubmitApplicationAsync(user, "Уникальная кофейня");

        // До модерации заведение в каталоге не видно
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/establishments/{id}")).StatusCode);

        var approve = await moderator.PutAsync($"/api/admin/applications/{id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var catalog = await anonymous.GetFromJsonAsync<PlaceListResponse>("/api/establishments?search=уникальная");
        Assert.Contains(catalog!.Items, p => p.Id == id);

        // Голосование: анонимам нельзя, пользователю — один раз в 15 дней
        var voteBody = new VoteRequest { Vote = "yes" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/establishments/{id}/vote", voteBody)).StatusCode);

        var first = await user.PostAsJsonAsync($"/api/establishments/{id}/vote", voteBody);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var stats = await first.Content.ReadFromJsonAsync<VoteStatsResponse>();
        Assert.Equal(1, stats!.Yes);
        Assert.False(stats.CanVoteAgain);

        var second = await user.PostAsJsonAsync($"/api/establishments/{id}/vote", new VoteRequest { Vote = "no" });
        Assert.Equal((HttpStatusCode)429, second.StatusCode);

        // Модератор голосует без ограничений
        await moderator.PostAsJsonAsync($"/api/establishments/{id}/vote", new VoteRequest { Vote = "no" });
        var second2 = await moderator.PostAsJsonAsync($"/api/establishments/{id}/vote", new VoteRequest { Vote = "no" });
        Assert.Equal(HttpStatusCode.OK, second2.StatusCode);

        var publicStats = await anonymous.GetFromJsonAsync<VoteStatsResponse>($"/api/establishments/{id}/votes");
        Assert.Equal(1, publicStats!.Yes);
        Assert.Equal(2, publicStats.No);
    }

    [Fact]
    public async Task Vote_InvalidValue_Returns400()
    {
        var user = await factory.CreateUserClientAsync("badvote@test.com");
        var moderator = await factory.CreateModeratorClientAsync();
        var id = await ApiFactory.SubmitApplicationAsync(user);
        await moderator.PutAsync($"/api/admin/applications/{id}/approve", null);

        var response = await user.PostAsJsonAsync($"/api/establishments/{id}/vote", new VoteRequest { Vote = "maybe" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_IsPublic()
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health")).StatusCode);
    }
}
