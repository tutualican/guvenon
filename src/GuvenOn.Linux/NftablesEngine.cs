using System.Text;
using GuvenOn.Core.Protection;

namespace GuvenOn.Linux;

internal sealed class NftablesEngine
{
    private static readonly string NftPath = ProcessRunner.FindExecutable(
        "/usr/sbin/nft",
        "/usr/bin/nft",
        "nft");

    public int RuleCount { get; private set; }

    public async Task ApplyAsync(
        ProtectionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        var ruleset = LinuxNftablesPolicy.BuildRuleset(policy);
        RuleCount = CountRules(ruleset);

        // Validate against a temporary table name. A check of the real table
        // would fail with EEXIST when protection is re-applied while the table
        // is still present.
        var checkRuleset = LinuxNftablesPolicy.BuildRuleset(policy, "guvenon_check");
        var check = await ProcessRunner.RunAsync(
            NftPath,
            new[] { "-c", "-f", "-" },
            checkRuleset,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (check.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"nftables rejected the ruleset: {Combine(check)}");
        }

        // The delete is intentionally best-effort: the table is absent on the
        // first enable and after a reboot.
        _ = await ProcessRunner.RunAsync(
            NftPath,
            new[] { "delete", "table", "inet", LinuxNftablesPolicy.TableName },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var apply = await ProcessRunner.RunAsync(
            NftPath,
            new[] { "-f", "-" },
            ruleset,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (apply.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"nftables could not apply the ruleset: {Combine(apply)}");
        }
    }

    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        _ = await ProcessRunner.RunAsync(
            NftPath,
            new[] { "delete", "table", "inet", LinuxNftablesPolicy.TableName },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        RuleCount = 0;
    }

    public async Task<bool> IsTablePresentAsync(CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            NftPath,
            new[] { "list", "table", "inet", LinuxNftablesPolicy.TableName },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    private static int CountRules(string ruleset) =>
        ruleset.Split('\n').Count(line =>
            line.TrimStart().StartsWith("oifname", StringComparison.Ordinal) ||
            line.TrimStart().StartsWith("iifname", StringComparison.Ordinal) ||
            line.Contains(" accept", StringComparison.Ordinal) ||
            line.Contains(" drop", StringComparison.Ordinal));

    private static string Combine(ProcessResult result)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            builder.Append(result.StandardError.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(result.StandardOutput.Trim());
        }

        return builder.Length == 0 ? $"exit code {result.ExitCode}" : builder.ToString();
    }
}
