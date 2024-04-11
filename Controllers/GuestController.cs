using BAL.Interfaces;
using DAL.DataContext;
using DAL.DataModels;
using DAL.ViewModels;
using Microsoft.AspNetCore.Mvc;
using BAL.Repository;
using AspNetCoreHero.ToastNotification.Abstractions;

namespace HalloDoc_Project.Controllers
{
    public class GuestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _config;
        private readonly IRequestRepo _patient_Request;
        private readonly IJwtToken _jwtToken;
        private readonly IResetPasswordService _resetPasswordService;
        private readonly IEmailService _emailService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IAgreement _agreement;
        private readonly INotyfService _notyf;
        public GuestController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration config, IRequestRepo request, IJwtToken token, IResetPasswordService resetPasswordService, IEmailService emailService, IPasswordHasher passwordHasher, IAgreement agreement, INotyfService notyf)
        {
            _context = context;
            _environment = environment;
            _config = config;
            _patient_Request = request;
            _jwtToken = token;
            _resetPasswordService = resetPasswordService;
            _emailService = emailService;
            _passwordHasher = passwordHasher;
            _agreement = agreement;
            _notyf = notyf;
        }
        //Only ResetPassword is not taken into the three tier architecture otherwise everything from GuestController is in three tier architecture.
        public IActionResult Agree(int Requestid)
        {
            _agreement.AgreementAccepted(Requestid);
            return RedirectToAction("login_page", "Guest");
        }
        public IActionResult CancelAgreement(int Requestid, string Notes)
        {
            _agreement.AgreementRejected(Requestid, Notes);
            return RedirectToAction("login_page", "Guest");
        }
        public IActionResult ReviewAgreement(int ReqId)
        {
            var user = _context.Requestclients.FirstOrDefault(x => x.Requestid == ReqId);
            if (user != null)
            {
                ReviewAgreementViewModel reviewmodel = new()
                {
                    reqID = ReqId,
                    PatientName = user.Firstname + " " + user.Lastname
                };
                return View(reviewmodel);
            }
            return RedirectToAction("submit_request_page");
        }
        [HttpPost]
        public JsonResult CheckEmail(string email)
        {
            bool emailExists = _context.Users.Any(u => u.Email == email);
            return Json(new { exists = emailExists });
        }

        #region GUEST VIEWS
        public IActionResult PrivacyPolicy()
        {
            return View();
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult AccessDenied()
        {
            return View();
        }
        public IActionResult submit_request_page()
        {
            return View();
        }
        public IActionResult patient_submit_request_screen()
        {
            return View();
        }
        public IActionResult SessionExpired()
        {
            return View();
        }
        public IActionResult TermsAndConditions()
        {
            return View();
        }
        #endregion

        #region BUSINESS REQUEST
        [HttpGet]
        public IActionResult Business_Info()
        
        {
            BusinessModel model=new BusinessModel();
            model.Regions=_context.Regions.ToList();
            return View(model);
        }
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Business_Info(BusinessModel bm)
        {
            if (ModelState.IsValid)
            {
                _patient_Request.BRequest(bm);
                return RedirectToAction("Business_Info","Guest");
            }
            bm.Regions=_context.Regions.ToList();   
            return View(bm);
        }
        #endregion

        #region CONCIERGE REQUEST
        [HttpGet]
        public IActionResult Concierge_info()
        {
            ConciergeModel model = new ConciergeModel();
            model.Regions = _context.Regions.ToList();
            return View(model);
        }
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Concierge_info(ConciergeModel cm)
        {
            if (ModelState.IsValid)
            {
                _patient_Request.CRequest(cm);
                return RedirectToAction("Concierge_info","Guest");
            }
            cm.Regions=_context.Regions.ToList();
            return View(cm);
        }
        #endregion

        #region FAMILY FRIEND REQUEST
        public IActionResult Friend_family()
        {
            FamilyFriendModel familyFriendModel = new FamilyFriendModel();
            familyFriendModel.PatientRegions = _context.Regions.ToList();
            return View(familyFriendModel);
        }
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Friend_family(FamilyFriendModel fmfr)
        {
            string path = _environment.WebRootPath;
            
            if (ModelState.IsValid)
            {
                var uniqueid = Guid.NewGuid().ToString();
                _patient_Request.FRequest(fmfr,uniqueid,path);
                return RedirectToAction("Friend_Family", "Guest");
            }
            fmfr.PatientRegions = _context.Regions.ToList();
            return View(fmfr);
        }
        #endregion

        #region PATIENT REQUEST 
        [HttpGet]
        public IActionResult create_patient_request()
        {
            PatientModel patient = new()
            {
                Regions = _context.Regions.ToList()
            };
            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult create_patient_request(PatientModel pm)
        {
            string path = _environment.WebRootPath;
            if (ModelState.IsValid)
            {
                var uniqueid = Guid.NewGuid().ToString();
                _patient_Request.PRequest(pm,uniqueid, path);
                return RedirectToAction("create_patient_request", "Guest");
            }

            pm.Regions = _context.Regions.ToList();
            return View(pm);
        }
        #endregion

        #region LOGIN PAGE METHODS
        
            #region Login
        public IActionResult login_page()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult login_page(Aspnetuser demouser)
        {
            var password = _passwordHasher.GenerateSHA256(demouser.Passwordhash);
            Aspnetuser v = _context.Aspnetusers.FirstOrDefault(dt => dt.Username == demouser.Username && dt.Passwordhash == password);
            if (v != null)
            {
                if (v.Role == "Patient")
                {
                    HttpContext.Session.SetString("Email", v.Email);
                    var token = _jwtToken.generateJwtToken(v.Email, "Patient");
                    Response.Cookies.Append("jwt", token);

                    _notyf.Success("Logged In Successfully");
                    return RedirectToAction("PatientDashboard", "Home");
                }
                else if(v.Role == "Admin")
                {
                    HttpContext.Session.SetString("Email", v.Email);
                    var token = _jwtToken.generateJwtToken(v.Email, "Admin");
                    Response.Cookies.Append("jwt", token);


                    _notyf.Success("Logged In Successfully");
                    return RedirectToAction("AdminDashboard", "Admin");
                }
            }
            else
            {
                _notyf.Error("Invalid Credentials");
            }
            return View();
        }
        #endregion

            #region ForgotPassword
        public IActionResult forgot_password_page()
        {
            return View();
        }
        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult forgot_password_page(ForgotPasswordViewModel fvm)
        {
            if (ModelState.IsValid)
            {
                var jwtToken = _resetPasswordService.GenerateJWTTokenForPassword(fvm);
                var resetLink = Url.Action("ResetPassword", "Guest", new { token = jwtToken }, Request.Scheme);
                _emailService.SendEmailForPasswordReset(fvm, resetLink);
                return RedirectToAction("login_page", "Guest");
            }
            return View();
        }
        #endregion

            #region Reset Password
        [HttpGet]
        public ActionResult ResetPassword(string token)
        {
            // 4. In the MVC controller, create an action method to handle the password reset request
            try
            {
                string emailid = _resetPasswordService.ValidateToken(token);
                ResetPasswordViewModel rpvm = new ResetPasswordViewModel()
                {
                    email = emailid
                };
                return View(rpvm);
            }
            catch (Exception ex)
            {
                return Content("Invalid token");
            }
        }
        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel rpvm)
        {
            Aspnetuser aspnetuser = _context.Aspnetusers.FirstOrDefault(u => u.Email == rpvm.email);
            if (rpvm.password == rpvm.confirmpassword)
            {
                aspnetuser.Passwordhash = _passwordHasher.GenerateSHA256(rpvm.password);
                aspnetuser.Modifieddate = DateTime.Now;
                _context.Aspnetusers.Update(aspnetuser);
                _context.SaveChanges();
                return View("login_page");
            }
            else
            {
                return View("Error");
            }
        }
        #endregion

        #endregion
    }
}
