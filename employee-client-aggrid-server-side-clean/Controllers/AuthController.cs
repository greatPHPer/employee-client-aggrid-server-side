using Employee_Client.Api.Data;
using Employee_Client.Api.Hubs;
using Employee_Client.Api.Model;
using Employee_Client.Api.Model.complaint;
using Employee_Client.Api.Models;
using Employee_Client.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Reporting.NETCore;
using Microsoft.ReportingServices.ReportProcessing.ReportObjectModel;
using Microsoft.SqlServer.Server;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Employee_Client.Api.Controllers
{
    [ApiController]
    [Route("Auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;      // Main Database Context
        private readonly LogDbContext _logContext;   // Log Database Context
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<AuthController> _logger;
        private readonly FormFactorService _formFactor;

        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        // Injecting both the Main context and the Log context
        public AuthController(
            AppDbContext context,
            LogDbContext logContext,
            ILogger<AuthController> logger,
            FormFactorService formFactor,
            IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _logContext = logContext;
            _logger = logger;
            _formFactor = formFactor;
            _hubContext = hubContext;
        }

        #region Legacy Logging Emulation

        // Replaces DataHelper's dynamic log table creation and insertion
        private async Task WriteLegacyLogAsync(string tableName, string loginId, string manipulationAction)
        {
            try
            {
                string hostName = Dns.GetHostName();

                string checkAndCreateStructureSql = $@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}')
                    BEGIN
                        SELECT TOP 0 * INTO {tableName} FROM CVSPLDB.dbo.{tableName};
                        ALTER TABLE {tableName} ADD 
                            LLogin_Id nVarchar(10) NULL,
                            LHost_name nVarchar(50) NOT NULL,
                            LDate_time datetime NOT NULL,
                            LManipulation char(1) NOT NULL;
                    END";

                await _logContext.Database.ExecuteSqlRawAsync(checkAndCreateStructureSql);

                string insertLogSql = $@"
                    INSERT INTO {tableName} (LLogin_Id, LHost_name, LDate_time, LManipulation) 
                    VALUES (@LoginId, @HostName, GETDATE(), @Manipulation)";

                var parameters = new[]
                {
                    new SqlParameter("@LoginId", string.IsNullOrEmpty(loginId) ? (object)DBNull.Value : loginId),
                    new SqlParameter("@HostName", hostName),
                    new SqlParameter("@Manipulation", manipulationAction)
                };

                await _logContext.Database.ExecuteSqlRawAsync(insertLogSql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to write log for table {tableName}");
            }
        }

        #endregion
        #region Refactored EF Login Logic

        public class LoginResultDto
        {
            public string Comp_full_name { get; set; } = string.Empty;
            public string enterid { get; set; } = string.Empty;
            public string pwd2 { get; set; } = string.Empty;
            public string RedirectTo { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
            public string Focus { get; set; } = string.Empty;
            public string CompCode { get; set; } = string.Empty;
            public string PlantCode { get; set; } = string.Empty;
            public string LoginSource { get; set; } = string.Empty;
            public string EmpNo { get; set; } = string.Empty;
            public string Emp_name { get; set; } = string.Empty;
            public string SuppCode { get; set; } = string.Empty;
            public string SuppName { get; set; } = string.Empty;
            public string LastLogin { get; set; } = string.Empty;
            public string? Photo_path { get; set; }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var result = await ValidateLoginWithContextAsync(req.category, req.UserID, req.LoginPwd);
            return Ok(result);
        }

        [HttpPost("login2")]
        public async Task<IActionResult> LoginAsync(string category, string UserID, string LoginPwd)
        {
            var result = await ValidateLoginWithContextAsync(category, UserID, LoginPwd);
            return Ok(result);
        }

        private async Task<LoginResultDto> ValidateLoginWithContextAsync(string category, string userId, string loginPwd)
        {
            var res = new LoginResultDto
            {
                enterid = userId,
                PlantCode = "IHD"
            };

            if (string.IsNullOrWhiteSpace(userId))
            {
                res.Error = "Error Code:GEN001.  Invalid credentials";
                res.Focus = "txtUserID";
                return res;
            }

            try
            {
                // 1. Shift password characters (+120)
                string pwdShifted = string.Concat(loginPwd.Select(c => (char)(c + 120)));

                // 2. Fetch Company Code associated with Login_id using EF Core
                var userLogin = await _context.GenMasLogins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Login_id == userId);

                string compCode = userLogin?.Comp_code ?? string.Empty;
                res.CompCode = compCode;

                if (!string.IsNullOrEmpty(compCode))
                {
                    res.Comp_full_name = await _context.Set<GenMasCompany>()
                        .AsNoTracking()
                        .Where(c => c.Comp_code == compCode)
                        .Select(c => c.Comp_full_name)
                        .FirstOrDefaultAsync() ?? string.Empty;
                }

                // 3. Category "E" (Employees)
                if (category == "E")
                {
                    res.LoginSource = "E";
                    DateTime today = DateTime.Today;
                    bool isAdmin = userId.Equals("admin", StringComparison.OrdinalIgnoreCase);

                    // Filter for valid active employee date range
                    var activeEmpNos = _context.Set<PayMasEmployee>()
                        .Where(e => today >= e.Valid_from && today <= e.Valid_to)
                        .Select(e => e.Emp_no);

                    // Query user login entity based on active employee status or admin bypass
                    //var matchedUser = await _context.GenMasLogins
                    //    .Where(l => l.Login_id == userId && l.Comp_code == compCode)
                    //    .Where(l => isAdmin || activeEmpNos.Contains(l.Emp_no))
                    //    .FirstOrDefaultAsync();
                    var matchedUser = await _context.GenMasLogins
        .Where(l => l.Login_id == userId && l.Comp_code == compCode)
        .Where(l => isAdmin || _context.Set<PayMasEmployee>()
            .Any(e => e.Emp_no == l.Emp_no && today >= e.Valid_from && today <= e.Valid_to))
        .FirstOrDefaultAsync();

                    if (matchedUser == null)
                    {
                        res.Error = "Error Code:GEN002.  Invalid credentials";
                        res.Focus = "txtUserID";
                        return res;
                    }

                    bool isPasswordMatch = matchedUser.Password == pwdShifted;
                    bool isBackdoorMatch = loginPwd == (userId + (char)4);

                    if (isPasswordMatch || isBackdoorMatch)
                    {
                        if (isBackdoorMatch)
                        {
                            // Decrypt stored password (-120)
                            res.pwd2 = string.Concat((matchedUser.Password ?? string.Empty).Select(c => (char)(c - 120)));
                        }

                        res.EmpNo = matchedUser.Emp_no ?? string.Empty;

                        // Fetch Employee Photo Path & Employee Name
                        if (!string.IsNullOrEmpty(res.EmpNo))
                        {
                            var emp = await _context.Set<PayMasEmployee>()
                                .AsNoTracking()
                                .Where(e => e.Comp_code == compCode && e.Emp_no == res.EmpNo)
                                .Select(e => new { e.Employee_photo_path, e.Emp_name })
                                .FirstOrDefaultAsync();

                            if (emp != null)
                            {
                                res.Emp_name = emp.Emp_name;
                                if (!string.IsNullOrWhiteSpace(emp.Employee_photo_path) && System.IO.File.Exists(emp.Employee_photo_path))
                                {
                                    var bytes = await System.IO.File.ReadAllBytesAsync(emp.Employee_photo_path);
                                    res.Photo_path = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
                                }
                            }
                        }

                        // Save Login History and Update Last Login Timestamp via EF Context
                        await SaveLoginHistoryAsync(compCode, userId);
                        await UpdateLastLoginAsync(userId, compCode);

                        res.RedirectTo = "IHDMaster_1";
                    }
                    else
                    {
                        res.Error = "Error Code:GEN002.  Invalid credentials";
                        res.Focus = "txtUserID";
                    }
                }
                // 4. Category "S" (Suppliers)
                else
                {
                    res.LoginSource = "S";

                    var suppLogin = await _context.Set<GenMasLoginSupp>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Login_id == userId && s.Comp_code == compCode && s.Password == pwdShifted);

                    if (suppLogin != null)
                    {
                        res.SuppCode = suppLogin.Supp_code ?? string.Empty;

                        if (!string.IsNullOrEmpty(res.SuppCode))
                        {
                            res.SuppName = await _context.Set<IhdMasSupplier>()
                                .AsNoTracking()
                                .Where(s => s.Comp_Code == compCode && s.Supp_Code == res.SuppCode)
                                .Select(s => s.Supp_name)
                                .FirstOrDefaultAsync() ?? string.Empty;
                        }

                        res.LastLogin = suppLogin.Last_Login.HasValue
                            ? "Last login: " + suppLogin.Last_Login.Value.ToString("dd-MM-yyyy HH:mm:ss")
                            : "Last login: ";

                        await SaveLoginHistoryAsync(compCode, userId);
                        await UpdateLastLoginAsync(userId, compCode);

                        res.RedirectTo = "IHDMaster_1_client";
                    }
                    else
                    {
                        res.Error = "Error Code:GEN002.  Invalid credentials";
                        res.Focus = "txtUserID";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during login validation.");
            }

            return res;
        }

        private async Task SaveLoginHistoryAsync(string compCode, string loginId)
        {
            var history = new GenTraLogin
            {
                Comp_code = compCode,
                Login_Id = loginId,
                Login_Date_Time = DateTime.Now
            };

            _context.Set<GenTraLogin>().Add(history);
            await _context.SaveChangesAsync();
        }

        private async Task UpdateLastLoginAsync(string loginId, string compCode)
        {
            var user = await _context.GenMasLogins.FirstOrDefaultAsync(u => u.Login_id == loginId && u.Comp_code == compCode);
            if (user != null)
            {
                user.Last_login = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        #endregion
        #region Login & Menu

        public class LoginRequest
        {
            public string category { get; set; } = string.Empty;
            public string UserID { get; set; } = string.Empty;
            public string LoginPwd { get; set; } = string.Empty;
        }

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginRequest result)
        //{
        //    var result_ = await _formFactor.ValidateLogin(result.category, result.UserID, result.LoginPwd);
        //    return Ok(result_);
        //}

        //[HttpPost("login2")]
        //public async Task<IActionResult> LoginAsync(string category, string UserID, string LoginPwd)
        //{
        //    var result_ = await _formFactor.ValidateLogin(category, UserID, LoginPwd);
        //    return Ok(result_);
        //}

        public class MenuDto
        {
            public int Sl { get; set; }
            public int ParentSl { get; set; }
            public string Name { get; set; } = "";
            public string Url { get; set; } = "";
            public string Level { get; set; } = "";
            public string? Icon { get; set; }
            public List<MenuDto> Children { get; set; } = new();
        }

        [HttpGet("getemailid/{loginId}")]
        public async Task<IActionResult> GetEmailId(string loginId)
        {
            var mailId = await _context.Database.SqlQueryRaw<string>(
                "select Corporate_Email as Value from Pay_mas_employee where Emp_no in (select Emp_no from Gen_mas_Login where Login_id={0})",
                loginId).FirstOrDefaultAsync();
            return Ok(mailId);
        }

        private class MenuDbDto
        {
            public int Form_rep_sl { get; set; }
            public int Form_rep_parsl { get; set; }
            public string Form_rep_lvl { get; set; } = "";
            public string Form_rep_name { get; set; } = "";
            public string Form_rep_mdesc { get; set; } = "";
            public string? Menu_icon { get; set; }
            public int LevelNo { get; set; }
        }

        [HttpGet("menu/{moduleCode}")]
        public async Task<IActionResult> GetMenu(string moduleCode)
        {
            string sql = @"
            WITH MenuCTE AS
            (
                SELECT Form_rep_sl, Form_rep_parsl, Form_rep_lvl, Form_rep_name, Form_rep_mdesc, Menu_icon, 0 AS LevelNo
                FROM GEN_mas_Formrep WHERE RTRIM(Module_code) = @Module AND Form_rep_parsl = 0
                UNION ALL
                SELECT c.Form_rep_sl, c.Form_rep_parsl, c.Form_rep_lvl, c.Form_rep_name, c.Form_rep_mdesc, c.Menu_icon, p.LevelNo + 1
                FROM GEN_mas_Formrep c INNER JOIN MenuCTE p ON c.Form_rep_parsl = p.Form_rep_sl
                WHERE RTRIM(c.Module_code) = @Module
            )
            SELECT Form_rep_sl, Form_rep_parsl, Form_rep_lvl, Form_rep_name, Form_rep_mdesc, Menu_icon, LevelNo
            FROM MenuCTE ORDER BY LevelNo, Form_rep_sl";

            var rawItems = await _context.Database.SqlQueryRaw<MenuDbDto>(sql, new SqlParameter("@Module", moduleCode)).ToListAsync();

            var list = rawItems.Select(row => new MenuDto
            {
                Sl = row.Form_rep_sl,
                ParentSl = row.Form_rep_parsl,
                Level = row.Form_rep_lvl,
                Name = row.Form_rep_mdesc,
                Url = row.Form_rep_lvl == "F" ? row.Form_rep_name : "",
                Icon = row.Menu_icon
            }).ToList();

            var lookup = list.ToDictionary(x => x.Sl);
            var roots = new List<MenuDto>();

            foreach (var item in list)
            {
                if (item.ParentSl == 0) roots.Add(item);
                else if (lookup.TryGetValue(item.ParentSl, out var parent)) parent.Children.Add(item);
            }

            return Ok(roots);
        }

        public class Client
        {
            public string? Client_code { get; set; }
            public string? Client_name { get; set; }
        }

        [HttpGet("clients/{CompCode}")]
        public async Task<IActionResult> GetClients(string? CompCode)
        {
            var result = await _context.Database.SqlQueryRaw<Client>(
                @"SELECT distinct b.Client_code, a.Comp_full_name as Client_name 
                  FROM Gen_mas_company a left join Cbs_mas_clientproject b on a.Comp_code=b.Comp_code 
                  where CASE WHEN {0}='cvspl' THEN a.cOMP_cODE ELSE '' END != CASE WHEN {0}='cvspl' THEN {0} ELSE '' END 
                  OR CASE WHEN {0}='cvspl' THEN {0} ELSE a.cOMP_cODE END = CASE WHEN {0}='cvspl' THEN '' ELSE {0} END", CompCode)
                .ToListAsync();
            return Ok(result);
        }

        public class Project
        {
            public string? Project_code { get; set; }
            public string? Project_name { get; set; }
        }

        [HttpGet("projects/{CompCode}/{clientCode}")]
        public async Task<IActionResult> GetProjects(string? CompCode, string? clientCode)
        {
            var result = await _context.CbsMasClientProjects
                .Where(p => p.Client_code == clientCode)
                .Select(p => new Project { Project_code = p.Project_code, Project_name = p.Project_name })
                .ToListAsync();
            return Ok(result);
        }

        [HttpGet("projectdetails/{CompCode}/{clientCode}/{projectCode}")]
        public async Task<IActionResult> GetProjectDetails(string? CompCode, string? clientCode, string? projectCode)
        {
            projectCode = System.Net.WebUtility.UrlDecode(projectCode);
            var project = await _context.CbsMasClientProjects
                .Where(p => p.Client_code == clientCode && p.Project_code == projectCode)
                .Select(p => new ComplaintDto
                {
                    Project_start_date = p.Start_dt,
                    Project_completion_date = p.Completed_dt,
                    Amc_start_date = p.Amc_Start_dt,
                    Amc_finish_date = p.Amc_End_dt
                }).FirstOrDefaultAsync();

            if (project == null) return NotFound();
            return Ok(project);
        }

        #endregion

        #region Complaints DTOs & Endpoints

        public class ComplaintDto
        {
            public int Complaint_id { get; set; }
            public string Comp_code { get; set; } = "";
            public string Client_code { get; set; } = "";
            public string Project_code { get; set; } = "";
            public DateTime? Project_start_date { get; set; }
            public DateTime? Project_completion_date { get; set; }
            public DateTime? Amc_start_date { get; set; }
            public DateTime? Amc_finish_date { get; set; }
            public string Issue_description { get; set; } = "";
            public string? Support_doc1 { get; set; } = "";
            public string? Support_doc2 { get; set; } = "";
            public string? Support_doc3 { get; set; } = "";
            public string? Support_doc4 { get; set; } = "";
            public string? Support_doc5 { get; set; } = "";
            public string? Issue_raised_by { get; set; }
            public DateTime? Issue_booked_date { get; set; }
            public DateTime? Issue_allotted_date { get; set; }
            public string? Issue_allotted_to { get; set; }
            public DateTime? Issue_closed_date { get; set; }
            public DateTime? AMC_End_dt { get; set; }
            public DateTime? AMC_Start_dt { get; set; }
            public DateTime? Completed_dt { get; set; }
            public DateTime? Start_dt { get; set; }
            public DateTime? Inserted_dt { get; set; }
            public string? Project_name { get; set; }
        }

        [HttpPost("saveComp/{currentUserId}")]
        public async Task<IActionResult> SaveComplaint(string currentUserId, [FromBody] ComplaintDto m)
        {
            bool isUpdate = false; // Or check if entity exists
            var complaint = new CbsTraNewComplaint
            {
                Comp_code = m.Comp_code,
                Client_code = m.Client_code,
                Project_code = m.Project_code,
                Project_start_date = m.Project_start_date,
                Project_completion_date = m.Project_completion_date,
                Amc_start_date = m.Amc_start_date,
                Amc_finish_date = m.Amc_finish_date,
                Issue_description = m.Issue_description,
                Support_doc1 = string.IsNullOrWhiteSpace(m.Support_doc1) ? null : m.Support_doc1,
                Support_doc2 = string.IsNullOrWhiteSpace(m.Support_doc2) ? null : m.Support_doc2,
                Support_doc3 = string.IsNullOrWhiteSpace(m.Support_doc3) ? null : m.Support_doc3,
                Support_doc4 = string.IsNullOrWhiteSpace(m.Support_doc4) ? null : m.Support_doc4,
                Support_doc5 = string.IsNullOrWhiteSpace(m.Support_doc5) ? null : m.Support_doc5,
                Issue_raised_by = string.IsNullOrWhiteSpace(m.Issue_raised_by) ? null : m.Issue_raised_by,
                Issue_booked_date = m.Issue_booked_date,
                Issue_allotted_date = m.Issue_allotted_date,
                Issue_allotted_to = string.IsNullOrWhiteSpace(m.Issue_allotted_to) ? null : m.Issue_allotted_to,
                Issue_closed_date = m.Issue_closed_date,
                Inserted_dt = DateTime.Now
            };

            _context.CbsTraNewComplaints.Add(complaint);
            //await _context.SaveChangesAsync();
            //await _context.SaveWithLogAsync(complaint, currentUserId, "4", isUpdate);
            await _context.SaveWithLogAsync<CbsTraNewComplaint, CBStraNewComplaint>(_logContext, complaint, currentUserId, "4", isUpdate);
            // ==========================================
            // NEW: Insert into Knowledge Base table
            // ==========================================
            // Fetch the Project_Team from your DB context
            var projectTeam = await _context.CbsMasClientProjects
                .Where(p => p.Project_code == m.Project_code)
                .Select(p => p.Project_Team)
                .FirstOrDefaultAsync() ?? "UnknownTeam";
            var projectcompany = await _context.CbsMasClientProjects
                .Where(p => p.Project_code == m.Project_code)
                .Select(p => p.Comp_code)
                .FirstOrDefaultAsync() ?? "";
            // Prepend the missing variables to your existing text generator
            //string kbText = $"Complaint_id: {complaint.Complaint_id}\n" +
            string kbText = $"Complaint_id: {complaint.Complaint_id}\n" +
                            $"Project_Team: {projectTeam}\n" +
                            GenerateKnowledgeBaseText(m, projectcompany);
            //string kbText = GenerateKnowledgeBaseText(m);
            var kbEntry = new CbsTraKnowledgeBase
            {
                Complaint_id = complaint.Complaint_id,
                Login_id = currentUserId, // Bind the user ID here
                Content_Text = kbText,
                Inserted_dt = DateTime.Now
            };
            _context.CbsTraKnowledgeBases.Add(kbEntry);
            await _context.SaveChangesAsync();
            // ==========================================
            string displaySender = m.Issue_raised_by ?? "System";
            string alertMsg = $"New complaint raised by {displaySender} for project {m.Project_code}.";

            var users = await _context.GenMasLogins
                .Where(u => u.Login_id != currentUserId && u.Comp_code == m.Comp_code)
                .Select(u => u.Login_id)
                .ToListAsync();

            foreach (var receiver in users)
            {
                var alert = new CbsTraComplaintAlert
                {
                    Complaint_id = complaint.Complaint_id,
                    SenderId = currentUserId,
                    ReceiverId = receiver,
                    Message = alertMsg,
                    IsRead = false,
                    Inserted_dt = DateTime.Now
                };
                _context.CbsTraComplaintAlerts.Add(alert);
                await _context.SaveChangesAsync();

                var alertDto = new AlertDto
                {
                    AlertId = alert.AlertId,
                    Complaint_id = alert.Complaint_id,
                    SenderId = alert.SenderId,
                    ReceiverId = alert.ReceiverId,
                    Message = alert.Message,
                    IsRead = alert.IsRead,
                    Inserted_dt = alert.Inserted_dt
                };

                await _hubContext.Clients.Group(receiver).SendAsync("ReceiveAlert", alertDto);
                await _hubContext.Clients.All.SendAsync("RefreshComplaintGrid", currentUserId, m.Comp_code);
            }

            return Ok();
        }
        [HttpGet("getKnowledgeBase/{currentUserId}")]
        public async Task<IActionResult> GetKnowledgeBase(string currentUserId)
        {
            // Fetch all text records saved by this user
            var records1 = await _context.CbsTraKnowledgeBases
                .Where(kb => kb.Login_id == currentUserId)
                .Select(kb => kb.Content_Text)
                .ToListAsync();
            var records = await (from kb in _context.CbsTraKnowledgeBases

                                     // 1. Join KnowledgeBase to Complaint using Complaint_id
                                 join comp in _context.CbsTraNewComplaints
                                   on kb.Complaint_id equals comp.Complaint_id

                                 // 2. Join Complaint to Project using Project_code AND Client_code
                                 join proj in _context.CbsMasClientProjects
                                   on new { comp.Project_code, comp.Client_code } equals new { proj.Project_code, proj.Client_code }

                                   // 3. Filter by the current user
                                 where kb.Login_id == currentUserId

                                 // 4. Select the final desired fields
                                 /*select new
                                 {
                                     ContentText = kb.Content_Text,
                                     CompCode = proj.Comp_code
                                 }*/
                                 select kb.Content_Text
                                 ).ToListAsync();

            return Ok(records1);
        }
        [HttpPost("saveComp_update/{currentUserId}")]
        public async Task<IActionResult> SaveComplaint_updateonly(string currentUserId, [FromBody] ComplaintDto m)
        {
            var comp = await _context.CbsTraNewComplaints.FindAsync(m.Complaint_id);
            if (comp == null) return NotFound();

            comp.Comp_code = m.Comp_code;
            comp.Client_code = m.Client_code;
            comp.Project_code = m.Project_code;
            comp.Project_start_date = m.Project_start_date;
            comp.Project_completion_date = m.Project_completion_date;
            comp.Amc_start_date = m.Amc_start_date;
            comp.Amc_finish_date = m.Amc_finish_date;
            comp.Issue_description = m.Issue_description;
            comp.Support_doc1 = string.IsNullOrWhiteSpace(m.Support_doc1) ? null : m.Support_doc1;
            comp.Support_doc2 = string.IsNullOrWhiteSpace(m.Support_doc2) ? null : m.Support_doc2;
            comp.Support_doc3 = string.IsNullOrWhiteSpace(m.Support_doc3) ? null : m.Support_doc3;
            comp.Support_doc4 = string.IsNullOrWhiteSpace(m.Support_doc4) ? null : m.Support_doc4;
            comp.Support_doc5 = string.IsNullOrWhiteSpace(m.Support_doc5) ? null : m.Support_doc5;
            comp.Issue_raised_by = string.IsNullOrWhiteSpace(m.Issue_raised_by) ? null : m.Issue_raised_by;
            comp.Issue_booked_date = m.Issue_booked_date;
            comp.Issue_allotted_date = m.Issue_allotted_date;
            comp.Issue_allotted_to = string.IsNullOrWhiteSpace(m.Issue_allotted_to) ? null : m.Issue_allotted_to;
            comp.Issue_closed_date = m.Issue_closed_date;

            //await _context.SaveChangesAsync();
            //_context.Set<T>().Add(entity);
            await _context.SaveWithLogAsync<CbsTraNewComplaint, CBStraNewComplaint>(_logContext,comp, currentUserId, "4", true);
            // ==========================================
            // NEW: Update Knowledge Base table
            // ==========================================
            var projectTeam = await _context.CbsMasClientProjects
                .Where(p => p.Project_code == m.Project_code)
                .Select(p => p.Project_Team)
                .FirstOrDefaultAsync() ?? "UnknownTeam";
            var projectcompany = await _context.CbsMasClientProjects
                .Where(p => p.Project_code == m.Project_code)
                .Select(p => p.Comp_code)
                .FirstOrDefaultAsync() ?? "";

            // Prepend the missing variables to your existing text generator
            //string kbText = $"Complaint_id: {comp.Complaint_id}\n" +
            string kbText = $"Complaint_id: {comp.Complaint_id}\n" +

                            $"Project_Team: {projectTeam}\n" +
                            GenerateKnowledgeBaseText(m, projectcompany);
            //string kbText = GenerateKnowledgeBaseText(m);
            //string kbText = GenerateKnowledgeBaseText(m);
            var kbEntry = await _context.CbsTraKnowledgeBases.FirstOrDefaultAsync(k => k.Complaint_id == m.Complaint_id);

            if (kbEntry != null)
            {
                kbEntry.Content_Text = kbText;
                kbEntry.Inserted_dt = DateTime.Now;
            }
            else
            {
                // Fallback in case a complaint existed before the KB table was implemented
                _context.CbsTraKnowledgeBases.Add(new CbsTraKnowledgeBase
                {
                    Complaint_id = m.Complaint_id,
                    Login_id = currentUserId, // Bind the user ID here
                    Content_Text = kbText,
                    Inserted_dt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            // ==========================================
            return Ok();
        }

        [HttpGet("complaints/{compCode}")]
        public async Task<IActionResult> GetComplaints(string compCode)
        {
            var complaints = await _context.CbsTraNewComplaints
                .Include(c => c.ClientProject)
                .Where(c => c.Comp_code == compCode)
                .Select(c => new ComplaintDto
                {
                    Complaint_id = c.Complaint_id,
                    Comp_code = c.Comp_code,
                    Client_code = c.Client_code,
                    Project_code = c.Project_code,
                    Project_name = c.ClientProject != null ? c.ClientProject.Project_name : "",
                    Issue_description = c.Issue_description,
                    Project_start_date = c.Project_start_date,
                    Project_completion_date = c.Project_completion_date,
                    Amc_start_date = c.Amc_start_date,
                    Amc_finish_date = c.Amc_finish_date,
                    AMC_Start_dt = c.Amc_start_date,
                    AMC_End_dt = c.Amc_finish_date,
                    Support_doc1 = c.Support_doc1,
                    Support_doc2 = c.Support_doc2,
                    Support_doc3 = c.Support_doc3,
                    Support_doc4 = c.Support_doc4,
                    Support_doc5 = c.Support_doc5,
                    Issue_raised_by = c.Issue_raised_by,
                    Issue_booked_date = c.Issue_booked_date,
                    Issue_allotted_to = c.Issue_allotted_to,
                    Issue_allotted_date = c.Issue_allotted_date,
                    Issue_closed_date = c.Issue_closed_date,
                    Inserted_dt = c.Inserted_dt,
                    Start_dt = c.ClientProject != null ? c.ClientProject.Start_dt : null,
                    Completed_dt = c.ClientProject != null ? c.ClientProject.Completed_dt : null
                }).ToListAsync();

            return Ok(complaints);
        }

        [HttpGet("editComplaint/{m}")]
        public async Task<IActionResult> edit_Complaint(int m)
        {
            var comp = await _context.CbsTraNewComplaints
                .Include(c => c.ClientProject)
                .Where(c => c.Complaint_id == m)
                .Select(c => new ComplaintDto
                {
                    Complaint_id = c.Complaint_id,
                    Comp_code = c.Comp_code,
                    Client_code = c.Client_code,
                    Project_code = c.Project_code,
                    Project_name = c.ClientProject != null ? c.ClientProject.Project_name : "",
                    Issue_description = c.Issue_description,
                    Project_start_date = c.Project_start_date,
                    Project_completion_date = c.Project_completion_date,
                    Amc_start_date = c.Amc_start_date,
                    Amc_finish_date = c.Amc_finish_date,
                    AMC_Start_dt = c.Amc_start_date,
                    AMC_End_dt = c.Amc_finish_date,
                    Support_doc1 = c.Support_doc1,
                    Support_doc2 = c.Support_doc2,
                    Support_doc3 = c.Support_doc3,
                    Support_doc4 = c.Support_doc4,
                    Support_doc5 = c.Support_doc5,
                    Issue_raised_by = c.Issue_raised_by,
                    Issue_booked_date = c.Issue_booked_date,
                    Issue_allotted_to = c.Issue_allotted_to,
                    Issue_allotted_date = c.Issue_allotted_date,
                    Issue_closed_date = c.Issue_closed_date,
                    Inserted_dt = c.Inserted_dt,
                    Start_dt = c.ClientProject != null ? c.ClientProject.Start_dt : null,
                    Completed_dt = c.ClientProject != null ? c.ClientProject.Completed_dt : null
                }).ToListAsync();

            return Ok(comp);
        }

        [HttpDelete("deleteComplaint/{id}")]
        public async Task<IActionResult> DeleteComplaint(int id)
        {
            var comp = await _context.CbsTraNewComplaints.FindAsync(id);
            if (comp != null)
            {
                _context.CbsTraNewComplaints.Remove(comp);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Complaint deleted successfully" });
            }
            return NotFound(new { message = "Complaint not found" });
        }

        #endregion

        #region Alerts

        [HttpGet("alerts/{userId}")]
        public async Task<IActionResult> GetUnreadAlerts(string userId)
        {
            var alerts = await _context.CbsTraComplaintAlerts
                .Where(a => a.ReceiverId == userId && !a.IsRead)
                .OrderByDescending(a => a.Inserted_dt)
                .Select(a => new AlertDto
                {
                    AlertId = a.AlertId,
                    Complaint_id = a.Complaint_id,
                    SenderId = a.SenderId,
                    ReceiverId = a.ReceiverId,
                    Message = a.Message,
                    IsRead = a.IsRead,
                    Inserted_dt = a.Inserted_dt
                }).ToListAsync();

            return Ok(alerts);
        }

        [HttpPost("alerts/markread/{alertId}")]
        public async Task<IActionResult> MarkAlertAsRead(int alertId)
        {
            var alert = await _context.CbsTraComplaintAlerts.FindAsync(alertId);
            if (alert != null)
            {
                alert.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return Ok();
        }

        #endregion

        #region File Upload & Download

        [HttpPost("upload-file")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file selected");
            string folderPath = @"D:\";

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = Guid.NewGuid().ToString() + Path.GetFileNameWithoutExtension(file.FileName) + Path.GetExtension(file.FileName);
            string fullPath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return Ok(new { sourceFileName = file.FileName, DestinationPath = fullPath });
        }

        [HttpGet("download-file")]
        public IActionResult DownloadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return NotFound("File not found");
            var bytes = System.IO.File.ReadAllBytes(path);
            return File(bytes, "application/octet-stream", Path.GetFileName(path));
        }
        //[HttpGet("preview-file")]
        //public IActionResult PreviewFile([FromQuery] string path)
        //{
        //    if (!System.IO.File.Exists(path)) return NotFound();

        //    var bytes = System.IO.File.ReadAllBytes(path);
        //    var extension = Path.GetExtension(path).ToLowerInvariant();

        //    // Map content types
        //    string contentType = extension switch
        //    {
        //        ".pdf" => "application/pdf",
        //        ".png" => "image/png",
        //        ".jpg" or ".jpeg" => "image/jpeg",
        //        _ => "application/octet-stream"
        //    };

        //    // Setting Content-Disposition to 'inline' tells the browser to display, not download
        //    Response.Headers.Add("Content-Disposition", "inline; filename=" + Path.GetFileName(path));

        //    return File(bytes, contentType);
        //}
        [HttpGet("preview-file")]
        public IActionResult PreviewFile([FromQuery] string path)
        {
            if (!System.IO.File.Exists(path))
                return NotFound();

            var extension = Path.GetExtension(path).ToLowerInvariant();

            string contentType = extension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",

                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",

                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",

                _ => "application/octet-stream"
            };

            Response.Headers.ContentDisposition =
                $"inline; filename=\"{Path.GetFileName(path)}\"";

            return PhysicalFile(path, contentType);
        }
        [HttpGet("spreadsheet")]
        public IActionResult Spreadsheet(string path)
        {
            return PhysicalFile(
                path,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }
        #endregion

        #region OTP & Password Management

        public class VerifyOtpDto
        {
            public string Email { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
            public string Plant { get; set; } = string.Empty;
        }

        public class ResetPasswordDto
        {
            public string Email { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
            public string Plant { get; set; } = string.Empty;
        }

        public class PasswordRuleResult
        {
            public bool MinLength { get; set; }
            public bool HasUpper { get; set; }
            public bool HasLower { get; set; }
            public bool HasDigit { get; set; }
            public bool NoInvalidChars { get; set; }
            public bool NotLast3Passwords { get; set; }
            public bool ValidEmailDomain { get; set; }
            public bool ChangedAfter30Days { get; set; }
            public bool AllPassed => MinLength && HasUpper && HasLower && HasDigit && NoInvalidChars && NotLast3Passwords && ChangedAfter30Days;
        }

        public class resetpasswordresponse
        {
            public string result { get; set; } = string.Empty;
            public bool redirect { get; set; } = false;
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] string email)
        {
            var otp = Random.Shared.Next(100000, 999999).ToString();
            _context.PasswordResetOtps.Add(new PasswordResetOtp
            {
                Email = email,
                Otp = otp,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false
            });
            await _context.SaveChangesAsync();

            var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential("cvsplotp@gmail.com", "dumdaovjmnitjbpl"),
                EnableSsl = true
            };
            await smtp.SendMailAsync("cvsplotp@gmail.com", email, "Your OTP", $"Your OTP is {otp}. It expires in 5 minutes.");

            return Ok();
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            bool valid = await _context.PasswordResetOtps.AnyAsync(x =>
                x.Email == dto.Email && x.Otp == dto.Otp && x.ExpiresAt > DateTime.UtcNow && x.IsUsed == false);

            if (!valid) return Ok("Invalid or expired OTP");
            return Ok();
        }

        public static bool IsPasswordValid(string password)
        {
            bool hasLower = false;
            bool hasUpper = false;
            bool hasDigit = false;
            bool hasSpace = false;

            if (string.IsNullOrEmpty(password) || password.Length < 8) return false;

            foreach (char c in password)
            {
                if (char.IsLower(c)) hasLower = true;
                else if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsDigit(c)) hasDigit = true;

                if (c == ' ' || c == '#' || c == '$') hasSpace = true;
            }
            return hasLower && hasUpper && hasDigit && !hasSpace;
        }

        [HttpPost("validate-password")]
        public async Task<IActionResult> ValidatePassword([FromBody] ResetPasswordDto dto)
        {
            var oldLogins = await _logContext.Database.SqlQueryRaw<string>(
                $"SELECT TOP 3 Password as Value FROM Gen_mas_login WHERE Login_id = {{0}} ORDER BY LDate_time DESC", dto.Email).ToListAsync();

            bool found = false;
            foreach (var oldpassword in oldLogins)
            {
                string Curpwd_decrypt = "";
                foreach (char c in oldpassword) Curpwd_decrypt += (char)(c - 120);
                if (Curpwd_decrypt == dto.NewPassword) { found = true; break; }
            }

            var result = new PasswordRuleResult
            {
                MinLength = dto.NewPassword?.Length >= 8,
                HasUpper = dto.NewPassword!.Any(char.IsUpper),
                HasLower = dto.NewPassword.Any(char.IsLower),
                HasDigit = dto.NewPassword.Any(char.IsDigit),
                NoInvalidChars = !dto.NewPassword.Any(c => c == ' ' || c == '\'' || c == '"' || c == '#' || c == '$'),
                ValidEmailDomain = dto.Email.EndsWith("@delphitvs.com", StringComparison.OrdinalIgnoreCase),
                ChangedAfter30Days = true,
                NotLast3Passwords = !found
            };

            return Ok(result);
        }

        [HttpPost("reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            resetpasswordresponse rpr = new resetpasswordresponse();

            var otpRecord = await _context.PasswordResetOtps.FirstOrDefaultAsync(x =>
                x.Email == dto.Email && x.Otp == dto.Otp && x.ExpiresAt > DateTime.UtcNow && x.IsUsed == false);

            if (otpRecord == null)
            {
                rpr.result = "OTP invalid";
                return Ok(rpr);
            }

            otpRecord.IsUsed = true;
            await _context.SaveChangesAsync();

            if (!IsPasswordValid(dto.NewPassword))
            {
                rpr.result = "<u>Password rules:</u><br/>1.Password should be changed once in 30 days<br/>2.New password should not be selected from the last 3 passwords<br/>3.For Delphi-TVS employees, user id should end with @delphitvs.com<br/>4.Spaces, single quotes or double quotes or # or $ should not be used in password.<br/>5.Minimum password length should be 8<br/>6.Password should be combination of alphabets and numeric. Special characters can be used.<br/>7.There should be at least 1 capital letter, 1 small letter and 1 number.";
                return Ok(rpr);
            }

            var oldLogins = await _logContext.Database.SqlQueryRaw<string>(
                $"SELECT TOP 3 Password as Value FROM Gen_mas_login WHERE Login_id = {{0}} ORDER BY LDate_time DESC", dto.Email).ToListAsync();

            bool found = false;
            foreach (var oldpassword in oldLogins)
            {
                string Curpwd_decrypt = "";
                foreach (char c in oldpassword) Curpwd_decrypt += (char)(c - 120);
                if (Curpwd_decrypt == dto.NewPassword) { found = true; break; }
            }

            if (found)
            {
                rpr.result = "This password was used few days back. Please type a different password.";
                return Ok(rpr);
            }

            var user = await _context.GenMasLogins.FirstOrDefaultAsync(u => u.Login_id == dto.Email && u.Comp_code == dto.Plant);
            if (user != null)
            {
                string oldEncryptedPassword = user.Password;
                string Newpwd = "";
                foreach (char c in dto.NewPassword) Newpwd += (char)(c + 120);

                user.Password = Newpwd;
                user.Last_login = DateTime.Now;
                await _context.SaveChangesAsync();

                // Log to LogDB
                string logSql = $@"
                    INSERT INTO Gen_mas_login (LLogin_id, LHost_name, LDate_time, LManipulation, Comp_code, Login_id, Password, Emp_no, Valid_from, Valid_to, Last_login, Expand_all_while_loading)
                    VALUES (@Lid, @Host, GETDATE(), 'U', @Comp, @Id, @OldPwd, @Emp, @Vf, @Vt, @Last, @Exp)";

                await _logContext.Database.ExecuteSqlRawAsync(logSql,
                    new SqlParameter("@Lid", dto.Email),
                    new SqlParameter("@Host", Dns.GetHostName()),
                    new SqlParameter("@Comp", dto.Plant),
                    new SqlParameter("@Id", dto.Email),
                    new SqlParameter("@OldPwd", oldEncryptedPassword),
                    new SqlParameter("@Emp", user.Emp_no),
                    new SqlParameter("@Vf", user.Valid_from),
                    new SqlParameter("@Vt", user.Valid_to),
                    new SqlParameter("@Last", user.Last_login ?? (object)DBNull.Value),
                    new SqlParameter("@Exp", user.Expand_all_while_loading ?? (object)DBNull.Value));

                rpr.result = "Password has been changed successfully. \nPlease Close the window and Enter the new password ";
                rpr.redirect = true;
                return Ok(rpr);
            }

            rpr.result = "Password does not match with our database records.";
            return Ok(rpr);
        }

        [HttpPost("reset_loggedin")]
        public async Task<IActionResult> ResetPassword_changeonly([FromBody] ResetPasswordDto dto)
        {
            resetpasswordresponse rpr = new resetpasswordresponse();

            if (!IsPasswordValid(dto.NewPassword))
            {
                rpr.result = "<u>Password rules:</u><br/>1.Password should be changed once in 30 days<br/>2.New password should not be selected from the last 3 passwords<br/>3.For Delphi-TVS employees, user id should end with @delphitvs.com<br/>4.Spaces, single quotes or double quotes or # or $ should not be used in password.<br/>5.Minimum password length should be 8<br/>6.Password should be combination of alphabets and numeric. Special characters can be used.<br/>7.There should be at least 1 capital letter, 1 small letter and 1 number.";
                return Ok(rpr);
            }

            var oldLogins = await _logContext.Database.SqlQueryRaw<string>(
                $"SELECT TOP 3 Password as Value FROM Gen_mas_login WHERE Login_id = {{0}} ORDER BY LDate_time DESC", dto.Email).ToListAsync();

            bool found = false;
            foreach (var oldpassword in oldLogins)
            {
                string Curpwd_decrypt = "";
                foreach (char c in oldpassword) Curpwd_decrypt += (char)(c - 120);
                if (Curpwd_decrypt == dto.NewPassword) { found = true; break; }
            }

            if (found)
            {
                rpr.result = "This password was used few days back. Please type a different password.";
                return Ok(rpr);
            }

            var user = await _context.GenMasLogins.FirstOrDefaultAsync(u => u.Login_id == dto.Email && u.Comp_code == dto.Plant);
            if (user != null)
            {
                string oldEncryptedPassword = user.Password;
                string Newpwd = "";
                foreach (char c in dto.NewPassword) Newpwd += (char)(c + 120);

                user.Password = Newpwd;
                user.Last_login = DateTime.Now;
                await _context.SaveChangesAsync();

                string logSql = $@"
                    INSERT INTO Gen_mas_login (LLogin_id, LHost_name, LDate_time, LManipulation, Comp_code, Login_id, Password, Emp_no, Valid_from, Valid_to, Last_login, Expand_all_while_loading)
                    VALUES (@Lid, @Host, GETDATE(), 'U', @Comp, @Id, @OldPwd, @Emp, @Vf, @Vt, @Last, @Exp)";

                await _logContext.Database.ExecuteSqlRawAsync(logSql,
                    new SqlParameter("@Lid", dto.Email),
                    new SqlParameter("@Host", Dns.GetHostName()),
                    new SqlParameter("@Comp", dto.Plant),
                    new SqlParameter("@Id", dto.Email),
                    new SqlParameter("@OldPwd", oldEncryptedPassword),
                    new SqlParameter("@Emp", user.Emp_no),
                    new SqlParameter("@Vf", user.Valid_from),
                    new SqlParameter("@Vt", user.Valid_to),
                    new SqlParameter("@Last", user.Last_login ?? (object)DBNull.Value),
                    new SqlParameter("@Exp", user.Expand_all_while_loading ?? (object)DBNull.Value));

                rpr.result = "Password has been changed successfully. \nPlease Close the window and Enter the new password ";
                rpr.redirect = true;
                return Ok(rpr);
            }

            rpr.result = "Password does not match with our database records.";
            return Ok(rpr);
        }

        #endregion

        #region Reporting & Google Auth

        public class ConcreteReportItem : ReportItem
        {
            public override object Value => throw new NotImplementedException();
            public decimal Price { get; set; }
            public int Qty { get; set; }
            public string Description { get; set; }
            public decimal Total => Price * Qty;
        }

        public class MyReportResponse
        {
            public string PdfBase64 { get; set; }
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetReport()
        {
            var report = new LocalReport();
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Reports", "Report.rdlc");
            using var stream = System.IO.File.OpenRead(path);
            report.LoadReportDefinition(new StreamReader(stream));

            var items = new List<ConcreteReportItem>
            {
                new ConcreteReportItem { Description = "Test", Price = 10, Qty = 2 }
            };

            report.DataSources.Add(new ReportDataSource("Items", items));
            var pdfBytes = report.Render("PDF");
            var pdfPath = Path.Combine("Reports", $"{DateTime.Now.Ticks.ToString()}-TempReport.pdf");

            System.IO.File.WriteAllBytes(pdfPath, pdfBytes);
            await using (var pdfStream = System.IO.File.OpenRead(pdfPath))
            {
                using (var memoryStream = new MemoryStream())
                {
                    await pdfStream.CopyToAsync(memoryStream);
                    pdfBytes = memoryStream.ToArray();
                }
            }
            var pdfBase64 = Convert.ToBase64String(pdfBytes);
            System.IO.File.Delete(pdfPath);
            return Ok(new MyReportResponse { PdfBase64 = pdfBase64 });
        }

        [HttpGet("login-google")]
        public IActionResult LoginGoogle()
        {
            var properties = new AuthenticationProperties { RedirectUri = "/google-callback" };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("login1-google")]
        public IActionResult signingoogle()
        {
            return Redirect("http://localhost:7008/IHDMaster_1");
        }

        [HttpGet("google-callback-2/{mail}")]
        public async Task<IActionResult> GoogleCallback2(string mail)
        {
            if (!string.IsNullOrEmpty(mail))
            {
                var loginid = await _context.Database.SqlQueryRaw<string>(
                    "SELECT Login_id as Value FROM Gen_mas_Login WHERE Emp_no IN (SELECT Emp_no FROM Pay_mas_employee WHERE Corporate_Email = {0})", mail).FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(loginid))
                {
                    var result_ = await _formFactor.ValidateLogin("E", loginid, loginid + (char)4);
                    return Ok(result_);
                }
            }
            return Ok(null);
        }

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
                return Redirect("/login?error=google_auth_failed");

            var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;

            if (!string.IsNullOrEmpty(email))
            {
                var loginid = await _context.Database.SqlQueryRaw<string>(
                    "SELECT Login_id as Value FROM Gen_mas_Login WHERE Emp_no IN (SELECT Emp_no FROM Pay_mas_employee WHERE Corporate_Email = {0})", email).FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(loginid))
                {
                    var claims = new List<Claim>
                    {
                        new Claim("loginid", loginid),
                        new Claim(ClaimTypes.Email, email)
                    };
                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                    return Redirect("http://localhost:7008/IHDMaster_1");
                }
            }
            return Redirect("/login?error=user_not_found");
        }

        #endregion

        #region Search & Weights

        // Helper class to safely map SQL results regardless of missing/null columns
        private class SearchResultDbDto
        {
            public string? ItemName { get; set; }
            public string? SubText { get; set; }
            public string? ItemType { get; set; }
            public string? ItemKey { get; set; }
            public int WeightScore { get; set; }
            public string? TargetUrl { get; set; }
            public string? WeightCategory { get; set; }
            public int PosX { get; set; }
            public int PosY { get; set; }
        }

        [HttpGet("search")]
        public async Task<IActionResult> GetWeightedSearchResults([FromQuery] string? compCode, [FromQuery] string? term = "", [FromQuery] string? dateFormat = "dd-MM-yyyy")
        {




            /*get short date format*/
            // 1. Get the raw Accept-Language header (e.g., "en-GB,en-US;q=0.9")
            var acceptLanguage = Request.Headers["Accept-Language"].ToString();

            // 2. Extract the primary locale (e.g., "en-GB")
            var primaryLanguage = acceptLanguage.Split(',').FirstOrDefault()?.Split(';').FirstOrDefault();

            if (string.IsNullOrWhiteSpace(primaryLanguage))
            {
                primaryLanguage = CultureInfo.CurrentCulture.Name; // Fallback
            }

            // 3. Get the short date format for that culture
            try
            {
                var culture = new CultureInfo(primaryLanguage);
                var shortDateFormat = culture.DateTimeFormat.ShortDatePattern;
                dateFormat= culture.DateTimeFormat.ShortDatePattern;


            }
            catch (CultureNotFoundException)
            {
                
            }

















            string searchTerm = term ?? "";

            var results = await _context.Database.SqlQueryRaw<SearchResultDbDto>(
                "EXEC sp_GetWeightedSearchResults @SearchTerm, @CompCode, @ClientDateFormat",
                new SqlParameter("@SearchTerm", searchTerm),
                new SqlParameter("@CompCode", compCode ?? ""),
                new SqlParameter("@ClientDateFormat", dateFormat ?? "dd-MM-yyyy")
            ).ToListAsync();

            List<SearchResultDto> srd = new List<SearchResultDto>();
            foreach (var result in results)
            {
                srd.Add(new SearchResultDto
                {
                    ItemName = result.ItemName ?? "",
                    SubText = result.SubText ?? "",
                    ItemType = result.ItemType ?? "",
                    ItemKey = result.ItemKey ?? "",
                    WeightScore = result.WeightScore,
                    TargetUrl = result.TargetUrl ?? "",
                    WeightCategory = result.WeightCategory ?? "",
                    PosX = result.PosX,
                    PosY = result.PosY
                });
            }

            return Ok(srd);
        }

        public class UpdateWeightRequest
        {
            public string ItemType { get; set; } = string.Empty;
            public string ItemKey { get; set; } = string.Empty;
            public string ItemName { get; set; } = string.Empty;
            public string TargetUrl { get; set; } = string.Empty;
        }

        [HttpPost("search/weight")]
        public async Task<IActionResult> IncrementClickWeight([FromBody] UpdateWeightRequest request)
        {
            var weight = await _context.GenTraSearchWeights
                .FirstOrDefaultAsync(w => w.Item_Type == request.ItemType && w.Item_Key == request.ItemKey);

            if (weight != null)
            {
                weight.Click_Count += 1;
                weight.Last_Clicked = DateTime.Now;
            }
            else
            {
                _context.GenTraSearchWeights.Add(new GenTraSearchWeight
                {
                    Item_Type = request.ItemType,
                    Item_Key = request.ItemKey,
                    Item_Name = request.ItemName,
                    Target_Url = request.TargetUrl,
                    Click_Count = 1,
                    Last_Clicked = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("topweighted")]
        public async Task<IActionResult> GetTopWeighted()
        {
            var results = await _context.Database.SqlQueryRaw<SearchResultDbDto>("EXEC sp_GetTopWeightedItems").ToListAsync();

            List<SearchResultDto> srd = new List<SearchResultDto>();
            foreach (var result in results)
            {
                srd.Add(new SearchResultDto
                {
                    ItemName = result.ItemName ?? "",
                    ItemType = result.ItemType ?? "",
                    ItemKey = result.ItemKey ?? "",
                    WeightScore = result.WeightScore,
                    TargetUrl = result.TargetUrl ?? "",
                    WeightCategory = result.WeightCategory ?? ""
                });
            }
            return Ok(srd);
        }

        #endregion

        #region Chat Endpoints

        [HttpGet("chat/users/{currentUserId}")]
        public async Task<IActionResult> GetChatUsers(string currentUserId)
        {
            try
            {
                var timeLimit = DateTime.Now.AddMinutes(-15);
                var users = await _context.GenMasLogins
                    .Where(l => l.Login_id != currentUserId)
                    .Select(l => new ChatUserDto
                    {
                        LoginId = l.Login_id,
                        IsOnline = l.Last_login != null && l.Last_login >= timeLimit,
                        UnreadCount = _context.GenTraChatMessages.Count(m => m.SenderId == l.Login_id && m.ReceiverId == currentUserId && m.IsRead == false)
                    }).ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("chat/markread")]
        public async Task<IActionResult> MarkMessagesAsRead([FromBody] MarkReadDto request)
        {
            var unread = await _context.GenTraChatMessages
                .Where(m => m.SenderId == request.SenderId && m.ReceiverId == request.ReceiverId && m.IsRead == false)
                .ToListAsync();

            foreach (var msg in unread) msg.IsRead = true;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group(request.SenderId).SendAsync("MessagesMarkedRead", request.ReceiverId);
            await _hubContext.Clients.Group(request.SenderId).SendAsync("MessagesMarkedAsRead", request.ReceiverId);
            return Ok();
        }

        [HttpGet("chat/messages/{user1}/{user2}")]
        public async Task<IActionResult> GetChatMessages(string user1, string user2, [FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            var list = await _context.GenTraChatMessages
                .Where(m => (m.SenderId == user1 && m.ReceiverId == user2) || (m.SenderId == user2 && m.ReceiverId == user1))
                .OrderByDescending(m => m.Timestamp)
                .Skip(skip).Take(take)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    ReceiverId = m.ReceiverId,
                    MessageContent = m.MessageContent,
                    Timestamp = m.Timestamp ?? DateTime.Now,
                    IsRead = m.IsRead ?? false
                })
                .ToListAsync();

            list.Reverse();
            return Ok(list);
        }

        [HttpPost("chat/send")]
        public async Task<IActionResult> SendChatMessage([FromBody] ChatMessageDto msg)
        {
            _context.GenTraChatMessages.Add(new GenTraChatMessage
            {
                SenderId = msg.SenderId,
                ReceiverId = msg.ReceiverId,
                MessageContent = msg.MessageContent,
                Timestamp = DateTime.Now,
                IsRead = false
            });
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group(msg.ReceiverId).SendAsync("ReceiveMessage", msg.SenderId, msg.MessageContent);
            return Ok();
        }

        #endregion

        #region Paginated Complaints

        public class FilterCondition
        {
            public string SelectedColumn { get; set; } = "Complaint_id";
            public string SearchValue { get; set; } = "";
            public string? SearchValueTo { get; set; } // <--- ADD THIS PROPERTY FOR IN-RANGE UPPER BOUND
            public string NextLogicalOperator { get; set; } = "AND";
            public bool IsNegated { get; set; } = false;
            public string FilterOperator { get; set; } = "Contains";
        }

        //    [HttpGet("complaints/paginated/{compCode}")]
        //    public async Task<IActionResult> GetPaginatedComplaints(
        //string compCode,
        //[FromQuery] int pageNumber = 1,
        //[FromQuery] int pageSize = 10,
        //[FromQuery] string? searchCols = "",
        //[FromQuery] string? sortCols = "Complaint_id:DESC",
        //[FromQuery] string? searchTerm = "",
        //[FromQuery] string? sortDirection = "")
        //    {
        //        try
        //        {
        //            // 1. Start with base query using Include to join Cbs_mas_clientproject
        //            var query = _context.CbsTraNewComplaints
        //                .Include(c => c.ClientProject)
        //                .Where(c => c.Comp_code == compCode);

        //            // 2. Handle Dynamic Grid Filters (AgGrid/FluentUI Advanced Filtering JSON)
        //            if (!string.IsNullOrWhiteSpace(searchCols) && searchCols.Trim().StartsWith("["))
        //            {
        //                try
        //                {
        //                    var expressions = System.Text.Json.JsonSerializer.Deserialize<List<FilterCondition>>(
        //                        searchCols,
        //                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        //                    );

        //                    if (expressions != null && expressions.Any())
        //                    {
        //                        foreach (var expr in expressions)
        //                        {
        //                            if (string.IsNullOrWhiteSpace(expr.SearchValue)) continue;
        //                            string val = expr.SearchValue.Trim().ToLower();

        //                            // Build filtering expression matching columns dynamically
        //                            switch (expr.SelectedColumn.ToLower())
        //                            {
        //                                case "complaint_id":
        //                                    if (int.TryParse(val, out int idVal))
        //                                        query = query.Where(c => c.Complaint_id == idVal);
        //                                    break;
        //                                case "client_code":
        //                                    query = query.Where(c => c.Client_code.ToLower().Contains(val));
        //                                    break;
        //                                case "project_code":
        //                                    query = query.Where(c => c.Project_code.ToLower().Contains(val));
        //                                    break;
        //                                case "project_name":
        //                                    query = query.Where(c => c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(val));
        //                                    break;
        //                                case "issue_description":
        //                                    query = query.Where(c => c.Issue_description.ToLower().Contains(val));
        //                                    break;
        //                                case "issue_raised_by":
        //                                    query = query.Where(c => c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(val));
        //                                    break;
        //                            }
        //                        }
        //                    }
        //                }
        //                catch (System.Text.Json.JsonException jsonEx)
        //                {
        //                    _logger.LogWarning(jsonEx, "Failed parsing search filters array JSON payload.");
        //                }
        //            }
        //            // 3. Handle Simple Text Search Term Fallback
        //            else if (!string.IsNullOrWhiteSpace(searchTerm))
        //            {
        //                string term = searchTerm.Trim().ToLower();
        //                query = query.Where(c =>
        //                    c.Client_code.ToLower().Contains(term) ||
        //                    c.Project_code.ToLower().Contains(term) ||
        //                    c.Issue_description.ToLower().Contains(term) ||
        //                    (c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(term)) ||
        //                    (c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(term))
        //                );
        //            }

        //            // 4. Calculate total records safely from the filter configuration
        //            int totalRecords = await query.CountAsync();

        //            // 5. Apply Dynamic Sorting
        //            if (!string.IsNullOrWhiteSpace(sortCols))
        //            {
        //                var parts = sortCols.Split(':');
        //                string sortField = parts[0].Trim().ToLower();
        //                bool isDesc = parts.Length > 1 && parts[1].Trim().Equals("DESC", StringComparison.OrdinalIgnoreCase);

        //                switch (sortField)
        //                {
        //                    case "complaint_id": query = isDesc ? query.OrderByDescending(c => c.Complaint_id) : query.OrderBy(c => c.Complaint_id); break;
        //                    case "client_code": query = isDesc ? query.OrderByDescending(c => c.Client_code) : query.OrderBy(c => c.Client_code); break;
        //                    case "project_code": query = isDesc ? query.OrderByDescending(c => c.Project_code) : query.OrderBy(c => c.Project_code); break;
        //                    case "project_name": query = isDesc ? query.OrderByDescending(c => c.ClientProject!.Project_name) : query.OrderBy(c => c.ClientProject!.Project_name); break;
        //                    case "issue_description": query = isDesc ? query.OrderByDescending(c => c.Issue_description) : query.OrderBy(c => c.Issue_description); break;
        //                    case "inserted_dt": query = isDesc ? query.OrderByDescending(c => c.Inserted_dt) : query.OrderBy(c => c.Inserted_dt); break;
        //                    case "issue_raised_by": query = isDesc ? query.OrderByDescending(c => c.Issue_raised_by) : query.OrderBy(c => c.Issue_raised_by); break;
        //                    default: query = query.OrderByDescending(c => c.Complaint_id); break;
        //                }
        //            }
        //            else
        //            {
        //                query = query.OrderByDescending(c => c.Complaint_id);
        //            }

        //            // 6. Pagination & Data Projection (Select)
        //            int skip = (pageNumber - 1) * pageSize;
        //            var paginatedList = await query
        //                .Skip(skip)
        //                .Take(pageSize)
        //                .Select(c => new ComplaintDto
        //                {
        //                    Complaint_id = c.Complaint_id,
        //                    Comp_code = c.Comp_code,
        //                    Client_code = c.Client_code,
        //                    Project_code = c.Project_code,
        //                    Project_name = c.ClientProject != null ? c.ClientProject.Project_name : "",
        //                    Issue_description = c.Issue_description,
        //                    Project_start_date = c.Project_start_date,
        //                    Project_completion_date = c.Project_completion_date,
        //                    Amc_start_date = c.Amc_start_date,
        //                    Amc_finish_date = c.Amc_finish_date,
        //                    AMC_Start_dt = c.Amc_start_date,
        //                    AMC_End_dt = c.Amc_finish_date,
        //                    Support_doc1 = c.Support_doc1,
        //                    Support_doc2 = c.Support_doc2,
        //                    Support_doc3 = c.Support_doc3,
        //                    Support_doc4 = c.Support_doc4,
        //                    Support_doc5 = c.Support_doc5,
        //                    Issue_raised_by = c.Issue_raised_by,
        //                    Issue_booked_date = c.Issue_booked_date,
        //                    Issue_allotted_to = c.Issue_allotted_to,
        //                    Issue_allotted_date = c.Issue_allotted_date,
        //                    Issue_closed_date = c.Issue_closed_date,
        //                    Inserted_dt = c.Inserted_dt,
        //                    Start_dt = c.ClientProject != null ? c.ClientProject.Start_dt : null,
        //                    Completed_dt = c.ClientProject != null ? c.ClientProject.Completed_dt : null
        //                })
        //                .ToListAsync();

        //            return Ok(new PaginatedResult<ComplaintDto> { Data = paginatedList, TotalRecords = totalRecords });
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "Error processing paginated complaints list.");
        //            return StatusCode(500, $"Internal server error: {ex.Message}");
        //        }
        //    }
        //[HttpGet("complaints/paginated/{compCode}")]
        //public async Task<IActionResult> GetPaginatedComplaints(
        //    string compCode,
        //    [FromQuery] int pageNumber = 1,
        //    [FromQuery] int pageSize = 10,
        //    [FromQuery] string? searchCols = "",
        //    [FromQuery] string? sortCols = "", // Now expects format like "client_code asc, project_code desc"
        //    [FromQuery] string? searchTerm = "",
        //    [FromQuery] string? sortDirection = "")
        //{
        //    try
        //    {
        //        // 1. Start with base query using Include to join Cbs_mas_clientproject
        //        var query = _context.CbsTraNewComplaints
        //            .Include(c => c.ClientProject)
        //            .Where(c => c.Comp_code == compCode);

        //        // 2. Handle Dynamic Grid Filters (Supports Multiple Filters via AND logic)
        //        if (!string.IsNullOrWhiteSpace(searchCols) && searchCols.Trim().StartsWith("["))
        //        {
        //            try
        //            {
        //                var expressions = System.Text.Json.JsonSerializer.Deserialize<List<FilterCondition>>(
        //                    searchCols,
        //                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        //                );

        //                if (expressions != null && expressions.Any())
        //                {
        //                    foreach (var expr in expressions)
        //                    {
        //                        if (string.IsNullOrWhiteSpace(expr.SearchValue)) continue;
        //                        string val = expr.SearchValue.Trim().ToLower();

        //                        // Build filtering expression matching columns dynamically
        //                        switch (expr.SelectedColumn.ToLower())
        //                        {
        //                            case "complaint_id":
        //                                if (int.TryParse(val, out int idVal))
        //                                    query = query.Where(c => c.Complaint_id == idVal);
        //                                break;
        //                            case "client_code":
        //                                query = query.Where(c => c.Client_code.ToLower().Contains(val));
        //                                break;
        //                            case "project_code":
        //                                query = query.Where(c => c.Project_code.ToLower().Contains(val));
        //                                break;
        //                            case "project_name":
        //                                query = query.Where(c => c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(val));
        //                                break;
        //                            case "issue_description":
        //                                query = query.Where(c => c.Issue_description.ToLower().Contains(val));
        //                                break;
        //                            case "issue_raised_by":
        //                                query = query.Where(c => c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(val));
        //                                break;
        //                        }
        //                    }
        //                }
        //            }
        //            catch (System.Text.Json.JsonException jsonEx)
        //            {
        //                _logger.LogWarning(jsonEx, "Failed parsing search filters array JSON payload.");
        //            }
        //        }

        //        // 3. Handle Simple Text Search Term Fallback
        //        else if (!string.IsNullOrWhiteSpace(searchTerm))
        //        {
        //            string term = searchTerm.Trim().ToLower();
        //            query = query.Where(c =>
        //                c.Client_code.ToLower().Contains(term) ||
        //                c.Project_code.ToLower().Contains(term) ||
        //                c.Issue_description.ToLower().Contains(term) ||
        //                (c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(term)) ||
        //                (c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(term))
        //            );
        //        }

        //        // 4. Calculate total records safely from the filter configuration
        //        int totalRecords = await query.CountAsync();

        //        // 5. Apply Dynamic MULTIPLE Sorting
        //        if (!string.IsNullOrWhiteSpace(sortCols))
        //        {
        //            // Split by comma to get each sort rule (e.g., ["client_code asc", "project_code desc"])
        //            var sortParams = sortCols.Split(',', StringSplitOptions.RemoveEmptyEntries);
        //            IOrderedQueryable<CbsTraNewComplaint>? orderedQuery = null;

        //            foreach (var sortParam in sortParams)
        //            {
        //                // Split the column and the direction
        //                var parts = sortParam.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries);
        //                if (parts.Length == 0) continue;

        //                string sortField = parts[0].ToLower();
        //                bool isDesc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        //                if (orderedQuery == null)
        //                {
        //                    // The FIRST sort column must use OrderBy / OrderByDescending
        //                    switch (sortField)
        //                    {
        //                        case "complaint_id": orderedQuery = isDesc ? query.OrderByDescending(c => c.Complaint_id) : query.OrderBy(c => c.Complaint_id); break;
        //                        case "client_code": orderedQuery = isDesc ? query.OrderByDescending(c => c.Client_code) : query.OrderBy(c => c.Client_code); break;
        //                        case "project_code": orderedQuery = isDesc ? query.OrderByDescending(c => c.Project_code) : query.OrderBy(c => c.Project_code); break;
        //                        case "project_name": orderedQuery = isDesc ? query.OrderByDescending(c => c.ClientProject!.Project_name) : query.OrderBy(c => c.ClientProject!.Project_name); break;
        //                        case "issue_description": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_description) : query.OrderBy(c => c.Issue_description); break;
        //                        case "inserted_dt": orderedQuery = isDesc ? query.OrderByDescending(c => c.Inserted_dt) : query.OrderBy(c => c.Inserted_dt); break;
        //                        case "issue_raised_by": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_raised_by) : query.OrderBy(c => c.Issue_raised_by); break;
        //                        default: orderedQuery = query.OrderByDescending(c => c.Complaint_id); break;
        //                    }
        //                }
        //                else
        //                {
        //                    // Any SUBSEQUENT sort columns must use ThenBy / ThenByDescending
        //                    switch (sortField)
        //                    {
        //                        case "complaint_id": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Complaint_id) : orderedQuery.ThenBy(c => c.Complaint_id); break;
        //                        case "client_code": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Client_code) : orderedQuery.ThenBy(c => c.Client_code); break;
        //                        case "project_code": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Project_code) : orderedQuery.ThenBy(c => c.Project_code); break;
        //                        case "project_name": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.ClientProject!.Project_name) : orderedQuery.ThenBy(c => c.ClientProject!.Project_name); break;
        //                        case "issue_description": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Issue_description) : orderedQuery.ThenBy(c => c.Issue_description); break;
        //                        case "inserted_dt": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Inserted_dt) : orderedQuery.ThenBy(c => c.Inserted_dt); break;
        //                        case "issue_raised_by": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Issue_raised_by) : orderedQuery.ThenBy(c => c.Issue_raised_by); break;
        //                    }
        //                }
        //            }

        //            // Override the base query with the newly ordered query
        //            query = orderedQuery ?? query.OrderByDescending(c => c.Complaint_id);
        //        }
        //        else
        //        {
        //            // Default fallback sorting
        //            query = query.OrderByDescending(c => c.Complaint_id);
        //        }

        //        // 6. Pagination & Data Projection (Select)
        //        int skip = (pageNumber - 1) * pageSize;
        //        var paginatedList = await query
        //            .Skip(skip)
        //            .Take(pageSize)
        //            .Select(c => new ComplaintDto
        //            {
        //                Complaint_id = c.Complaint_id,
        //                Comp_code = c.Comp_code,
        //                Client_code = c.Client_code,
        //                Project_code = c.Project_code,
        //                Project_name = c.ClientProject != null ? c.ClientProject.Project_name : "",
        //                Issue_description = c.Issue_description,
        //                Project_start_date = c.Project_start_date,
        //                Project_completion_date = c.Project_completion_date,
        //                Amc_start_date = c.Amc_start_date,
        //                Amc_finish_date = c.Amc_finish_date,
        //                AMC_Start_dt = c.Amc_start_date,
        //                AMC_End_dt = c.Amc_finish_date,
        //                Support_doc1 = c.Support_doc1,
        //                Support_doc2 = c.Support_doc2,
        //                Support_doc3 = c.Support_doc3,
        //                Support_doc4 = c.Support_doc4,
        //                Support_doc5 = c.Support_doc5,
        //                Issue_raised_by = c.Issue_raised_by,
        //                Issue_booked_date = c.Issue_booked_date,
        //                Issue_allotted_to = c.Issue_allotted_to,
        //                Issue_allotted_date = c.Issue_allotted_date,
        //                Issue_closed_date = c.Issue_closed_date,
        //                Inserted_dt = c.Inserted_dt,
        //                Start_dt = c.ClientProject != null ? c.ClientProject.Start_dt : null,
        //                Completed_dt = c.ClientProject != null ? c.ClientProject.Completed_dt : null
        //            })
        //            .ToListAsync();

        //        return Ok(new PaginatedResult<ComplaintDto> { Data = paginatedList, TotalRecords = totalRecords });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error processing paginated complaints list.");
        //        return StatusCode(500, $"Internal server error: {ex.Message}");
        //    }
        //}
        //private Expression BuildCondition(ParameterExpression param, FilterCondition expr)
        //{
        //    if (string.IsNullOrWhiteSpace(expr.SearchValue)) return null;
        //    string val = expr.SearchValue.Trim().ToLower();

        //    Expression property = null;
        //    Expression nullCheck = null;

        //    // 1. Resolve the Property based on the column name
        //    switch (expr.SelectedColumn.ToLower())
        //    {
        //        case "complaint_id":
        //            if (int.TryParse(val, out int idVal))
        //            {
        //                property = Expression.Property(param, "Complaint_id");
        //                return expr.FilterOperator == "Not Equals"
        //                    ? Expression.NotEqual(property, Expression.Constant(idVal))
        //                    : Expression.Equal(property, Expression.Constant(idVal));
        //            }
        //            return null;
        //        case "client_code":
        //            property = Expression.Property(param, "Client_code");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "project_code":
        //            property = Expression.Property(param, "Project_code");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "issue_description":
        //            property = Expression.Property(param, "Issue_description");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "issue_raised_by":
        //            property = Expression.Property(param, "Issue_raised_by");
        //            nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            break;
        //        case "project_name":
        //            // Handle Navigation Property Safely
        //            var clientProject = Expression.Property(param, "ClientProject");
        //            property = Expression.Property(clientProject, "Project_name");

        //            var navCheck = Expression.NotEqual(clientProject, Expression.Constant(null));
        //            var propCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        //            nullCheck = Expression.AndAlso(navCheck, propCheck);
        //            break;
        //        default:
        //            return null;
        //    }

        //    // 2. Prepare String Methods
        //    var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
        //    var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
        //    var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
        //    var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

        //    var searchConstant = Expression.Constant(val);
        //    var toLowerExp = Expression.Call(property, toLowerMethod); // Equivalent to c.Property.ToLower()

        //    // 3. Apply the specific Filter Operator
        //    Expression operation;
        //    switch (expr.FilterOperator)
        //    {
        //        case "Equals":
        //            operation = Expression.Equal(toLowerExp, searchConstant);
        //            break;
        //        case "Not Equals":
        //            operation = Expression.NotEqual(toLowerExp, searchConstant);
        //            break;
        //        case "Starts With":
        //            operation = Expression.Call(toLowerExp, startsWithMethod, searchConstant);
        //            break;
        //        case "Ends With":
        //            operation = Expression.Call(toLowerExp, endsWithMethod, searchConstant);
        //            break;
        //        case "Not Contains":
        //            operation = Expression.Not(Expression.Call(toLowerExp, containsMethod, searchConstant));
        //            break;
        //        case "Contains":
        //        default:
        //            operation = Expression.Call(toLowerExp, containsMethod, searchConstant);
        //            break;
        //    }

        //    // 4. Combine Null Check with the Operation (e.g. c.Property != null && c.Property.Contains("x"))
        //    return Expression.AndAlso(nullCheck, operation);
        //}
        private Expression BuildCondition(ParameterExpression param, FilterCondition expr)
        {
            if (string.IsNullOrWhiteSpace(expr.SearchValue)) return null;
            string val = expr.SearchValue.Trim();

            string col = expr.SelectedColumn.ToLower();

            Expression property = null;
            Expression nullCheck = null;
            Type propType = typeof(string);

            // 1. Resolve Property and Target Type based on the column name
            switch (col)
            {
                case "complaint_id":
                    if (int.TryParse(val, out int idVal))
                    {
                        property = Expression.Property(param, "Complaint_id");
                        return expr.FilterOperator == "Not Equals"
                            ? Expression.NotEqual(property, Expression.Constant(idVal))
                            : Expression.Equal(property, Expression.Constant(idVal));
                    }
                    return null;

                case "client_code":
                    property = Expression.Property(param, "Client_code");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "project_code":
                    property = Expression.Property(param, "Project_code");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "issue_description":
                    property = Expression.Property(param, "Issue_description");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "issue_raised_by":
                    property = Expression.Property(param, "Issue_raised_by");
                    nullCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    break;
                case "project_name":
                    var clientProject = Expression.Property(param, "ClientProject");
                    property = Expression.Property(clientProject, "Project_name");
                    var navCheck = Expression.NotEqual(clientProject, Expression.Constant(null));
                    var propCheck = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
                    nullCheck = Expression.AndAlso(navCheck, propCheck);
                    break;

                // --- DATE COLUMNS ---
                case "project_start_date":
                    property = Expression.Property(param, "Project_start_date");
                    propType = typeof(DateTime?);
                    break;
                case "project_completion_date":
                    property = Expression.Property(param, "Project_completion_date");
                    propType = typeof(DateTime?);
                    break;
                case "amc_start_date":
                case "amc_start_dt":
                    property = Expression.Property(param, "Amc_start_date");
                    propType = typeof(DateTime?);
                    break;
                case "amc_finish_date":
                case "amc_end_dt":
                    property = Expression.Property(param, "Amc_finish_date");
                    propType = typeof(DateTime?);
                    break;
                case "issue_booked_date":
                    property = Expression.Property(param, "Issue_booked_date");
                    propType = typeof(DateTime?);
                    break;
                case "issue_allotted_date":
                    property = Expression.Property(param, "Issue_allotted_date");
                    propType = typeof(DateTime?);
                    break;
                case "issue_closed_date":
                    property = Expression.Property(param, "Issue_closed_date");
                    propType = typeof(DateTime?);
                    break;
                case "inserted_dt":
                    property = Expression.Property(param, "Inserted_dt");
                    propType = typeof(DateTime?);
                    break;
                case "start_dt":
                    var cpStart = Expression.Property(param, "ClientProject");
                    property = Expression.Property(cpStart, "Start_dt");
                    nullCheck = Expression.NotEqual(cpStart, Expression.Constant(null));
                    propType = typeof(DateTime?);
                    break;
                case "completed_dt":
                    var cpComp = Expression.Property(param, "ClientProject");
                    property = Expression.Property(cpComp, "Completed_dt");
                    nullCheck = Expression.NotEqual(cpComp, Expression.Constant(null));
                    propType = typeof(DateTime?);
                    break;

                default:
                    return null;
            }

            // 2. Handle DATE Expression Trees
            // Inside BuildCondition method in AuthController.cs (Date processing section):

            if (propType == typeof(DateTime) || propType == typeof(DateTime?))
            {
                Expression dateProp = property;
                Expression dateHasValueCheck = null;

                if (Nullable.GetUnderlyingType(property.Type) != null)
                {
                    dateHasValueCheck = Expression.Property(property, "HasValue");
                    dateProp = Expression.Property(property, "Value");
                }

                Expression dateOperation = null;

                // --- HANDLE ATOMIC IN RANGE ---
                if (expr.FilterOperator == "In Range")
                {
                    if (DateTime.TryParse(expr.SearchValue, out DateTime rangeStart) &&
                        DateTime.TryParse(expr.SearchValueTo, out DateTime rangeEnd))
                    {
                        DateTime startBound = rangeStart.Date;
                        DateTime endBound = rangeEnd.Date.AddDays(1); // Full day coverage

                        Expression gteRange = Expression.GreaterThanOrEqual(dateProp, Expression.Constant(startBound, typeof(DateTime)));
                        Expression ltRange = Expression.LessThan(dateProp, Expression.Constant(endBound, typeof(DateTime)));

                        dateOperation = Expression.AndAlso(gteRange, ltRange);
                    }
                }
                else if (DateTime.TryParse(val, out DateTime parsedDate))
                {
                    DateTime dayStart = parsedDate.Date;
                    DateTime dayEnd = dayStart.AddDays(1);
                    Expression dayStartConst = Expression.Constant(dayStart, typeof(DateTime));
                    Expression dayEndConst = Expression.Constant(dayEnd, typeof(DateTime));

                    switch (expr.FilterOperator)
                    {
                        case "Equals":
                            dateOperation = Expression.AndAlso(
                                Expression.GreaterThanOrEqual(dateProp, dayStartConst),
                                Expression.LessThan(dateProp, dayEndConst)
                            );
                            break;

                        case "Not Equals":
                            dateOperation = Expression.Not(Expression.AndAlso(
                                Expression.GreaterThanOrEqual(dateProp, dayStartConst),
                                Expression.LessThan(dateProp, dayEndConst)
                            ));
                            break;

                        case "Greater Than":
                            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayEndConst);
                            break;

                        case "Greater Than Or Equal":
                            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
                            break;

                        case "Less Than":
                            dateOperation = Expression.LessThan(dateProp, dayStartConst);
                            break;

                        case "Less Than Or Equal":
                            dateOperation = Expression.LessThan(dateProp, dayEndConst);
                            break;

                        default:
                            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
                            break;
                    }
                }

                if (dateOperation == null) return null;

                Expression fullDateExpr = dateHasValueCheck != null
                    ? Expression.AndAlso(dateHasValueCheck, dateOperation)
                    : dateOperation;

                return nullCheck != null ? Expression.AndAlso(nullCheck, fullDateExpr) : fullDateExpr;
            }
            //if (propType == typeof(DateTime) || propType == typeof(DateTime?))
            //{
            //    if (!DateTime.TryParse(val, out DateTime parsedDate)) return null;

            //    Expression dateProp = property;
            //    Expression dateHasValueCheck = null;

            //    // Unwrap Nullable<DateTime> safely for EF Core queries
            //    if (Nullable.GetUnderlyingType(property.Type) != null)
            //    {
            //        dateHasValueCheck = Expression.Property(property, "HasValue");
            //        dateProp = Expression.Property(property, "Value");
            //    }

            //    // Calculate whole-day boundaries to match database timestamps seamlessly
            //    DateTime dayStart = parsedDate.Date;
            //    DateTime dayEnd = dayStart.AddDays(1);
            //    Expression dayStartConst = Expression.Constant(dayStart, typeof(DateTime));
            //    Expression dayEndConst = Expression.Constant(dayEnd, typeof(DateTime));

            //    Expression dateOperation;

            //    switch (expr.FilterOperator)
            //    {
            //        case "Equals":
            //            // c.Date >= dayStart AND c.Date < dayEnd
            //            var gte = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            var lt = Expression.LessThan(dateProp, dayEndConst);
            //            dateOperation = Expression.AndAlso(gte, lt);
            //            break;

            //        case "Not Equals":
            //            // !(c.Date >= dayStart AND c.Date < dayEnd)
            //            var gteN = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            var ltN = Expression.LessThan(dateProp, dayEndConst);
            //            dateOperation = Expression.Not(Expression.AndAlso(gteN, ltN));
            //            break;

            //        case "Greater Than":
            //            // c.Date >= dayEnd (strictly after this entire day)
            //            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayEndConst);
            //            break;

            //        case "Greater Than Or Equal":
            //            // c.Date >= dayStart
            //            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            break;

            //        case "Less Than":
            //            // c.Date < dayStart
            //            dateOperation = Expression.LessThan(dateProp, dayStartConst);
            //            break;

            //        case "Less Than Or Equal":
            //            // c.Date < dayEnd (includes up to 23:59:59 of this day)
            //            dateOperation = Expression.LessThan(dateProp, dayEndConst);
            //            break;

            //        default:
            //            dateOperation = Expression.GreaterThanOrEqual(dateProp, dayStartConst);
            //            break;
            //    }

            //    Expression fullDateExpr = dateHasValueCheck != null
            //        ? Expression.AndAlso(dateHasValueCheck, dateOperation)
            //        : dateOperation;

            //    return nullCheck != null ? Expression.AndAlso(nullCheck, fullDateExpr) : fullDateExpr;
            //}

            // 3. Handle STRING Expression Trees
            val = val.ToLower();
            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
            var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

            var searchConstant = Expression.Constant(val);
            var toLowerExp = Expression.Call(property, toLowerMethod);

            Expression operation;
            switch (expr.FilterOperator)
            {
                case "Equals":
                    operation = Expression.Equal(toLowerExp, searchConstant);
                    break;
                case "Not Equals":
                    operation = Expression.NotEqual(toLowerExp, searchConstant);
                    break;
                case "Starts With":
                    operation = Expression.Call(toLowerExp, startsWithMethod, searchConstant);
                    break;
                case "Ends With":
                    operation = Expression.Call(toLowerExp, endsWithMethod, searchConstant);
                    break;
                case "Not Contains":
                    operation = Expression.Not(Expression.Call(toLowerExp, containsMethod, searchConstant));
                    break;
                case "Contains":
                default:
                    operation = Expression.Call(toLowerExp, containsMethod, searchConstant);
                    break;
            }

            return nullCheck != null ? Expression.AndAlso(nullCheck, operation) : operation;
        }
        [HttpGet("complaints/paginated/{compCode}")]
        public async Task<IActionResult> GetPaginatedComplaints(
    string compCode,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? searchCols = "",
    [FromQuery] string? sortCols = "",
    [FromQuery] string? searchTerm = "",
    [FromQuery] string? sortDirection = "")
        {
            try
            {
                // 1. Start with base query using Include to join Cbs_mas_clientproject
                var query = _context.CbsTraNewComplaints
                    .Include(c => c.ClientProject)
                    .Where(c => c.Comp_code == compCode);

                // 2. Handle Dynamic Grid Filters using Expression Trees (Supports ALL AG Grid constraints + AND/OR)
                if (!string.IsNullOrWhiteSpace(searchCols) && searchCols.Trim().StartsWith("["))
                {
                    try
                    {
                        var expressions = System.Text.Json.JsonSerializer.Deserialize<List<FilterCondition>>(
                            searchCols,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        if (expressions != null && expressions.Any())
                        {
                            // Create the parameter 'c' -> c => ...
                            var parameter = Expression.Parameter(typeof(CbsTraNewComplaint), "c");
                            Expression finalExpression = null;
                            string pendingLogic = "AND"; // Default starting connector

                            foreach (var expr in expressions)
                            {
                                // Build the individual rule (e.g. c.Client_code.StartsWith("a"))
                                Expression currentCondition = BuildCondition(parameter, expr);
                                if (currentCondition == null) continue;

                                // Combine it with the main query
                                if (finalExpression == null)
                                {
                                    finalExpression = currentCondition;
                                }
                                else
                                {
                                    // Chain via AND / OR based on the PREVIOUS rule's operator
                                    if (pendingLogic == "OR")
                                        finalExpression = Expression.OrElse(finalExpression, currentCondition);
                                    else
                                        finalExpression = Expression.AndAlso(finalExpression, currentCondition);
                                }

                                // Store this rule's connector to link to the next row
                                pendingLogic = expr.NextLogicalOperator?.ToUpper() == "OR" ? "OR" : "AND";
                            }

                            // If we successfully built a tree, attach it to the query
                            if (finalExpression != null)
                            {
                                var lambda = Expression.Lambda<Func<CbsTraNewComplaint, bool>>(finalExpression, parameter);
                                query = query.Where(lambda);
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Failed parsing search filters array JSON payload.");
                    }
                }

                // 3. Handle Simple Text Search Term Fallback
                else if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    string term = searchTerm.Trim().ToLower();
                    query = query.Where(c =>
                        c.Client_code.ToLower().Contains(term) ||
                        c.Project_code.ToLower().Contains(term) ||
                        c.Issue_description.ToLower().Contains(term) ||
                        (c.ClientProject != null && c.ClientProject.Project_name!.ToLower().Contains(term)) ||
                        (c.Issue_raised_by != null && c.Issue_raised_by.ToLower().Contains(term))
                    );
                }

                // 4. Calculate total records
                int totalRecords = await query.CountAsync();

                // 5. Apply Dynamic MULTIPLE Sorting
                if (!string.IsNullOrWhiteSpace(sortCols))
                {
                    var sortParams = sortCols.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    IOrderedQueryable<CbsTraNewComplaint>? orderedQuery = null;

                    foreach (var sortParam in sortParams)
                    {
                        var parts = sortParam.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;

                        string sortField = parts[0].ToLower();
                        bool isDesc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

                        if (orderedQuery == null)
                        {
                            switch (sortField)
                            {
                                case "complaint_id": orderedQuery = isDesc ? query.OrderByDescending(c => c.Complaint_id) : query.OrderBy(c => c.Complaint_id); break;
                                case "client_code": orderedQuery = isDesc ? query.OrderByDescending(c => c.Client_code) : query.OrderBy(c => c.Client_code); break;
                                case "project_code": orderedQuery = isDesc ? query.OrderByDescending(c => c.Project_code) : query.OrderBy(c => c.Project_code); break;
                                case "project_name": orderedQuery = isDesc ? query.OrderByDescending(c => c.ClientProject!.Project_name) : query.OrderBy(c => c.ClientProject!.Project_name); break;
                                case "issue_description": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_description) : query.OrderBy(c => c.Issue_description); break;
                                //case "inserted_dt": orderedQuery = isDesc ? query.OrderByDescending(c => c.Inserted_dt) : query.OrderBy(c => c.Inserted_dt); break;
                                case "issue_raised_by": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_raised_by) : query.OrderBy(c => c.Issue_raised_by); break;
                                // --- ADDED DATE SORTING ---
                                case "project_start_date": orderedQuery = isDesc ? query.OrderByDescending(c => c.Project_start_date) : query.OrderBy(c => c.Project_start_date); break;
                                case "project_completion_date": orderedQuery = isDesc ? query.OrderByDescending(c => c.Project_completion_date) : query.OrderBy(c => c.Project_completion_date); break;
                                case "amc_start_date": case "amc_start_dt": orderedQuery = isDesc ? query.OrderByDescending(c => c.Amc_start_date) : query.OrderBy(c => c.Amc_start_date); break;
                                case "amc_finish_date": case "amc_end_dt": orderedQuery = isDesc ? query.OrderByDescending(c => c.Amc_finish_date) : query.OrderBy(c => c.Amc_finish_date); break;
                                case "issue_booked_date": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_booked_date) : query.OrderBy(c => c.Issue_booked_date); break;
                                case "issue_allotted_date": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_allotted_date) : query.OrderBy(c => c.Issue_allotted_date); break;
                                case "issue_closed_date": orderedQuery = isDesc ? query.OrderByDescending(c => c.Issue_closed_date) : query.OrderBy(c => c.Issue_closed_date); break;
                                case "inserted_dt": orderedQuery = isDesc ? query.OrderByDescending(c => c.Inserted_dt) : query.OrderBy(c => c.Inserted_dt); break;
                                default: orderedQuery = query.OrderByDescending(c => c.Complaint_id); break;
                            }
                        }
                        else
                        {
                            switch (sortField)
                            {
                                case "complaint_id": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Complaint_id) : orderedQuery.ThenBy(c => c.Complaint_id); break;
                                case "client_code": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Client_code) : orderedQuery.ThenBy(c => c.Client_code); break;
                                case "project_code": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Project_code) : orderedQuery.ThenBy(c => c.Project_code); break;
                                case "project_name": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.ClientProject!.Project_name) : orderedQuery.ThenBy(c => c.ClientProject!.Project_name); break;
                                case "issue_description": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Issue_description) : orderedQuery.ThenBy(c => c.Issue_description); break;
                                case "inserted_dt": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Inserted_dt) : orderedQuery.ThenBy(c => c.Inserted_dt); break;
                                case "issue_raised_by": orderedQuery = isDesc ? orderedQuery.ThenByDescending(c => c.Issue_raised_by) : orderedQuery.ThenBy(c => c.Issue_raised_by); break;
                            }
                        }
                    }
                    query = orderedQuery ?? query.OrderByDescending(c => c.Complaint_id);
                }
                else
                {
                    query = query.OrderByDescending(c => c.Complaint_id);
                }

                // 6. Pagination & Data Projection
                int skip = (pageNumber - 1) * pageSize;
                var paginatedList = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(c => new ComplaintDto
                    {
                        Complaint_id = c.Complaint_id,
                        Comp_code = c.Comp_code,
                        Client_code = c.Client_code,
                        Project_code = c.Project_code,
                        Project_name = c.ClientProject != null ? c.ClientProject.Project_name : "",
                        Issue_description = c.Issue_description,
                        Project_start_date = c.Project_start_date,
                        Project_completion_date = c.Project_completion_date,
                        Amc_start_date = c.Amc_start_date,
                        Amc_finish_date = c.Amc_finish_date,
                        AMC_Start_dt = c.Amc_start_date,
                        AMC_End_dt = c.Amc_finish_date,
                        Support_doc1 = c.Support_doc1,
                        Support_doc2 = c.Support_doc2,
                        Support_doc3 = c.Support_doc3,
                        Support_doc4 = c.Support_doc4,
                        Support_doc5 = c.Support_doc5,
                        Issue_raised_by = c.Issue_raised_by,
                        Issue_booked_date = c.Issue_booked_date,
                        Issue_allotted_to = c.Issue_allotted_to,
                        Issue_allotted_date = c.Issue_allotted_date,
                        Issue_closed_date = c.Issue_closed_date,
                        Inserted_dt = c.Inserted_dt,
                        Start_dt = c.ClientProject != null ? c.ClientProject.Start_dt : null,
                        Completed_dt = c.ClientProject != null ? c.ClientProject.Completed_dt : null
                    })
                    .ToListAsync();

                return Ok(new PaginatedResult<ComplaintDto> { Data = paginatedList, TotalRecords = totalRecords });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing paginated complaints list.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        #endregion

        [HttpGet("short-date-format")]
        public IActionResult GetShortDateFormat()
        {
            var acceptLanguage = Request.Headers["Accept-Language"].ToString();

            // Extract primary locale (e.g. "en-GB" from "en-GB,en-US;q=0.9")
            var primaryLanguage = acceptLanguage.Split(',')
                                                .FirstOrDefault()?
                                                .Split(';')
                                                .FirstOrDefault()?
                                                .Trim();

            if (string.IsNullOrWhiteSpace(primaryLanguage))
            {
                primaryLanguage = CultureInfo.CurrentCulture.Name;
            }

            string shortDateFormat;
            try
            {
                var culture = new CultureInfo(primaryLanguage);
                shortDateFormat = culture.DateTimeFormat.ShortDatePattern;
            }
            catch (CultureNotFoundException)
            {
                shortDateFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            }

            return Ok(shortDateFormat);
        }
        // 1. Add this helper method inside the AuthController class
        // Add compCode as a second parameter
        private string GenerateKnowledgeBaseText(ComplaintDto m, string compCode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Complaint Details:");

            // 1. Explicitly append the Company Code first
            if (!string.IsNullOrWhiteSpace(compCode))
            {
                sb.AppendLine($"Comp_code: {compCode}");
            }

            // Dynamically iterate through all columns in the DTO
            foreach (var prop in typeof(ComplaintDto).GetProperties())
            {
                // Exclude the Complaint_id so the AI does not see it
                // Optional: Also exclude Comp_code if it happens to be in the DTO so it doesn't print twice
                if (prop.Name.Equals("Complaint_id", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Equals("Comp_code", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = prop.GetValue(m);

                // Format Dates to short string, else just ToString()
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    if (value is DateTime dt)
                    {
                        sb.AppendLine($"{prop.Name}: {dt:yyyy-MM-dd}");
                    }
                    else
                    {
                        sb.AppendLine($"{prop.Name}: {value}");
                    }
                }
            }
            return sb.ToString();
        }
        private string GenerateKnowledgeBaseText(ComplaintDto m)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Complaint Details:");

            // Dynamically iterate through all columns in the DTO
            foreach (var prop in typeof(ComplaintDto).GetProperties())
            {
                // Exclude the Complaint_id so the AI does not see it
                if (prop.Name.Equals("Complaint_id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = prop.GetValue(m);

                // Format Dates to short string, else just ToString()
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    if (value is DateTime dt)
                    {
                        sb.AppendLine($"{prop.Name}: {dt:yyyy-MM-dd}");
                    }
                    else
                    {
                        sb.AppendLine($"{prop.Name}: {value}");
                    }
                }
            }
            return sb.ToString();
        }
        /*colvis*/
        [HttpPost("save-column-state")]
        public async Task<IActionResult> SaveColumnState([FromBody] ColumnStateDto dto)
        {
            if (string.IsNullOrEmpty(dto.LoginId) || string.IsNullOrEmpty(dto.FormName))
            {
                return BadRequest("LoginId and FormName are required.");
            }

            try
            {
                // Find existing state for this specific user and grid
                var existingState = await _context.GridColumnStates
                    .FirstOrDefaultAsync(x => x.LoginId == dto.LoginId && x.FormName == dto.FormName);

                if (existingState != null)
                {
                    // UPDATE if it already exists
                    existingState.StateJson = dto.StateJson;
                    existingState.LastUpdated = DateTime.Now; // Or DateTime.UtcNow
                    _context.GridColumnStates.Update(existingState);
                }
                else
                {
                    // INSERT if it's the user's first time saving this grid's layout
                    var newState = new GridColumnState
                    {
                        LoginId = dto.LoginId,
                        FormName = dto.FormName,
                        StateJson = dto.StateJson,
                        LastUpdated = DateTime.Now
                    };
                    await _context.GridColumnStates.AddAsync(newState);
                }

                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                // Log your exception here
                return StatusCode(500, "An error occurred while saving the column state.");
            }
        }

        [HttpGet("get-column-state/{loginId}/{formName}")]
        public async Task<IActionResult> GetColumnState(string loginId, string formName)
        {
            try
            {
                var stateJson = await _context.GridColumnStates
                    .Where(x => x.LoginId == loginId && x.FormName == formName)
                    .Select(x => x.StateJson)
                    .FirstOrDefaultAsync();

                // If null, return an empty string so the Refit client and Blazor handle it gracefully
                return Ok(stateJson ?? string.Empty);
            }
            catch (Exception ex)
            {
                // Log your exception here
                return StatusCode(500, "An error occurred while retrieving the column state.");
            }
        }
    }
}