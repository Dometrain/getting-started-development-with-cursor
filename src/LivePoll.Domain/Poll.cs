namespace LivePoll.Domain;

public sealed class Poll
{
    public const int OpenDurationSeconds = 600;

    private readonly List<PollOption> options = [];
    private readonly HashSet<string> voterUsernames = new(StringComparer.Ordinal);

    private Poll(string title, IEnumerable<string> optionTexts)
    {
        Id = Guid.NewGuid();
        Title = title;
        foreach (var text in optionTexts)
        {
            options.Add(new PollOption(text));
        }
    }

    public Guid Id { get; }

    public string Title { get; private set; }

    public IReadOnlyList<PollOption> Options => options;

    public static Poll Restore(
        Guid id,
        string title,
        IEnumerable<(Guid Id, string Text)> options,
        IEnumerable<string> voterUsernames)
    {
        var poll = new Poll(id, title);
        foreach (var (optionId, text) in options)
        {
            poll.options.Add(PollOption.Restore(optionId, text));
        }

        foreach (var username in voterUsernames)
        {
            poll.voterUsernames.Add(username);
        }

        return poll;
    }

    private Poll(Guid id, string title)
    {
        Id = id;
        Title = title;
    }

    public static Poll Create(string title, IEnumerable<string> optionTexts)
    {
        ArgumentNullException.ThrowIfNull(optionTexts);

        var normalizedTitle = NormalizeTitle(title);
        var texts = optionTexts.Select(NormalizeOptionText).ToList();
        if (texts.Count is < 2 or > 6)
        {
            throw new DomainException("A poll must have between 2 and 6 options.");
        }

        return new Poll(normalizedTitle, texts);
    }

    public void UpdateTitle(string title, ActorRole role)
    {
        EnsureAdmin(role);
        EnsureNoVotes();
        Title = NormalizeTitle(title);
    }

    public PollOption AddOption(string text, ActorRole role)
    {
        EnsureAdmin(role);
        EnsureNoVotes();
        if (options.Count >= 6)
        {
            throw new DomainException("A poll cannot have more than 6 options.");
        }

        var option = new PollOption(NormalizeOptionText(text));
        options.Add(option);
        return option;
    }

    public void EditOption(Guid optionId, string text, ActorRole role)
    {
        EnsureAdmin(role);
        EnsureNoVotes();
        FindOption(optionId).SetText(NormalizeOptionText(text));
    }

    public void RemoveOption(Guid optionId, ActorRole role)
    {
        EnsureAdmin(role);
        EnsureNoVotes();
        if (options.Count <= 2)
        {
            throw new DomainException("A poll must keep at least 2 options.");
        }

        var option = FindOption(optionId);
        options.Remove(option);
    }

    public void CastVote(string username, Guid optionId)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username is required.");
        }

        _ = FindOption(optionId);
        if (!voterUsernames.Add(username))
        {
            throw new DomainException("This username has already voted on this poll.");
        }
    }

    private static void EnsureAdmin(ActorRole role)
    {
        if (role != ActorRole.Admin)
        {
            throw new DomainException("Only an admin can change the poll.");
        }
    }

    private void EnsureNoVotes()
    {
        if (voterUsernames.Count > 0)
        {
            throw new DomainException("A poll that has votes cannot be changed.");
        }
    }

    private PollOption FindOption(Guid optionId)
    {
        var option = options.FirstOrDefault(o => o.Id == optionId);
        if (option is null)
        {
            throw new DomainException("Option was not found.");
        }

        return option;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title cannot be blank.");
        }

        if (title.Length > 120)
        {
            throw new DomainException("Title cannot be longer than 120 characters.");
        }

        return title;
    }

    private static string NormalizeOptionText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Option cannot be blank.");
        }

        if (text.Length < 3)
        {
            throw new DomainException("Option must be at least 3 characters.");
        }

        return text;
    }
}