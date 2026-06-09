using ARS;
using ARS.Classess;
using ARS.Classess.Utils;
using ARS.Data;
using ARS.Jobs;
using ARS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Quartz;


var builder = WebApplication.CreateBuilder(args);

//var generator = new MerchantGenerator();

//await generator.InsertMerchantsAsync(
//    connectionString: "Host=localhost;Port=5432;Database=merchants_mock;Username=postgres;Password=1",
//    totalRecords: 3_000_000,
//    batchSize: 10_000       // optional, defaults to 10 000
//);


builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    // Use in-memory store (swap to AdoJobStore for persistence across restarts)
    q.UseInMemoryStore();
    q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 5);
});

builder.Services.AddQuartzHostedService(q =>
{
    q.WaitForJobsToComplete = true;
});


builder.Services.AddScoped<ReportSchedulerService>();
builder.Services.AddHostedService<ReportSchedulerStartup>();



// ── Database ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Cookie Authentication ─────────────────────────────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/Login";
    options.Cookie.Name = "ARS.Portal";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(
        int.Parse(builder.Configuration["SessionExpiryMinutes"] ?? "480"));
    options.SlidingExpiration = true;
});

// ── Authorization ─────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Super Admin", "Admin"));
});

// Add services to the container.
builder.Services.AddControllersWithViews();



var app = builder.Build();
Startup.Initialize();

// ── Seed Super Admin on first run ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await GlobalFunctions.SeedSuperAdminAsync(db, config);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();
