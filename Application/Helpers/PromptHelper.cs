public static class PromptHelper
{
  private static readonly string PromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts");
  private static readonly string PrayerPromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts/Prayer");
  private static readonly string ChatPromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts/GeneralChat");
  private static readonly string ConversationGospelPromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts/ConversationGospel");
  private static readonly string ApplyVersePromptsFolderPath = Path.Combine(AppContext.BaseDirectory, "Prompts/ApplyVerse");


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

  public static List<ChatMessageDto> GenerateConversationGospelChatPrompt(
    IPromptFileReader files,
    IReadOnlyList<ChatMessageDto> conversationMessages,
    string? userFirstName)
  {
    var instructionPath = Path.Combine(ConversationGospelPromptsFolderPath, "ConversationGospelInstruction.txt");
    var basePrompt = files.ReadAllText(instructionPath);
    var fullSystemPrompt = $"{UserFirstNameContext(userFirstName)}{basePrompt}";

    var messages = new List<ChatMessageDto>(conversationMessages.Count + 1)
    {
      new ChatMessageDto { Role = "system", Content = fullSystemPrompt }
    };

    foreach (var m in conversationMessages)
      messages.Add(new ChatMessageDto { Role = m.Role, Content = m.Content });

    return messages;
  }

  public static List<ChatMessageDto> GenerateApplyVersePrompt(
    IPromptFileReader files,
    string reference,
    string verseText,
    string? userNote,
    string? userFirstName)
  {
    var instructionPath = Path.Combine(ApplyVersePromptsFolderPath, "ApplyVerseInstruction.txt");
    var formatPath = Path.Combine(ApplyVersePromptsFolderPath, "ApplyVerseFormat.json");

    var basePrompt = files.ReadAllText(instructionPath);
    var jsonFormat = files.ReadAllText(formatPath);
    var fullSystemPrompt = $"{UserFirstNameContext(userFirstName)}{basePrompt}\n\nHere is the required JSON response structure:\n{jsonFormat}";

    var userBlock =
      $"Anchor reference: {reference}\n\nAnchor verse text:\n{verseText}\n\n"
      + (userNote is null
        ? "The user did not add extra context."
        : $"Additional context or question from the user:\n{userNote}");

    return new List<ChatMessageDto>
    {
      new ChatMessageDto { Role = "system", Content = fullSystemPrompt },
      new ChatMessageDto { Role = "user", Content = userBlock }
    };
  }
}

