namespace OnPremAppSample.Models;

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced
    public string Duration { get; set; } = string.Empty; // e.g. "12 Hours"
    public string Instructor { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
