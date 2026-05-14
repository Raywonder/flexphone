namespace FlexPhone.Models
{
    public sealed class RememberedFlexPhoneAccount
    {
        public string Server { get; set; } = "";
        public string Extension { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string SessionToken { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Group { get; set; } = "";
        public string Team { get; set; } = "";
        public string AutoQueuePolicy { get; set; } = "";

        public bool CanRegister =>
            !string.IsNullOrWhiteSpace(Server)
            && !string.IsNullOrWhiteSpace(Extension)
            && !string.IsNullOrWhiteSpace(Password);
    }
}
