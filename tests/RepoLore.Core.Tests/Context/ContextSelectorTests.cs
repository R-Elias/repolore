using FluentAssertions;
using RepoLore.Core.Context;
using Xunit;

namespace RepoLore.Core.Tests.Context;

public class ContextSelectorTests
{
    [Fact]
    public void Durable_path_selection_orders_root_ancestors_then_target()
    {
        var selection = ContextSelector.Select(new ContextRequest { SourceTarget = "src/sub/" });
        selection.Candidates.Should().HaveCount(3);
        selection.Candidates[0].CanonicalPath.Should().Be("_repolore/root.md");
        selection.Candidates[0].Kind.Should().Be(CandidateKind.Root);
        selection.Candidates[1].CanonicalPath.Should().Be("_repolore/sparse-tree/src/src.md");
        selection.Candidates[1].LegacyPath.Should().Be("_repolore/tree/src/src.md");
        selection.Candidates[1].Kind.Should().Be(CandidateKind.Ancestor);
        selection.Candidates[2].CanonicalPath.Should().Be("_repolore/sparse-tree/src/sub/sub.md");
        selection.Candidates[2].Kind.Should().Be(CandidateKind.Target);
    }

    [Fact]
    public void Root_target_maps_only_to_root_md_and_method_leads()
    {
        var selection = ContextSelector.Select(new ContextRequest { SourceTarget = ".", IncludeMethod = true });
        selection.Candidates.Should().HaveCount(2);
        selection.Candidates[0].CanonicalPath.Should().Be("_repolore/method.md");
        selection.Candidates[0].Kind.Should().Be(CandidateKind.Method);
        selection.Candidates[1].CanonicalPath.Should().Be("_repolore/root.md");
        selection.Candidates[1].Kind.Should().Be(CandidateKind.Root);
    }

    [Fact]
    public void Session_selection_yields_session_root_then_named_nodes()
    {
        var selection = ContextSelector.Select(new ContextRequest
        {
            SessionId = "session-a",
            Nodes = new[] { "_repolore/sessions/session-a/investigation.md" }
        });
        selection.Candidates.Should().HaveCount(2);
        selection.Candidates[0].CanonicalPath.Should().Be("_repolore/sessions/session-a/root.md");
        selection.Candidates[0].Scope.Should().Be("session:session-a");
        selection.Candidates[0].Kind.Should().Be(CandidateKind.SessionRoot);
        selection.Candidates[1].CanonicalPath.Should().Be("_repolore/sessions/session-a/investigation.md");
        selection.Candidates[1].Scope.Should().Be("session:session-a");
        selection.Candidates[1].Kind.Should().Be(CandidateKind.Node);
    }

    [Fact]
    public void Duplicate_nodes_and_mapped_notes_are_deduplicated()
    {
        var byNode = ContextSelector.Select(new ContextRequest
        {
            Nodes = new[] { "_repolore/root.md", "_repolore/root.md" }
        });
        byNode.Candidates.Should().HaveCount(1);

        var byTarget = ContextSelector.Select(new ContextRequest
        {
            SourceTarget = "src/",
            Nodes = new[] { "_repolore/sparse-tree/src/src.md" }
        });
        byTarget.Candidates.Should().HaveCount(2);
        byTarget.Candidates[0].CanonicalPath.Should().Be("_repolore/root.md");
        byTarget.Candidates[1].CanonicalPath.Should().Be("_repolore/sparse-tree/src/src.md");
    }

    [Fact]
    public void Nodes_alone_read_only_explicit_nodes_plus_method()
    {
        var selection = ContextSelector.Select(new ContextRequest
        {
            IncludeMethod = true,
            Nodes = new[] { "_repolore/architecture/a.md" }
        });
        selection.Candidates.Should().HaveCount(2);
        selection.Candidates[0].CanonicalPath.Should().Be("_repolore/method.md");
        selection.Candidates[1].CanonicalPath.Should().Be("_repolore/architecture/a.md");
        selection.Candidates[1].Scope.Should().Be("durable");
    }

    [Fact]
    public void Invalid_cross_session_and_history_nodes_are_rejected()
    {
        new Action(() => ContextSelector.Select(new ContextRequest
        {
            Nodes = new[] { "_repolore/sessions/session-b/investigation.md" }
        })).Should().Throw<ArgumentException>("session node without --session");

        new Action(() => ContextSelector.Select(new ContextRequest
        {
            SessionId = "session-a",
            Nodes = new[] { "_repolore/sessions/session-b/investigation.md" }
        })).Should().Throw<ArgumentException>("cross-session node");

        new Action(() => ContextSelector.Select(new ContextRequest
        {
            Nodes = new[] { "_repolore/.history/objects/x.md" }
        })).Should().Throw<ArgumentException>("history node");

        new Action(() => ContextSelector.Select(new ContextRequest
        {
            Nodes = new[] { "outside.md" }
        })).Should().Throw<ArgumentException>("node outside _repolore");

        new Action(() => ContextSelector.Select(new ContextRequest
        {
            Nodes = new[] { "_repolore/repolore.json" }
        })).Should().Throw<ArgumentException>("non-markdown node");
    }

    [Fact]
    public void Unsupported_source_target_segments_are_rejected()
    {
        foreach (var bad in new[] { "a/../b", "a//b", "a\\b", "/abs", "a:b" })
            new Action(() => ContextSelector.Select(new ContextRequest { SourceTarget = bad })).Should().Throw<ArgumentException>(bad);
    }
}
