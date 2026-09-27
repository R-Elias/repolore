using RepoLore.Cli.Tests;

namespace RepoLore.Cli.Tests;

public static class DurableSessionContextTests
{
    public static void Run()
    {
        TestRunner.Check("default durable context excludes both sessions", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", ".");
                TestRunner.Equal(0, result.Code, result.Error);
                var outText = TestSupport.Normalize(result.Out);
                TestRunner.True(outText.Contains("TWO_SESSIONS_DURABLE_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(!outText.Contains("SESSION_A", StringComparison.Ordinal));
                TestRunner.True(!outText.Contains("SESSION_B", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("selecting session A reads only A's root and named nodes", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var root = TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-a");
                TestRunner.Equal(0, root.Code, root.Error);
                var rootText = TestSupport.Normalize(root.Out);
                TestRunner.True(rootText.Contains("SESSION_A_ROOT_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(!rootText.Contains("SESSION_B", StringComparison.Ordinal));
                TestRunner.True(!rootText.Contains("TWO_SESSIONS_DURABLE", StringComparison.Ordinal));

                var withNode = TestSupport.Run(temp, TestSupport.Cli, "context",
                    "--session", "session-a", "--node", "_repolore/sessions/session-a/investigation.md");
                TestRunner.Equal(0, withNode.Code, withNode.Error);
                var nodeText = TestSupport.Normalize(withNode.Out);
                TestRunner.True(nodeText.Contains("SESSION_A_ROOT_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(nodeText.Contains("SESSION_A_DETAIL_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(!nodeText.Contains("SESSION_B", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("missing session is a finding", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-missing");
                TestRunner.Equal(1, result.Code);
                TestRunner.True(result.Error.Contains("missing-session", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("missing explicit node is a finding", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "--node", "_repolore/does-not-exist.md");
                TestRunner.Equal(1, result.Code);
                TestRunner.True(result.Error.Contains("missing-note", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("invalid --session is a usage error even with a tiny budget", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "bad id!", "--budget-tokens", "1");
                TestRunner.Equal(2, result.Code);
                TestRunner.True(result.Error.Contains("invalid --session", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("cross-session node is rejected", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context",
                    "--session", "session-a", "--node", "_repolore/sessions/session-b/investigation.md");
                TestRunner.Equal(2, result.Code);
            });
        });

        TestRunner.Check("no selector and non-positive budget are usage errors", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                TestRunner.Equal(2, TestSupport.Run(temp, TestSupport.Cli, "context").Code);
                TestRunner.Equal(2, TestSupport.Run(temp, TestSupport.Cli, "context", ".", "--budget-tokens", "0").Code);
                TestRunner.Equal(2, TestSupport.Run(temp, TestSupport.Cli, "context", ".", "--budget-tokens", "-5").Code);
            });
        });

        TestRunner.Check("duplicate nodes appear once", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context",
                    "--node", "_repolore/root.md", "--node", "_repolore/root.md");
                TestRunner.Equal(0, result.Code, result.Error);
                TestRunner.Equal(1, Count(TestSupport.Normalize(result.Out), "## _repolore/root.md"));
            });
        });

        TestRunner.Check("oversized ancestor cannot hide a later fitting target note", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                WriteBudgetFixture(temp);
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50");
                TestRunner.Equal(0, result.Code, result.Error);
                var outText = TestSupport.Normalize(result.Out);
                TestRunner.True(outText.Contains("TARGET_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(!outText.Contains("ANCESTOR_MARKER", StringComparison.Ordinal));
                TestRunner.True(result.Error.Contains("omitted: _repolore/sparse-tree/src/src.md", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("budget omissions with --strict exit 1 and keep the partial result", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                WriteBudgetFixture(temp);
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50", "--strict");
                TestRunner.Equal(1, result.Code);
                var outText = TestSupport.Normalize(result.Out);
                TestRunner.True(outText.Contains("TARGET_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(!outText.Contains("ANCESTOR_MARKER", StringComparison.Ordinal));
                TestRunner.True(result.Error.Contains("omitted:", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("alpha read support: sparse-only, tree-only, and conflict", () =>
        {
            WithFixture("alpha-sparse-only", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/");
                TestRunner.Equal(0, result.Code, result.Error);
                TestRunner.True(TestSupport.Normalize(result.Out).Contains("ALPHA_SPARSE_ONLY_SENTINEL", StringComparison.Ordinal));
            });

            WithFixture("alpha-local-only", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/");
                TestRunner.Equal(0, result.Code, result.Error);
                TestRunner.True(TestSupport.Normalize(result.Out).Contains("ALPHA_LOCAL_ONLY_SENTINEL", StringComparison.Ordinal));
            });

            WithFixture("alpha-conflict", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/");
                TestRunner.Equal(3, result.Code);
                TestRunner.True(result.Error.Contains("conflict", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("path lists durable note status", () =>
        {
            WithFixture("minimal-v1", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "path", "src/");
                TestRunner.Equal(0, result.Code, result.Error);
                var outText = TestSupport.Normalize(result.Out);
                TestRunner.True(outText.Contains("present _repolore/root.md", StringComparison.Ordinal));
                TestRunner.True(outText.Contains("present _repolore/sparse-tree/src/src.md", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("tree lists durable knowledge and session IDs only when requested", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var durable = TestSupport.Run(temp, TestSupport.Cli, "tree");
                TestRunner.Equal(0, durable.Code, durable.Error);
                var durableText = TestSupport.Normalize(durable.Out);
                TestRunner.True(durableText.Contains("_repolore/root.md", StringComparison.Ordinal));
                TestRunner.True(!durableText.Contains("sessions", StringComparison.Ordinal));

                var sessions = TestSupport.Run(temp, TestSupport.Cli, "tree", "--start", "_repolore/sessions/");
                TestRunner.Equal(0, sessions.Code, sessions.Error);
                var sessionsText = TestSupport.Normalize(sessions.Out);
                TestRunner.True(sessionsText.Contains("_repolore/sessions/session-a/", StringComparison.Ordinal));
                TestRunner.True(sessionsText.Contains("_repolore/sessions/session-b/", StringComparison.Ordinal));
                TestRunner.True(!sessionsText.Contains("root.md", StringComparison.Ordinal));

                var inside = TestSupport.Run(temp, TestSupport.Cli, "tree", "--start", "_repolore/sessions/session-a/");
                TestRunner.Equal(0, inside.Code, inside.Error);
                var insideText = TestSupport.Normalize(inside.Out);
                TestRunner.True(insideText.Contains("_repolore/sessions/session-a/root.md", StringComparison.Ordinal));
                TestRunner.True(insideText.Contains("_repolore/sessions/session-a/investigation.md", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("json and plain context render the same logical sets", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                WriteBudgetFixture(temp);
                var plain = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50");
                var json = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50", "--json");
                TestRunner.Equal(plain.Code, json.Code, json.Error);
                var plainText = TestSupport.Normalize(plain.Out);
                var jsonText = TestSupport.Normalize(json.Out);
                TestRunner.True(jsonText.Contains("TARGET_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(!jsonText.Contains("ANCESTOR_MARKER", StringComparison.Ordinal));
                TestRunner.True(plainText.Contains("TARGET_SENTINEL", StringComparison.Ordinal) == jsonText.Contains("TARGET_SENTINEL", StringComparison.Ordinal));
                TestRunner.True(jsonText.Contains("omitted", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("read-only commands leave repository and history files unchanged", () =>
        {
            WithFixture("two-sessions", temp =>
            {
                var before = TestSupport.Snapshot(temp);
                TestSupport.Run(temp, TestSupport.Cli, "context", ".");
                TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-a");
                TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-b", "--node", "_repolore/sessions/session-b/investigation.md");
                TestSupport.Run(temp, TestSupport.Cli, "path", ".");
                TestSupport.Run(temp, TestSupport.Cli, "tree");
                TestSupport.Run(temp, TestSupport.Cli, "tree", "--start", "_repolore/sessions/");
                TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-missing");
                TestRunner.Equal(before, TestSupport.Snapshot(temp));
            });
        });
    }

    private static void WriteBudgetFixture(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "_repolore", "sparse-tree", "src", "sub"));
        File.WriteAllText(Path.Combine(root, "_repolore", "root.md"), "ROOT\n");
        File.WriteAllText(Path.Combine(root, "_repolore", "sparse-tree", "src", "src.md"), "ANCESTOR_MARKER" + new string('x', 500));
        File.WriteAllText(Path.Combine(root, "_repolore", "sparse-tree", "src", "sub", "sub.md"), "TARGET_SENTINEL\n");
    }

    private static void WithFixture(string name, Action<string> action) => TestSupport.WithFixture(name, action);

    private static int Count(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
