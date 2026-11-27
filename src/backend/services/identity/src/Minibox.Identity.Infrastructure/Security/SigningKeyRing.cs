using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Minibox.Identity.Infrastructure.Tokens;

/// <summary>Khóa ký ES256 xoay vòng theo chu kỳ cố định. JWKS công bố khóa chu kỳ trước (token còn hạn),
/// hiện tại và kế tiếp (công bố sẵn trước khi dùng nên service cache JWKS không gặp <c>kid</c> lạ lúc xoay).</summary>
/// <remarks>ponytail: khóa chỉ nằm trong bộ nhớ (không private key nào ở đĩa/DB) — identity khởi động lại làm access token cũ
/// (≤ 5 phút) bị từ chối và BFF refresh lại; chạy nhiều instance identity thì phải chuyển key ring sang kho dùng chung.</remarks>
/// <param name="time">Đồng hồ.</param>
/// <param name="period">Chu kỳ xoay khóa.</param>
public sealed class SigningKeyRing(TimeProvider time, TimeSpan period)
{
    /// <summary>Chu kỳ xoay mặc định.</summary>
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromHours(24);

    readonly Lock _lock = new();
    readonly SortedDictionary<long, ECDsaSecurityKey> _keys = [];

    /// <summary>Khóa ký của chu kỳ hiện tại.</summary>
    public ECDsaSecurityKey Current
    {
        get
        {
            lock (_lock)
                return _keys[Refresh()];
        }
    }

    /// <summary>Khóa đang công bố, mới nhất trước.</summary>
    public IReadOnlyList<ECDsaSecurityKey> Published
    {
        get
        {
            lock (_lock)
            {
                Refresh();
                return [.. _keys.Values.Reverse()];
            }
        }
    }

    /// <summary>Tài liệu JWKS (RFC 7517) chỉ gồm phần công khai của các khóa đang công bố.</summary>
    /// <returns>Đối tượng <c>{"keys":[…]}</c> để serialize JSON.</returns>
    public object Jwks() => new
    {
        keys = Published.Select(k =>
        {
            var p = k.ECDsa.ExportParameters(includePrivateParameters: false);
            return new
            {
                kty = "EC", crv = "P-256", use = "sig", alg = "ES256", kid = k.KeyId,
                x = Base64UrlEncoder.Encode(p.Q.X), y = Base64UrlEncoder.Encode(p.Q.Y),
            };
        }).ToList(),
    };

    long Refresh()
    {
        var slot = time.GetUtcNow().ToUnixTimeMilliseconds() / (long)period.TotalMilliseconds;
        foreach (var old in _keys.Keys.Where(s => s < slot - 1).ToList())
            _keys.Remove(old);
        for (var s = slot; s <= slot + 1; s++)
            if (!_keys.ContainsKey(s))
                _keys[s] = new ECDsaSecurityKey(ECDsa.Create(ECCurve.NamedCurves.nistP256))
                {
                    KeyId = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(16)),
                };
        return slot;
    }
}
