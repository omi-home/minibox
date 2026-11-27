using System.Security.Cryptography;
using Minibox.Identity.Application.Abstractions;

namespace Minibox.Identity.Infrastructure.Security;

/// <summary>Mã hoá secret TOTP bằng AES-256-GCM: <c>nonce(12) ‖ tag(16) ‖ ciphertext</c>, AAD là id tài khoản.</summary>
/// <remarks>Không log secret. Mất khoá = mọi secret TOTP vô hiệu.</remarks>
public sealed class AesGcmMfaSecretProtector : IMfaSecretProtector
{
    const int NonceSize = 12, TagSize = 16;
    readonly byte[] _key;

    /// <summary>Tạo bộ mã hoá với khoá AES-256.</summary>
    /// <param name="key">32 byte, đọc từ secret <c>Mfa:EncryptionKey</c>.</param>
    /// <exception cref="ArgumentException">Khoá không dài 32 byte.</exception>
    public AesGcmMfaSecretProtector(byte[] key) =>
        _key = key.Length == 32 ? key : throw new ArgumentException("Khóa mã hóa MFA phải dài 32 byte (AES-256).", nameof(key));

    /// <inheritdoc/>
    public byte[] Protect(Guid accountId, byte[] secret)
    {
        var output = new byte[NonceSize + TagSize + secret.Length];
        RandomNumberGenerator.Fill(output.AsSpan(0, NonceSize));
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(output.AsSpan(0, NonceSize), secret, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize), accountId.ToByteArray());
        return output;
    }

    /// <inheritdoc/>
    public byte[] Unprotect(Guid accountId, byte[] encrypted)
    {
        var secret = new byte[encrypted.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(encrypted.AsSpan(0, NonceSize), encrypted.AsSpan(NonceSize + TagSize), encrypted.AsSpan(NonceSize, TagSize), secret, accountId.ToByteArray());
        return secret;
    }
}
