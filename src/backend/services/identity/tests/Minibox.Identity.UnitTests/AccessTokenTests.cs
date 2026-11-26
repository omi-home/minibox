using Microsoft.IdentityModel.JsonWebTokens;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Infrastructure.Tokens;

namespace Minibox.Identity.UnitTests;

/// <summary>Access token ES256 và key ring xoay vòng (spec §3.2).</summary>
public class AccessTokenTests
{
    sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static readonly DateTimeOffset T0 = new(2026, 11, 26, 3, 0, 0, TimeSpan.Zero);
    readonly FakeTime _time = new(T0);

    JwtAccessTokenIssuer Issuer(SigningKeyRing ring) => new(ring, _time, "http://identity:8080");

    static AccessTokenRequest Request() => new("acc-1", "staff",
        new Dictionary<string, IReadOnlyCollection<string>> { ["*"] = ["catalog.product.read"], ["wh:2"] = ["inventory.adjustment.approve"] },
        T0.AddMinutes(-1), ["pwd", "mfa"]);

    [Fact(DisplayName = "ac195_4 Access token ES256 hết hạn sau 5 phút, mang sub/kind/perms/auth_time/amr/jti")]
    public void ac195_4_token_expires_after_5_minutes_with_claims()
    {
        var ring = new SigningKeyRing(_time, SigningKeyRing.DefaultPeriod);
        var issued = Issuer(ring).Issue(Request());
        var jwt = new JsonWebToken(issued.Token);

        Assert.Equal("ES256", jwt.Alg);
        Assert.Equal(ring.Current.KeyId, jwt.Kid);
        Assert.Equal(TimeSpan.FromMinutes(5), jwt.ValidTo - jwt.IssuedAt);
        Assert.Equal(T0.AddMinutes(5), issued.ExpiresAt);
        Assert.Equal("acc-1", jwt.Subject);
        Assert.Equal("staff", jwt.GetClaim("kind").Value);
        Assert.Equal(T0.AddMinutes(-1).ToUnixTimeSeconds(), jwt.GetPayloadValue<long>("auth_time"));
        Assert.Equal(["pwd", "mfa"], jwt.GetPayloadValue<string[]>("amr"));
        var perms = jwt.GetPayloadValue<Dictionary<string, string[]>>("perms");
        Assert.Equal(["inventory.adjustment.approve"], perms["wh:2"]);
        Assert.Equal("http://identity:8080", jwt.Issuer);
        Assert.Equal(["minibox"], jwt.Audiences);
        Assert.NotEqual(jwt.Id, new JsonWebToken(Issuer(ring).Issue(Request()).Token).Id);
    }

    [Fact(DisplayName = "Xoay khóa: khóa kế tiếp đã công bố trước khi dùng, khóa cũ còn một chu kỳ rồi bị gỡ")]
    public void keys_rotate_with_prepublished_next_and_retained_previous()
    {
        var period = TimeSpan.FromHours(24);
        var ring = new SigningKeyRing(_time, period);
        var first = ring.Current.KeyId;
        var published = ring.Published.Select(k => k.KeyId).ToList();
        Assert.Equal(2, published.Count);
        var next = published.Single(k => k != first);

        _time.Now += period;
        Assert.Equal(next, ring.Current.KeyId);
        Assert.Contains(first, ring.Published.Select(k => k.KeyId)); // token ký trước lúc xoay vẫn kiểm được

        _time.Now += period;
        Assert.DoesNotContain(first, ring.Published.Select(k => k.KeyId));
        Assert.Equal(3, ring.Published.Count);
    }

    [Fact(DisplayName = "JWKS chỉ có phần khóa công khai")]
    public void jwks_has_no_private_part()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new SigningKeyRing(_time, SigningKeyRing.DefaultPeriod).Jwks());
        Assert.Contains("\"kty\":\"EC\"", json);
        Assert.DoesNotContain("\"d\"", json);
    }
}
