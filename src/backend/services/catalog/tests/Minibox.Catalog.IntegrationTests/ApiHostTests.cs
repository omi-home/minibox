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
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Application.Security;
using Minibox.BuildingBlocks.Infrastructure.Localization;
using Minibox.Catalog.Application;
using Minibox.Catalog.Application.Features.Categories.Queries.GetCategoryTree;
using Minibox.Catalog.Application.Features.Categories.Queries.ListCategoryTree;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.Taxa.Queries.GetTaxa;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.RabbitMQ;

namespace Minibox.Catalog.IntegrationTests;

/// <summary>Api thật trên PostgreSQL + RabbitMQ tạm: --migrate, health, bảng Wolverine khớp cấu hình, bản sao locale.</summary>
public sealed class ApiHostTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    WebApplicationFactory<Program> _factory = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role catalog_app/catalog_migrator.
    // search_path như role migrator thật để migration không ghi schema rơi vào catalog.
    string Connection => _pg.GetConnectionString() + ";Search Path=catalog";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        await using (var c = new NpgsqlConnection(_pg.GetConnectionString()))
        {
            await c.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("CREATE SCHEMA catalog; CREATE ROLE catalog_app", c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal(0, Migrate());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:App", Connection)
            .UseSetting("ConnectionStrings:RabbitMq", _mq.GetConnectionString())
            .UseSetting("Identity:Authority", Issuer)
            // Thay localization phát sự kiện: cùng exchange fanout mà localization dùng thật.
            .ConfigureServices(s => s.ConfigureWolverine(o => o.PublishMessage<LocaleChangedMessage>()
                .ToRabbitExchange(LocaleReplicaSetup.LocaleChangedExchange, e => e.ExchangeType = ExchangeType.Fanout)))
            .ConfigureTestServices(s => s.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.Authority = null;
                o.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                o.TokenValidationParameters.ValidIssuer = Issuer;
                o.TokenValidationParameters.IssuerSigningKey = SigningKey;
            })));
    }

    // Khoá ký giả lập identity: Api kiểm token bằng khoá này thay vì lấy JWKS qua HTTP.
    const string Issuer = "http://identity.test";
    static readonly ECDsaSecurityKey SigningKey = new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

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

    [Fact(DisplayName = "Bảng Wolverine do migration 0003_wolverine.sql tạo khớp cấu hình Wolverine hiện tại")]
    public async Task wolverine_tables_match_configuration()
    {
        var store = (Weasel.Core.Migrations.IDatabase)_factory.Services.GetRequiredService<IMessageStore>();
        await store.AssertDatabaseMatchesConfigurationAsync(Ct);
    }

    async Task<IReadOnlyList<string>> Chain(string locale)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILocaleReplica>().ChainAsync(locale, Ct);
    }

    [Fact(DisplayName = "#483: catalog có bản sao locale seed vi/en, cập nhật khi nhận localization.LocaleChanged.v1")]
    public async Task locale_replica_follows_locale_changed()
    {
        Assert.Equal(["en", "vi"], await Chain("en"));
        Assert.Equal(["vi"], await Chain("ja"));

        await using (var scope = _factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new LocaleChangedMessage("ja", "日本語", true, 2, "en"));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        IReadOnlyList<string> chain;
        while ((chain = await Chain("ja")).Count == 1 && DateTime.UtcNow < deadline)
            await Task.Delay(200, Ct);
        Assert.Equal(["ja", "en", "vi"], chain);
    }

    // ---- Danh mục (#484) ----

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

    static readonly string Manager = Token(CatalogPermissions.CategoryManage);

    HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    static object[] Texts(string vi, string? en = null) => en is null
        ? [new { locale = "vi", name = vi, slug = vi }]
        : [new { locale = "vi", name = vi, slug = vi }, new { locale = "en", name = en, description = "EN", slug = en }];

    async Task<Guid> Create(string vi, Guid? parentId = null, string? en = null)
    {
        var response = await Client(Manager).PostAsJsonAsync("/admin/categories", new { parentId, sortOrder = 0, translations = Texts(vi, en) }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    async Task<CategoryAdminNode> Admin(Guid id)
    {
        var tree = (await Client(Manager).GetFromJsonAsync<CategoryAdminNode[]>("/admin/categories", Ct))!;
        static IEnumerable<CategoryAdminNode> All(IEnumerable<CategoryAdminNode> nodes) => nodes.SelectMany(n => All(n.Children).Prepend(n));
        return All(tree).Single(n => n.Id == id);
    }

    async Task<HttpStatusCode> Move(Guid id, Guid? parentId, string? token = null) =>
        (await Client(token ?? Manager).PutAsJsonAsync($"/admin/categories/{id}/parent", new { parentId, version = (await Admin(id)).Version }, Ct)).StatusCode;

    [Fact(DisplayName = "ac236_1: danh mục lồng nhiều cấp qua API; chuyển vào chính nó hoặc vào cháu bị 400, cây không đổi")]
    public async Task ac236_1_nested_categories_reject_cycle()
    {
        var root = await Create("c1-goc");
        var child = await Create("c1-con", root);
        var grandchild = await Create("c1-chau", child);

        Assert.Equal(HttpStatusCode.BadRequest, await Move(root, grandchild));
        Assert.Equal(HttpStatusCode.BadRequest, await Move(root, root));
        Assert.Null((await Admin(root)).ParentId);
        Assert.Equal(child, Assert.Single((await Admin(root)).Children).Id);

        Assert.Equal(HttpStatusCode.NoContent, await Move(grandchild, root));
        Assert.Equal(2, (await Admin(root)).Children.Length);
        Assert.Equal(HttpStatusCode.NotFound, await Move(child, Guid.CreateVersion7()));
        Assert.Equal(1L, await Scalar($"SELECT count(*) FROM audit_log WHERE action = 'CategoryMoved' AND aggregate_id = '{grandchild}'"));
    }

    [Fact(DisplayName = "ac236_4: chưa đăng nhập 401, thiếu quyền catalog.category.manage 403 cho mọi thao tác ghi và cây quản trị")]
    public async Task ac236_4_category_writes_require_permission()
    {
        var id = await Create("c4-goc");
        var other = Token(CatalogPermissions.ProductManage);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().PostAsJsonAsync("/admin/categories", new { sortOrder = 0, translations = Texts("c4-x") }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).PostAsJsonAsync("/admin/categories", new { sortOrder = 0, translations = Texts("c4-x") }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).PutAsJsonAsync($"/admin/categories/{id}", new { sortOrder = 0, translations = Texts("c4-y"), version = 0 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await Move(id, null, other));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).DeleteAsync($"/admin/categories/{id}?version=0", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).GetAsync("/admin/categories", Ct)).StatusCode);
        Assert.Equal(0L, await Scalar("SELECT count(*) FROM category_translation WHERE slug IN ('c4-x', 'c4-y')"));
    }

    [Fact(DisplayName = "Đổi slug lưu lịch sử; slug trùng cùng locale 409; sai version 409; cây công khai theo locale có fallback vi; xóa mềm ẩn khỏi cây công khai, còn con thì 422")]
    public async Task slug_history_public_tree_and_archive()
    {
        var root = await Create("c5-den", en: "c5-lamps");
        var child = await Create("c5-den-ngu", root);
        var client = Client(Manager);
        var version = (await Admin(root)).Version;

        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/admin/categories/{root}", new { sortOrder = 0, translations = Texts("c5-den-ngu"), version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/admin/categories/{root}", new { sortOrder = 0, translations = Texts("c5-den-moi", "c5-lamps"), version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/admin/categories/{root}", new { sortOrder = 0, translations = Texts("c5-den-x"), version }, Ct)).StatusCode);
        Assert.Equal(1L, await Scalar($"SELECT count(*) FROM category_slug_history WHERE category_id = '{root}' AND locale = 'vi' AND slug = 'c5-den'"));

        var en = (await Client().GetFromJsonAsync<CategoryTreeNode[]>("/categories?locale=en", Ct))!.Single(n => n.Id == root);
        Assert.Equal(("en", "c5-lamps", "EN"), (en.Locale, en.Slug, en.Description));
        Assert.Equal(("vi", "c5-den-ngu"), (Assert.Single(en.Children).Locale, en.Children[0].Slug));
        Assert.Equal(HttpStatusCode.BadRequest, (await Client().GetAsync("/categories?locale=EN!", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.DeleteAsync($"/admin/categories/{root}?version={(await Admin(root)).Version}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/admin/categories/{child}?version={(await Admin(child)).Version}", Ct)).StatusCode);
        Assert.Empty((await Client().GetFromJsonAsync<CategoryTreeNode[]>("/categories?locale=vi", Ct))!.Single(n => n.Id == root).Children);
        Assert.True((await Admin(child)).Archived);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/admin/categories", new { parentId = child, sortOrder = 0, translations = Texts("c5-moi") }, Ct)).StatusCode);
    }

    // ---- Bộ sưu tập và thẻ (#485) ----

    static readonly string Curator = Token(CatalogPermissions.CollectionManage);

    async Task<Guid> CreateTaxon(string path, string vi, string? en = null)
    {
        var response = await Client(Curator).PostAsJsonAsync($"/admin/{path}", new { sortOrder = 0, translations = Texts(vi, en) }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<Guid>(Ct);
    }

    async Task<HttpStatusCode> SetTaxa(Guid product, string path, params Guid[] ids) =>
        (await Client(Curator).PutAsJsonAsync($"/admin/products/{product}/{path}", new { ids }, Ct)).StatusCode;

    async Task<long> TaxonVersion(string path, Guid id) =>
        (await Client(Curator).GetFromJsonAsync<TaxonAdminRow[]>($"/admin/{path}", Ct))!.Single(r => r.Id == id).Version;

    [Fact(DisplayName = "ac236_2: một sản phẩm thuộc nhiều bộ sưu tập và nhiều thẻ; đổi bộ sưu tập không đụng thẻ; danh mục độc lập")]
    public async Task ac236_2_product_in_many_collections_and_tags()
    {
        var product = Guid.CreateVersion7();
        var (a, b) = (await CreateTaxon("collections", "t2-qua-tang"), await CreateTaxon("collections", "t2-ban-chay"));
        var (x, y) = (await CreateTaxon("tags", "t2-de-cham"), await CreateTaxon("tags", "t2-trong-nha"));
        var category = await Create("t2-den");

        Assert.Equal(HttpStatusCode.NoContent, await SetTaxa(product, "collections", a, b));
        Assert.Equal(HttpStatusCode.NoContent, await SetTaxa(product, "tags", x, y, y));
        Assert.Equal(4L, await Scalar($"SELECT count(*) FROM product_taxon WHERE product_id = '{product}'"));

        Assert.Equal(HttpStatusCode.NoContent, await SetTaxa(product, "collections", b));
        Assert.Equal(3L, await Scalar($"SELECT count(*) FROM product_taxon WHERE product_id = '{product}'"));
        Assert.Equal(1L, await Scalar($"SELECT count(*) FROM product_taxon WHERE product_id = '{product}' AND taxon_id = '{b}'"));

        // Thẻ không gắn vào chỗ bộ sưu tập, Id danh mục không phải taxon, taxon đã xóa không gắn được: 404, dữ liệu không đổi.
        Assert.Equal(HttpStatusCode.NotFound, await SetTaxa(product, "collections", x));
        Assert.Equal(HttpStatusCode.NotFound, await SetTaxa(product, "collections", category));
        Assert.Equal(HttpStatusCode.NoContent, (await Client(Curator).DeleteAsync($"/admin/collections/{a}?version={await TaxonVersion("collections", a)}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, await SetTaxa(product, "collections", a, b));
        Assert.Equal(3L, await Scalar($"SELECT count(*) FROM product_taxon WHERE product_id = '{product}'"));

        Assert.Equal(HttpStatusCode.NoContent, await SetTaxa(product, "tags"));
        Assert.Equal(1L, await Scalar($"SELECT count(*) FROM product_taxon WHERE product_id = '{product}'"));
    }

    [Fact(DisplayName = "ac236_4: thiếu quyền catalog.collection.manage 403 cho mọi thao tác ghi bộ sưu tập/thẻ và danh sách quản trị; 401 khi chưa đăng nhập")]
    public async Task ac236_4_taxon_writes_require_permission()
    {
        var id = await CreateTaxon("collections", "t4-goc");
        var other = Manager; // có catalog.category.manage nhưng không có catalog.collection.manage
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().PostAsJsonAsync("/admin/tags", new { sortOrder = 0, translations = Texts("t4-x") }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).PostAsJsonAsync("/admin/tags", new { sortOrder = 0, translations = Texts("t4-x") }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).PutAsJsonAsync($"/admin/collections/{id}", new { sortOrder = 0, translations = Texts("t4-y"), version = 0 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).DeleteAsync($"/admin/collections/{id}?version=0", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).PutAsJsonAsync($"/admin/products/{Guid.CreateVersion7()}/collections", new { ids = new[] { id } }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client(other).GetAsync("/admin/tags", Ct)).StatusCode);
        Assert.Equal(0L, await Scalar("SELECT count(*) FROM taxon_translation WHERE slug IN ('t4-x', 't4-y')"));
    }

    [Fact(DisplayName = "Bộ sưu tập: đổi slug lưu lịch sử, slug trùng cùng locale 409 (kể cả với thẻ), sai loại 404; danh sách công khai theo locale có fallback vi, ẩn đã xóa")]
    public async Task taxon_slug_history_and_public_list()
    {
        var id = await CreateTaxon("collections", "t5-qua", "t5-gifts");
        var tag = await CreateTaxon("tags", "t5-the");
        var client = Client(Curator);
        var version = await TaxonVersion("collections", id);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/admin/collections/{id}", new { sortOrder = 0, translations = Texts("t5-the"), version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/admin/tags/{id}", new { sortOrder = 0, translations = Texts("t5-z"), version }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/admin/collections/{id}", new { sortOrder = 0, translations = Texts("t5-qua-moi", "t5-gifts"), version }, Ct)).StatusCode);
        Assert.Equal(1L, await Scalar($"SELECT count(*) FROM taxon_slug_history WHERE taxon_id = '{id}' AND locale = 'vi' AND slug = 't5-qua'"));
        Assert.Equal(1L, await Scalar($"SELECT count(*) FROM audit_log WHERE action = 'TaxonUpdated' AND aggregate_id = '{id}'"));

        var en = (await Client().GetFromJsonAsync<TaxonView[]>("/collections?locale=en", Ct))!.Single(v => v.Id == id);
        Assert.Equal(("en", "t5-gifts", "EN"), (en.Locale, en.Slug, en.Description));
        var tagEn = (await Client().GetFromJsonAsync<TaxonView[]>("/tags?locale=en", Ct))!.Single(v => v.Id == tag);
        Assert.Equal(("vi", "t5-the"), (tagEn.Locale, tagEn.Slug));
        Assert.DoesNotContain((await Client().GetFromJsonAsync<TaxonView[]>("/tags", Ct))!, v => v.Id == id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/admin/tags/{tag}?version={await TaxonVersion("tags", tag)}", Ct)).StatusCode);
        Assert.DoesNotContain((await Client().GetFromJsonAsync<TaxonView[]>("/tags", Ct))!, v => v.Id == tag);
    }

    async Task<long> Scalar(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        return (long)(await cmd.ExecuteScalarAsync(Ct))!;
    }
}
