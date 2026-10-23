namespace RepoLore.Cli.Tests.Packaging.Journey;

public static class JourneyContent
{
    public const string SessionAId = "2026-10-23-retry-path-a1c4";
    public const string SessionBId = "2026-10-23-queue-drain-b7e2";

    public const string DurableBaselineSentinel = "DURABLE-BASELINE";
    public const string PromotedFactSentinel = "PROMOTED-FACT";
    public const string SessionARootSentinel = "SESSION-A-ROOT";
    public const string SessionADraftSentinel = "SESSION-A-DRAFT";
    public const string SessionAVerifiedSentinel = "SESSION-A-VERIFIED";
    public const string SessionBRootSentinel = "SESSION-B-ROOT";

    public const string DurableNotePath = "_repolore/sparse-tree/src/src.md";
    public const string SessionAInvestigationPath = $"_repolore/sessions/{SessionAId}/investigation.md";
    public const string SessionARootPath = $"_repolore/sessions/{SessionAId}/root.md";
    public const string SessionBRootPath = $"_repolore/sessions/{SessionBId}/root.md";

    public static readonly JourneyFile GitIgnore = new(".gitignore",
        "_repolore/sessions/\n_repolore/.history/\n",
        "2cec448722d8e260077b5a34b20f1ed9c003b5f28542dc56a837c8e47282ff26");

    public static readonly JourneyFile RetryPolicy = new("src/retry.txt",
        "max-attempts=3\n",
        "8afac8c60546f58b8bd3d8b2df38eff8dd49cb3ab50e196ec5b3ffbdb84f1163");

    public static readonly JourneyFile DurableBaseline = new(DurableNotePath,
        "# src\n\nsrc/retry.txt holds the retry policy. DURABLE-BASELINE\n",
        "2be2d48d8a16f82dbb3b4736d190bee412f848c7236ce4cfd6c4e3657250fb04");

    public static readonly JourneyFile DurablePromoted = new(DurableNotePath,
        "# src\n\nsrc/retry.txt holds the retry policy. DURABLE-BASELINE\n\n" +
        "Retries stop after 3 attempts; the limit is max-attempts in src/retry.txt. PROMOTED-FACT\n",
        "1bd8c49a33a5fd42c44a7a9848bbe596f630084e5aea9bde55fd97312bb46927");

    public static readonly JourneyFile SessionARoot = new(SessionARootPath,
        "# Session A\n\nObjective: confirm the retry limit. SESSION-A-ROOT\n",
        "c5d3d6c2e41808d669df112073bd38df77a60ff02d8b8627a0164383be8695ca");

    public static readonly JourneyFile SessionAInvestigationDraft = new(SessionAInvestigationPath,
        "# Investigation\n\nUnverified: retries may stop after 3 attempts. SESSION-A-DRAFT\n",
        "5590104c5f6dfaf644ae2923c4e05bdd8d30b81d1af3001f37a9e95e5d39416b");

    public static readonly JourneyFile SessionAInvestigationVerified = new(SessionAInvestigationPath,
        "# Investigation\n\nVerified in src/retry.txt: retries stop after 3 attempts. SESSION-A-VERIFIED\n",
        "44fe86825b2b529b8a964bb3c9abaeb0348fa616383a23eddb719ffe56c27165");

    public static readonly JourneyFile SessionARootComplete = new(SessionARootPath,
        "# Session A\n\nStatus: complete; the retry limit was promoted to src/src.md. SESSION-A-ROOT\n",
        "5d6b943e0b21ea89bd57ccadbd0f5f62f1da9a8f6bce32e8c61d10480c7d58cc");

    public static readonly JourneyFile SessionBRoot = new(SessionBRootPath,
        "# Session B\n\nObjective: unrelated queue investigation. SESSION-B-ROOT\n",
        "d5189b38adedd187de8414cac70e0dd89d63b8560b402e41fad2edb48e6469c5");

    public static readonly JourneyFile FormatMarker = new("_repolore/repolore.json",
        "{\"formatVersion\":1}",
        "1186589f022f55694dae32f0ed63dafcc7f04efc0a0b8b29fb57229cd129bc98");

    public const string LeakyPromotionSuffix =
        $"\nSee [the investigation](../../sessions/{SessionAId}/investigation.md).\n";
}
