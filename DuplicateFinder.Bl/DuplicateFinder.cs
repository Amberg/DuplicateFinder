using System.Security.Cryptography;
using DuplicateCheck;
using DuplicateFinder.Bl.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DuplicateFinder.Bl
{
	public class DuplicateFinder
	{
		private readonly Settings m_settings;
		private readonly ILogger<DuplicateFinder> m_logger;
		private readonly IHashStorage m_hashStorage;

		public DuplicateFinder(IOptions<Settings> settings, ILogger<DuplicateFinder> logger, IHashStorage hashStorage)
		{
			m_settings = settings.Value;
			m_logger = logger;
			m_hashStorage = hashStorage;
		}

		public void SearchDuplicates()
		{
			m_logger.LogInformation("Check for duplicates:");
			var folders = m_hashStorage.DetermineModifiedFolders(m_settings.Folders);
			if (!folders.Any())
			{
				m_logger.LogInformation("No new files detected");
			}

			var counter = 0;
			foreach (var folder in folders)
			{
				m_logger.LogInformation($"hashing folder {folder}");
				var files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly);
				foreach (var file in files)
				{
					if (m_hashStorage.IsHashUpToDate(file))
					{
						continue;
					}
					ProcessFile(file);
					counter++;
				}
				Console.WriteLine();
			}

			if (counter > 0)
			{
				m_hashStorage.Persist();
			}
			m_logger.LogInformation($"Hashed {counter} files");
		}
		
		public void ProcessFile(string file)
		{
			try
			{
				Console.Write(".");
				using MD5 md5 = MD5.Create();
				using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
				var hash = md5.ComputeHash(new BufferedStream(fileStream, 16 * 1024 * 1024));
				m_hashStorage.AddNewItem(new HashedFile(file, hash, DateTimeOffset.UtcNow));
			}
			catch (Exception ex)
			{
				m_logger.LogWarning(ex, ex.Message);
			}
		}
	}
}

