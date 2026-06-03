namespace OnPremAppSample.Models;

public class Lesson
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string DocumentPath { get; set; } = string.Empty; // Points to a local file in wwwroot/uploads
}
