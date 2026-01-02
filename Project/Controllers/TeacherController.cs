using Microsoft.AspNetCore.Mvc;
using Project.Models;
using Microsoft.EntityFrameworkCore;
using Project.Filters;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Project.Controllers
{
    [RoleAuthorize("Teacher","Admin")]
    public class TeacherController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TeacherController> _logger;

        public TeacherController(ApplicationDbContext context, ILogger<TeacherController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Simple teacher dashboard
        public IActionResult Dashboard(int id)
        {
            // Load teacher including classes so view lists them properly
            var teacher = _context.Teachers
                .Include(t => t.Classes)
                .FirstOrDefault(t => t.Id == id);
            if (teacher == null) return NotFound();

            // If there is a current timetable entry for this teacher, redirect to attendance for that class
            try
            {
                var now = DateTime.Now;
                var today = now.DayOfWeek;
                var timeOfDay = now.TimeOfDay;

                // Use inclusive start, exclusive end (Start <= now < End) to avoid edge equality issues
                var current = _context.TimeTables
                    .Include(tt => tt.Class)
                    .Where(tt => tt.Class != null && tt.Class.TeacherId == teacher.Id && tt.Day == today && tt.StartTime <= timeOfDay && tt.EndTime > timeOfDay)
                    .OrderBy(tt => tt.StartTime)
                    .FirstOrDefault();

                if (current != null && current.Class != null)
                {
                    _logger.LogInformation("Teacher {TeacherId} matched timetable {TimeTableId} for class {ClassId} (start={Start}, end={End}, now={Now})",
                        id, current.Id, current.ClassId, current.StartTime, current.EndTime, timeOfDay);
                    return RedirectToAction("Attendance", new { classId = current.ClassId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate timetable for teacher {TeacherId}", id);
                // ignore and fall back to dashboard
            }

            // No current class - show a simple page indicating nothing to take now
            ViewBag.TeacherName = teacher.Name;
            return View("NoClass");
        }

        // Attendances for a specific class
        public IActionResult Attendance(int classId)
        {
            // Ensure we are using DateOnly for comparison
            var today = DateOnly.FromDateTime(DateTime.Now);

            // Load existing attendance records for this class for today
            var records = _context.Attendances
                .Include(a => a.Student)
                .Include(a => a.Class)
                .Where(a => a.ClassId == classId && a.Date == today)
                .ToList();

            // If no attendance records for today, create default entries from registered students or section membership
            if (!records.Any())
            {
                // Try Registered table first
                var students = _context.Registereds
                    .Include(r => r.Student)
                    .Where(r => r.ClassId == classId)
                    .Select(r => r.Student)
                    .ToList();

                // Fallback: if TimeTable has a SectionId, use students in that section
                if (!students.Any())
                {
                    var tt = _context.TimeTables.FirstOrDefault(t => t.ClassId == classId);
                    if (tt != null && tt.SectionId.HasValue)
                    {
                        students = _context.Students.Where(s => s.SectionId == tt.SectionId.Value).ToList();
                    }
                }

                // As last resort, no students -> just return empty view with class name
                if (!students.Any())
                {
                    var clsInfo = _context.Classes.Find(classId);
                    ViewBag.ClassName = clsInfo?.ClassName ?? "(unknown)";
                    ViewBag.ClassId = classId;
                    return View(new System.Collections.Generic.List<Attendance>());
                }

                // Create Attendance rows defaulting to Absent
                var toCreate = students.Select(s => new Attendance
                {
                    ClassId = classId,
                    StudentId = s.Id,
                    Date = today,
                    Status = "Absent"
                }).ToList();

                _context.Attendances.AddRange(toCreate);
                _context.SaveChanges();

                // Reload with navigation properties
                records = _context.Attendances
                    .Include(a => a.Student)
                    .Include(a => a.Class)
                    .Where(a => a.ClassId == classId && a.Date == today)
                    .ToList();
            }

            ViewBag.ClassName = records.FirstOrDefault()?.Class?.ClassName ?? _context.Classes.Find(classId)?.ClassName;
            ViewBag.ClassId = classId;
            return View(records);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveAttendance(int classId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            try
            {
                var updated = new System.Collections.Generic.List<int>();
                foreach (var key in Request.Form.Keys)
                {
                    if (!key.StartsWith("status_")) continue;
                    var idPart = key.Substring("status_".Length);
                    if (!int.TryParse(idPart, out var attendanceId)) continue;

                    var val = Request.Form[key].ToString();
                    _logger.LogDebug("Form key {Key} = {Val}", key, val);

                    var rec = _context.Attendances.FirstOrDefault(a => a.Id == attendanceId && a.ClassId == classId && a.Date == today);
                    if (rec == null) continue;

                    if (!string.IsNullOrWhiteSpace(val) && rec.Status != val)
                    {
                        _logger.LogInformation("Updating attendance {AttendanceId} status {Old} -> {New}", rec.Id, rec.Status, val);
                        rec.Status = val;
                        updated.Add(rec.Id);
                    }
                }

                if (updated.Any())
                {
                    _context.SaveChanges();
                    // Optionally set a brief success message to show on dashboard
                    TempData["SuccessMessage"] = "Attendance saved.";
                }
                else
                {
                    TempData["SuccessMessage"] = "No changes detected.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed saving attendance for class {ClassId}", classId);
                TempData["ErrorMessage"] = "Failed to save attendance.";
            }

            // After saving, redirect the teacher back to their Dashboard so they can see classes
            var cls = _context.Classes.Find(classId);
            var teacherId = cls?.TeacherId ?? HttpContext.Session.GetInt32("TeacherId") ?? 0;
            return RedirectToAction("Dashboard", "Teacher", new { id = teacherId });
        }
    }
}
