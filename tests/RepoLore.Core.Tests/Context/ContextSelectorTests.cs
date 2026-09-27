using RepoLore.Core.Context;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Context;

public static class ContextSelectorTests
{
    public static void Run()
    {
        TestRunner.Check("durable path selection orders root, ancestors, then target", () =>
        {
            var selection = ContextSelector.Select(new ContextRequest { SourceTarget = "src/sub/" });
            TestRunner.Equal(3, selection.Candidates.Count);
            TestRunner.Equal("_repolore/root.md", selection.Candidates[0].CanonicalPath);
            TestRunner.Equal(CandidateKind.Root, selection.Candidates[0].Kind);
            TestRunner.Equal("_repolore/sparse-tree/src/src.md", selection.Candidates[1].CanonicalPath);
            TestRunner.Equal("_repolore/tree/src/src.md", selection.Candidates[1].LegacyPath);
            TestRunner.Equal(CandidateKind.Ancestor, selection.Candidates[1].Kind);
            TestRunner.Equal("_repolore/sparse-tree/src/sub/sub.md", selection.Candidates[2].CanonicalPath);
            TestRunner.Equal(CandidateKind.Target, selection.Candidates[2].Kind);
        });

        TestRunner.Check("root target maps only to root.md and method leads", () =>
        {
            var selection = ContextSelector.Select(new ContextRequest { SourceTarget = ".", IncludeMethod = true });
            TestRunner.Equal(2, selection.Candidates.Count);
            TestRunner.Equal("_repolore/method.md", selection.Candidates[0].CanonicalPath);
            TestRunner.Equal(CandidateKind.Method, selection.Candidates[0].Kind);
            TestRunner.Equal("_repolore/root.md", selection.Candidates[1].CanonicalPath);
            TestRunner.Equal(CandidateKind.Root, selection.Candidates[1].Kind);
        });

        TestRunner.Check("session selection yields session root then named nodes", () =>
        {
            var selection = ContextSelector.Select(new ContextRequest
            {
                SessionId = "session-a",
                Nodes = new[] { "_repolore/sessions/session-a/investigation.md" }
            });
            TestRunner.Equal(2, selection.Candidates.Count);
            TestRunner.Equal("_repolore/sessions/session-a/root.md", selection.Candidates[0].CanonicalPath);
            TestRunner.Equal("session:session-a", selection.Candidates[0].Scope);
            TestRunner.Equal(CandidateKind.SessionRoot, selection.Candidates[0].Kind);
            TestRunner.Equal("_repolore/sessions/session-a/investigation.md", selection.Candidates[1].CanonicalPath);
            TestRunner.Equal("session:session-a", selection.Candidates[1].Scope);
            TestRunner.Equal(CandidateKind.Node, selection.Candidates[1].Kind);
        });

        TestRunner.Check("duplicate nodes and mapped notes are deduplicated", () =>
        {
            var byNode = ContextSelector.Select(new ContextRequest
            {
                Nodes = new[] { "_repolore/root.md", "_repolore/root.md" }
            });
            TestRunner.Equal(1, byNode.Candidates.Count);

            var byTarget = ContextSelector.Select(new ContextRequest
            {
                SourceTarget = "src/",
                Nodes = new[] { "_repolore/sparse-tree/src/src.md" }
            });
            TestRunner.Equal(2, byTarget.Candidates.Count);
            TestRunner.Equal("_repolore/root.md", byTarget.Candidates[0].CanonicalPath);
            TestRunner.Equal("_repolore/sparse-tree/src/src.md", byTarget.Candidates[1].CanonicalPath);
        });

        TestRunner.Check("nodes alone read only explicit nodes plus method", () =>
        {
            var selection = ContextSelector.Select(new ContextRequest
            {
                IncludeMethod = true,
                Nodes = new[] { "_repolore/architecture/a.md" }
            });
            TestRunner.Equal(2, selection.Candidates.Count);
            TestRunner.Equal("_repolore/method.md", selection.Candidates[0].CanonicalPath);
            TestRunner.Equal("_repolore/architecture/a.md", selection.Candidates[1].CanonicalPath);
            TestRunner.Equal("durable", selection.Candidates[1].Scope);
        });

        TestRunner.Check("invalid, cross-session, and history nodes are rejected", () =>
        {
            TestRunner.Throws<ArgumentException>(() => ContextSelector.Select(new ContextRequest
            {
                Nodes = new[] { "_repolore/sessions/session-b/investigation.md" }
            }), "session node without --session");

            TestRunner.Throws<ArgumentException>(() => ContextSelector.Select(new ContextRequest
            {
                SessionId = "session-a",
                Nodes = new[] { "_repolore/sessions/session-b/investigation.md" }
            }), "cross-session node");

            TestRunner.Throws<ArgumentException>(() => ContextSelector.Select(new ContextRequest
            {
                Nodes = new[] { "_repolore/.history/objects/x.md" }
            }), "history node");

            TestRunner.Throws<ArgumentException>(() => ContextSelector.Select(new ContextRequest
            {
                Nodes = new[] { "outside.md" }
            }), "node outside _repolore");

            TestRunner.Throws<ArgumentException>(() => ContextSelector.Select(new ContextRequest
            {
                Nodes = new[] { "_repolore/repolore.json" }
            }), "non-markdown node");
        });

        TestRunner.Check("unsupported source target segments are rejected", () =>
        {
            foreach (var bad in new[] { "a/../b", "a//b", "a\\b", "/abs", "a:b" })
                TestRunner.Throws<ArgumentException>(() => ContextSelector.Select(new ContextRequest { SourceTarget = bad }), bad);
        });
    }
}
