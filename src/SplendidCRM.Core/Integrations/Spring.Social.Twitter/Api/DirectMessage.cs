#nullable disable
using System;
namespace Spring.Social.Twitter.Api
{
    [Serializable]
    public class DirectMessage
    {
        public DirectMessage() { }
        public long ID { get; set; }
        public string Text { get; set; }
        public long SenderId { get; set; }
        public string SenderScreenName { get; set; }
        public long RecipientId { get; set; }
        public string RecipientScreenName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public TwitterProfile Sender { get; set; }
        public TwitterProfile Recipient { get; set; }
    }
}
