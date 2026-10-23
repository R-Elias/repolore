using RepoLore.Core.Json;

namespace RepoLore.Cli.Rendering;

public static class ErrorRenderer
{
    public static void Write(bool json, string id, string message, long? recoveryId)
    {
        Console.Error.WriteLine("repolore: " + message);
        if (recoveryId is long recovery)
            Console.Error.WriteLine($"repolore: recover with: repolore restore {recovery}");

        if (!json)
            return;

        var error = new JsonObject();
        error.Members.Add(new JsonMember("id", new JsonStringValue(id)));
        error.Members.Add(new JsonMember("message", new JsonStringValue(message)));
        error.Members.Add(new JsonMember("recoveryId", recoveryId is long value ? Renderer.Number(value) : new JsonNullValue()));
        var root = new JsonObject();
        root.Members.Add(new JsonMember("error", error));
        Console.WriteLine(JsonWriter.Write(root));
    }
}
