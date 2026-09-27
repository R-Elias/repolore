using RepoLore.Core.Context;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Context;

public static class SessionIdTests
{
    public static void Run()
    {
        TestRunner.Check("session ids accept portable directory names", () =>
        {
            foreach (var id in new[] { "a", "a0", "0abc", "session-a", "2026-09-05-payment-retry-a7c2", "ABC_123-x", "COM0", "COM10", "LPT0" })
                TestRunner.True(SessionId.IsValid(id, out _), id);
        });

        TestRunner.Check("session ids reject invalid and reserved names", () =>
        {
            foreach (var id in new[] { "", "_abc", "-abc", "ab c", "ab.c", "a/b", "a\\b", "CON", "con", "Con", "PRN", "AUX", "NUL", "COM1", "COM9", "LPT1", "LPT9", "com3", "lpt5" })
            {
                TestRunner.True(!SessionId.IsValid(id, out var finding), $"expected rejection: {id}");
                TestRunner.True(!string.IsNullOrEmpty(finding), $"expected finding text: {id}");
            }
        });

        TestRunner.Check("session ids enforce the 100-character limit", () =>
        {
            TestRunner.True(SessionId.IsValid(new string('a', 100), out _));
            TestRunner.True(!SessionId.IsValid(new string('a', 101), out _));
        });
    }
}
