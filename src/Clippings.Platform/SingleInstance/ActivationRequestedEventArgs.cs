namespace Clippings.Platform.SingleInstance;

public sealed class ActivationRequestedEventArgs : EventArgs
{
    public ActivationRequestedEventArgs(IReadOnlyList<string> arguments)
    {
        Arguments = arguments;
    }

    public IReadOnlyList<string> Arguments { get; }
}
