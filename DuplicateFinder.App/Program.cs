using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.Options;

namespace DuplicateFinder
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.
			builder.Services.AddRazorPages();

			builder.Services.AddHangfire(c => c.UseMemoryStorage());
			builder.Services.AddHangfireServer();

			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
			}
			app.UseStaticFiles();

			app.UseRouting();

			app.UseAuthorization();
			app.MapRazorPages();

			app.UseHangfireDashboard("/hangfire");

			app.Run();

		}
	}
}