using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models; // Include your project namespace

namespace Project.Controllers
{
    public class AttendController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Inject the DbContext through the constructor
        public AttendController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Attendance(int id)
        {
            // Fetch the student from the database
            var student = _context.Students.FirstOrDefault(s => s.Id == id);
            if (student == null)
            {
                return NotFound("Student not found");
            }

            var attendanceRecords = _context.Attendances
                                             .Include(a => a.Class) // Include related class data
                                             .Where(a => a.StudentId == id)
                                             .ToList();

            return View(attendanceRecords);
        }
    }
}
