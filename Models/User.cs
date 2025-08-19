namespace PushNotificationsApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public virtual ICollection<PushSubscription> Subscriptions { get; set; } = new List<PushSubscription>();
    }
}
