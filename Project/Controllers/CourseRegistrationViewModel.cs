using Project.Models;

namespace Project.Controllers
{
    internal class CourseRegistrationViewModel
    {
        public Student Student { get; set; }
        public List<Class> AvailableCourses { get; set; }
        public List<int> RegisteredCourseIds { get; set; }
    }
}