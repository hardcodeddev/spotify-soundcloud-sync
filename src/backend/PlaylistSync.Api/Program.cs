using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using PlaylistSync.Api.Services;
using PlaylistSync.Infrastructure;
using PlaylistSync.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDataProtection();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();
builder.Services.AddAuthorization();

builder.Services.AddPlaylistSyncInfrastructure(builder.Configuration);

builder.Services.AddScoped<ICronScheduleValidator, CronScheduleValidator>();
builder.Services.AddScoped<ISyncExecutionService, SyncExecutionService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlaylistSyncDbContext>();
    await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
