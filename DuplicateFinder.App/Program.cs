using DuplicateCheck;
using DuplicateFinder.Bl.Storage;
using Hangfire;
using Hangfire.MemoryStorage;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace DuplicateFinder.App
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Xabe.FFmpeg.Downloader.FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official).GetAwaiter().GetResult();
			FFmpeg.SetExecutablesPath(".");
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.
			builder.Services.AddControllersWithViews();

			builder.Services.AddHangfire(c => c.UseMemoryStorage());
			builder.Services.AddHangfireServer(o => o.WorkerCount = 1);

			builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));
			RegisterComponents(builder.Services);

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Home/Error");
			}

			app.UseStaticFiles();

			app.UseRouting();

			app.UseAuthorization();

			app.MapControllerRoute(
				name: "default",
				pattern: "{controller=Home}/{action=Index}/{id?}");

			app.UseHangfireDashboard("/hangfire");
			ConfigureBackgroundJobs(app.Services);

			app.Run();
		}

		private static void RegisterComponents(IServiceCollection services)
		{
			services.AddSingleton<IHashStorage, HashStorage>();
			services.AddSingleton<Bl.DuplicateFinder>();
		}

		private static void ConfigureBackgroundJobs(IServiceProvider serviceCollection)
		{
			serviceCollection.GetService<IRecurringJobManager>()
				.AddOrUpdate("Find Duplicates", (Bl.DuplicateFinder m) => m.SearchDuplicates(), Cron.Hourly());

		}
	}
}