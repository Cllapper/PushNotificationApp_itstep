using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebPush;
using PushNotificationsApp.Models; // <-- Переконайтесь, що AppDbContext не тут
using PushNotificationsApp.Data;   // <-- А тут
using PushNotificationsApp.DTOs;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Реєструємо DbContext
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// --- FIX 1: Змінюємо реєстрацію сервісів WebPush ---
// Реєструємо VapidDetails як singleton, щоб легко отримати доступ до ключів
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var subject = config.GetValue<string>("VAPID:Subject")!;
    var publicKey = config.GetValue<string>("VAPID:PublicKey")!;
    var privateKey = config.GetValue<string>("VAPID:PrivateKey")!;
    return new VapidDetails(subject, publicKey, privateKey);
});

// Реєструємо WebPushClient
builder.Services.AddHttpClient<WebPushClient>();


// Додаємо логер та інші сервіси
builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// --- FIX 2: Оголошуємо групу ендпоінтів "api" ---
var api = app.MapGroup("/api");

// === Ендпоінт для отримання публічного VAPID ключа ===
api.MapGet("/push/key", (IConfiguration config) =>
{
    return Results.Ok(config.GetValue<string>("VAPID:PublicKey"));
});

// === Ендпоінт для реєстрації користувача ===
api.MapPost("/users", async (CreateUserDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
    {
        return Results.BadRequest("Name and Email are required.");
    }
    var user = new User { Name = dto.Name, Email = dto.Email };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", user);
});

// === Ендпоінт для підписки на сповіщення ===
api.MapPost("/push/subscribe", async (SubscribeDto dto, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(dto.UserId);
    if (user == null)
    {
        return Results.NotFound("User not found.");
    }

    var existingSubscription = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
    if (existingSubscription != null)
    {
        return Results.Ok("Already subscribed.");
    }

    // Тут ми використовуємо ваш клас з Models
    var subscription = new PushNotificationsApp.Models.PushSubscription
    {
        UserId = dto.UserId,
        Endpoint = dto.Endpoint,
        P256dh = dto.P256dh,
        Auth = dto.Auth
    };
    db.PushSubscriptions.Add(subscription);
    await db.SaveChangesAsync();

    return Results.Created();
});

// === Ендпоінт для відправки сповіщення конкретному користувачу ===
api.MapPost("/push/send/{userId}", async (int userId, NotificationPayloadDto payload, AppDbContext db, WebPushClient client, VapidDetails vapidDetails, ILogger<Program> logger) =>
{
    var subscriptions = await db.PushSubscriptions
                                .Where(s => s.UserId == userId)
                                .ToListAsync();

    if (!subscriptions.Any())
    {
        return Results.NotFound("No subscriptions found for this user.");
    }

    var jsonPayload = JsonSerializer.Serialize(new { title = payload.Title, message = payload.Message });

    foreach (var sub in subscriptions)
    {
        // --- FIX 3: Вирішуємо конфлікт імен і правильно створюємо об'єкт ---
        var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
        try
        {
            // --- FIX 4: Передаємо VapidDetails у метод ---
            await client.SendNotificationAsync(pushSubscription, jsonPayload, vapidDetails);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send push notification to {Endpoint}", sub.Endpoint);
        }
    }

    return Results.Ok($"{subscriptions.Count} notifications sent.");
});

// === Ендпоінт для розсилки всім ===
api.MapPost("/push/broadcast", async (NotificationPayloadDto payload, AppDbContext db, WebPushClient client, VapidDetails vapidDetails, ILogger<Program> logger) =>
{
    var subscriptions = await db.PushSubscriptions.ToListAsync();
    var jsonPayload = JsonSerializer.Serialize(new { title = payload.Title, message = payload.Message });

    foreach (var sub in subscriptions)
    {
        // Аналогічно до FIX 3
        var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
        try
        {
            // Аналогічно до FIX 4
            await client.SendNotificationAsync(pushSubscription, jsonPayload, vapidDetails);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Broadcast failed for {Endpoint}", sub.Endpoint);
        }
    }

    return Results.Ok($"{subscriptions.Count} total notifications attempted.");
});

app.Run();
