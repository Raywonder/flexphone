using FlexPhone.Services;

namespace FlexPhone.Models
{
    public sealed class FlexPhoneContact
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string DisplayName { get; set; } = "";
        public string Extension { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public string Group { get; set; } = "General";
        public string Notes { get; set; } = "";
        public bool IsFavorite { get; set; }

        public string PrimaryDestination =>
            FirstText(Extension, PhoneNumber, Email);

        public string AccessibleSummary
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(DisplayName) ? "Unnamed contact" : DisplayName;
                var group = string.IsNullOrWhiteSpace(Group) ? "General" : Group;
                var extension = string.IsNullOrWhiteSpace(Extension) ? "no extension" : $"extension {Extension}";
                var phone = string.IsNullOrWhiteSpace(PhoneNumber) ? "no phone number" : $"phone {PhoneNumber}";
                var email = string.IsNullOrWhiteSpace(Email) ? "no email" : $"email {Email}";
                var favorite = IsFavorite ? ", favorite" : "";
                return $"{name}, group {group}, {extension}, {phone}, {email}{favorite}";
            }
        }

        public static FlexPhoneContact FromPresence(FlexPhonePresenceInfo person)
        {
            return new FlexPhoneContact
            {
                DisplayName = string.IsNullOrWhiteSpace(person.DisplayName) ? person.Extension : person.DisplayName,
                Extension = person.Extension,
                Group = string.IsNullOrWhiteSpace(person.Role) ? "Directory" : person.Role
            };
        }

        private static string FirstText(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }
    }
}
