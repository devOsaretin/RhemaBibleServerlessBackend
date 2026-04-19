public sealed class ServiceBusSettings
{
    public const string SectionName = "ServiceBus";
    public string ConnectionString { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
}