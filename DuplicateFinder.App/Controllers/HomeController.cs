using DuplicateFinder.App.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using DuplicateFinder.Bl.Storage;
using Xabe.FFmpeg;
using System.Resources;

namespace DuplicateFinder.App.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> m_logger;
        private readonly IHashStorage m_hashStorage;

        public HomeController(ILogger<HomeController> logger, IHashStorage hashStorage)
        {
	        m_logger = logger;
	        m_hashStorage = hashStorage;
        }

        public IActionResult Index()
        {
	        var duplicates = m_hashStorage.FindDuplicates();

			return View(new DuplicatesModel(){Duplicates = duplicates.Select(x => x.Path).ToList()});
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> GetImage(string path)
        {
			if(!System.IO.File.Exists(path))
			{
				return NotFound();
			}

			if (Path.GetExtension(path) == ".mp4")
			{
				string output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
				IConversion conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(path, output, TimeSpan.FromSeconds(0));
				IConversionResult result = await conversion.Start();
				return File(new FileStream(output, FileMode.Open, FileAccess.Read), "image/png");
			}
	        return File(new FileStream(path, FileMode.Open, FileAccess.Read), "image/jpg");
        }

        public IActionResult SkipDuplicate()
        {
	        m_hashStorage.SkipDuplicate();
	        return RedirectToAction("Index");
		}

        public IActionResult Choose(string path)
        {
	        m_hashStorage.Choose(path);
			return RedirectToAction("Index");
        }
    }
}