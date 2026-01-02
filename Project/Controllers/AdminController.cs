using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Filters;
using System.Linq;
using System.Collections.Generic;

namespace Project.Controllers
{
    [RoleAuthorize("Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper to exclude the built-in admin account from lists and dropdowns
        private List<Teacher> GetAssignableTeachers()
        {
            return _context.Teachers
                .Where(t => t.Email != null && t.Email.ToLower() != "admin@example.com")
                .ToList();
        }

        public IActionResult Index()
        {
            return View();
        }

        // Admin dashboard overview
        public IActionResult Dashboard()
        {
            var model = new
            {
                Batches = _context.Batches.Count(),
                Sections = _context.Sections.Count(),
                Courses = _context.Courses.Count(),
                Classes = _context.Classes.Count(),
                Students = _context.Students.Count(),
                Teachers = _context.Teachers.Count()
            };

            return View(model);
        }

        // Batches
        public IActionResult Batches()
        {
            var batches = _context.Batches.Include(b => b.Sections).ToList();
            return View(batches);
        }

        public IActionResult CreateBatch()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateBatch(Batch batch)
        {
            if (ModelState.IsValid)
            {
                _context.Batches.Add(batch);
                _context.SaveChanges();
                return RedirectToAction(nameof(Batches));
            }
            return View(batch);
        }

        // AJAX endpoint to create batch from modal
        [HttpPost]
        public IActionResult CreateBatchAjax(string name, string session)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, error = "Name is required" });
            }
            var batch = new Batch { Name = name, Session = string.IsNullOrWhiteSpace(session) ? null : session };
            _context.Batches.Add(batch);
            _context.SaveChanges();
            return Json(new { success = true, id = batch.Id, name = batch.Name, session = batch.Session });
        }

        public IActionResult EditBatch(int id)
        {
            var batch = _context.Batches.Find(id);
            if (batch == null) return NotFound();
            return View(batch);
        }

        [HttpPost]
        public IActionResult EditBatch(Batch batch)
        {
            if (ModelState.IsValid)
            {
                _context.Batches.Update(batch);
                _context.SaveChanges();
                return RedirectToAction(nameof(Batches));
            }
            return View(batch);
        }

        [HttpPost]
        public IActionResult DeleteBatch(int id)
        {
            // Load batch including its sections and the students in those sections
            var batch = _context.Batches
                .Include(b => b.Sections)
                    .ThenInclude(s => s.Students)
                .FirstOrDefault(b => b.Id == id);

            if (batch == null) return NotFound();

            // For each student that references a section in this batch, clear the SectionId so the FK won't block deletion.
            foreach (var section in batch.Sections ?? Enumerable.Empty<Section>())
            {
                if (section.Students == null) continue;
                foreach (var student in section.Students)
                {
                    // SectionId is nullable in the model, so set to null to disassociate
                    student.SectionId = null;
                }
            }

            // Now remove the batch (this will cascade-delete sections)
            _context.Batches.Remove(batch);
            _context.SaveChanges();
            return RedirectToAction(nameof(Batches));
        }

        // Sections
        public IActionResult Sections()
        {
            var sections = _context.Sections.Include(s => s.Batch).Include(s => s.Students).ToList();
            ViewBag.Batches = _context.Batches.ToList();
            return View(sections);
        }

        public IActionResult CreateSection()
        {
            ViewBag.Batches = _context.Batches.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult CreateSection(Section section)
        {
            // Ensure a batch was selected
            if (section.BatchId <= 0)
            {
                ModelState.AddModelError("BatchId", "Please select a batch.");
            }

            if (ModelState.IsValid)
            {
                _context.Sections.Add(section);
                _context.SaveChanges();
                return RedirectToAction(nameof(Sections));
            }

            // Collect model state errors for debugging and show them in the view
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage + (e.Exception != null ? " - " + e.Exception.Message : "")).ToList();
            if (errors.Any())
            {
                TempData["ModelErrors"] = string.Join(" | ", errors);
            }

            ViewBag.Batches = _context.Batches.ToList();
            return View(section);
        }

        public IActionResult EditSection(int id)
        {
            var section = _context.Sections.Find(id);
            if (section == null) return NotFound();
            ViewBag.Batches = _context.Batches.ToList();
            return View(section);
        }

        [HttpPost]
        public IActionResult EditSection(Section section)
        {
            if (ModelState.IsValid)
            {
                _context.Sections.Update(section);
                _context.SaveChanges();
                return RedirectToAction(nameof(Sections));
            }
            ViewBag.Batches = _context.Batches.ToList();
            return View(section);
        }

        [HttpPost]
        public IActionResult DeleteSection(int id)
        {
            var section = _context.Sections.Include(s => s.Students).FirstOrDefault(s => s.Id == id);
            if (section == null) return NotFound();
            _context.Sections.Remove(section);
            _context.SaveChanges();
            return RedirectToAction(nameof(Sections));
        }

        // Courses
        public IActionResult Courses()
        {
            var courses = _context.Courses.ToList();
            return View(courses);
        }

        public IActionResult CreateCourse()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateCourse(Course course)
        {
            if (ModelState.IsValid)
            {
                _context.Courses.Add(course);
                _context.SaveChanges();
                return RedirectToAction(nameof(Courses));
            }
            return View(course);
        }

        public IActionResult EditCourse(int id)
        {
            var course = _context.Courses.Find(id);
            if (course == null) return NotFound();
            return View(course);
        }

        [HttpPost]
        public IActionResult EditCourse(Course course)
        {
            if (ModelState.IsValid)
            {
                _context.Courses.Update(course);
                _context.SaveChanges();
                return RedirectToAction(nameof(Courses));
            }
            return View(course);
        }

        [HttpPost]
        public IActionResult DeleteCourse(int id)
        {
            var course = _context.Courses.Find(id);
            if (course == null) return NotFound();
            _context.Courses.Remove(course);
            _context.SaveChanges();
            return RedirectToAction(nameof(Courses));
        }

        // Classes
        public IActionResult Classes()
        {
            var classes = _context.Classes.Include(c => c.Teacher).ToList();
            ViewBag.Teachers = GetAssignableTeachers();
            return View(classes);
        }

        public IActionResult CreateClass()
        {
            ViewBag.Teachers = GetAssignableTeachers();
            return View();
        }

        [HttpPost]
        public IActionResult CreateClass(Class cls)
        {
            // Ensure a teacher was selected
            if (cls.TeacherId <= 0)
            {
                ModelState.AddModelError("TeacherId", "Please select a teacher.");
            }

            if (ModelState.IsValid)
            {
                cls.Created = DateTime.Now;
                _context.Classes.Add(cls);
                _context.SaveChanges();
                return RedirectToAction(nameof(Classes));
            }

            // Collect model state errors for debugging
            var errors = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage + (e.Exception != null ? " - " + e.Exception.Message : "")).ToList();
            if (errors.Any()) TempData["ModelErrors"] = string.Join(" | ", errors);

            ViewBag.Teachers = GetAssignableTeachers();
            return View(cls);
        }

        public IActionResult EditClass(int id)
        {
            var cls = _context.Classes.Find(id);
            if (cls == null) return NotFound();
            ViewBag.Teachers = GetAssignableTeachers();
            return View(cls);
        }

        [HttpPost]
        public IActionResult EditClass(Class cls)
        {
            if (ModelState.IsValid)
            {
                _context.Classes.Update(cls);
                _context.SaveChanges();
                return RedirectToAction(nameof(Classes));
            }
            ViewBag.Teachers = GetAssignableTeachers();
            return View(cls);
        }

        [HttpPost]
        public IActionResult DeleteClass(int id)
        {
            var cls = _context.Classes.Find(id);
            if (cls == null) return NotFound();
            _context.Classes.Remove(cls);
            _context.SaveChanges();
            return RedirectToAction(nameof(Classes));
        }

        // Students
        public IActionResult Students()
        {
            var students = _context.Students.Include(s => s.Section).ToList();
            ViewBag.Sections = _context.Sections.Include(s => s.Batch).ToList();
            return View(students);
        }

        public IActionResult CreateStudent()
        {
            ViewBag.Sections = _context.Sections.Include(s => s.Batch).ToList();
            return View();
        }

        [HttpPost]
        public IActionResult CreateStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                _context.Students.Add(student);
                _context.SaveChanges();
                return RedirectToAction(nameof(Students));
            }
            ViewBag.Sections = _context.Sections.Include(s => s.Batch).ToList();
            return View(student);
        }

        public IActionResult EditStudent(int id)
        {
            var student = _context.Students.Find(id);
            if (student == null) return NotFound();
            ViewBag.Sections = _context.Sections.Include(s => s.Batch).ToList();
            return View(student);
        }

        [HttpPost]
        public IActionResult EditStudent(Student student)
        {
            if (ModelState.IsValid)
            {
                _context.Students.Update(student);
                _context.SaveChanges();
                return RedirectToAction(nameof(Students));
            }
            ViewBag.Sections = _context.Sections.Include(s => s.Batch).ToList();
            return View(student);
        }

        [HttpPost]
        public IActionResult DeleteStudent(int id)
        {
            var student = _context.Students.Find(id);
            if (student == null) return NotFound();
            _context.Students.Remove(student);
            _context.SaveChanges();
            return RedirectToAction(nameof(Students));
        }

        // Teachers
        public IActionResult Teachers()
        {
            var teachers = _context.Teachers
                .Where(t => t.Email != null && t.Email.ToLower() != "admin@example.com")
                .Select(t => new
                {
                    Id = t.Id,
                    Name = t.Name,
                    Email = t.Email,
                    ClassCount = _context.Classes.Count(c => c.TeacherId == t.Id)
                })
                .ToList();

            ViewBag.TeachersSummary = teachers;
            return View();
        }

        public IActionResult CreateTeacher()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateTeacher(Teacher teacher)
        {
            if (string.IsNullOrWhiteSpace(teacher?.Email) || string.IsNullOrWhiteSpace(teacher?.Name) || string.IsNullOrWhiteSpace(teacher?.Password))
            {
                ModelState.AddModelError(string.Empty, "All fields are required.");
            }

            if (_context.Teachers.Any(t => t.Email == teacher.Email))
            {
                ModelState.AddModelError("Email", "A teacher with this email already exists.");
            }

            if (ModelState.IsValid)
            {
                _context.Teachers.Add(teacher);
                _context.SaveChanges();
                return RedirectToAction(nameof(Teachers));
            }

            return View(teacher);
        }

        public IActionResult DeleteTeacher(int id)
        {
            var teacher = _context.Teachers.Find(id);
            if (teacher == null) return NotFound();

            var classes = _context.Classes.Where(c => c.TeacherId == id).ToList();
            var otherTeachers = _context.Teachers.Where(t => t.Id != id && t.Email.ToLower() != "admin@example.com").ToList();

            ViewBag.Classes = classes;
            ViewBag.OtherTeachers = otherTeachers;
            return View(teacher);
        }

        [HttpPost]
        public IActionResult DeleteTeacherConfirmed(int id, int? reassignTeacherId)
        {
            var teacher = _context.Teachers.Find(id);
            if (teacher == null) return NotFound();

            var classes = _context.Classes.Where(c => c.TeacherId == id).ToList();

            if (classes.Any() && !reassignTeacherId.HasValue)
            {
                TempData["ModelErrors"] = "There are classes assigned to this teacher. Choose a teacher to reassign the classes to before deleting.";
                return RedirectToAction("DeleteTeacher", new { id });
            }

            if (classes.Any() && reassignTeacherId.HasValue)
            {
                // Ensure replacement teacher exists
                var replacement = _context.Teachers.Find(reassignTeacherId.Value);
                if (replacement == null)
                {
                    TempData["ModelErrors"] = "Replacement teacher not found.";
                    return RedirectToAction("DeleteTeacher", new { id });
                }

                foreach (var cls in classes)
                {
                    cls.TeacherId = replacement.Id;
                }
                _context.Classes.UpdateRange(classes);
            }

            _context.Teachers.Remove(teacher);
            _context.SaveChanges();

            return RedirectToAction(nameof(Teachers));
        }

        // TimeTables
        public IActionResult TimeTables()
        {
            var tts = _context.TimeTables
                .Include(tt => tt.Class)
                .Include(tt => tt.Section)
                .ToList();

            return View(tts);
        }

        public IActionResult CreateTimeTable()
        {
            ViewBag.Classes = _context.Classes.ToList();
            ViewBag.Sections = _context.Sections.ToList();
            ViewBag.Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
            return View();
        }

        [HttpPost]
        public IActionResult CreateTimeTable(TimeTable tt)
        {
            if (tt.EndTime <= tt.StartTime)
            {
                ModelState.AddModelError("EndTime", "End time must be after start time.");
            }
            if (ModelState.IsValid)
            {
                _context.TimeTables.Add(tt);
                _context.SaveChanges();
                return RedirectToAction(nameof(TimeTables));
            }
            ViewBag.Classes = _context.Classes.ToList();
            ViewBag.Sections = _context.Sections.ToList();
            ViewBag.Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
            return View(tt);
        }

        public IActionResult EditTimeTable(int id)
        {
            var tt = _context.TimeTables.Find(id);
            if (tt == null) return NotFound();
            ViewBag.Classes = _context.Classes.ToList();
            ViewBag.Sections = _context.Sections.ToList();
            ViewBag.Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
            return View(tt);
        }

        [HttpPost]
        public IActionResult EditTimeTable(TimeTable tt)
        {
            if (tt.EndTime <= tt.StartTime)
            {
                ModelState.AddModelError("EndTime", "End time must be after start time.");
            }
            if (ModelState.IsValid)
            {
                _context.TimeTables.Update(tt);
                _context.SaveChanges();
                return RedirectToAction(nameof(TimeTables));
            }
            ViewBag.Classes = _context.Classes.ToList();
            ViewBag.Sections = _context.Sections.ToList();
            ViewBag.Days = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
            return View(tt);
        }

        [HttpPost]
        public IActionResult DeleteTimeTable(int id)
        {
            var tt = _context.TimeTables.Find(id);
            if (tt == null) return NotFound();
            _context.TimeTables.Remove(tt);
            _context.SaveChanges();
            return RedirectToAction(nameof(TimeTables));
        }
    }
}
