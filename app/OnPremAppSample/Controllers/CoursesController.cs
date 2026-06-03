using System.Linq;
using Microsoft.AspNetCore.Mvc;
using OnPremAppSample.Data;

namespace OnPremAppSample.Controllers;

public class CoursesController : Controller
{
    public IActionResult Index()
    {
        var courses = MockDataStore.Courses;
        return View(courses);
    }

    public IActionResult Details(int id)
    {
        var course = MockDataStore.Courses.FirstOrDefault(c => c.Id == id);
        if (course == null)
        {
            return NotFound();
        }

        // Get related lessons for this course
        ViewBag.Lessons = MockDataStore.Lessons.Where(l => l.CourseId == id).ToList();

        return View(course);
    }
}
