using System.Collections.Generic;

namespace Project.Models
{
    public class Section
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int BatchId { get; set; }

        // Make navigation properties nullable / initialize collections to avoid implicit required validation
        public virtual Batch? Batch { get; set; }
        public virtual ICollection<Student> Students { get; set; } = new List<Student>();
    }
}