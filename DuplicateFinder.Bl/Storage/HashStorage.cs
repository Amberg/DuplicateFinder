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
		private readonly object m_lock = new object();
		private int m_skipCount = 0;

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
					lock (m_lock)
					{
						foreach (var value in JsonSerializer.Deserialize<HashedFile[]>(File.ReadAllText(HashFileName))!)
						{
							m_files.Add(value);
							m_pathHashLookup[value.Path] = value;
						}
					}
				}
			}
			catch (Exception ex)
			{
				m_logger.LogError(ex, $"Loading {HashFileName} failed");
			}
		}

		public IEnumerable<HashedFile> FindDuplicates()
		{
			var result = new List<HashedFile>();
			lock (m_lock)
			{
				while (result.Count < 2)
				{
					result = m_files.GroupBy(x => x.Hash, new ByteArrayComparer())
						         .OrderBy(x => x.Key, new ByteArraySortComparer())
						         .Where(x => x.Count() > 1)
						         .Skip(m_skipCount)
						         .Take(1)
						         .ToList()
						         .FirstOrDefault()
						         ?.ToList() ??
					         new List<HashedFile>();
					result = result.ToList();
					if (result.Count == 0)
					{
						if (m_skipCount == 0)
						{
							return result;
						}

						m_skipCount = 0;
					}

					foreach (var file in result.ToList())
					{
						if (!File.Exists(file.Path))
						{
							result.Remove(file);
							Remove(file);
							Persist();
						}
					}
				}
			}
			return result;
		}

		public void SkipDuplicate()
		{
			lock (m_lock)
			{
				m_skipCount++;
			}
		}

		public void Remove(HashedFile existing)
		{
			lock (m_lock)
			{
				m_files.Remove(existing);
				m_pathHashLookup.Remove(existing.Path);
			}
		}

		public void AddNewItem(HashedFile hashedFile)
		{
			lock (m_lock)
			{
				if (!m_files.Add(hashedFile))
				{
					// only the case if something went wrong -- add same file again to update HashDate
					m_files.Remove(hashedFile);
					m_files.Add(hashedFile);
				}

				m_pathHashLookup[hashedFile.Path] = hashedFile;
			}
		}

		public void Persist()
		{
			lock (m_lock)
			{
				File.WriteAllText(HashFileName, JsonSerializer.Serialize(m_files.ToArray()));
			}
		}

		public bool IsHashUpToDate(string file)
		{
			lock (m_lock)
			{
				return m_pathHashLookup.TryGetValue(file, out var hashedFile) &&
				       hashedFile.HashDate > File.GetLastWriteTimeUtc(file);
			}
		}

		public void Choose(string path)
		{
			try
			{
				lock (m_lock)
				{
					if (m_pathHashLookup.TryGetValue(path, out var hash))
					{
						foreach (var hashedFile in m_files.Where(x =>
							         x.Hash.SequenceEqual(hash.Hash) && x.Path != path))
						{
							FileSystem.DeleteFile(hashedFile.Path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
							Remove(hashedFile);
						}
					}
				}
			}
			catch (Exception ex)
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
			lock (m_lock)
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
						Path = grp.Key,
						HashDate = grp.Min(x => x.HashDate)
					})
					.Where(x => x.Path != null)
					.ToDictionary(x => x.Path!, x => x.HashDate);
				return DetermineModifiedFoldersInternal(rootFolders, hashedFolders);
			}
		}

		private IReadOnlyCollection<string> DetermineModifiedFoldersInternal(IReadOnlyCollection<string> rootFolders,
			Dictionary<string, DateTimeOffset> hashedFolders)
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
				else
				{
					var files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly).ToList();
					if (files.Any() && files.Max(File.GetLastWriteTimeUtc) > oldestHash)
					{
						result.Add(folder);
					}
				}

				result.AddRange(DetermineModifiedFoldersInternal(
					Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly), hashedFolders));
			}

			return result;
		}

		private class ByteArraySortComparer : IComparer<byte[]>
		{
			public int Compare(byte[] x, byte[] y)
			{
				if (ReferenceEquals(x, y)) return 0;
				if (x == null) return 1;
				if (y == null) return -1;
				// get the 2 lengths and the minimum
				int xLen = x.Length, yLen = y.Length, len = xLen < yLen ? xLen : yLen;
				// loop and test
				for (int i = 0; i < len; i++)
				{
					int result = x[i].CompareTo(y[i]);
					if (result != 0) return result; // found a difference
				}

				if (xLen == yLen) return 0; // same bytes and length;
				return xLen < yLen ? -1 : 1; // different lengths
			}
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
