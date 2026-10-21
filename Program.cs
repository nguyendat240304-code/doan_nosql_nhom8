using doan_cuoiky_nosql.Models;
using doan_cuoiky_nosql.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using QuestPDF.Infrastructure;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.FontDiscoveryPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "fonts");

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.WriteIndented = false;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddMemoryCache();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var username = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Principal?.Identity?.Name;
                var tokenRole = context.Principal?.FindFirstValue(ClaimTypes.Role);

                if (string.IsNullOrEmpty(username))
                {
                    context.Fail("TÃ i khoáº£n bá»‹ vÃ´ hiá»‡u hÃ³a");
                    return;
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
                var cacheKey = $"user_active_{username}";

                if (!cache.TryGetValue(cacheKey, out TaiKhoan? dbUser))
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<MongoDbService>();
                    dbUser = await db.TaiKhoans.Find(x => x.Id == username).FirstOrDefaultAsync();

                    if (dbUser != null)
                    {
                        cache.Set(cacheKey, dbUser, TimeSpan.FromSeconds(30));
                    }
                }

                if (dbUser == null || !dbUser.IsActive)
                {
                    context.Fail("TÃ i khoáº£n bá»‹ vÃ´ hiá»‡u hÃ³a");
                    return;
                }

                if (!string.Equals(dbUser.Role, tokenRole, StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("TÃ i khoáº£n bá»‹ vÃ´ hiá»‡u hÃ³a");
                    return;
                }
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();



app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

// Äá»“ng bá»™ giáº£ng viÃªn trong BangDiem khi server khá»Ÿi Ä‘á»™ng
await app.Services.GetRequiredService<MongoDbService>().InitializeIndexesAndEnrollmentCountsAsync();
await SyncGiangVienInBangDiem(app.Services);

app.Run();

// ======= HÃ€M Äá»’NG Bá»˜ GIáº¢NG VIÃŠN =======
static async Task SyncGiangVienInBangDiem(IServiceProvider services)
{
    try
    {
        var db = services.GetRequiredService<MongoDbService>();
        var allLhp = await db.LopHocPhans.Find(_ => true).ToListAsync();
        int updated = 0;

        foreach (var lhp in allLhp.Where(l => l.GiangVien != null))
        {
            var svFilter = Builders<SinhVien>.Filter
                .ElemMatch(x => x.BangDiem, b => b.MaLHP == lhp.Id);

            var svUpdate = Builders<SinhVien>.Update
                .Set("BangDiem.$[elem].GiangVien.MaGV", lhp.GiangVien!.MaGV)
                .Set("BangDiem.$[elem].GiangVien.HoTen", lhp.GiangVien!.HoTen);

            var arrayFilters = new List<ArrayFilterDefinition>
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("elem.MaLHP", lhp.Id))
            };

            var result = await db.SinhViens.UpdateManyAsync(
                svFilter, svUpdate,
                new UpdateOptions { ArrayFilters = arrayFilters });

            if (result.ModifiedCount > 0) updated++;
        }

        Console.WriteLine($"[SYNC] Äá»“ng bá»™ giáº£ng viÃªn hoÃ n táº¥t: {updated}/{allLhp.Count} lá»›p há»c pháº§n Ä‘Ã£ cáº­p nháº­t.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SYNC] Lá»—i Ä‘á»“ng bá»™ giáº£ng viÃªn: {ex.Message}");
    }
}
