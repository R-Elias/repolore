using System.Globalization;
using RepoLore.Core.Context;
using RepoLore.Core.Json;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;
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

    public static void Checkpoint(CheckpointResult result, bool json, bool quiet)
    {
        if (json)
        {
            var root = new JsonObject();
            root.Members.Add(new JsonMember("noOp", new JsonBooleanValue(result.WasNoOp)));
            root.Members.Add(new JsonMember("id", result.PublishedId is long id ? Number(id) : new JsonNullValue()));
            root.Members.Add(new JsonMember("files", Number(result.CapturedFileCount)));
            root.Members.Add(new JsonMember("added", StringArray(result.Added)));
            root.Members.Add(new JsonMember("changed", StringArray(result.Changed)));
            root.Members.Add(new JsonMember("removed", StringArray(result.Removed)));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (quiet)
            return;

        if (result.WasNoOp)
        {
            Console.WriteLine("checkpoint: no changes");
            return;
        }

        Console.WriteLine($"checkpoint {ManifestId.Format(result.PublishedId!.Value)}: {result.CapturedFileCount} files, {result.Added.Count} added, {result.Changed.Count} changed, {result.Removed.Count} removed");
    }

    public static void History(List<CheckpointManifest> manifests, bool json, bool quiet)
    {
        var ordered = new List<CheckpointManifest>(manifests);
        ordered.Reverse();

        if (json)
        {
            var array = new JsonArray();
            foreach (var manifest in ordered)
            {
                var obj = new JsonObject();
                obj.Members.Add(new JsonMember("id", Number(manifest.Id)));
                obj.Members.Add(new JsonMember("timestamp", new JsonStringValue(manifest.Timestamp)));
                var files = new JsonArray();
                foreach (var file in manifest.Files)
                    files.Items.Add(new JsonStringValue(file.Path));
                obj.Members.Add(new JsonMember("files", files));
                array.Items.Add(obj);
            }
            var root = new JsonObject();
            root.Members.Add(new JsonMember("checkpoints", array));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (quiet)
            return;

        foreach (var manifest in ordered)
        {
            Console.WriteLine($"{ManifestId.Format(manifest.Id)} {manifest.Timestamp}");
            foreach (var file in manifest.Files)
                Console.WriteLine("  " + file.Path);
        }
    }

    private static JsonNumberValue Number(long value) => new(value.ToString(CultureInfo.InvariantCulture));

    private static JsonArray StringArray(IReadOnlyList<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Items.Add(new JsonStringValue(value));
        return array;
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
