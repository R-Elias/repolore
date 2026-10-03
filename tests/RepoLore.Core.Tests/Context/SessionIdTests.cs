using FluentAssertions;
using RepoLore.Core.Context;
using Xunit;

namespace RepoLore.Core.Tests.Context;

public class SessionIdTests
{
    [Fact]
    public void Session_ids_accept_portable_directory_names()
    {
        foreach (var id in new[] { "a", "a0", "0abc", "session-a", "2026-09-05-payment-retry-a7c2", "ABC_123-x", "COM0", "COM10", "LPT0" })
            SessionId.IsValid(id, out _).Should().BeTrue(id);
    }

    [Fact]
    public void Session_ids_reject_invalid_and_reserved_names()
    {
        foreach (var id in new[] { "", "_abc", "-abc", "ab c", "ab.c", "a/b", "a\\b", "CON", "con", "Con", "PRN", "AUX", "NUL", "COM1", "COM9", "LPT1", "LPT9", "com3", "lpt5" })
        {
            SessionId.IsValid(id, out var finding).Should().BeFalse($"expected rejection: {id}");
            finding.Should().NotBeNullOrEmpty($"expected finding text: {id}");
        }
    }

    [Fact]
    public void Session_ids_enforce_the_100_character_limit()
    {
        SessionId.IsValid(new string('a', 100), out _).Should().BeTrue();
        SessionId.IsValid(new string('a', 101), out _).Should().BeFalse();
    }
}
