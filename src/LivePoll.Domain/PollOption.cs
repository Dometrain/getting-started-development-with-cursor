namespace LivePoll.Domain;

public sealed class PollOption
{
    internal PollOption(string text)
    {
        Id = Guid.NewGuid();
        Text = text;
    }

    internal static PollOption Restore(Guid id, string text) => new(id, text);

    private PollOption(Guid id, string text)
    {
        Id = id;
        Text = text;
    }

    public Guid Id { get; }

    public string Text { get; private set; }

    internal void SetText(string text) => Text = text;
}