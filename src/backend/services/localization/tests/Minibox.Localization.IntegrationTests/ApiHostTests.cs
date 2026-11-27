using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Minibox.BuildingBlocks.Application.Security;
using Minibox.Localization.Application;
using Minibox.Localization.Application.Features.Locales.Queries.ListAllLocales;
using Minibox.Localization.Application.Features.Messages.Commands.PublishMessages;
using Minibox.Localization.Application.Features.Messages.Queries.GetPublishedMessages;
using Minibox.Localization.Application.Features.Messages.Queries.ListMessages;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Application.Features.Locales.Queries.ListLocales;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine.Persistence.Durability;

namespace Minibox.Localization.IntegrationTests;

/// <summary>Api thật trên PostgreSQL + RabbitMQ tạm: --migrate, health, query qua pipeline Mediator, mapping EF và ràng buộc khớp DDL.</summary>
public sealed class ApiHostTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    WebApplicationFactory<Program> _factory = null!;

    // Khoá ký giả lập identity: Api kiểm token bằng khoá này thay vì lấy JWKS qua HTTP.
    const string Issuer = "http://identity.test";
    static readonly ECDsaSecurityKey SigningKey = new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role localization_app/localization_migrator.
    // search_path như role migrator thật để migration không ghi schema rơi vào localization.
    string Connection => _pg.GetConnectionString() + ";Search Path=localization";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        await using (var c = new NpgsqlConnection(_pg.GetConnectionString()))
        {
            await c.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("CREATE SCHEMA localization; CREATE ROLE localization_app", c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal(0, Migrate());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:App", Connection)
            .UseSetting("ConnectionStrings:RabbitMq", _mq.GetConnectionString())
            .UseSetting("Identity:Authority", Issuer)
            .ConfigureTestServices(s => s.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.Authority = null;
                o.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                o.TokenValidationParameters.ValidIssuer = Issuer;
                o.TokenValidationParameters.IssuerSigningKey = SigningKey;
            })));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    int Migrate()
    {
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, Connection);
        try
        {
            return (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!;
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
    }

    [Theory(DisplayName = "Api trả 200 ở /health/live và /health/ready khi PostgreSQL sẵn sàng")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task health_endpoints_ok(string path) =>
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync(path, Ct)).StatusCode);

    [Fact(DisplayName = "--migrate chạy lại lần hai vẫn thoát mã 0 (migration đã áp bỏ qua)")]
    public void migrate_is_rerunnable() => Assert.Equal(0, Migrate());

    [Fact(DisplayName = "Bảng Wolverine do migration 0004_wolverine.sql tạo khớp cấu hình Wolverine hiện tại")]
    public async Task wolverine_tables_match_configuration()
    {
        var store = (Weasel.Core.Migrations.IDatabase)_factory.Services.GetRequiredService<IMessageStore>();
        await store.AssertDatabaseMatchesConfigurationAsync(Ct);
    }

    [Fact(DisplayName = "GET /locales trả locale đang bật đã seed (vi rồi en, en rơi về vi), không cần đăng nhập")]
    public async Task seeded_locales_listed() =>
        Assert.Equal([new LocaleDto("vi", "Tiếng Việt", null), new LocaleDto("en", "English", "vi")],
            (await _factory.CreateClient().GetFromJsonAsync<LocaleDto[]>("/locales", Ct))!);

    [Fact(DisplayName = "Mapping EF khớp DDL: locale tắt không hiện, bật thì xếp theo sort_order")]
    public async Task ef_mapping_matches_migrated_schema()
    {
        await Exec("INSERT INTO locale (code, name, sort_order, fallback_code) VALUES ('ja', '日本語', -1, 'en')");
        await using (var scope = _factory.Services.CreateAsyncScope())
            Assert.Equal(["vi", "en"], (await scope.ServiceProvider.GetRequiredService<ILocaleReadService>().ListEnabledAsync(Ct)).Select(l => l.Code));

        await Exec("UPDATE locale SET enabled = true WHERE code = 'ja'");
        await using (var scope = _factory.Services.CreateAsyncScope())
            Assert.Equal([new LocaleDto("ja", "日本語", "en"), new LocaleDto("vi", "Tiếng Việt", null), new LocaleDto("en", "English", "vi")],
                await scope.ServiceProvider.GetRequiredService<ILocaleReadService>().ListEnabledAsync(Ct));
    }

    [Theory(DisplayName = "Ràng buộc DB chặn dữ liệu sai: mã locale/tiền tệ, fallback chính nó, tỷ giá VND/âm, chuỗi trùng hoặc xuất bản nửa vời")]
    [InlineData("INSERT INTO locale (code, name) VALUES ('VI_vn', 'x')")]
    [InlineData("INSERT INTO locale (code, name, fallback_code) VALUES ('ja', 'x', 'ja')")]
    [InlineData("INSERT INTO locale (code, name, fallback_code) VALUES ('ja', 'x', 'ko')")]
    [InlineData("INSERT INTO currency (code, name, symbol, minor_unit) VALUES ('usd', 'x', '$', 2)")]
    [InlineData("INSERT INTO exchange_rate (id, currency_code, vnd_per_unit, effective_at, actor) VALUES (gen_random_uuid(), 'VND', 1, now(), 's')")]
    [InlineData("INSERT INTO currency (code, name, symbol, minor_unit) VALUES ('USD', 'Đô la Mỹ', '$', 2); INSERT INTO exchange_rate (id, currency_code, vnd_per_unit, effective_at, actor) VALUES (gen_random_uuid(), 'USD', 0, now(), 's')")]
    [InlineData("INSERT INTO ui_message (id, namespace, key, locale_code, draft_text) VALUES (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'a'), (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'b')")]
    [InlineData("INSERT INTO ui_message (id, namespace, key, locale_code, draft_text, published_text) VALUES (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'a', 'a')")]
    [InlineData("INSERT INTO ui_message (id, namespace, key, locale_code, draft_text) VALUES (gen_random_uuid(), 'shop', 'cart.title', 'xx', 'a')")]
    public async Task constraints_reject_bad_rows(string sql) =>
        await Assert.ThrowsAsync<PostgresException>(() => Exec(sql));

    [Fact(DisplayName = "Seed: VND là tiền tệ gốc; một chuỗi giao diện mỗi (namespace, key, locale) ghi được cho mọi locale")]
    public async Task seed_and_valid_rows()
    {
        Assert.Equal(1L, await Scalar("SELECT count(*) FROM currency WHERE code = 'VND' AND enabled AND minor_unit = 0"));
        await Exec("""
            INSERT INTO ui_message (id, namespace, key, locale_code, draft_text, published_text, published_at) VALUES
              (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'Giỏ hàng', 'Giỏ hàng', now()),
              (gen_random_uuid(), 'shop', 'cart.title', 'en', 'Cart', NULL, NULL)
            """);
        Assert.Equal(2L, await Scalar("SELECT count(*) FROM ui_message WHERE key = 'cart.title'"));
    }

    async Task Exec(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    async Task<long> Scalar(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        return (long)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    // ---- API chuỗi giao diện và quản trị (#391) ----

    static string Token(params string[] perms) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = AccessTokenClaims.Audience,
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.EcdsaSha256),
        Claims = new Dictionary<string, object>
        {
            [AccessTokenClaims.Subject] = "staff-1",
            [AccessTokenClaims.Kind] = "staff",
            [AccessTokenClaims.Permissions] = JsonSerializer.SerializeToElement(new Dictionary<string, string[]> { ["*"] = perms }),
            [AccessTokenClaims.AuthTime] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            [AccessTokenClaims.AuthMethods] = new[] { "pwd", "mfa" },
        },
    });

    static readonly string Admin = Token(LocalizationPermissions.MessageManage, LocalizationPermissions.MessagePublish, LocalizationPermissions.LocaleManage);

    HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    async Task<HttpStatusCode> SaveDraft(string ns, string key, string locale, string text, long? version = null, string? token = null) =>
        (await Client(token ?? Admin).PutAsJsonAsync($"/admin/messages/{ns}/{key}/{locale}", new { text, version }, Ct)).StatusCode;

    async Task<int> Publish(string ns, string locale)
    {
        var response = await Client(Admin).PostAsync($"/admin/messages/{ns}/{locale}/publish", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PublishMessagesResult>(Ct))!.Published;
    }

    [Fact(DisplayName = "Bản nháp chưa công khai; xuất bản rồi GET công khai trả chuỗi theo locale, key thiếu rơi về vi")]
    public async Task draft_publish_and_fallback()
    {
        Assert.Equal(HttpStatusCode.NoContent, await SaveDraft("shop", "cart.title", "vi", "Giỏ hàng"));
        Assert.Equal(HttpStatusCode.NoContent, await SaveDraft("shop", "cart.empty", "vi", "Giỏ trống"));
        Assert.Equal(HttpStatusCode.NoContent, await SaveDraft("shop", "cart.title", "en", "Cart"));
        Assert.Empty((await Client().GetFromJsonAsync<PublishedMessagesDto>("/messages/en/shop", Ct))!.Messages);

        Assert.Equal(2, await Publish("shop", "vi"));
        Assert.Equal(1, await Publish("shop", "en"));
        Assert.Equal(0, await Publish("shop", "en"));

        var en = (await Client().GetFromJsonAsync<PublishedMessagesDto>("/messages/en/shop", Ct))!;
        Assert.Equal(new Dictionary<string, string> { ["cart.empty"] = "Giỏ trống", ["cart.title"] = "Cart" }, en.Messages);
        Assert.Equal(1L, await Scalar("SELECT count(*) FROM audit_log WHERE action = 'UiMessagePublished' AND payload->>'LocaleCode' = 'en'"));
    }

    [Fact(DisplayName = "GET công khai có ETag: If-None-Match trùng → 304; xuất bản mới đổi ETag")]
    public async Task etag_revalidation()
    {
        await SaveDraft("common", "ok", "vi", "Đồng ý");
        await Publish("common", "vi");
        var first = await Client().GetAsync("/messages/vi/common", Ct);
        var etag = first.Headers.ETag!;
        Assert.Equal("no-cache", first.Headers.CacheControl!.ToString());

        var again = new HttpRequestMessage(HttpMethod.Get, "/messages/vi/common");
        again.Headers.IfNoneMatch.Add(etag);
        Assert.Equal(HttpStatusCode.NotModified, (await Client().SendAsync(again, Ct)).StatusCode);

        var current = (await Client(Admin).GetFromJsonAsync<UiMessageDto[]>("/admin/messages?namespace=common&locale=vi", Ct))!.Single();
        Assert.Equal(HttpStatusCode.NoContent, await SaveDraft("common", "ok", "vi", "OK", current.Version));
        await Publish("common", "vi");
        Assert.NotEqual(etag, (await Client().GetAsync("/messages/vi/common", Ct)).Headers.ETag);
    }

    [Fact(DisplayName = "GET công khai: locale tắt hoặc không có → 404, namespace sai dạng → 400")]
    public async Task public_get_rejects_unknown()
    {
        await Exec("INSERT INTO locale (code, name) VALUES ('ja', '日本語')");
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync("/messages/ja/shop", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync("/messages/fr/shop", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client().GetAsync("/messages/vi/SHOP", Ct)).StatusCode);
    }

    [Fact(DisplayName = "API quản trị: chưa đăng nhập 401, thiếu quyền 403, sửa chuỗi đã có thiếu/cũ version 409, nội dung quá 10000 ký tự 400")]
    public async Task admin_api_guards()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/admin/messages?namespace=shop", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await SaveDraft("shop", "k", "vi", "x", token: Token(LocalizationPermissions.LocaleManage)));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Client(Token(LocalizationPermissions.MessageManage)).PostAsync("/admin/messages/shop/vi/publish", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, await SaveDraft("shop", "k", "vi", "x"));
        Assert.Equal(HttpStatusCode.Conflict, await SaveDraft("shop", "k", "vi", "y"));
        Assert.Equal(HttpStatusCode.Conflict, await SaveDraft("shop", "k", "vi", "y", version: 7));
        Assert.Equal(HttpStatusCode.NotFound, await SaveDraft("shop", "k2", "vi", "y", version: 0));
        Assert.Equal(HttpStatusCode.BadRequest, await SaveDraft("shop", "k3", "vi", new string('a', 10001)));
        Assert.Equal(HttpStatusCode.NotFound, await SaveDraft("shop", "k", "fr", "x"));
    }

    [Fact(DisplayName = "Quản trị locale: thêm (tắt), bật, đặt fallback; vòng fallback 400, tắt vi 422, sai version 409")]
    public async Task admin_locales()
    {
        var client = Client(Admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/admin/locales", new { code = "ja", name = "日本語", sortOrder = 2, fallbackCode = "en" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/admin/locales", new { code = "ja", name = "x", sortOrder = 2 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/admin/locales", new { code = "ko", name = "x", sortOrder = 3, fallbackCode = "fr" }, Ct)).StatusCode);

        var all = (await client.GetFromJsonAsync<LocaleAdminDto[]>("/admin/locales", Ct))!;
        var ja = all.Single(l => l.Code == "ja");
        var en = all.Single(l => l.Code == "en");
        Assert.False(ja.Enabled);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/admin/locales/ja", new { name = "日本語", enabled = true, sortOrder = 2, fallbackCode = "en", version = ja.Version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/admin/locales/ja", new { name = "日本語", enabled = true, sortOrder = 2, fallbackCode = "en", version = ja.Version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/admin/locales/en", new { name = "English", enabled = true, sortOrder = 1, fallbackCode = "ja", version = en.Version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PutAsJsonAsync("/admin/locales/vi", new { name = "Tiếng Việt", enabled = false, sortOrder = 0, version = 0 }, Ct)).StatusCode);
        Assert.Contains("ja", (await Client().GetFromJsonAsync<LocaleDto[]>("/locales", Ct))!.Select(l => l.Code));
        Assert.Equal(2L, await Scalar("SELECT count(*) FROM audit_log WHERE action = 'LocaleChanged' AND aggregate_id = 'ja'"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(Token(LocalizationPermissions.MessageManage)).GetAsync("/admin/locales", Ct)).StatusCode);
    }
}
