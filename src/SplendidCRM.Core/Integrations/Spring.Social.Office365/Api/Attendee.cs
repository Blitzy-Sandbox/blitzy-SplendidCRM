#nullable disable
using System;
using System.Collections.Generic;
namespace Spring.Social.Office365.Api
{
    [Serializable] public class Attendee { public EmailAddress EmailAddress { get; set; } public ResponseStatus Status { get; set; } public string Type { get; set; } }
}
