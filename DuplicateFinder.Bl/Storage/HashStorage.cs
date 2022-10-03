using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using SearchOption = System.IO.SearchOption;

namespace DuplicateFinder.Bl.Storage
{
	public class HashStorage : IHashStorage
	{
		private readonly ILogger<HashStorage> m_logger;
		private const string HashFileName = "hashes.dat";
		private readonly HashSet<HashedFile> m_files = new HashSet<HashedFile>();
		private readonly Dictionary<string, HashedFile> m_pathHashLookup = new Dictionary<string, HashedFile>();

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
						m_pathHashLookup[value.Path] = value;
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

		public IEnumerable<HashedFile> FindDuplicates()
		{
			return m_files.GroupBy(x => x.Hash, new ByteArrayComparer()).FirstOrDefault(x => x.Count() > 1) 
			       ?? (IEnumerable<HashedFile>)new List<HashedFile>();
		}

		public void Remove(HashedFile existing)
		{
			m_files.Remove(existing);
			m_pathHashLookup.Remove(existing.Path);
		}

		public void AddNewItem(HashedFile hashedFile)
		{
			if (!m_files.Add(hashedFile))
			{
				// only the case if something went wrong -- add same file again to update HashDate
				m_files.Remove(hashedFile);
				m_files.Add(hashedFile);
			}
			m_pathHashLookup[hashedFile.Path] = hashedFile;
		}
		
		public void Persist()
		{
			File.WriteAllText(HashFileName, JsonSerializer.Serialize(m_files.ToArray()));
		}

		public bool IsHashUpToDate(string file)
		{
			return m_pathHashLookup.TryGetValue(file, out var hashedFile) && hashedFile.HashDate > File.GetLastWriteTimeUtc(file);
		}

		public void Choose(string path)
		{
			try
			{
				if (m_pathHashLookup.TryGetValue(path, out var hash))
				{
					foreach (var hashedFile in m_files.Where(x => x.Hash.SequenceEqual(hash.Hash) && x.Path != path))
					{
						FileSystem.DeleteFile(hashedFile.Path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
						Remove(hashedFile);
					}
				}
			}
			catch(Exception ex)
			{
				m_logger.LogError(ex, $"Could not delete file {path}");
			}
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
				else if (Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly).Max(File.GetLastWriteTimeUtc) > oldestHash)
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

		private class ByteArrayComparer : EqualityComparer<byte[]>
		{
			public override bool Equals(byte[] x, byte[] y)
			{
				return x == y || x.SequenceEqual(y);
			}

			public override int GetHashCode(byte[] obj)
			{
				var hash = 0;
				foreach (var element in obj)
				{
					hash = hash * 31 + element.GetHashCode();
				}
				return hash;
			}
		}
	}
}
