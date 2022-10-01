using System.Buffers;
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
				m_logger.LogInformation($"hashing file {file}");
				using MD5 md5 = MD5.Create();
				var data = ReadFile(file, out var length);
				var hash = md5.ComputeHash(data, 0, length);
				ArrayPool<byte>.Shared.Return(data);
				m_hashStorage.AddNewItem(new HashedFile(file, hash, DateTimeOffset.UtcNow));
			}
			catch (Exception ex)
			{
				m_logger.LogWarning(ex.Message);
			}
		}

		byte[] ReadFile(string path, out int length)
		{
			using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
			length = (int) fileStream.Length;
			var buffer = ArrayPool<byte>.Shared.Rent(length);
			fileStream.Read(buffer, 0, length);
			return buffer;
		}
	}
}

