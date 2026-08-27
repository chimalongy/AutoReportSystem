//using ARS;
//using ARS.Classess;
//using ARS.Classess.Utils;
//using ARS.Data;
//using ARS.Jobs;
//using ARS.Services;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.EntityFrameworkCore;
//using Npgsql;
//using Quartz;

//var builder = WebApplication.CreateBuilder(args);

//// Initialize EmailSender with config

//EmailSender.Initialize(builder.Configuration);

//// Configure Kestrel port from appsettings.json
//var port = builder.Configuration.GetValue<int>("ServerSettings:Port");

//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.ListenAnyIP(port);
//});

////var generator = new MerchantGenerator();

////await generator.InsertMerchantsAsync(
////    connectionString: "Host=localhost;Port=5432;Database=merchants_mock;Username=postgres;Password=1",
////    totalRecords: 3_000_000,
////    batchSize: 10_000
////);

//builder.Services.AddQuartz(q =>
//{
//    q.UseMicrosoftDependencyInjectionJobFactory();

//    // Use in-memory store (swap to AdoJobStore for persistence across restarts)
//    q.UseInMemoryStore();
//    q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 10);
//});

//builder.Services.AddQuartzHostedService(q =>
//{
//    q.WaitForJobsToComplete = true;
//});

//builder.Services.AddScoped<ReportSchedulerService>();
//builder.Services.AddScoped<DownloadTokenStore>();
//builder.Services.AddScoped<DownloadLinkGenerator>();
//builder.Services.AddHostedService<ReportSchedulerStartup>();




//// ── Database ──────────────────────────────────────────────────────────────
//var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//// 1. Create and configure the NpgsqlDataSourceBuilder
//var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

//// Optional: Register specific enums or custom mappings here if needed in the future
//// dataSourceBuilder.MapEnum<YourEnum>("your_enum_name");

//// 2. Build the NpgsqlDataSource
//var dataSource = dataSourceBuilder.Build();

//// 3. Register the DataSource with DI (optional but recommended for non-EF database calls)
//builder.Services.AddSingleton(dataSource);

//// 4. Pass the configured dataSource directly into EF Core
//builder.Services.AddDbContext<AppDbContext>(options =>
//{
//    options.UseNpgsql(
//        dataSource,
//        npgsqlOptions =>
//        {
//            npgsqlOptions.CommandTimeout(300);
//            // Add automatic retries for transient connection drops
//            npgsqlOptions.EnableRetryOnFailure(
//                maxRetryCount: 5,
//                maxRetryDelay: TimeSpan.FromSeconds(10),
//                errorCodesToAdd: null);
//        });

//    options.EnableDetailedErrors();
//    options.EnableSensitiveDataLogging();
//});

//// ── Cookie Authentication ─────────────────────────────────────────────────
//builder.Services.AddAuthentication(options =>
//{
//    options.DefaultAuthenticateScheme =
//        CookieAuthenticationDefaults.AuthenticationScheme;

//    options.DefaultChallengeScheme =
//        CookieAuthenticationDefaults.AuthenticationScheme;

//    options.DefaultSignInScheme =
//        CookieAuthenticationDefaults.AuthenticationScheme;
//})
//.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
//{
//    options.LoginPath = "/Auth/Login";
//    options.LogoutPath = "/Auth/Logout";
//    options.AccessDeniedPath = "/Auth/Login";

//    options.Cookie.Name = "ARS.Portal";
//    options.Cookie.HttpOnly = true;
//    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

//    options.ExpireTimeSpan = TimeSpan.FromMinutes(
//        int.Parse(
//            builder.Configuration["SessionExpiryMinutes"] ?? "480"
//        ));

//    options.SlidingExpiration = true;
//});

//// ── Authorization ─────────────────────────────────────────────────────────
//builder.Services.AddAuthorization(options =>
//{
//    options.AddPolicy("AdminOnly", policy =>
//        policy.RequireRole("Super Admin", "Admin"));
//});

//// Add services to the container.
//builder.Services.AddControllersWithViews();

//var app = builder.Build();

//Startup.Initialize();

//// ── Seed Super Admin on first run ─────────────────────────────────────────
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

//    await GlobalFunctions.SeedSuperAdminAsync(db, config);
//}

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    app.UseHsts();
//}

////app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();

//app.UseAuthentication();
//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Auth}/{action=Login}/{id?}");

//app.Run();