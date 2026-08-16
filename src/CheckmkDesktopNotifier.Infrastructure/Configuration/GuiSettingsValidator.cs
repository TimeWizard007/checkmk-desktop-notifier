namespace CheckmkDesktopNotifier.Infrastructure.Configuration;

public static class GuiSettingsValidator
{
    public static CheckmkOptions CreateOptions(
        string? baseUrl,
        string? site,
        string? username,
        string? secret,
        string? pollIntervalText,
        bool requireSecret)
    {
        if (string.IsNullOrWhiteSpace(pollIntervalText))
        {
            throw new CheckmkOptionsValidationException("Polling interval is required.");
        }

        if (!int.TryParse(pollIntervalText.Trim(), out var poll)
            || poll < CheckmkOptions.MinimumPollIntervalSeconds)
        {
            throw new CheckmkOptionsValidationException(
                $"Polling interval must be an integer of at least {CheckmkOptions.MinimumPollIntervalSeconds} seconds.");
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new CheckmkOptionsValidationException("BaseUrl is required.");
        }

        var normalized = CheckmkOptionsValidator.NormalizeBaseUrl(baseUrl);
        if (!normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new CheckmkOptionsValidationException("BaseUrl must be an absolute https URL without credentials.");
        }

        var options = new CheckmkOptions
        {
            Mode = ClientMode.Real,
            BaseUrl = normalized,
            Site = site?.Trim(),
            Username = username?.Trim(),
            Secret = secret,
            PollIntervalSeconds = poll
        };

        CheckmkOptionsValidator.ValidateGui(options, requireSecret);
        return options;
    }
}
