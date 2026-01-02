using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Project.Models;
using Project.Filters;

namespace Project.Controllers
{
    [RoleAuthorize("Admin","Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Admin can view a student's dashboard by id; students can view their own dashboard only
        public IActionResult Dashboard(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role == "Student")
            {
                var sid = HttpContext.Session.GetInt32("StudentId");
                if (sid == null)
                {
                    // No session student id – force login
                    return RedirectToAction("Login", "Account");
                }

                // If a student attempted to access another student's dashboard, redirect them to their own
                if (sid.Value != id)
                {
                    return RedirectToAction("Dashboard", new { id = sid.Value });
                }
            }

            var student = _context.Students.FirstOrDefault(s => s.Id == id);
            if (student == null) return NotFound();
            return View(student);
        }

        // Admin can view student profile by id; students can view their own profile only
        public IActionResult Profile(int id)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role == "Student")
            {
                var sid = HttpContext.Session.GetInt32("StudentId");
                if (sid == null)
                {
                    // No session student id – force login
                    return RedirectToAction("Login", "Account");
                }

                // If a student attempted to access another student's profile, redirect them to their own
                if (sid.Value != id)
                {
                    return RedirectToAction("Profile", new { id = sid.Value });
                }
            }

            var student = _context.Students.FirstOrDefault(s => s.Id == id);
            if (student == null) return NotFound();
            return View(student);
        }
    }
}
