using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Globomantics.Survey.Data;
using Microsoft.AspNetCore.Identity.UI.Services;
using Globomantics.Survey.Services;
using Globomantics.Survey.Areas.Identity.Data;
using Microsoft.Net.Http.Headers;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("IdentityDbContextConnection") ?? throw new InvalidOperationException("Connection string 'IdentityDbContextConnection' not found.");

// Add services to the container.
builder.Services.AddControllersWithViews();

if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddSecretsManager(
        configurator:  options =>
        {
            options.SecretFilter = entry => 
                entry.Name.StartsWith($"{builder.Environment.ApplicationName}");
            options.KeyGenerator = (_, s) => s
                .Replace($"{builder.Environment.ApplicationName}.", string.Empty)
                .Replace("__", ":");
        }
    );
}

builder.Services.AddDbContext<GlobomanticsSurveyDbContext>(
    dbContextoptions => 
    {
        dbContextoptions.UseSqlite(builder.Configuration["ConnectionStrings:SurveyDbConnectionString"]);
        dbContextoptions.EnableSensitiveDataLogging();
    }
    );

builder.Services.AddDbContext<IdentityDbContext>(
    dbContextoptions => 
    {
        dbContextoptions.UseSqlite(builder.Configuration["ConnectionStrings:IdentityDbContextConnection"]);
        dbContextoptions.EnableSensitiveDataLogging();
    }
    );

builder.Services.AddDefaultIdentity<GloboSurveyUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddRoleManager<RoleManager<IdentityRole>>()
    .AddEntityFrameworkStores<IdentityDbContext>();

builder.Services.ConfigureApplicationCookie(options => 
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.Name = "__Host-Identity";
    options.Cookie.MaxAge = TimeSpan.FromHours(12);
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
});

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
});

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<GlobomanticsApiService>();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.Name = "__Host-Session";
    options.Cookie.MaxAge = TimeSpan.FromHours(1);
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});

builder.Services.AddAuthorization(options =>
{
   options.AddPolicy("SuperAdmin", 
    policy => policy.RequireClaim("IsManager", "true")
    );
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });

    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = (int)HttpStatusCode.PermanentRedirect;
        options.HttpsPort = 443;
    });
}
else
{
    builder.Services.AddHsts(options =>
    {
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromMinutes(1);
    }); 

    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = (int)HttpStatusCode.TemporaryRedirect;
        options.HttpsPort = 7236;
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHsts();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.Use(async (context, next) =>
{
    context.Response.GetTypedHeaders().CacheControl =
        new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
        {
            NoCache = true,
            NoStore = true
        };
    context.Response.Headers[HeaderNames.Pragma] = "no-cache";
    context.Response.Headers[HeaderNames.Expires] = "0";
    await next();
});

app.UseAuthentication();;
app.UseAuthorization();

app.UseSession();

app.MapAreaControllerRoute(
    name: "Admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
