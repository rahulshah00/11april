using DAL.DataModels;
using HalloDoc_Project.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using DAL.DataContext;
using DAL.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Web;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using System.Net.Cache;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Net.Mail;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BAL.Interfaces;
namespace HalloDoc_Project.Controllers
{
    [CustomAuthorize("Patient")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _config;
        private readonly IRequestRepo _patient_Request;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IFileOperations _insertfiles;
        private readonly IPatientDashboard _patientDashboard;
        public HomeController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration config, IRequestRepo request, IPasswordHasher passwordHasher,IFileOperations insertfiles, IPatientDashboard patientDashboard)
        {
            _context = context;
            _environment = environment;
            _config = config;
            _patient_Request = request;
            _passwordHasher = passwordHasher;
            _insertfiles = insertfiles;
            _patientDashboard = patientDashboard;
        }
        //DownloadAllFiles, Logout and error method are not converted to three tier.
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult patient_submit_request_screen()
        {
            return View();
        }
        public IActionResult SelectedDownload()
        {
            return View();
        }
        
        [HttpPost]
        public JsonResult CheckEmail(string email)
        {
            bool emailExists = _context.Users.Any(u => u.Email == email);
            return Json(new { exists = emailExists });
        }
        public IActionResult PatientProfile()
        {
            var email = HttpContext.Session.GetString("Email");
            PatientProfileViewModel ppm= _patientDashboard.PatientProfile(email);
            return View(ppm);
        }
        public IActionResult forgot_password_page()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult editprofile(PatientProfileViewModel ppm)
        {
            //string phoneNumber = "+" + /*pm.CountryCode*/ + '-' + pm.Phone;
            var email=HttpContext.Session.GetString("Email");
            _patientDashboard.EditProfile(ppm, email);
            
            return RedirectToAction("PatientProfile");
        }
        public IActionResult ViewDocuments(int requestid)
        {
            string? email = HttpContext.Session.GetString("Email");
            ViewDocumentsViewModel vm = _patientDashboard.ViewPatientDocsGet(requestid,email);
            return View(vm);
        }
        [HttpPost]
        public IActionResult ViewDocuments(ViewDocumentsViewModel vm)
        {
            string path = _environment.WebRootPath;
            vm = _patientDashboard.ViewPatientDocsPost(vm, path);
            return ViewDocuments(vm.RequestID);
        }
        public IActionResult PatientDashboard()
        {
            var email = HttpContext.Session.GetString("Email");
            PatientDashboardViewModel pd= _patientDashboard.PatientDashboard(email);

            return View(pd);
        }
        public async Task<IActionResult> DownloadAllFiles(int requestId)
        {
            try
            {
                // Fetch all document details for the given request:
                var documentDetails = _context.Requestwisefiles.Where(m => m.Requestid == requestId).ToList();

                if (documentDetails == null || documentDetails.Count == 0)
                {
                    return NotFound("No documents found for download");
                }

                // Create a unique zip file name
                var zipFileName = $"Documents_{DateTime.Now:yyyyMMddHHmmss}.zip";
                var zipFilePath = Path.Combine(_environment.WebRootPath, "DownloadableZips", zipFileName);

                // Create the directory if it doesn't exist
                var zipDirectory = Path.GetDirectoryName(zipFilePath);
                if (!Directory.Exists(zipDirectory))
                {
                    Directory.CreateDirectory(zipDirectory);
                }

                // Create a new zip archive
                using (var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    // Add each document to the zip archive
                    foreach (var document in documentDetails)
                    {
                        var documentPath = Path.Combine(_environment.WebRootPath, "Content", document.Filename);
                        zipArchive.CreateEntryFromFile(documentPath, document.Filename);
                    }
                }

                // Return the zip file for download
                var zipFileBytes = await System.IO.File.ReadAllBytesAsync(zipFilePath);
                return File(zipFileBytes, "application/zip", zipFileName);
            }
            catch
            {
                return BadRequest("Error downloading files");
            }
        }
        public IActionResult logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("login_page", "Guest");
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}