using System;

namespace Project.Models
{
    public class TimeTable
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        // Make SectionId nullable so empty selection binds correctly
        public int? SectionId { get; set; }
        public DayOfWeek Day { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public virtual Class? Class { get; set; }
        public virtual Section? Section { get; set; }
    }
}