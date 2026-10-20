using System.Security.Cryptography;
using Valdrio.Application;

namespace Valdrio.Infrastructure.CompanyFiles;

public sealed partial class SqliteCompanyFileService
{
    private sealed partial class Session
    {
        private byte[]? validatedContent;
        private CompanyFileSnapshot? validatedSnapshot;

        // Called under gate. Reuse only validation of identical database bytes, never
        // a timestamp/revision approximation. Every write and external alteration
        // changes the digest, including changes outside the Company aggregate.
        private CompanyFileSnapshot ReadValidated(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var before = ContentDigest(token);
            if (before is not null && validatedContent is not null &&
                before.AsSpan().SequenceEqual(validatedContent))
                return validatedSnapshot!;

            validatedContent = null;
            validatedSnapshot = null;
            var snapshot = Validate(Path, token);
            var after = ContentDigest(token);
            if (before is not null && after is not null && before.AsSpan().SequenceEqual(after))
            {
                validatedSnapshot = snapshot;
                validatedContent = after;
            }
            return snapshot;
        }

        private byte[]? ContentDigest(CancellationToken token)
        {
            // A live journal/WAL may hold data absent from the main file. In that
            // case retain the original transactional full-validation behavior.
            bool HasSidecar() => File.Exists(Path + "-wal") || File.Exists(Path + "-journal");
            if (HasSidecar()) return null;
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 128 * 1024, FileOptions.SequentialScan);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            int count;
            while ((count = stream.Read(buffer)) > 0)
            {
                token.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, count);
            }
            token.ThrowIfCancellationRequested();
            return HasSidecar() ? null : hash.GetHashAndReset();
        }
    }
}
