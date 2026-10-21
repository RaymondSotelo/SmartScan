using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using SmartScan.Services;
using SmartScan.Data;
using System.Diagnostics;
using System.IO;
using SmartScan.Hubs;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<SmartScan.Services.ApplicationUserClaimsPrincipalFactory>();

builder.Services.AddControllersWithViews();
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Service Configuration
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Check security stamp immediately rather than the default 30-minute delay
    options.ValidationInterval = TimeSpan.Zero;
});

builder.Services.AddSignalR();

// === ADDED YOLO SCANNER SERVICE ===
builder.Services.AddSingleton<SmartScan.Services.YoloScannerService>();
// ==================================

var app = builder.Build();

<<<<<<< HEAD

// AUTOMATIC LOCAL YOLO SCANNER LAUNCHER
// ==========================================
if (app.Environment.IsDevelopment())
{
    try
    {
        var projectRoot = app.Environment.ContentRootPath;
        var yoloFolder = Path.Combine(projectRoot, "PythonScanner");
        var scriptPath = Path.Combine(yoloFolder, "live_scan.py");

        Console.WriteLine($"[SmartScan Debug] Looking for script at: {scriptPath}");

        if (!File.Exists(scriptPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[SmartScan WARNING] Script NOT FOUND at: {scriptPath}");
            Console.ResetColor();
        }
        else
        {
            var condaPath = app.Configuration["PythonScanner:CondaActivatePath"];
            Console.WriteLine($"[SmartScan Debug] Conda path from JSON: {condaPath}");

            string commandArgs;
            if (!string.IsNullOrEmpty(condaPath) && File.Exists(condaPath))
            {
                commandArgs = $"/k CALL \"{condaPath}\" && cd /d \"{yoloFolder}\" && python live_scan.py";
            }
            else
            {
                Console.WriteLine("[SmartScan Warning] Conda path invalid or not found on disk. Falling back to system python.");
                commandArgs = $"/k cd /d \"{yoloFolder}\" && python live_scan.py";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = commandArgs,
                UseShellExecute = true,       // Required to pop up a visible window
                CreateNoWindow = false,      // Must be false to show the window
                WorkingDirectory = yoloFolder
            };

            var pythonProcess = Process.Start(startInfo);

            if (pythonProcess != null)
            {
                Console.WriteLine($"[SmartScan SUCCESS] Process launched with PID: {pythonProcess.Id}");

                app.Lifetime.ApplicationStopping.Register(() =>
                {
                    try
                    {
                        if (!pythonProcess.HasExited)
                        {
                            Console.WriteLine("[SmartScan] Stopping YOLO Scanner process tree...");
                            // Kills cmd.exe, activate.bat, python.exe, and the Flask server
                            pythonProcess.Kill(entireProcessTree: true);
                            pythonProcess.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SmartScan] Error terminating Python process: {ex.Message}");
                    }
                });
            }
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[SmartScan ERROR] Auto-launch crashed: " + ex.ToString());
        Console.ResetColor();
    }
}
else
{
    Console.WriteLine("[SmartScan Debug] Skipped: app.Environment.IsDevelopment() is false.");
}
// -------------------------------------

=======
>>>>>>> 816fe500a7d84af274cdf015cb0a4f2ba32ec339
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapHub<UserSessionHub>("/hubs/userSession");

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

QuestPDF.Settings.License = LicenseType.Community;

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

    string[] roles = { "Admin", "Cashier" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    var adminEmail = "admin@test.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}

app.Run();