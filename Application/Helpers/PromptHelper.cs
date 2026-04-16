public static class PromptHelper
{
  private static readonly string PromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts");
  private static readonly string PrayerPromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts/Prayer");
  private static readonly string ChatPromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts/GeneralChat");


  private static string UserFirstNameContext(string? userFirstName)
  {
    var name = SanitizeNameForPrompt(userFirstName);
    if (name.Length == 0)
    {
      return "The authenticated user has not provided a first name on their account. Address them in a friendly, respectful way without using a personal name unless they share one in the conversation.\n\n---\n\n";
    }

    return $"The authenticated user's first name is: {name}. Use their name when it feels natural and appropriate.\n\n---\n\n";
  }

  private static string SanitizeNameForPrompt(string? firstName)
  {
    if (string.IsNullOrWhiteSpace(firstName))
      return string.Empty;
    return firstName.Trim().Replace("\r", " ").Replace("\n", " ");
  }

  public static List<ChatMessageDto> GeneratePrompt(IPromptFileReader files, string userQuery, string? userFirstName)
  {
    var promptFormatFilePath = Path.Combine(PromptsFolderPath, "PremiumPromptFormat.json");
    var baseInstructionFilePath = Path.Combine(PromptsFolderPath, "PremiumInstruction.txt");

    string basePrompt = files.ReadAllText(baseInstructionFilePath);
    string jsonFormat = files.ReadAllText(promptFormatFilePath);

    string fullSystemPrompt = $"{UserFirstNameContext(userFirstName)}{basePrompt}\n\nHere is the required JSON response structure:\n{jsonFormat}";

    return new List<ChatMessageDto>
            {
                new ChatMessageDto { Role = "system", Content = fullSystemPrompt },
                new ChatMessageDto { Role = "user", Content = userQuery }
            };
  }

  public static List<ChatMessageDto> GeneratePrayerPrompt(IPromptFileReader files, string userQuery, string? userFirstName)
  {
    var prayerFormatFileName = "PrayerFormat.json";
    var prayerFormatFilePath = Path.Combine(PrayerPromptsFolderPath, prayerFormatFileName);
    var basePrayerInstructionFilePath = Path.Combine(PrayerPromptsFolderPath, "PremiumPrayerInstruction.txt");

    string basePrompt = files.ReadAllText(basePrayerInstructionFilePath);
    string jsonFormat = files.ReadAllText(prayerFormatFilePath);

    string fullSystemPrompt = $"{UserFirstNameContext(userFirstName)}{basePrompt}\n\nHere is the required JSON response structure:\n{jsonFormat}";

    return new List<ChatMessageDto>
            {
                new ChatMessageDto { Role = "system", Content = fullSystemPrompt },
                new ChatMessageDto { Role = "user", Content = userQuery }
            };
  }

  public static List<ChatMessageDto> GenerateChatPrompt(IPromptFileReader files, string userQuery, string? userFirstName)
  {
    var chatFormatFilePath = Path.Combine(ChatPromptsFolderPath, "ChatFormat.json");
    var premiumInstructionPath = Path.Combine(PromptsFolderPath, "PremiumInstruction.txt");

    string basePrompt = files.ReadAllText(premiumInstructionPath);
    string jsonFormat = files.ReadAllText(chatFormatFilePath);

    string fullSystemPrompt = $"{UserFirstNameContext(userFirstName)}{basePrompt}\n\nHere is the required JSON response structure:\n{jsonFormat}";

    return new List<ChatMessageDto>
            {
                new ChatMessageDto { Role = "system", Content = fullSystemPrompt },
                new ChatMessageDto { Role = "user", Content = userQuery }
            };
  }
}

