using DemoApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemoApp.Pages
{
    public class PeopleModel : PageModel
    {
        private readonly MyDbContext _context;
        private readonly ILogger<PeopleModel> _logger;

        public List<Person> People { get; set; } = new List<Person>();

        [BindProperty]
        public Person NewPerson {  get; set; }

        public PeopleModel(MyDbContext context, ILogger<PeopleModel> logger)
        {
            _context = context;
            _logger = logger;

		}
        public void OnGet()
        {
            _logger.LogInformation("This is the People's Page");
            People = _context.People.ToList();
        }

        public IActionResult OnPost()
        {
            _logger.LogInformation("Added a new Person");
			_context.People.Add(NewPerson);
            _context.SaveChanges();
            return RedirectToPage();
        }
    }
}
