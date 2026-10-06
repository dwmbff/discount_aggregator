using AggStudentDiscounts.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AggStudentDiscounts.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/feedback")]
public class FeedbackController(IConfiguration config) : ControllerBase
{
    [HttpGet("contacts")]
    [ProducesResponseType(typeof(ContactsResponse), 200)]
    public IActionResult GetContacts() => Ok(new ContactsResponse
    {
        TelegramUrl = config["Feedback:TelegramUrl"] ?? "https://t.me/support",
        Email = config["Feedback:Email"] ?? "admin@example.com"
    });
}
