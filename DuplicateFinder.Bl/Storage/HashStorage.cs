using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DuplicateFinder.Bl.Storage
{
	internal class HashStorage : IHashStorage
	{
		private readonly ILogger<HashStorage> m_logger;
		private const string HashFileName = "hashes.dat";
		private HashSet<HashedFile> m_files = new HashSet<HashedFile>();

		public HashStorage(ILogger<HashStorage> logger)
		{
			m_logger = logger;
			//try to load existing
			LoadData();
		}

		private void LoadData()
		{
			try
			{
				if (File.Exists(HashFileName))
				{
					foreach (var value in JsonSerializer.Deserialize<HashedFile[]>(File.ReadAllText(HashFileName))!)
					{
						m_files.Add(value);
					}
				}
			}
			catch(Exception ex)
			{
				m_logger.LogError(ex, $"Loading {HashFileName} failed");
			}
		}

		public IEnumerable<HashedFile> EnumerateHashedFiles()
		{
			return m_files;
		}

		public bool FindDuplicate(byte[] hash, out IEnumerable<HashedFile> existing)
		{
			existing = m_files.Where(x => x.Hash.SequenceEqual(hash));
			return existing.Any();
		}

		public void Remove(HashedFile existing)
		{
			m_files.Remove(existing);
		}

		public void AddNewItem(HashedFile hashedFile)
		{
			m_files.Add(hashedFile);
		}
		
		public void Persist()
		{
			File.WriteAllText(HashFileName, JsonSerializer.Serialize(m_files.ToArray()));
		}


		/// <summary>
		/// Compares the last write time of the folders with the oldest hash time to
		/// re hash only required folders
		/// </summary>
		public IReadOnlyCollection<string> DetermineModifiedFolders(IReadOnlyCollection<string> rootFolders)
		{
			// get minimum hash date per hashed folder
			var hashedFolders = m_files.Select(x => new
				{
					Path = Path.GetDirectoryName(x.Path),
					x.HashDate
				})
				.GroupBy(x => x.Path)
				.Select(grp => new
				{
					Path= grp.Key,
					HashDate = grp.Min(x => x.HashDate)
				})
				.Where(x => x.Path != null)
				.ToDictionary(x => x.Path!, x => x.HashDate);
			return DetermineModifiedFoldersInternal(rootFolders, hashedFolders);
		}

		private IReadOnlyCollection<string> DetermineModifiedFoldersInternal(IReadOnlyCollection<string> rootFolders, Dictionary<string, DateTimeOffset> hashedFolders)
		{
			List<string> result = new List<string>();
			foreach (var folder in rootFolders)
			{
				if (!Directory.Exists(folder))
				{
					m_logger.LogWarning($"folder {folder} does not exist");
					continue;
				}

				if (!hashedFolders.TryGetValue(folder, out var oldestHash))
				{
					result.Add(folder);
				}
				else if (Directory.GetLastWriteTimeUtc(folder) > oldestHash)
				{
					result.Add(folder);
				}
				result.AddRange(DetermineModifiedFoldersInternal(Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly), hashedFolders));
			}
			return result;
		}

		public IReadOnlyCollection<string> GetHashedFolders()
		{
			return m_files.Select(x => Path.GetDirectoryName(x.Path)).Where(x => x != null).Distinct().ToList()!;
		}
	}
}
