using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OnPremAppSample.Data;

namespace OnPremAppSample.Controllers;

public class LessonsController : Controller
{
    public IActionResult Index(int? courseId)
    {
        var lessons = MockDataStore.Lessons.AsQueryable();
        
        if (courseId.HasValue)
        {
            lessons = lessons.Where(l => l.CourseId == courseId.Value);
            ViewBag.SelectedCourse = MockDataStore.Courses.FirstOrDefault(c => c.Id == courseId.Value)?.Title;
        }

        return View(lessons.ToList());
    }
}
