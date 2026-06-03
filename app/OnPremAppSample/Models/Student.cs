using System;

namespace OnPremAppSample.Models;

public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime EnrollmentDate { get; set; }
    public string Status { get; set; } = "Active"; // Active, Completed, Suspended
    public string Major { get; set; } = string.Empty;
}
