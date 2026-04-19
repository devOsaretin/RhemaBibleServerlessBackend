using System.Globalization;
using Azure.Messaging.ServiceBus;

internal static class ServiceBusDeliveryDedupeKey
{
  /// <summary>
  /// Stable per broker message (redeliveries keep the same MessageId and sequence number).
  /// </summary>
  public static string Build(string queueName, ServiceBusReceivedMessage message)
  {
    var idPart = string.IsNullOrEmpty(message.MessageId)
      ? message.SequenceNumber.ToString(CultureInfo.InvariantCulture)
      : message.MessageId;
    return $"{queueName}:{idPart}";
  }
}
