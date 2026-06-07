namespace FlexPhone.Models
{
    public sealed class CallLogEntry
    {
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string Category { get; init; } = "System";
        public string Message { get; init; } = "";

        public string DisplayText => $"{Timestamp:T}  {Category}: {Message}";
    }
}
