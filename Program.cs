using AspNetCoreHero.ToastNotification;
using AspNetCoreHero.ToastNotification.Extensions;
using BAL.Interfaces;
using BAL.Repository;
using DAL.DataContext;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Rotativa.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>();
builder.Services.AddScoped<IRequestRepo, RequestRepo>();
builder.Services.AddScoped<IJwtToken, JwtTokenServices>();
builder.Services.AddScoped<IEmailService, EmailServiceRepo>();
builder.Services.AddScoped<IResetPasswordService,ResetPasswordServiceRepo>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasherRepo>();
builder.Services.AddScoped<IFileOperations, FileOperationsRepo>();
builder.Services.AddScoped<ILogin, LoginRepo>();
builder.Services.AddScoped<IAgreement, AgreementRepo>();
builder.Services.AddScoped<IAdminTables,AdminTablesRepo>();
builder.Services.AddScoped<IAdminActions, AdminActionsRepo>();
builder.Services.AddScoped<IPatientDashboard,PatientDashboardRepo>();
builder.Services.AddScoped<IEncounterForm, EncounterFormRepo>();
builder.Services.AddScoped<IAdmin,AdminRepo>();

builder.Services.AddNotyf(config =>
{
    config.DurationInSeconds = 2; config.IsDismissable = false;
    config.HasRippleEffect = true;
    config.Position = NotyfPosition.TopRight;
});

//For Creating a session
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".HalloDoc.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(120); // Adjust as needed
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

//Jwt configuration starts here
var jwtIssuer = builder.Configuration.GetSection("Jwt:Issuer").Get<string>();
var jwtKey = builder.Configuration.GetSection("Jwt:Key").Get<string>();

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
         ValidAudience = jwtIssuer,
         IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
     };
 });
//Jwt configuration ends here   
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRotativa();
app.UseRouting();
app.UseSession();

app.UseAuthorization();
app.UseNotyf();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Guest}/{action=submit_request_page}/{id?}");

app.Run();
