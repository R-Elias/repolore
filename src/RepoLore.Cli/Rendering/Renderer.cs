using System.Globalization;
using RepoLore.Core.Context;
using RepoLore.Core.Json;
using RepoLore.Core.Migration;
using RepoLore.Core.Restore;
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
        if (!result.Cleanup.BudgetOk)
            Console.Error.WriteLine("repolore: history budget was not achieved after cleanup; retry checkpoint later");

        if (json)
        {
            var root = new JsonObject();
            root.Members.Add(new JsonMember("noOp", new JsonBooleanValue(result.WasNoOp)));
            root.Members.Add(new JsonMember("id", result.PublishedId is long id ? Number(id) : new JsonNullValue()));
            root.Members.Add(new JsonMember("files", Number(result.CapturedFileCount)));
            root.Members.Add(new JsonMember("added", StringArray(result.Added)));
            root.Members.Add(new JsonMember("changed", StringArray(result.Changed)));
            root.Members.Add(new JsonMember("removed", StringArray(result.Removed)));
            root.Members.Add(new JsonMember("retainedBytes", Number(result.Cleanup.RetainedBytes)));
            root.Members.Add(new JsonMember("evicted", Number(result.Cleanup.Evicted)));
            root.Members.Add(new JsonMember("reclaimed", Number(result.Cleanup.Reclaimed)));
            root.Members.Add(new JsonMember("budgetOk", new JsonBooleanValue(result.Cleanup.BudgetOk)));
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

        Console.WriteLine($"checkpoint {ManifestId.Format(result.PublishedId!.Value)}: {result.CapturedFileCount} files, {result.Added.Count} added, {result.Changed.Count} changed, {result.Removed.Count} removed, {result.Cleanup.Evicted} evicted, {result.Cleanup.Reclaimed} reclaimed, {result.Cleanup.RetainedBytes} bytes retained");
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

    public static void Init(InitResult result, bool json, bool quiet)
    {
        var exclusions = new[] { "_repolore/sessions/", "_repolore/.history/" };

        if (json)
        {
            var root = new JsonObject();
            root.Members.Add(new JsonMember("noOp", new JsonBooleanValue(result.WasNoOp)));
            root.Members.Add(new JsonMember("created", StringArray(result.Created)));
            root.Members.Add(new JsonMember("historyDisabled", new JsonBooleanValue(result.HistoryDisabled)));
            root.Members.Add(new JsonMember("checkpointId", result.PublishedId is long id ? Number(id) : new JsonNullValue()));
            root.Members.Add(new JsonMember("methodUpdated", new JsonBooleanValue(result.MethodUpdated)));
            root.Members.Add(new JsonMember("recommendedGitExclusions", StringArray(exclusions)));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (quiet)
            return;

        if (result.WasNoOp)
        {
            Console.WriteLine("init: nothing to create");
            return;
        }

        foreach (var path in result.Created)
            Console.WriteLine("created " + path);

        if (result.MethodUpdated)
            Console.WriteLine("updated _repolore/method.md");

        if (result.HistoryDisabled)
        {
            Console.WriteLine("history is disabled in _repolore/repolore.json; destructive updates are not protected by checkpoints");
            return;
        }

        if (result.CheckpointNoOp)
            Console.WriteLine("checkpoint: no changes");
        else if (result.PublishedId is long id)
            Console.WriteLine($"checkpoint {ManifestId.Format(id)}");

        Console.WriteLine("recommended .gitignore additions: _repolore/sessions/ _repolore/.history/");
    }

    public static void RestorePlan(RestorePlan plan, bool json, bool quiet)
    {
        if (json)
        {
            var array = new JsonArray();
            foreach (var entry in plan.Entries)
            {
                var obj = new JsonObject();
                obj.Members.Add(new JsonMember("action", new JsonStringValue(ActionName(entry.Action))));
                obj.Members.Add(new JsonMember("path", new JsonStringValue(entry.Path)));
                obj.Members.Add(new JsonMember("reason", new JsonStringValue(entry.Reason)));
                obj.Members.Add(new JsonMember("before", entry.Before is null ? new JsonNullValue() : new JsonStringValue(entry.Before)));
                obj.Members.Add(new JsonMember("after", entry.After is null ? new JsonNullValue() : new JsonStringValue(entry.After)));
                array.Items.Add(obj);
            }
            var root = new JsonObject();
            root.Members.Add(new JsonMember("entries", array));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (quiet)
            return;

        if (plan.Entries.Count == 0)
        {
            Console.WriteLine("restore: no changes");
            return;
        }

        foreach (var entry in plan.Entries)
            Console.WriteLine($"{ActionName(entry.Action)} {entry.Path} ({entry.Reason})");
    }

    public static void Restore(RestoreResult result, bool json, bool quiet)
    {
        if (json)
        {
            var root = new JsonObject();
            root.Members.Add(new JsonMember("noOp", new JsonBooleanValue(result.WasNoOp)));
            root.Members.Add(new JsonMember("recovery", new JsonBooleanValue(result.WasRecovery)));
            root.Members.Add(new JsonMember("checkpointId", result.PublishedId is long id ? Number(id) : new JsonNullValue()));
            root.Members.Add(new JsonMember("recoveryId", Number(result.RecoveryId)));
            root.Members.Add(new JsonMember("added", Number(result.Added)));
            root.Members.Add(new JsonMember("replaced", Number(result.Replaced)));
            root.Members.Add(new JsonMember("deleted", Number(result.Deleted)));
            root.Members.Add(new JsonMember("recovered", Number(result.Recovered)));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (quiet)
            return;

        if (result.WasNoOp)
        {
            Console.WriteLine("restore: no changes");
            return;
        }

        if (result.WasRecovery)
        {
            Console.WriteLine($"recovered {result.Recovered} files to the pre-operation state (checkpoint {ManifestId.Format(result.PublishedId!.Value)})");
            return;
        }

        Console.WriteLine($"restore complete: {result.Added} added, {result.Replaced} replaced, {result.Deleted} deleted (checkpoint {ManifestId.Format(result.PublishedId!.Value)})");
    }

    public static void MigratePlan(MigrationInventory inventory, IReadOnlyList<MigrationMapping> mappings, bool json, bool quiet, bool dryRun)
    {
        if (json)
        {
            var root = new JsonObject();
            var array = new JsonArray();
            foreach (var mapping in mappings)
            {
                var obj = new JsonObject();
                obj.Members.Add(new JsonMember("status", new JsonStringValue(MappingStatusName(mapping.Status))));
                obj.Members.Add(new JsonMember("key", new JsonStringValue(mapping.Key)));
                obj.Members.Add(new JsonMember("destination", new JsonStringValue(mapping.Destination)));
                if (mapping.SourcePath is not null)
                    obj.Members.Add(new JsonMember("source", new JsonStringValue(mapping.SourcePath)));
                if (mapping.Finding is not null)
                    obj.Members.Add(new JsonMember("finding", new JsonStringValue(mapping.Finding)));
                array.Items.Add(obj);
            }
            root.Members.Add(new JsonMember("mappings", array));
            root.Members.Add(new JsonMember("legacyToRemove", StringArray(inventory.LegacyToRemove)));
            root.Members.Add(new JsonMember("unknownFiles", StringArray(inventory.UnknownFiles)));
            root.Members.Add(new JsonMember("customAreas", StringArray(inventory.CustomAreas)));
            root.Members.Add(new JsonMember("sessionAreas", StringArray(inventory.SessionAreas)));
            root.Members.Add(new JsonMember("sessionsNamingConflict", new JsonBooleanValue(inventory.SessionsNamingConflict)));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        if (quiet)
            return;

        foreach (var mapping in mappings)
        {
            switch (mapping.Status)
            {
                case MappingStatus.Candidate:
                    if (mapping.SourcePath is not null)
                        Console.WriteLine((dryRun ? "copy " : "candidate ") + mapping.SourcePath + " -> " + mapping.Destination);
                    else
                        Console.WriteLine((dryRun ? "keep " : "candidate ") + mapping.Destination);
                    break;
                case MappingStatus.Conflict:
                case MappingStatus.Ambiguous:
                    Console.WriteLine("conflict " + mapping.Destination + ": " + mapping.Finding);
                    break;
            }
        }

        if (dryRun)
        {
            foreach (var legacy in inventory.LegacyToRemove)
                Console.WriteLine("remove " + legacy);
            Console.WriteLine("create " + MigrationEngine.MarkerPath);
        }

        foreach (var area in inventory.CustomAreas)
            Console.WriteLine("unchanged " + area);
        foreach (var session in inventory.SessionAreas)
            Console.WriteLine("session " + session);
        if (inventory.SessionsNamingConflict)
            Console.WriteLine("conflict _repolore/sessions/ does not match the session layout");
        foreach (var unknown in inventory.UnknownFiles)
            Console.WriteLine("left-in-place " + unknown);
    }

    public static void Migrate(MigrationResult result, bool json, bool quiet)
    {
        if (json)
        {
            var root = new JsonObject();
            root.Members.Add(new JsonMember("checkpointId", result.PublishedId is long id ? Number(id) : new JsonNullValue()));
            root.Members.Add(new JsonMember("created", Number(result.Created)));
            root.Members.Add(new JsonMember("removed", Number(result.Removed)));
            root.Members.Add(new JsonMember("leftBehind", StringArray(result.LeftBehind)));
            Console.WriteLine(JsonWriter.Write(root));
            return;
        }

        foreach (var left in result.LeftBehind)
            Console.Error.WriteLine("repolore: left in place (unknown legacy content): " + left);

        if (quiet)
            return;

        var checkpoint = result.PublishedId is long published ? ManifestId.Format(published) : "?";
        Console.WriteLine($"migrated: {result.Created} created, {result.Removed} removed (checkpoint {checkpoint})");
    }

    private static string MappingStatusName(MappingStatus status) => status switch
    {
        MappingStatus.Candidate => "candidate",
        MappingStatus.Conflict => "conflict",
        MappingStatus.Ambiguous => "ambiguous",
        _ => "unknown"
    };

    private static string ActionName(RestoreAction action) => action switch
    {
        RestoreAction.Add => "add",
        RestoreAction.Replace => "replace",
        RestoreAction.Delete => "delete",
        RestoreAction.Unchanged => "unchanged",
        _ => "unknown"
    };

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
