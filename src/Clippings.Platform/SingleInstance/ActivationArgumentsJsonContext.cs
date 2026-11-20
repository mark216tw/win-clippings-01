using System.Text.Json.Serialization;

namespace Clippings.Platform.SingleInstance;

[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
internal sealed partial class ActivationArgumentsJsonContext : JsonSerializerContext
{
}
