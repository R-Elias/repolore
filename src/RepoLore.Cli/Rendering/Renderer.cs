using RepoLore.Core.Context;
using RepoLore.Core.Json;
using RepoLore.Cli.Reading;

namespace RepoLore.Cli.Rendering;

public static class Renderer
{
    public static void Context(ContextEstimate estimate, List<Finding> findings, bool json, bool quiet)
    {
        if (json)
        {
            var root = new JsonObject();
            root.Members.Add(new JsonMember("included", IncludedArray(estimate)));
            root.Members.Add(new JsonMember("omitted", OmittedArray(estimate)));
            root.Members.Add(new JsonMember("findings", FindingsArray(findings)));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (!quiet)
        {
            foreach (var block in estimate.Included)
                Console.Write(block.Render());
        }

        foreach (var finding in findings)
            Console.Error.WriteLine($"finding: {finding.Id} {finding.Path}: {finding.Message}");

        foreach (var omission in estimate.Omissions)
            Console.Error.WriteLine($"omitted: {omission.Path} ({omission.Reason})");
    }

    public static void Path(List<ResolvedNote> notes, bool json)
    {
        if (json)
        {
            var array = new JsonArray();
            foreach (var note in notes)
            {
                var obj = new JsonObject();
                obj.Members.Add(new JsonMember("path", new JsonStringValue(note.Path)));
                obj.Members.Add(new JsonMember("status", new JsonStringValue(StatusName(note.Status))));
                array.Items.Add(obj);
            }
            var root = new JsonObject();
            root.Members.Add(new JsonMember("notes", array));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        foreach (var note in notes)
            Console.WriteLine($"{StatusName(note.Status)} {note.Path}");
    }

    public static void Tree(List<TreeEntry> entries, bool json)
    {
        if (json)
        {
            var array = new JsonArray();
            foreach (var entry in entries)
            {
                var obj = new JsonObject();
                obj.Members.Add(new JsonMember("path", new JsonStringValue(entry.Path)));
                obj.Members.Add(new JsonMember("type", new JsonStringValue(entry.IsDirectory ? "directory" : "file")));
                array.Items.Add(obj);
            }
            var root = new JsonObject();
            root.Members.Add(new JsonMember("entries", array));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        foreach (var entry in entries)
            Console.WriteLine(entry.IsDirectory ? entry.Path + "/" : entry.Path);
    }

    private static string StatusName(NoteStatus status) => status switch
    {
        NoteStatus.Found => "present",
        NoteStatus.Empty => "empty",
        NoteStatus.Missing => "missing",
        NoteStatus.Conflict => "conflict",
        NoteStatus.InvalidUtf8 => "invalid-utf8",
        _ => "unknown"
    };

    private static JsonArray IncludedArray(ContextEstimate estimate)
    {
        var array = new JsonArray();
        foreach (var block in estimate.Included)
        {
            var obj = new JsonObject();
            obj.Members.Add(new JsonMember("path", new JsonStringValue(block.Path)));
            obj.Members.Add(new JsonMember("scope", new JsonStringValue(block.Scope)));
            obj.Members.Add(new JsonMember("text", new JsonStringValue(block.Text)));
            array.Items.Add(obj);
        }
        return array;
    }

    private static JsonArray OmittedArray(ContextEstimate estimate)
    {
        var array = new JsonArray();
        foreach (var omission in estimate.Omissions)
        {
            var obj = new JsonObject();
            obj.Members.Add(new JsonMember("path", new JsonStringValue(omission.Path)));
            obj.Members.Add(new JsonMember("scope", new JsonStringValue(omission.Scope)));
            obj.Members.Add(new JsonMember("reason", new JsonStringValue(omission.Reason)));
            array.Items.Add(obj);
        }
        return array;
    }

    private static JsonArray FindingsArray(List<Finding> findings)
    {
        var array = new JsonArray();
        foreach (var finding in findings)
        {
            var obj = new JsonObject();
            obj.Members.Add(new JsonMember("id", new JsonStringValue(finding.Id)));
            obj.Members.Add(new JsonMember("path", new JsonStringValue(finding.Path)));
            obj.Members.Add(new JsonMember("message", new JsonStringValue(finding.Message)));
            array.Items.Add(obj);
        }
        return array;
    }
}
