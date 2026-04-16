public static class CapitalizeHelper
{

  public static string Capitalize(string value)
  {

    if (string.IsNullOrWhiteSpace(value))
      return value;

    return char.ToUpper(value[0]) + value.Substring(1).ToLower();
  }
}
