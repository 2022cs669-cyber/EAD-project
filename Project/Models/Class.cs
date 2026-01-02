using System;
using System.Collections.Generic;

namespace Project.Models;

public partial class Class
{
    public int Id { get; set; }

    public string ClassName { get; set; } = null!;

    public int TeacherId { get; set; }

    public DateTime Created { get; set; }

    public string NumStudents { get; set; } = null!;

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual ICollection<Registered> Registereds { get; set; } = new List<Registered>();

    // Make Teacher navigation nullable to avoid implicit required validation on model binding
    public virtual Teacher? Teacher { get; set; }
}
