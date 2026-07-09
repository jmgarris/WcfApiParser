namespace WcfNetTcpClientGenerator.App.Models;

public sealed class SelectionOption<T>
{
    public T Value { get; init; } = default!;

    public string Label { get; init; } = string.Empty;
}
