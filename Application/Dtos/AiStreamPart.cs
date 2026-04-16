

public abstract record AiStreamPart;

public sealed record AiStreamUsagePart(AiUsageDto AiUsage) : AiStreamPart;

public sealed record AiStreamDeltaPart(string Delta) : AiStreamPart;

public sealed record AiStreamDonePart : AiStreamPart;
