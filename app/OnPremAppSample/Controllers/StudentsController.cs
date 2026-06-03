using Microsoft.AspNetCore.Mvc;
using OnPremAppSample.Data;

namespace OnPremAppSample.Controllers;

public class StudentsController : Controller
{
    public IActionResult Index()
    {
        var students = MockDataStore.Students;
        return View(students);
    }
}
