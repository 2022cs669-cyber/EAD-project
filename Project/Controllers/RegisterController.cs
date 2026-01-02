using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using System.Linq;

namespace Project.Controllers
{
    public class RegisterController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RegisterController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Display all subjects for the student, including registered ones
        public IActionResult CourseRegister(int id)
        {
            // Get all subjects, including the ones the student is already registered for
            var allSubjects = _context.Classes
                .Include(c => c.Teacher)
                .ToList();

            // Get the subjects the student is already registered for
            var registeredSubjects = _context.Registereds
                .Where(r => r.StudentId == id)
                .Select(r => r.ClassId)
                .ToList();

            ViewBag.StudentId = id; // Pass student ID to the view
            ViewBag.RegisteredSubjects = registeredSubjects; // Pass the registered subjects to the view

            return View(allSubjects);
        }

        // POST: Register the student for a subject
        [HttpPost]
        public IActionResult CourseRegister(int studentId, int classId)
        {
            var alreadyRegistered = _context.Registereds
                .Any(r => r.StudentId == studentId && r.ClassId == classId);

            if (!alreadyRegistered)
            {
                // Add the registration record
                var registration = new Registered
                {
                    StudentId = studentId,
                    ClassId = classId
                };
                _context.Registereds.Add(registration);
                _context.SaveChanges();
                TempData["Success"] = "Successfully registered for the subject.";
            }
            else
            {
                TempData["Error"] = "You are already registered for this subject.";
            }

            // Redirect back to the same page with the student ID
            return RedirectToAction("CourseRegister", new { id = studentId });
        }

        // POST: Unregister the student from a subject
        [HttpPost]
        public IActionResult Unregister(int studentId, int classId)
        {
            var registration = _context.Registereds
                .FirstOrDefault(r => r.StudentId == studentId && r.ClassId == classId);

            if (registration != null)
            {
                _context.Registereds.Remove(registration);
                _context.SaveChanges();
                TempData["Success"] = "Successfully unregistered from the subject.";
            }
            else
            {
                TempData["Error"] = "You are not registered for this subject.";
            }

            // Redirect back to the same page with the student ID
            return RedirectToAction("CourseRegister", new { id = studentId });
        }
    }
}
