using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using System;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;
using Project.Services;

namespace Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _email;

        public AccountController(ApplicationDbContext context, EmailService email)
        {
            _context = context;
            _email = email;
        }

        public IActionResult Login()
        {
            // If already logged in, redirect based on role stored in session
            var role = HttpContext.Session.GetString("Role");
            if (!string.IsNullOrEmpty(role))
            {
                if (role == "Student")
                {
                    var sid = HttpContext.Session.GetInt32("StudentId");
                    return RedirectToAction("Dashboard", "Student", new { id = sid });
                }
                if (role == "Teacher")
                {
                    var tid = HttpContext.Session.GetInt32("TeacherId");
                    return RedirectToAction("Dashboard", "Teacher", new { id = tid });
                }
                if (role == "Admin")
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                var msg = "Please provide both Email and Password.";
                ViewBag.ErrorMessage = msg;
                // If AJAX, return JSON; otherwise redirect back to Login with error in TempData
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, errorMessage = msg });
                TempData["ErrorMessage"] = msg;
                return RedirectToAction("Login");
            }

            // Check for teacher first (teachers may also be admins)
            var teacher = _context.Teachers.FirstOrDefault(t => t.Email == email && t.Password == password);
            if (teacher != null)
            {
                // If teacher is the admin account (use email match), treat as admin
                if (teacher.Email.ToLower() == "admin@example.com")
                {
                    HttpContext.Session.SetString("Role", "Admin");
                    HttpContext.Session.SetInt32("TeacherId", teacher.Id);
                    HttpContext.Session.SetString("TeacherName", teacher.Name);
                    var redirectUrl = Url.Action("Dashboard", "Admin");
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = true, redirectUrl });
                    return Redirect(redirectUrl);
                }

                HttpContext.Session.SetString("Role", "Teacher");
                HttpContext.Session.SetInt32("TeacherId", teacher.Id);
                HttpContext.Session.SetString("TeacherName", teacher.Name);

                // Try to find the teacher's current class based on the TimeTable
                try
                {
                    var now = DateTime.Now;
                    var today = now.DayOfWeek; // Fixed: use DayOfWeek type
                    var timeOfDay = now.TimeOfDay;

                    var current = _context.TimeTables
                        .Include(tt => tt.Class)
                        .Where(tt => tt.Class != null && tt.Class.TeacherId == teacher.Id && tt.Day == today && tt.StartTime <= timeOfDay && tt.EndTime >= timeOfDay)
                        .OrderBy(tt => tt.StartTime)
                        .FirstOrDefault();

                    if (current != null && current.Class != null)
                    {
                        // Redirect teacher to attendance page for the class in timetable
                        var attendanceRedirect = Url.Action("Attendance", "Teacher", new { classId = current.ClassId });
                        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            return Json(new { success = true, redirectUrl = attendanceRedirect });
                        return Redirect(attendanceRedirect);
                    }
                }
                catch
                {
                    // Ignore timetable lookup failures and fall back to dashboard
                }

                var teacherRedirect = Url.Action("Dashboard", "Teacher", new { id = teacher.Id });
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, redirectUrl = teacherRedirect });
                return Redirect(teacherRedirect);
            }

            // Check student
            var student = _context.Students.FirstOrDefault(s => s.Email == email && s.Password == password);

            if (student == null)
            {
                var msg = "Invalid email or password.";
                ViewBag.ErrorMessage = msg;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, errorMessage = msg });
                TempData["ErrorMessage"] = msg;
                return RedirectToAction("Login");
            }

            // Set session on successful student login
            HttpContext.Session.SetString("Role", "Student");
            HttpContext.Session.SetInt32("StudentId", student.Id);
            HttpContext.Session.SetString("StudentName", student.Name);

            // Return success with redirect URL
            var redirectUrlStudent = Url.Action("Dashboard", "Student", new { id = student.Id });
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, redirectUrl = redirectUrlStudent });
            return Redirect(redirectUrlStudent);

        }

        public IActionResult Logout()
        {
            // Clear session
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        public IActionResult SignUp()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SignUp(Student newStudent)
        {
            if (ModelState.IsValid)
            {
                if (_context.Students.Any(s => s.Email == newStudent.Email))
                {
                    ViewBag.ErrorMessage = "Email is already registered.";
                    return View(newStudent);
                }

                _context.Students.Add(newStudent);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Sign-up successful! Please log in.";
                return RedirectToAction("Login");
            }

            return View(newStudent);
        }

        // Tokenized password reset workflow - using session storage instead of DB
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Please provide an email address.";
                return RedirectToAction("ForgotPassword");
            }

            // Check if email exists for either student or teacher
            var userExists = _context.Students.Any(s => s.Email == email) || _context.Teachers.Any(t => t.Email == email);
            if (!userExists)
            {
                TempData["ErrorMessage"] = "Email not found.";
                return RedirectToAction("ForgotPassword");
            }

            // Generate a numeric code for email verification
            var code = new Random().Next(100000, 999999).ToString();
            var expiresAt = DateTime.UtcNow.AddMinutes(15);

            // Store code and expiry in session (keyed by email)
            HttpContext.Session.SetString($"ResetCode:{email}", code);
            HttpContext.Session.SetString($"ResetCodeExpires:{email}", expiresAt.ToString("o"));

            // Send code by email
            var subject = "Your password reset code";
            var body = $"Your password reset code is: <strong>{code}</strong>. It expires in 15 minutes.";
            bool emailSent = false;
            string? emailError = null;
            try
            {
                emailSent = _email.Send(email, subject, body, out emailError);
            }
            catch (Exception ex)
            {
                emailError = ex.ToString();
            }

            // Prefill email on the next page so user doesn't need to re-enter
            TempData["ResetEmail"] = email;

            if (emailSent)
            {
                TempData["SuccessMessage"] = "If the email exists, a reset code has been sent to your email.";
            }
            else
            {
                TempData["SuccessMessage"] = "Email sending failed in this environment. Use the code shown on-screen (for dev only).";
                TempData["ResetCode"] = code;
                TempData["EmailError"] = emailError;
            }

            return RedirectToAction("EnterResetCode");
        }

        public IActionResult EnterResetCode()
        {
            return View();
        }

        [HttpPost]
        public IActionResult EnterResetCode(string email, string code)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                TempData["ErrorMessage"] = "Email and code are required.";
                return RedirectToAction("EnterResetCode");
            }

            var storedCode = HttpContext.Session.GetString($"ResetCode:{email}");
            var storedExpires = HttpContext.Session.GetString($"ResetCodeExpires:{email}");

            if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(storedExpires) || storedCode != code)
            {
                TempData["ErrorMessage"] = "Invalid or expired code.";
                return RedirectToAction("EnterResetCode");
            }

            if (!DateTime.TryParse(storedExpires, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt) || expiresAt < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "Invalid or expired code.";
                return RedirectToAction("EnterResetCode");
            }

            // Show form to reset password, pass email and code
            ViewBag.Email = email;
            ViewBag.Code = code;
            return View("ResetPasswordByCode");
        }

        [HttpPost]
        public IActionResult ResetPasswordByCode(string email, string code, string newPassword)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(newPassword))
            {
                TempData["ErrorMessage"] = "All fields are required.";
                return RedirectToAction("EnterResetCode");
            }

            var storedCode = HttpContext.Session.GetString($"ResetCode:{email}");
            var storedExpires = HttpContext.Session.GetString($"ResetCodeExpires:{email}");

            if (string.IsNullOrEmpty(storedCode) || string.IsNullOrEmpty(storedExpires) || storedCode != code)
            {
                TempData["ErrorMessage"] = "Invalid or expired code.";
                return RedirectToAction("EnterResetCode");
            }

            if (!DateTime.TryParse(storedExpires, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt) || expiresAt < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "Invalid or expired code.";
                return RedirectToAction("EnterResetCode");
            }

            var teacher = _context.Teachers.FirstOrDefault(t => t.Email == email);
            if (teacher != null)
            {
                teacher.Password = newPassword;
                _context.Teachers.Update(teacher);
            }
            else
            {
                var student = _context.Students.FirstOrDefault(s => s.Email == email);
                if (student == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("EnterResetCode");
                }

                student.Password = newPassword;
                _context.Students.Update(student);
            }

            // Clear session-stored reset code
            HttpContext.Session.Remove($"ResetCode:{email}");
            HttpContext.Session.Remove($"ResetCodeExpires:{email}");

            _context.SaveChanges();

            TempData["SuccessMessage"] = "Password updated. Please log in with your new password.";
            return RedirectToAction("Login");
        }
    }
}
