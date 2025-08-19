namespace PushNotificationsApp.DTOs
{
    public record SubscribeDto(int UserId, string Endpoint, string P256dh, string Auth);
}
