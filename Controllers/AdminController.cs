using DAL.ViewModels;
using DAL.DataModels;
using Microsoft.AspNetCore.Mvc;
using DAL.DataContext;
using System.Text;
using BAL.Interfaces;
using ClosedXML.Excel;
using Rotativa.AspNetCore;
using System.Text.Json.Nodes;

namespace HalloDoc_Project.Controllers
{
    [CustomAuthorize("Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;
        private readonly IAdminActions _adminActions;
        private readonly IAdminTables _adminTables;
        private readonly IFileOperations _fileOperations;
        private readonly IEncounterForm _encounterForm;
        private readonly IAdmin _admin;
        private readonly IPasswordHasher _passwordHasher;
        public AdminController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration config, IEmailService emailService, IAdminTables adminTables, IAdminActions adminActions, IFileOperations fileOperations, IEncounterForm encounterForm, IAdmin admin, IPasswordHasher passwordHasher)
        {
            _context = context;
            _environment = environment;
            _config = config;
            _emailService = emailService;
            _adminActions = adminActions;
            _adminTables = adminTables;
            _fileOperations = fileOperations;
            _encounterForm = encounterForm;
            _admin = admin;
            _passwordHasher = passwordHasher;
        }
        public IActionResult Index()
        {
            return View();
        }

        public enum RequestStatus
        {
            Unassigned = 1,
            Accepted = 2,
            Cancelled = 3,
            MDEnRoute = 4,
            MDOnSite = 5,
            Conclude = 6,
            CancelledByPatient = 7,
            Closed = 8,
            Unpaid = 9,
            Clear = 10,
            Block = 11,
        }

        public enum DashboardStatus
        {
            New = 1,
            Pending = 2,
            Active = 3,
            Conclude = 4,
            ToClose = 5,
            Unpaid = 6,
        }

        public enum RequestType
        {
            Business = 1,
            Patient = 2,
            Family = 3,
            Concierge = 4
        }

        public enum AllowRole
        {
            Admin = 1,
            Patient = 2,
            Physician = 3
        }


        public IActionResult ProviderLocation()
        {
            IEnumerable<PhyLocationRow> list = (from pl in _context.Physicianlocations
                                                select new PhyLocationRow
                                                {
                                                    PhysicianName = pl.Physicianname,
                                                    Latitude = pl.Latitude ?? 0,
                                                    Longitude = pl.Longtitude ?? 0,
                                                });

            ProviderLocationViewModel model = new ProviderLocationViewModel()
            {
                locationList = list.ToList(),
            };
            return View("ProviderLocation", model);
        }
        [HttpPost]
        public IActionResult Calendar()
        {
            return View("ProviderViews/Calendar");
        }

        public IActionResult ProviderScheduling()
        {
            return View("ProviderViews/ProviderScheduling");
        }
        public async Task<string> GetLatitudeLongitude(EditPhysicianViewModel model)
        {
            string state = _context.Regions.FirstOrDefault(x => x.Regionid == model.Regionid).Name;
            using (var client = new HttpClient())
            {
                string apiKey = _config["Maps:GeocodingAPIkey"];
                string baseUrl = $"https://geocode.maps.co/search?street={model.Address1 + model.Address2}&city={model.City}&state={state}&postalcode={model.ZipCode}&country=India&api_key=" + apiKey;
                //HTTP GET

                var responseTask = client.GetAsync(baseUrl);
                responseTask.Wait();

                var result = responseTask.Result;
                if (result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsStringAsync();

                    var json = JsonArray.Parse(content);

                    string? latitude = json?[0]?["lat"]?.ToString();
                    string? longitude = json?[0]?["lon"]?.ToString();
                }
                else
                {
                    //log response status here
                    ModelState.AddModelError(string.Empty, "Server error. Please contact administrator.");
                }
            }
            return "";
        }


        public IActionResult Access()
        {
            return View();
        }
        //Delete, DeleteAll, ViewUploads, SendOrders(Get) methods are not converted to three tier.
        public IActionResult ViewCase(int requestid)
        {
            if (ModelState.IsValid)
            {
                ViewCaseViewModel vc = _adminActions.ViewCaseAction(requestid);
                return View(vc);
            }
            return View();
        }
        public IActionResult UserAccess()
        {
            return View("AccessViews/UserAccess");
        }
        public ActionResult AssignCase()
        {
            return Ok();
        }
        public IActionResult AccountAccess()
        {
            var roles = _context.Roles.ToList();
            AccountAccessViewModel AccountAccess = new AccountAccessViewModel()
            {
                Roles = roles,
            };
            return View("AccessViews/AccountAccess", AccountAccess);
        }
        public IActionResult CreateRole()
        {
            CreateRoleViewModel RoleModel = new CreateRoleViewModel();
            RoleModel.AccountType = _context.Aspnetroles.ToList();
            return View("AccessViews/CreateRole", RoleModel);
        }

        public List<Menu> MenuFilter(int AccountType)
        {
            List<Menu> menu = _context.Menus.Where(x => x.Accounttype == AccountType).ToList();
            return menu;
        }

        public bool CreateNewRole(string Rolename, int AccountType, List<int> checkboxes)
        {
            var AdminEmail = HttpContext.Session.GetString("Email");
            Admin admin = _context.Admins.FirstOrDefault(x => x.Email == AdminEmail);

            Role roles = new Role()
            {
                Name = Rolename,
                Accounttype = (short)AccountType,
                Createdby = admin.Aspnetuserid,
                Createddate = DateTime.Now,
                Isdeleted = false,
            };
            _context.Roles.Add(roles);
            _context.SaveChanges();

            for (int i = 0; i < checkboxes.Count; i++)
            {
                Rolemenu rolemenu = new Rolemenu()
                {
                    Roleid = roles.Roleid,
                    Menuid = checkboxes[i],
                };
                _context.Rolemenus.Add(rolemenu);
            }
            _context.SaveChanges();
            return true;
        }

        public IActionResult EditRoleAccountAccess(int id)
        {

            EditAccessViewModel model = new EditAccessViewModel();

            var role = _context.Roles.FirstOrDefault(x => x.Roleid == id);
            List<Menu> menu = _context.Menus.Where(x => x.Accounttype == role.Accounttype).ToList();
            List<int> rolmenu = _context.Rolemenus.Where(x => x.Roleid == id).Select(x => (int)x.Menuid).ToList();
            var accountType = _context.Aspnetroles.ToList();

            model.id = id;
            model.role = role.Name;
            model.type = (short)role.Accounttype;
            model.accountTypes = accountType;
            model.menu = menu;
            model.rolemenu = rolmenu;

            return View("AccessViews/EditAccess", model);
        }

        public bool SaveEditRole(int id, string role, List<int> newPages)
        {
            var AdminEmail = HttpContext.Session.GetString("Email");
            Admin admin = _context.Admins.FirstOrDefault(x => x.Email == AdminEmail);

            Role roles = _context.Roles.FirstOrDefault(u => u.Roleid == id);
            List<Rolemenu> oldList = _context.Rolemenus.Where(x => x.Roleid == id).ToList();

            roles.Name = role;
            roles.Modifieddate = DateTime.Now;
            roles.Modifiedby = admin.Aspnetuserid;

            _context.Roles.Update(roles);

            for (int i = 0; i < oldList.Count; i++)
            {
                _context.Rolemenus.Remove(oldList[i]);
            }
            for (int x = 0; x < newPages.Count; x++)
            {
                Rolemenu rolemenu = new Rolemenu()
                {
                    Roleid = id,
                    Menuid = newPages[x]
                };
                _context.Rolemenus.Add(rolemenu);
            }
            _context.SaveChanges();


            return true;
        }

        public IActionResult CreatePhysician()
        {
            EditPhysicianViewModel model = new EditPhysicianViewModel();
            model.Role = _context.Roles.ToList();
            model.States = _context.Regions.ToList();
            return View("ProviderViews/CreatePhysician", model);
        }
        public IActionResult EditPhysicianProfile(int PhysicianId)
        {
            var Physician = _context.Physicians.FirstOrDefault(x => x.Physicianid == PhysicianId);
            var PhysicianAspData = _context.Aspnetusers.FirstOrDefault(x => x.Id == Physician.Aspnetuserid);
            EditPhysicianViewModel EditPhysician = new EditPhysicianViewModel();
            if (Physician != null)
            {
                EditPhysician.PhysicianId = PhysicianId;
                EditPhysician.PhoneNo = Physician.Mobile;
                EditPhysician.Status = Physician.Status;
                //EditPhysician.Role = (int)Physician.Roleid;
                EditPhysician.Email = Physician.Email;
                EditPhysician.FirstName = Physician.Firstname;
                EditPhysician.LastName = Physician.Lastname;
                EditPhysician.MedicalLicense = Physician.Medicallicense;
                EditPhysician.NPINumber = Physician.Npinumber;
                EditPhysician.SyncEmail = Physician.Syncemailaddress;
                EditPhysician.Address1 = Physician.Address1;
                EditPhysician.Address2 = Physician.Address2;
                EditPhysician.City = Physician.City;
                EditPhysician.States = _context.Regions.ToList();
                EditPhysician.ZipCode = Physician.Zip;
                EditPhysician.BillingPhoneNo = Physician.Altphone;
                EditPhysician.BusinessName = Physician.Businessname;
                EditPhysician.BusinessWebsite = Physician.Businesswebsite;
                EditPhysician.PhysicianUsername = PhysicianAspData.Username;
            }
            return View("ProviderViews/EditPhysicianProfile", EditPhysician);
        }
        [HttpPost]
        public IActionResult SubmitPhysicianAccountInfo(EditPhysicianViewModel PhysicianAccountInfo)
        {
            var Physician = _context.Physicians.FirstOrDefault(x => x.Physicianid == PhysicianAccountInfo.PhysicianId);
            if (Physician != null)
            {
                Physician.Status = PhysicianAccountInfo.Status;
                //Physician.Roleid = PhysicianAccountInfo.Role;
            }
            _context.Physicians.Update(Physician);
            _context.SaveChanges();
            return EditPhysicianProfile(PhysicianAccountInfo.PhysicianId);
        }
        [HttpPost]
        public IActionResult SubmitPhysicianInfo(EditPhysicianViewModel PhysicianInfoModel)
        {
            //ADDING REGION ID IS REMAINING
            var Physician = _context.Physicians.FirstOrDefault(x => x.Physicianid == PhysicianInfoModel.PhysicianId);
            if (Physician != null)
            {
                Physician.Firstname = PhysicianInfoModel.FirstName;
                Physician.Lastname = PhysicianInfoModel.LastName;
                Physician.Email = PhysicianInfoModel.Email;
                Physician.Mobile = PhysicianInfoModel.PhoneNo;
                Physician.Medicallicense = PhysicianInfoModel.MedicalLicense;
                Physician.Npinumber = PhysicianInfoModel.NPINumber;
                Physician.Syncemailaddress = PhysicianInfoModel.SyncEmail;
            }
            _context.Physicians.Update(Physician);
            _context.SaveChanges();
            return EditPhysicianProfile(PhysicianInfoModel.PhysicianId);
        }
        [HttpPost]
        public IActionResult SubmitPhysicianMailingBillingDetails(EditPhysicianViewModel MailingBillingModel)
        {
            GetLatitudeLongitude(MailingBillingModel);
            var Physician = _context.Physicians.FirstOrDefault(x => x.Physicianid == MailingBillingModel.PhysicianId);
            if (Physician != null)
            {
                Physician.Address1 = MailingBillingModel.Address1;
                Physician.Address2 = MailingBillingModel.Address2;
                Physician.City = MailingBillingModel.City;
                Physician.Regionid = MailingBillingModel.Regionid;
                Physician.Zip = MailingBillingModel.ZipCode;
                Physician.Altphone = MailingBillingModel.BillingPhoneNo;
            }
            _context.Physicians.Update(Physician);
            _context.SaveChanges();
            return EditPhysicianProfile(MailingBillingModel.PhysicianId);
        }
        [HttpPost]
        public IActionResult SubmitProviderProfile(EditPhysicianViewModel ProviderProfile)
        {
            var Physician = _context.Physicians.FirstOrDefault(x => x.Physicianid == ProviderProfile.PhysicianId);
            if (Physician != null)
            {
                Physician.Businessname = ProviderProfile.BusinessName;
                Physician.Businesswebsite = ProviderProfile.BusinessWebsite;
                if (ProviderProfile.SelectPhoto != null)
                {
                    var filename = "Photo";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", ProviderProfile.PhysicianId.ToString());
                    InsertFileAfterRename(ProviderProfile.SelectPhoto, filepath, filename);
                    Physician.Photo = Guid.NewGuid().ToString() + ProviderProfile.SelectPhoto.Name;
                }
                if (ProviderProfile.SelectSignature != null)
                {
                    var filename = "Signature";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", ProviderProfile.PhysicianId.ToString());
                    InsertFileAfterRename(ProviderProfile.SelectSignature, filepath, filename);
                    Physician.Signature = Guid.NewGuid().ToString() + ProviderProfile.SelectSignature.Name;
                }
            }
            _context.Physicians.Update(Physician);
            _context.SaveChanges();
            return EditPhysicianProfile(ProviderProfile.PhysicianId);
        }
        [HttpPost]
        public IActionResult UploadOnboardingDocuments(EditPhysicianViewModel Model)
        {
            var PhysicianDocuments = _context.Physicians.FirstOrDefault(x => x.Physicianid == Model.PhysicianId);
            if (PhysicianDocuments != null)
            {
                if (Model.SelectPhoto != null)
                {
                    var filename = "Photo";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Model.PhysicianId.ToString());
                    InsertFileAfterRename(Model.SelectPhoto, filepath, filename);
                    PhysicianDocuments.Photo = Guid.NewGuid().ToString() + Model.SelectPhoto.Name;
                }
                if (Model.SelectSignature != null)
                {
                    var filename = "Signature";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Model.PhysicianId.ToString());
                    InsertFileAfterRename(Model.SelectSignature, filepath, filename);
                    PhysicianDocuments.Signature = Guid.NewGuid().ToString() + Model.SelectSignature.Name;
                }
                if (Model.IndependentContractAgreement != null)
                {
                    var filename = "ICA";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Model.PhysicianId.ToString());
                    InsertFileAfterRename(Model.IndependentContractAgreement, filepath, filename);
                    PhysicianDocuments.Isagreementdoc = true;
                }
                if (Model.BackgroundCheck != null)
                {
                    var filename = "BC";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Model.PhysicianId.ToString());
                    InsertFileAfterRename(Model.BackgroundCheck, filepath, filename);
                    PhysicianDocuments.Isbackgrounddoc = true;
                }
                if (Model.HIPAACompliance != null)
                {
                    var filename = "HIPPA";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Model.PhysicianId.ToString());
                    InsertFileAfterRename(Model.HIPAACompliance, filepath, filename);
                    PhysicianDocuments.Istrainingdoc = true;
                }
                if (Model.NonDisclosureAgreement != null)
                {
                    var filename = "NDA";
                    var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Model.PhysicianId.ToString());
                    InsertFileAfterRename(Model.NonDisclosureAgreement, filepath, filename);
                    PhysicianDocuments.Isnondisclosuredoc = true;
                }
                _context.Physicians.Update(PhysicianDocuments);
                _context.SaveChanges();
            }
            return EditPhysicianProfile(PhysicianDocuments.Physicianid);
        }

        public ActionResult BlockCase(String blockreason)
        {
            return Ok();
        }
        public IActionResult ProviderMenu(ProviderMenuViewModel ProviderMenuData)
        {
            ProviderMenuData.Region = _context.Regions.ToList();

            var DoctorDetails = (from p in _context.Physicians
                                 join ph in _context.Physiciannotifications on p.Physicianid equals ph.Physicianid into notigroup
                                 from notiitem in notigroup.DefaultIfEmpty()
                                 select new Providers
                                 {
                                     PhysicianId = p.Physicianid,
                                     Name = p.Firstname + " " + p.Lastname,
                                     ProviderStatus = p.Status ?? 0,
                                     Email = p.Email,
                                     Notification = notiitem.Isnotificationstopped ? true : false,
                                     Role = p.Roleid ?? 0
                                 }).ToList();
            ProviderMenuData.providers = DoctorDetails;
            return View("ProviderViews/ProviderMenu", ProviderMenuData);
        }
        public void UpdateNotifications(int PhysicianId)
        {
            var PhysicianNotification = _context.Physiciannotifications.FirstOrDefault(x => x.Physicianid == PhysicianId);
            if (PhysicianNotification.Isnotificationstopped)
            {
                PhysicianNotification.Isnotificationstopped = false;
            }
            else if (!PhysicianNotification.Isnotificationstopped)
            {
                PhysicianNotification.Isnotificationstopped = true;
            }
            _context.Physiciannotifications.Update(PhysicianNotification);
            _context.SaveChanges();
        }
        [HttpPost]
        public IActionResult SendEmailToProvider(String RadioButtonValue, String EmailMessage, String PhysicianId)
        {
            var physician = _context.Physicians.FirstOrDefault(x => x.Physicianid == int.Parse(PhysicianId));
            if (RadioButtonValue == "1")
            {

            }
            if (RadioButtonValue == "2" && physician.Email != null)
            {
                _emailService.SendEmailMessage(EmailMessage, physician.Email);
            }
            else if (RadioButtonValue == "3")
            {

            }
            return Ok();
        }
        public IActionResult PhysicianLocation()
        {
            return View();
        }
        public IActionResult CreatePhysicianAccount()
        {
            return View("ProviderViews/CreatePhysicianAccount");
        }
        [HttpPost]
        public IActionResult CreatePhysician(EditPhysicianViewModel Model)
        {
            string id = Guid.NewGuid().ToString();
            Aspnetuser user = new Aspnetuser();
            user.Id = id;
            user.Username = Model.PhysicianUsername;
            user.Passwordhash = _passwordHasher.GenerateSHA256(Model.PhysicianPassword);
            user.Email = Model.Email;
            user.Phonenumber = Model.PhoneNo;
            user.Createddate = DateTime.Now;
            user.Role = "Physician";


            Physician Doctor = new Physician();
            Doctor.Aspnetuserid = id;
            Doctor.Firstname = Model.FirstName;
            Doctor.Lastname = Model.LastName;
            Doctor.Email = Model.Email;
            Doctor.Mobile = Model.PhoneNo;
            Doctor.Medicallicense = Model.MedicalLicense;
            Doctor.Adminnotes = Model.AdminNotes;
            Doctor.Address1 = Model.Address1;
            Doctor.Address2 = Model.Address2;
            Doctor.City = Model.City;
            Doctor.Zip = Model.ZipCode;
            Doctor.Altphone = Model.PhoneNo;
            Doctor.Npinumber = Model.NPINumber;
            Doctor.Medicallicense = Model.MedicalLicense;
            Doctor.Businessname = Model.BusinessName;
            Doctor.Businesswebsite = Model.BusinessWebsite;
            Doctor.Syncemailaddress = Model.SyncEmail;
            Doctor.Regionid = Model.Regionid;
            Doctor.Roleid = Model.PhysicianRole;
            Doctor.Createdby = id;
            Doctor.Regionid = Model.PhysicianState;

            _context.Aspnetusers.Add(user);

            _context.Physicians.Add(Doctor);
            _context.SaveChanges();

            Physiciannotification notifications = new Physiciannotification();
            notifications.Physicianid = Doctor.Physicianid;
            notifications.Isnotificationstopped = false;

            _context.Physiciannotifications.Add(notifications);
            _context.SaveChanges();

            if (Model.SelectPhoto != null)
            {
                var filename = "Photo";
                var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Doctor.Physicianid.ToString());
                InsertFileAfterRename(Model.SelectPhoto, filepath, filename);
                Doctor.Photo = Guid.NewGuid().ToString() + Model.SelectPhoto.Name;
            }
            if (Model.SelectSignature != null)
            {
                var filename = "Signature";
                var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Doctor.Physicianid.ToString());
                InsertFileAfterRename(Model.SelectSignature, filepath, filename);
                Doctor.Signature = Guid.NewGuid().ToString() + Model.SelectSignature.Name;
            }
            if (Model.IndependentContractAgreement != null)
            {
                var filename = "ICA";
                var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Doctor.Physicianid.ToString());
                InsertFileAfterRename(Model.IndependentContractAgreement, filepath, filename);
                Doctor.Isagreementdoc = true;
            }
            if (Model.BackgroundCheck != null)
            {
                var filename = "BC";
                var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Doctor.Physicianid.ToString());
                InsertFileAfterRename(Model.BackgroundCheck, filepath, filename);
                Doctor.Isbackgrounddoc = true;
            }
            if (Model.HIPAACompliance != null)
            {
                var filename = "HIPPA";
                var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Doctor.Physicianid.ToString());
                InsertFileAfterRename(Model.HIPAACompliance, filepath, filename);
                Doctor.Istrainingdoc = true;
            }
            if (Model.NonDisclosureAgreement != null)
            {
                var filename = "NDA";
                var filepath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "Content", "Providers", Doctor.Physicianid.ToString());
                InsertFileAfterRename(Model.NonDisclosureAgreement, filepath, filename);
                Doctor.Isnondisclosuredoc = true;
            }

            _context.Physicians.Update(Doctor);
            _context.SaveChanges();

            for (int i = 0; i < Model.SelectedRegions.Count; i++)
            {
                var physicinRegion = new Physicianregion
                {
                    Physicianid = Doctor.Physicianid,
                    Regionid = Model.SelectedRegions[i]
                };
                _context.Physicianregions.Add(physicinRegion);
            }
            _context.SaveChanges();

            return RedirectToAction("AdminDashboard");
        }
        public void InsertFileAfterRename(IFormFile file, string path, string updateName)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            string[] oldfiles = Directory.GetFiles(path, updateName + ".*");
            foreach (string f in oldfiles)
            {
                System.IO.File.Delete(f);
            }

            string extension = Path.GetExtension(file.FileName);

            string fileName = updateName + extension;

            string fullPath = Path.Combine(path, fileName);

            using FileStream stream = new(fullPath, FileMode.Create);
            file.CopyTo(stream);
        }

        public IActionResult ViewNotes(int requestid)
        {
            ViewCaseViewModel vn = new ViewCaseViewModel();
            return View();
        }
        public ActionResult TransferNotes()
        {
            return Ok();
        }
        public IActionResult AdminDBView()
        {
            AdminDashboardViewModel advm = _adminTables.AdminDashboardView();
            return View(advm);
        }
        public static string GetDOB(Requestclient reqcli)
        {
            string dob = reqcli.Intyear + "-" + reqcli.Strmonth + "-" + reqcli.Intdate;
            if (reqcli.Intyear == null || reqcli.Strmonth == null || reqcli.Intdate == null)
            {
                return " ";
            }

            string dobdate = DateTime.Parse(dob).ToString("MMM dd, yyyy");

            return dobdate;
        }
        public static string GetPatientDOB(Requestclient u)
        {

            string udb = u.Intyear + "-" + u.Strmonth + "-" + u.Intdate;
            if (u.Intyear == null || u.Strmonth == null || u.Intdate == null)
            {
                return "";
            }

            DateTime dobDate = DateTime.Parse(udb);
            string dob = dobDate.ToString("MMM dd, yyyy");
            var today = DateTime.Today;
            var age = today.Year - dobDate.Year;
            if (dobDate.Date > today.AddYears(-age)) age--;

            string dobString = dob + " (" + age + ")";

            return dobString;
        }
        public IActionResult Partners()
        {
            return View();
        }
        public IActionResult Profile()
        {
            return View();
        }
        public IActionResult Providers()
        {
            return View();
        }
        public IActionResult Records()
        {
            return View();
        }
        public IActionResult AdminDashboard()
        {
            //var email = HttpContext.Session.GetString("Email");
            //AdminDashboardViewModel advm = _adminTables.AdminDashboard(email);
            //return View(advm,this.ExcelFile());
            var email = HttpContext.Session.GetString("Email");
            AdminDashboardViewModel advm = _adminTables.AdminDashboard(email);
            //advm.excelfiles = this.;
            return View(advm);
        }
        [HttpPost]
        public byte[] ExportToExcel(int status, int page, int region, int type, string search)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("EmployeeList");

            // Add headers with yellow background color
            var headers = new string[] { "Name", "PhoneNo", "Email", "Requestid", "Status", "Address", "RequestTypeId", "UserID" };
            var headerCell = worksheet.Cell(1, 1);
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }

            // Get employee data (assuming GetEmployeeList() returns a list of employees)
            var records = ExcelFile(status, page, region, type, search);
            for (int i = 0; i < records.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = records[i].Name;
                worksheet.Cell(i + 2, 2).Value = records[i].PhoneNo;
                worksheet.Cell(i + 2, 3).Value = records[i].email;
                worksheet.Cell(i + 2, 4).Value = records[i].requestid;
                worksheet.Cell(i + 2, 5).Value = records[i].status;
                worksheet.Cell(i + 2, 6).Value = records[i].address;
                worksheet.Cell(i + 2, 7).Value = records[i].requesttypeid;
                worksheet.Cell(i + 2, 8).Value = records[i].userid;
            }

            // Prepare the response
            MemoryStream stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            // Return the Excel file
            return stream.ToArray();

        }
        public List<ExcelFileViewModel> ExcelFile(int dashboardstatus, int page, int region, int type, string search)
        {
            List<ExcelFileViewModel> ExcelData = new List<ExcelFileViewModel>();
            int pagesize = 5;
            int pageNumber = 1;
            if (page > 0)
            {
                pageNumber = page;
            }
            DashboardFilter filter = new DashboardFilter()
            {
                PatientSearchText = search,
                RegionFilter = region,
                RequestTypeFilter = type,
                pageNumber = pageNumber,
                pageSize = pagesize,
                page = page,
                status = dashboardstatus
            };

            List<short> validRequestTypes = new List<short>();
            switch (filter.status)
            {
                case (int)DashboardStatus.New:
                    validRequestTypes.Add((short)RequestStatus.Unassigned);
                    break;
                case (int)DashboardStatus.Pending:
                    validRequestTypes.Add((short)RequestStatus.Accepted);
                    break;
                case (int)DashboardStatus.Active:
                    validRequestTypes.Add((short)RequestStatus.MDEnRoute);
                    validRequestTypes.Add((short)RequestStatus.MDOnSite);
                    break;
                case (int)DashboardStatus.Conclude:
                    validRequestTypes.Add((short)RequestStatus.Conclude);
                    break;
                case (int)DashboardStatus.ToClose:
                    validRequestTypes.Add((short)RequestStatus.Cancelled);
                    validRequestTypes.Add((short)RequestStatus.CancelledByPatient);
                    validRequestTypes.Add((short)RequestStatus.Closed);

                    break;
                case (int)DashboardStatus.Unpaid:
                    validRequestTypes.Add((short)RequestStatus.Unpaid);
                    break;
            }
            pagesize = 5;
            pageNumber = 1;
            if (filter.page > 0)
            {
                pageNumber = filter.page;
            }
            ExcelData = (from r in _context.Requests
                         join rc in _context.Requestclients on r.Requestid equals rc.Requestid
                         where (filter.RequestTypeFilter == 0 || r.Requesttypeid == filter.RequestTypeFilter)
                         && (filter.RegionFilter == 0 || rc.Regionid == filter.RegionFilter)
                         && (validRequestTypes.Contains(r.Status))
                         && (string.IsNullOrEmpty(filter.PatientSearchText) || (rc.Firstname + " " + rc.Lastname).ToLower().Contains(filter.PatientSearchText.ToLower()))
                         select new ExcelFileViewModel
                         {
                             requestid = r.Requestid,
                             Name = rc.Firstname + " " + rc.Lastname,
                             email = rc.Email,
                             PhoneNo = rc.Phonenumber,
                             address = rc.Address,
                             requesttypeid = r.Requesttypeid,
                             status = r.Status
                         }).Skip((pageNumber - 1) * pagesize).Take(pagesize).ToList();
            return ExcelData;
        }

        [HttpPost]
        public byte[] ExportAllToExcel(int status)
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("EmployeeList");

            // Add headers with yellow background color
            var headers = new string[] { "Name", "PhoneNo", "Email", "Requestid", "Status", "Address", "RequestTypeId", "UserID" };
            var headerCell = worksheet.Cell(1, 1);
            var headerStyle = headerCell.Style;
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
            }

            // Get employee data (assuming GetEmployeeList() returns a list of employees)
            var records = ExcelFileExportAll(status);
            for (int i = 0; i < records.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = records[i].Name;
                worksheet.Cell(i + 2, 2).Value = records[i].PhoneNo;
                worksheet.Cell(i + 2, 3).Value = records[i].email;
                worksheet.Cell(i + 2, 4).Value = records[i].requestid;
                worksheet.Cell(i + 2, 5).Value = records[i].status;
                worksheet.Cell(i + 2, 6).Value = records[i].address;
                worksheet.Cell(i + 2, 7).Value = records[i].requesttypeid;
                worksheet.Cell(i + 2, 8).Value = records[i].userid;
            }

            // Prepare the response
            MemoryStream stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            // Return the Excel file
            return stream.ToArray();

        }
        public List<ExcelFileViewModel> ExcelFileExportAll(int dashboardstatus)
        {
            List<ExcelFileViewModel> ExcelData = new List<ExcelFileViewModel>();
            int pagesize = 5;
            int pageNumber = 1;

            DashboardFilter filter = new DashboardFilter()
            {
                status = dashboardstatus
            };

            List<short> validRequestTypes = new List<short>();
            switch (filter.status)
            {
                case (int)DashboardStatus.New:
                    validRequestTypes.Add((short)RequestStatus.Unassigned);
                    break;
                case (int)DashboardStatus.Pending:
                    validRequestTypes.Add((short)RequestStatus.Accepted);
                    break;
                case (int)DashboardStatus.Active:
                    validRequestTypes.Add((short)RequestStatus.MDEnRoute);
                    validRequestTypes.Add((short)RequestStatus.MDOnSite);
                    break;
                case (int)DashboardStatus.Conclude:
                    validRequestTypes.Add((short)RequestStatus.Conclude);
                    break;
                case (int)DashboardStatus.ToClose:
                    validRequestTypes.Add((short)RequestStatus.Cancelled);
                    validRequestTypes.Add((short)RequestStatus.CancelledByPatient);
                    validRequestTypes.Add((short)RequestStatus.Closed);

                    break;
                case (int)DashboardStatus.Unpaid:
                    validRequestTypes.Add((short)RequestStatus.Unpaid);
                    break;
            }

            ExcelData = (from r in _context.Requests
                         join rc in _context.Requestclients on r.Requestid equals rc.Requestid
                         where (validRequestTypes.Contains(r.Status))
                         select new ExcelFileViewModel
                         {
                             requestid = r.Requestid,
                             Name = rc.Firstname + " " + rc.Lastname,
                             email = rc.Email,
                             PhoneNo = rc.Phonenumber,
                             address = rc.Address,
                             requesttypeid = r.Requesttypeid,
                             status = r.Status
                         }).ToList();
            return ExcelData;
        }
        [HttpPost]
        public IActionResult AssignCase(int RequestId, string AssignPhysician, string AssignDescription)
        {
            _adminActions.AssignCaseAction(RequestId, AssignPhysician, AssignDescription);
            return Ok();
        }
        [HttpPost]
        public ActionResult CancelCase(int requestid, string Reason, string Description)
        {
            _adminActions.CancelCaseAction(requestid, Reason, Description);
            return Ok();
        }
        [HttpPost]
        public IActionResult BlockCase(int requestid, string blocknotes)
        {
            _adminActions.BlockCaseAction(requestid, blocknotes);
            return Ok();
        }
        [HttpPost]
        public IActionResult TransferCase(int RequestId, string TransferPhysician, string TransferDescription)
        {
            var email = HttpContext.Session.GetString("Email");
            var admin = _context.Admins.FirstOrDefault(x => x.Email == email);
            _adminActions.TransferCase(RequestId, TransferPhysician, TransferDescription, admin.Adminid);
            return Ok();
        }
        [HttpPost]
        public bool ClearCaseModal(int requestid)
        {
            string AdminEmail = HttpContext.Session.GetString("Email");
            //Admin admin = _context.Admins.GetFirstOrDefault(a => a.Email == AdminEmail);
            return _adminActions.ClearCaseModal(requestid);
        }
        [HttpPost]
        public void SendLink(string FirstName, string LastName, string Email)
        {
            var AgreementLink = Url.Action("patient_submit_request_screen", "Guest", new { }, Request.Scheme);
            _emailService.SendEmailWithLink(FirstName, LastName, Email, AgreementLink);

        }
        public IActionResult CreateRequestAdminDashboard()
        {
            CreateRequestViewModel model = new CreateRequestViewModel()
            {
                regions = _context.Regions.ToList(),
            };
            return View(model);
        }
        [HttpPost]
        public IActionResult CreateRequestAdminDashboard(CreateRequestViewModel model)
        {
            var user = _context.Requests.FirstOrDefault(x => x.Email == model.email);
            if (user != null)
            {
                //var newvm=new PatientModel();
                Aspnetuser newUser = new Aspnetuser();

                string id = Guid.NewGuid().ToString();
                newUser.Id = id;
                newUser.Email = model.email;
                newUser.Phonenumber = model.phoneno;
                newUser.Username = model.firstname;
                newUser.Createddate = DateTime.Now;
                _context.Aspnetusers.Add(newUser);
                _context.SaveChanges();

                User user_obj = new User();
                user_obj.Aspnetuserid = newUser.Id;
                user_obj.Firstname = model.firstname;
                user_obj.Lastname = model.lastname;
                user_obj.Email = model.email;
                user_obj.Mobile = model.phoneno;
                user_obj.Street = model.street;
                user_obj.City = model.city;
                user_obj.State = model.state;
                user_obj.Zipcode = model.zipcode;
                user_obj.Createddate = DateTime.Now;
                user_obj.Createdby = id;
                _context.Users.Add(user_obj);
                _context.SaveChanges();

                Request request = new Request();
                //change the fname, lname , and contact detials acc to the requestor
                request.Requesttypeid = 2;
                request.Userid = user_obj.Userid;
                request.Firstname = model.firstname;
                request.Lastname = model.lastname;
                request.Phonenumber = model.phoneno;
                request.Email = model.email;
                request.Createddate = DateTime.Now;
                request.Patientaccountid = id;
                request.Status = 1;
                request.Createduserid = user_obj.Userid;
                _context.Requests.Add(request);
                _context.SaveChanges();

                Requestclient rc = new Requestclient();
                rc.Requestid = request.Requestid;
                rc.Firstname = model.firstname;
                rc.Lastname = model.lastname;
                rc.Phonenumber = model.phoneno;
                rc.Location = model.city + model.state;
                rc.Email = model.email;
                rc.Address = model.street + " " + model.city + " " + model.state + " " + model.zipcode;
                rc.Street = model.street;
                rc.City = model.city;
                rc.State = model.state;
                rc.Zipcode = model.zipcode;
                rc.Notes = model.adminNotes;

                _context.Requestclients.Add(rc);
                _context.SaveChanges();
            }
            else
            {
                User user_obj = _context.Users.FirstOrDefault(u => u.Email == model.email);
                Request request = new Request();
                //change the fname, lname , and contact detials acc to the requestor
                request.Requesttypeid = 2;
                request.Firstname = model.firstname;
                request.Lastname = model.lastname;
                request.Phonenumber = model.phoneno;
                request.Email = model.email;
                request.Createddate = DateTime.Now;
                request.Status = 1;
                //request.Createduserid = user_obj.Userid;
                _context.Requests.Add(request);
                _context.SaveChanges();

                Requestclient rc = new Requestclient();
                rc.Requestid = request.Requestid;
                rc.Firstname = model.firstname;
                rc.Lastname = model.lastname;
                rc.Phonenumber = model.phoneno;
                rc.Location = model.city + " " + model.state;
                rc.Email = model.email;
                rc.Address = model.city + ", " + model.street + ", " + model.state + ", " + model.zipcode;
                rc.Street = model.street;
                rc.City = model.city;
                rc.State = model.state;
                rc.Zipcode = model.zipcode;
                rc.Notes = model.adminNotes;

                _context.Requestclients.Add(rc);
                _context.SaveChanges();

            }
            return RedirectToAction("CreateRequestAdminDashboard");
        }
        public IActionResult PatientHistoryPartialTable(string FirstName, string LastName, string Email, string PhoneNo)
        {
            var PatientHistoryList = (from user in _context.Users
                                      where (string.IsNullOrEmpty(Email) || user.Email.ToLower().Contains(Email))
                                      && (string.IsNullOrEmpty(FirstName) || user.Firstname.ToLower().Contains(FirstName))
                                      && (string.IsNullOrEmpty(LastName) || user.Lastname.ToLower().Contains(LastName))
                                      && (string.IsNullOrEmpty(PhoneNo) || user.Mobile.ToLower().Contains(PhoneNo))
                                      //&& (string.IsNullOrEmpty(patientEmail) || (rc.Email).ToLower().Contains(patientEmail))
                                      select new PatientHistoryTableViewModel
                                      {
                                          Email = user.Email,
                                          FirstName = user.Firstname,
                                          LastName = user.Lastname,
                                          Address = user.Street + " " + user.City + " " + user.State + " " + user.Zipcode,
                                          PhoneNumber = user.Mobile,
                                          UserId = user.Userid
                                      }).ToList();
            return PartialView("Records/PatienthistoryPartialTable", PatientHistoryList);
        }
        public IActionResult PatientHistory()
        {
            return View("Records/PatientHistory");
        }
        public IActionResult PatientRecords(int Userid)
        {
            var PatientRecordsList = (from clients in _context.Requestclients
                                      join requests in _context.Requests on clients.Requestid equals requests.Requestid
                                      join doctors in _context.Physicians on requests.Physicianid equals doctors.Physicianid into phyGroup
                                      from physicians in phyGroup.DefaultIfEmpty()
                                      join files in _context.Requestwisefiles on clients.Requestid equals files.Requestid into fileGroup
                                      from fileItems in fileGroup.DefaultIfEmpty()
                                      join encounter in _context.Encounterforms on clients.Requestid equals encounter.Requestid into formGroup
                                      from encounterform in formGroup.DefaultIfEmpty()
                                      join status in _context.Requeststatuses on requests.Status equals status.Statusid
                                      where requests.Userid == Userid
                                      select new PatientRecordsViewModel
                                      {
                                          ClientName = clients.Firstname + " " + clients.Lastname,
                                          CreatedDate = requests.Createddate,
                                          ConfirmationNo = requests.Confirmationnumber,
                                          ProviderName = physicians.Firstname + " " + physicians.Lastname,
                                          ConcludedDate = requests.Lastwellnessdate,
                                          Status = status.Name,
                                          Requestid = requests.Requestid
                                      }).ToList();
            return View("Records/PatientRecords", PatientRecordsList);
        }
        [HttpPost]
        public IActionResult NewTable(int page, int region, int type, string search)
        {
            int pagesize = 5;
            int pageNumber = 1;
            if (page > 0)
            {
                pageNumber = page;
            }
            DashboardFilter filter = new DashboardFilter()
            {
                PatientSearchText = search,
                RegionFilter = region,
                RequestTypeFilter = type,
                pageNumber = pageNumber,
                pageSize = pagesize,
                page = page,
            };
            AdminDashboardViewModel model = _adminTables.GetNewTable(filter);
            model.currentPage = pageNumber;

            return PartialView("NewTable", model);
        }
        [HttpPost]
        public IActionResult ActiveTable(int page, int region, int type, string search)
        {

            int pagesize = 5;
            int pageNumber = 1;
            if (page > 0)
            {
                pageNumber = page;
            }
            DashboardFilter filter = new DashboardFilter()
            {
                PatientSearchText = search,
                RegionFilter = region,
                RequestTypeFilter = type,
                pageNumber = pageNumber,
                pageSize = pagesize,
                page = page,
            };
            AdminDashboardViewModel model = _adminTables.GetActiveTable(filter);
            model.currentPage = pageNumber;
            return PartialView("ActiveTable", model);
        }
        [HttpPost]
        public IActionResult PendingTable(int page, int region, int type, string search)
        {
            int pagesize = 5;
            int pageNumber = 1;
            if (page > 0)
            {
                pageNumber = page;
            }
            DashboardFilter filter = new DashboardFilter()
            {
                PatientSearchText = search,
                RegionFilter = region,
                RequestTypeFilter = type,
                pageNumber = pageNumber,
                pageSize = pagesize,
                page = page,
            };
            AdminDashboardViewModel model = _adminTables.GetPendingTable(filter);
            model.currentPage = pageNumber;

            return PartialView("PendingTable", model);
        }
        [HttpPost]
        public IActionResult ConcludeTable(int page, int region, int type, string search)
        {
            int pagesize = 5;
            int pageNumber = 1;
            if (page > 0)
            {
                pageNumber = page;
            }
            DashboardFilter filter = new DashboardFilter()
            {
                PatientSearchText = search,
                RegionFilter = region,
                RequestTypeFilter = type,
                pageNumber = pageNumber,
                pageSize = pagesize,
                page = page,
            };
            AdminDashboardViewModel model = _adminTables.GetConcludeTable(filter);
            model.currentPage = pageNumber;

            return PartialView("ConcludeTable", model);
        }
        [HttpPost]
        public IActionResult ToCloseTable(int page, int region, int type, string search)
        {
            int pagesize = 5;
            int pageNumber = 1;
            if (page > 0)
            {
                pageNumber = page;
            }
            DashboardFilter filter = new DashboardFilter()
            {
                PatientSearchText = search,
                RegionFilter = region,
                RequestTypeFilter = type,
                pageNumber = pageNumber,
                pageSize = pagesize,
                page = page,
            };

            AdminDashboardViewModel model = _adminTables.GetToCloseTable(filter);
            model.currentPage = pageNumber;

            return PartialView("ToCloseTable", model);
        }
        [HttpPost]
        public IActionResult UnpaidTable(int page, int region, int type, string search)
        {
            int pagesize = 5;
            int pageNumber = 1;
            if (page > 0)
            {
                pageNumber = page;
            }
            DashboardFilter filter = new DashboardFilter()
            {
                PatientSearchText = search,
                RegionFilter = region,
                RequestTypeFilter = type,
                pageNumber = pageNumber,
                pageSize = pagesize,
                page = page,
            };
            AdminDashboardViewModel model = _adminTables.GetUnpaidTable(filter);
            model.currentPage = pageNumber;

            return PartialView("UnpaidTable", model);
        }
        [HttpGet]
        public IActionResult FinalizeDownload(int requestid)
        {
            var EncounterModel = _encounterForm.EncounterFormGet(requestid);
            if (EncounterModel == null)
            {
                return NotFound();
            }
            return new ViewAsPdf("EncounterFormFinalizeView", EncounterModel)
            {
                FileName = "FinalizedEncounterForm.pdf"
            };
        }
        public IActionResult EncounterForm(int requestId, EncounterFormViewModel EncModel)
        {
            EncModel = _encounterForm.EncounterFormGet(requestId);
            var RequestExistStatus = _context.Encounterforms.FirstOrDefault(x => x.Requestid == requestId);
            if (RequestExistStatus == null)
            {
                EncModel.ifExist = false;
            }
            if (RequestExistStatus != null)
            {
                EncModel.ifExist = true;
            }
            return View("EncounterForm", EncModel);
        }
        public IActionResult FinalizeForm(int requestid)
        {
            var encounterRecord = _context.Encounterforms.FirstOrDefault(x => x.Requestid == requestid);
            encounterRecord.Isfinalize = true;
            _context.Encounterforms.Update(encounterRecord);
            _context.SaveChanges();

            return RedirectToAction("AdminDashboard", "Admin");
        }
        [HttpPost]
        public IActionResult EncounterForm(EncounterFormViewModel model)
        {
            _encounterForm.EncounterFormPost(model.requestId, model);
            return EncounterForm(model.requestId, model);
        }
        public IActionResult AdminProfile()
        {
            var email = HttpContext.Session.GetString("Email");
            AdminProfileViewModel model = new AdminProfileViewModel();
            if (email != null)
            {
                model = _admin.AdminProfileGet(email);
            }
            return View("AdminProfile", model);
        }
        [HttpPost]
        public IActionResult AdminInfoPost(AdminProfileViewModel apvm)
        {
            _admin.AdminInfoPost(apvm);
            return AdminProfile();
        }
        [HttpPost]
        public IActionResult BillingInfoPost(AdminProfileViewModel apvm)
        {
            _admin.BillingInfoPost(apvm);
            return AdminProfile();
        }
        [HttpPost]
        public IActionResult PasswordPost(AdminProfileViewModel apvm)
        {
            var email = HttpContext.Session.GetString("Email");
            _admin.PasswordPost(apvm, email);
            return AdminProfile();
        }
        public IActionResult DeleteFile(int fileid, int requestid)
        {
            var fileRequest = _context.Requestwisefiles.FirstOrDefault(x => x.Requestwisefileid == fileid);
            fileRequest.Isdeleted = true;

            _context.Update(fileRequest);
            _context.SaveChanges();

            return RedirectToAction("ViewUploads", new { requestid = requestid });
        }
        public IActionResult DeleteAllFiles(int requestid)
        {
            var request = _context.Requestwisefiles.Where(r => r.Requestid == requestid && r.Isdeleted != true).ToList();
            for (int i = 0; i < request.Count; i++)
            {
                request[i].Isdeleted = true;
                _context.Update(request[i]);
            }
            _context.SaveChanges();
            return RedirectToAction("ViewUploads", new { requestid = requestid });
        }
        public IActionResult ViewUploads(int requestid)
        {

            var user = _context.Requests.FirstOrDefault(r => r.Requestid == requestid);
            var requestFile = _context.Requestwisefiles.Where(r => r.Requestid == requestid).ToList();
            var requests = _context.Requests.FirstOrDefault(r => r.Requestid == requestid);

            ViewUploadsViewModel uploads = new()
            {
                ConfirmationNo = requests.Confirmationnumber,
                Patientname = user.Firstname + " " + user.Lastname,
                RequestID = requestid,
                Requestwisefiles = requestFile
            };
            return View(uploads);
        }
        public IActionResult SetPath(int requestid)
        {
            var path = _environment.WebRootPath;
            return SendMail(requestid, path);
        }
        public IActionResult SendMail(int requestid, string path)
        {
            _emailService.SendEmailWithAttachments(requestid, path);
            return RedirectToAction("ViewUploads", "Admin", new { requestid = requestid });
        }
        [HttpPost]
        public IActionResult SendAgreement(int RequestId, string PhoneNo, string email)
        {
            if (ModelState.IsValid)
            {
                var AgreementLink = Url.Action("ReviewAgreement", "Guest", new { ReqId = RequestId }, Request.Scheme);
                //----------------------------------
                _emailService.SendAgreementLink(RequestId, AgreementLink, email);
                return RedirectToAction("AdminDashboard", "Guest");
            }
            return View();
        }
        public IActionResult BusinessData(int BusinessId)
        {
            var result = _context.Healthprofessionals.FirstOrDefault(x => x.Vendorid == BusinessId);
            return Json(result);
        }
        [HttpPost]
        public IActionResult ViewUploads(ViewUploadsViewModel uploads)
        {
            if (uploads.File != null)
            {
                var uniqueid = Guid.NewGuid().ToString();
                var path = _environment.WebRootPath;
                _fileOperations.insertfilesunique(uploads.File, uniqueid, path);

                var filestring = Path.GetFileNameWithoutExtension(uploads.File.FileName);
                var extensionstring = Path.GetExtension(uploads.File.FileName);
                Requestwisefile requestwisefile = new()
                {
                    Filename = uniqueid + "$" + uploads.File.FileName,
                    Requestid = uploads.RequestID,
                    Createddate = DateTime.Now
                };
                _context.Update(requestwisefile);
                _context.SaveChanges();
            }
            return RedirectToAction("ViewUploads", new { requestid = uploads.RequestID });
        }
        public IActionResult SelectRecordsPartialTable(int requestStatus, string patientName, int requestType, string phoneNumber, DateTime? fromDateOfService, DateTime? toDateOfService, string providerName, string patientEmail)
        {
            var PatientRecords = (from r in _context.Requests
                                  join rc in _context.Requestclients on r.Requestid equals rc.Requestid
                                  join rs in _context.Requeststatuses on r.Status equals rs.Statusid
                                  //join rn in _context.Requestnotes on r.Requestid equals rn.Requestid
                                  join rt in _context.Requesttypes on r.Requesttypeid equals rt.Requesttypeid
                                  join phy in _context.Physicians on r.Physicianid equals phy.Physicianid into phyGroup
                                  from phyItem in phyGroup.DefaultIfEmpty()
                                  where (requestStatus == 0 || r.Status == requestStatus)
                                  && (string.IsNullOrEmpty(patientName) || (rc.Firstname + " " + rc.Lastname).ToLower().Contains(patientName.ToLower()))
                                  && (requestType == 0 || r.Requesttypeid == requestType)
                                  && (r.Accepteddate >= fromDateOfService || fromDateOfService == null)
                                  && (r.Accepteddate <= toDateOfService || toDateOfService == null)
                                  && (string.IsNullOrEmpty(providerName) || (r.Physician.Firstname + " " + r.Physician.Lastname).ToLower().Contains(providerName.ToLower()))
                                  && (string.IsNullOrEmpty(patientEmail) || (rc.Email).ToLower().Contains(patientEmail))
                                  && (string.IsNullOrEmpty(phoneNumber) || (rc.Phonenumber).ToLower().Contains(phoneNumber))
                                  select new SearchRecordsTableViewModel
                                  {
                                      PatientName = rc.Firstname + " " + rc.Lastname,
                                      Requestor = r.Firstname + " " + r.Lastname,
                                      DateOfService = DateOnly.FromDateTime(DateTime.Now),
                                      CloseCaseDate = DateOnly.FromDateTime(DateTime.Now),
                                      Email = rc.Email,
                                      PhoneNumber = rc.Phonenumber,
                                      Address = rc.Address,
                                      Zip = rc.Zipcode,
                                      RequestStatus = rs.Name,
                                      PhysicianNotes = /*rn.Physiciannotes*/"xxx",
                                      AdminNotes = /*rn.Adminnotes*/"xxx",
                                      PatientNotes = "PatientNotes",
                                      PhysicianName = phyItem.Firstname + " " + phyItem.Lastname,
                                      CancelledByPhysicianNotes = "N/A"
                                  }).ToList();
            return PartialView("Records/SearchRecordsPartialTable", PatientRecords);
        }
        public IActionResult SearchRecords()
        {
            SearchRecordViewModel model = new SearchRecordViewModel()
            {
                RequestStatus = _context.Requeststatuses.ToList(),
                RequestType = _context.Requesttypes.ToList()
            };
            return View("Records/SearchRecords", model);
        }
        public IActionResult FilterPhysicianByRegion(int regionid)
        {
            var physicians = _context.Physicians.Where(x => x.Regionid == regionid).ToList();
            return Json(physicians);
        }
        public IActionResult CloseCase(int requestid)
        {
            CloseCaseViewModel model = _adminActions.CloseCaseGet(requestid);
            return View(model);
        }
        [HttpPost]
        public IActionResult CloseCase(CloseCaseViewModel model, int requestid)
        {
            _adminActions.CloseCasePost(model, requestid);
            return CloseCase(requestid);
        }
        public IActionResult CloseInstance(int reqid)
        {
            var user = _context.Requests.FirstOrDefault(x => x.Requestid == reqid);
            user.Status = 9;
            _context.Update(user);
            _context.SaveChanges();
            return RedirectToAction("AdminDashboard", "Admin");
        }
        public IActionResult SendOrders(int requestid)
        {
            List<Healthprofessional> healthprofessionals = _context.Healthprofessionals.ToList();
            List<Healthprofessionaltype> healthprofessionaltypes = _context.Healthprofessionaltypes.ToList();
            SendOrderViewModel model = new SendOrderViewModel()
            {
                requestid = requestid,
                healthprofessionals = healthprofessionals,
                healthprofessionaltype = healthprofessionaltypes
            };
            return View(model);
        }
        [HttpPost]
        public IActionResult SendOrders(int requestid, SendOrderViewModel sendOrder)
        {
            _adminActions.SendOrderAction(requestid, sendOrder);
            return SendOrders(requestid);
        }

        public List<Healthprofessional> filterVenByPro(string ProfessionId)
        {
            var result = _context.Healthprofessionals.Where(u => u.Profession == int.Parse(ProfessionId)).ToList();
            return result;
        }

        #region ASSIGN CASE PHYSICIAN FILTER
        public IActionResult GetPhysicianForTransfer(int regionid)
        {
            var result = (from physician in _context.Physicians
                          join
                           region in _context.Physicianregions on
                           physician.Physicianid equals region.Physicianid into phy
                          select physician).Where(s => s.Regionid == regionid).ToList();

            
            return Json(result);
        }
        #endregion

        #region BLOCK HISTORY RECORDS
        public IActionResult BlockHistory()
        {
            return View("Records/BlockHistory");
        }
        public IActionResult BlockHistoryPartialTable(string FirstName, string LastName, string Email, string PhoneNo)
        {
            var BlockHistoryRecords = (from blockedRequests in _context.Blockrequests
                                       join patientRequests in _context.Requests
                                       on blockedRequests.Requestid equals patientRequests.Requestid
                                       join clientRequests in _context.Requestclients
                                       on blockedRequests.Requestid equals clientRequests.Requestid
                                       where blockedRequests.Isactive == true
                                       && (string.IsNullOrEmpty(Email) || clientRequests.Email.ToLower().Contains(Email))
                                       && (string.IsNullOrEmpty(FirstName) || clientRequests.Firstname.ToLower().Contains(FirstName))
                                       && (string.IsNullOrEmpty(LastName) || clientRequests.Lastname.ToLower().Contains(LastName))
                                       && (string.IsNullOrEmpty(PhoneNo) || clientRequests.Phonenumber.ToLower().Contains(PhoneNo))
                                       select new BlockHistoryViewModel
                                       {
                                           PatientName = clientRequests.Firstname + " " + clientRequests.Lastname,
                                           PhoneNo = clientRequests.Phonenumber ?? " ",
                                           Email = clientRequests.Email ?? " ",
                                           CreatedDate = patientRequests.Createddate,
                                           Notes = clientRequests.Notes ?? " ",
                                           IsActive = blockedRequests.Isactive ?? true,
                                           RequestId = clientRequests.Requestid
                                       }).ToList();
            return PartialView("Records/BlockHistoryPartialTable", BlockHistoryRecords);
        }
        public IActionResult UnblockRequest(int requestid)
        {
            var BlockedRequest = _context.Blockrequests.FirstOrDefault(request => request.Requestid == requestid);
            if (BlockedRequest != null)
            {
                BlockedRequest.Modifieddate = DateTime.Now;
                BlockedRequest.Isactive = false;
                _context.Blockrequests.Update(BlockedRequest);

            }
            var Request = _context.Requests.FirstOrDefault(request => request.Requestid == requestid);
            if (Request != null)
            {
                Request.Status = 1;
                _context.Requests.Update(Request);
            }
            _context.SaveChanges();
            return RedirectToAction("BlockHistory");
        }
        #endregion

        #region EMAIL LOG RECORDS

        public IActionResult EmailLogs()
        {
            EmailLogViewModel emaildata = new()
            {
                roles = _context.Roles.ToList()
            };
            return View("Records/EmailLog",emaildata);
        }
        public IActionResult EmailLogPartialTable(string ReceiverName, string Email, DateTime? CreatedDate, DateTime? SentDate,int RoleId)
        {
            var EmailList = (from emails in _context.Emaillogs
                             join roles in _context.Roles on emails.Roleid equals roles.Roleid
                             where (string.IsNullOrEmpty(ReceiverName) /*|| emails.Recipient.ToLower().Contains(ReceiverName)*/)
                             && (string.IsNullOrEmpty(Email) || emails.Emailid.ToLower().Contains(Email))
                             && (CreatedDate == emails.Createdate.Date || CreatedDate== null)
                             && (SentDate == emails.Sentdate.Value.Date || SentDate==null)
                             && (RoleId ==0 || RoleId==emails.Roleid)
                             select new EmailLogViewModel
                             {
                                 Action = emails.Subjectname,
                                 RoleId = emails.Roleid,
                                 Email = emails.Emailid,
                                 CreateDate = emails.Createdate,
                                 SentDate = emails.Sentdate,
                                 Sent = false,
                                 SentTries = 1,
                                 ConfirmationNumber = emails.Confirmationnumber ?? "n/a"
                             }).ToList();
            return PartialView("Records/EmailLogPartialTable", EmailList);
        }
        #endregion

        #region SMS LOG RECORDS
        public IActionResult SMSLogs()
        {
            SMSLogViewModel SMSdata = new()
            {
                roles = _context.Roles.ToList()
            };
            return View("Records/SMSLog", SMSdata);
        }
        public IActionResult SMSLogPartialTable(string ReceiverName, string PhoneNo, DateTime? CreatedDate, DateTime? SentDate, int RoleId)
        {
            var SMSList = (from sms in _context.Smslogs
                             join roles in _context.Roles on sms.Roleid equals roles.Roleid
                             where (string.IsNullOrEmpty(ReceiverName) /*|| emails.Recipient.ToLower().Contains(ReceiverName)*/)
                             && (string.IsNullOrEmpty(PhoneNo) || sms.Mobilenumber.ToLower().Contains(PhoneNo))
                             && (CreatedDate == sms.Createdate.Date || CreatedDate == null)
                             && (SentDate == sms.Sentdate.Value.Date || SentDate == null)
                             && (RoleId == 0 || RoleId == sms.Roleid)
                             select new SMSLogViewModel
                             {
                                 Action = sms.Action.ToString(),
                                 RoleId = sms.Roleid,
                                 MoblieNumber = sms.Mobilenumber,
                                 CreateDate =sms.Createdate,
                                 SentDate = sms.Sentdate,
                                 Sent = false,
                                 SentTries = 1,
                                 ConfirmationNumber = sms.Confirmationnumber ?? "n/a"
                             }).ToList();
            return PartialView("Records/SMSLogPartialTable", SMSList);
        }
        #endregion

        #region VENDOR DETAILS / CREATE VENDORS / EDIT VENDORS
        public IActionResult VendorDetails()
        {
            //int adminId = (int)HttpContext.Session.GetString("Email");
            //Admin admin = _context.Admins.FirstOrDefault(u => u.email == adminId);
            var type = _context.Healthprofessionaltypes.ToList();

            VendorDetailsViewModel model = new VendorDetailsViewModel();

            //model.UserName = admin.Firstname + " " + admin.Lastname;
            model.Healthprofessionaltypes = type;

            return View("Partners/VendorDetails", model);
        }

        public IActionResult VendorsFilter(string filterSearch, int filterProfession)
        {
            VendorDetailsViewModel model = new VendorDetailsViewModel();

            var list = from professionals in _context.Healthprofessionals
                       join types in _context.Healthprofessionaltypes on professionals.Profession equals types.Healthprofessionalid into professionGroup
                       from proType in professionGroup.DefaultIfEmpty()
                       where (string.IsNullOrEmpty(filterSearch) || professionals.Vendorname.ToLower().Contains(filterSearch.ToLower()))
                       && (filterProfession == 0 || filterProfession == proType.Healthprofessionalid)
                       && (professionals.Isdeleted != true)
                       select new VendorDetailsTableViewModel
                       {
                           profession = proType.Professionname,
                           businessName = professionals.Vendorname,
                           email = professionals.Email,
                           faxNumber = professionals.Faxnumber,
                           phone = professionals.Phonenumber,
                           businessContact = professionals.Businesscontact,
                           vendorId = professionals.Vendorid
                       };

            model.VendorsTable = list.ToList();
            return PartialView("Partners/VendorDetailsPartialTable", model);
        }

        public IActionResult DeleteVendor(int id)
        {
            var vendor = _context.Healthprofessionals.FirstOrDefault(x => x.Vendorid == id);

            vendor.Isdeleted = true;

            _context.Healthprofessionals.Update(vendor);
            _context.SaveChanges();

            return RedirectToAction("VendorDetails");
        }


        public IActionResult AddBusiness()
        { 
        
        //    int adminId = (int)HttpContext.Session.GetInt32("adminId");
        //    Admin admin = _context.Admins.FirstOrDefault(u => u.Adminid == adminId);
            var types = _context.Healthprofessionaltypes.ToList();
            var region = _context.Regions.ToList();

            CreateUpdateVendorViewModel model = new CreateUpdateVendorViewModel();
            //model.UserName = admin.Firstname + " " + admin.Lastname;
            model.types = types;
            model.regions = region;

            return View("Partners/AddBusiness", model);
        }

        [HttpPost]
        public IActionResult AddBusiness(CreateUpdateVendorViewModel model)
        {
            var mobile = "+" + model.code + "-" + model.phone;
            var mobile1 = "+" + model.code1 + "-" + model.phone1;


            //int adminId = (int)HttpContext.Session.GetInt32("adminId");
            //Admin admin = _context.Admins.FirstOrDefault(u => u.Adminid == adminId);
            var region = _context.Regions.FirstOrDefault(x => x.Regionid == model.state);

            Healthprofessional profession = new Healthprofessional()
            {
                Vendorname = model.BusinessName,
                Profession = model.type,
                Faxnumber = model.Fax,
                Phonenumber = mobile,
                Email = model.Email,
                Businesscontact = mobile1,
                Address = model.street,
                City = model.city,
                State = region.Name,
                Zip = model.zip,
                Regionid = model.state,
                Createddate = DateTime.Now,
            };

            _context.Healthprofessionals.Add(profession);
            _context.SaveChanges();

            return RedirectToAction("VendorDetails");
        }


        public IActionResult EditBusiness(int id)
        {
            //int adminId = (int)HttpContext.Session.GetInt32("adminId");
            //Admin admin = _context.Admins.FirstOrDefault(u => u.Adminid == adminId);

            var types = _context.Healthprofessionaltypes.ToList();
            var region = _context.Regions.ToList();


            Healthprofessional vendor = _context.Healthprofessionals.FirstOrDefault(x => x.Vendorid == id);

            CreateUpdateVendorViewModel model = new CreateUpdateVendorViewModel();

            //model.UserName = admin.Firstname + " " + admin.Lastname;
            model.types = types;
            model.regions = region;
            model.BusinessName = vendor.Vendorname;
            model.type = vendor.Profession;
            model.Fax = vendor.Faxnumber;
            model.phone = vendor.Phonenumber;
            model.Email = vendor.Email;
            model.phone1 = vendor.Businesscontact;
            model.street = vendor.Address;
            model.city = vendor.City;
            model.state = vendor.Regionid;
            model.zip = vendor.Zip;
            model.id = id;

            return View("Partners/EditBusiness", model);
        }

        [HttpPost]
        public IActionResult EditBusiness(CreateUpdateVendorViewModel model)
        {
            var mobile = "+" + model.code + "-" + model.phone;
            var mobile1 = "+" + model.code1 + "-" + model.phone1;

            var region = _context.Regions.FirstOrDefault(x => x.Regionid == model.state);

            Healthprofessional vendor = _context.Healthprofessionals.FirstOrDefault(x => x.Vendorid == model.id);

            vendor.Vendorname = model.BusinessName;
            vendor.Profession = model.type;
            vendor.Faxnumber = model.Fax;
            vendor.Phonenumber = mobile;
            vendor.Email = model.Email;
            vendor.Businesscontact = mobile1;
            vendor.Address = model.street;
            vendor.City = model.city;
            vendor.State = region.Name;
            vendor.Zip = model.zip;
            vendor.Regionid = model.state;
            vendor.Modifieddate = DateTime.Now;

            _context.Healthprofessionals.Update(vendor);
            _context.SaveChanges();

            return RedirectToAction("EditBusiness", model.id);
        }
        #endregion

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("jwt");
            return RedirectToAction("login_page", "Guest");
        }
    }
}