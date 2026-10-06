using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AggStudentDiscounts.Tests;

public class GeocodingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Suggest_ReturnsPlacesWithAddressCategoryAndContacts()
    {
        var client = factory.CreateClient();

        var places = await client.GetFromJsonAsync<List<GeoPlaceResponse>>("/api/geocode/suggest?q=зерно");

        var place = Assert.Single(places!);
        Assert.Equal("N12345", place.ExternalId);
        Assert.Equal("Общепит", place.Category);
        Assert.Equal("Mo-Su 08:00-22:00", place.WorkingHours);
        Assert.Contains("Тверская", place.Address);
    }

    [Fact]
    public async Task Suggest_SecondCallIsServedFromCache()
    {
        var client = factory.CreateClient();
        var before = factory.Geo.Calls;

        await client.GetAsync("/api/geocode/suggest?q=кэш-тест");
        await client.GetAsync("/api/geocode/suggest?q=кэш-тест");

        Assert.Equal(1, factory.Geo.Calls - before);
    }

    [Fact]
    public async Task Suggest_WhenUpstreamDown_UsesStaleCache_OrReturns503()
    {
        var client = factory.CreateClient();
        await client.GetAsync("/api/geocode/suggest?q=устойчивость");

        // Кэш протух, сервис недоступен → отдаём последние известные данные
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var entry in await db.GeocodeCache.ToListAsync())
            {
                entry.ExpiresAt = DateTime.UtcNow.AddHours(-1);
            }

            await db.SaveChangesAsync();
        }

        factory.Geo.Fail = true;
        try
        {
            var stale = await client.GetAsync("/api/geocode/suggest?q=устойчивость");
            Assert.Equal(HttpStatusCode.OK, stale.StatusCode);

            var unknown = await client.GetAsync("/api/geocode/suggest?q=никогда-не-запрашивали");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unknown.StatusCode);
        }
        finally
        {
            factory.Geo.Fail = false;
        }
    }

    [Fact]
    public async Task Suggest_ShortQuery_Returns400()
    {
        var response = await factory.CreateClient().GetAsync("/api/geocode/suggest?q=ab");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class PhotoAndEnrichmentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CreateApplication_WithExternalId_EnrichesAttributes()
    {
        var user = await factory.CreateUserClientAsync("enrich@test.com");

        var response = await user.PostAsync("/api/applications", ApiFactory.NewApplicationForm(externalId: "N12345"));

        var created = await response.Content.ReadFromJsonAsync<ApplicationResponse>();
        Assert.Equal("Общепит", created!.Category);
        Assert.Equal("https://zerno.example", created.Website);
    }

    [Fact]
    public async Task CreateApplication_WhenGeoDown_StillSucceeds()
    {
        var user = await factory.CreateUserClientAsync("geodown@test.com");
        factory.Geo.Fail = true;
        try
        {
            var response = await user.PostAsync("/api/applications", ApiFactory.NewApplicationForm(externalId: "N999"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        finally
        {
            factory.Geo.Fail = false;
        }
    }

    [Fact]
    public async Task CreateApplication_FileWithSpoofedExtension_Returns400()
    {
        var user = await factory.CreateUserClientAsync("spoof@test.com");
        var form = ApiFactory.NewApplicationForm(withPhoto: false);
        var fake = new ByteArrayContent("<script>alert(1)</script>"u8.ToArray());
        fake.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fake, "Photos", "evil.jpg");

        var response = await user.PostAsync("/api/applications", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Photo_IsStored_AndVisibilityFollowsApplicationStatus()
    {
        var owner = await factory.CreateUserClientAsync("photoowner@test.com");
        var stranger = await factory.CreateUserClientAsync("photostranger@test.com");
        var moderator = await factory.CreateModeratorClientAsync();
        var anonymous = factory.CreateClient();

        var created = await (await owner.PostAsync("/api/applications", ApiFactory.NewApplicationForm()))
            .Content.ReadFromJsonAsync<ApplicationResponse>();
        var url = Assert.Single(created!.Photos);

        // Пока заявка на проверке: автор и модератор видят файл, остальные — нет
        var ownerPhoto = await owner.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, ownerPhoto.StatusCode);
        Assert.Equal(ApiFactory.JpegBytes, await ownerPhoto.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, (await moderator.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);

        // После публикации файл виден всем и приходит в карточке заведения
        await moderator.PutAsync($"/api/admin/applications/{created.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(url)).StatusCode);
        var place = await anonymous.GetFromJsonAsync<PlaceResponse>($"/api/establishments/{created.Id}");
        Assert.Equal(url, Assert.Single(place!.Photos));
    }

    [Fact]
    public async Task CancelledApplication_IsHidden_ButStillCountsTowardDailyLimit()
    {
        var user = await factory.CreateUserClientAsync("cancel@test.com");

        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            ids.Add(await ApiFactory.SubmitApplicationAsync(user, $"Place {i}"));
        }

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/applications/{ids[0]}")).StatusCode);

        var mine = await user.GetFromJsonAsync<ApplicationListResponse>("/api/applications");
        Assert.Equal(3, mine!.Total);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/applications/{ids[0]}")).StatusCode);

        // Отмена не освобождает слот в суточном лимите
        var fifth = await user.PostAsync("/api/applications", ApiFactory.NewApplicationForm("Place 5"));
        Assert.Equal((HttpStatusCode)429, fifth.StatusCode);
    }

    [Fact]
    public async Task CancelledApplication_CannotBeCancelledTwice_Or_Moderated()
    {
        var user = await factory.CreateUserClientAsync("cancel2@test.com");
        var moderator = await factory.CreateModeratorClientAsync();
        var id = await ApiFactory.SubmitApplicationAsync(user);
        await user.DeleteAsync($"/api/applications/{id}");

        Assert.Equal(HttpStatusCode.NotFound, (await user.DeleteAsync($"/api/applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await moderator.PutAsync($"/api/admin/applications/{id}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Moderator_CanEditCategoryAndContactsOfPublishedPlace()
    {
        var user = await factory.CreateUserClientAsync("editplace@test.com");
        var moderator = await factory.CreateModeratorClientAsync();
        var id = await ApiFactory.SubmitApplicationAsync(user);
        await moderator.PutAsync($"/api/admin/applications/{id}/approve", null);

        var edit = await moderator.PutAsJsonAsync($"/api/admin/places/{id}",
            new UpdateApplicationRequest { Category = "Общепит", Phone = "+7 999 000-00-00" });

        var place = await edit.Content.ReadFromJsonAsync<PlaceResponse>();
        Assert.Equal("Общепит", place!.Category);
        Assert.Equal("+7 999 000-00-00", place.Phone);
    }
}

public class CancelStateTests
{
    [Fact]
    public void Cancel_OnlyFromOnModeration()
    {
        var app = new Application();
        app.Cancel();
        Assert.Equal(ApplicationStatus.Cancelled, app.Status);

        Assert.Throws<InvalidOperationException>(() => app.Cancel());
        Assert.Throws<InvalidOperationException>(() => app.Approve());
    }
}
