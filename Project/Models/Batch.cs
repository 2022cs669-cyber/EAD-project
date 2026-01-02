using System.Collections.Generic;

namespace Project.Models
{
    public class Batch
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Session { get; set; }

        public virtual ICollection<Section> Sections { get; set; } = new List<Section>();
    }
}