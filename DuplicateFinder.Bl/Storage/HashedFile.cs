using System;

namespace DuplicateFinder.Bl.Storage;

public class HashedFile
{
	public string Path { get; }
	public byte[] Hash { get; }
	public DateTimeOffset HashDate { get; }

	public HashedFile(string path, byte[] hash, DateTimeOffset hashDate)
	{
		Path = path;
		Hash = hash;
		HashDate = hashDate;
	}

	protected bool Equals(HashedFile other)
	{
		return Path == other.Path && Hash.SequenceEqual(other.Hash);
	}

	public override bool Equals(object obj)
	{
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != this.GetType()) return false;
		return Equals((HashedFile) obj);
	}

	public override int GetHashCode()
	{
		int hash = 0;
		foreach (var element in Hash)
		{
			hash = hash * 31 + element.GetHashCode();
		}
		return HashCode.Combine(Path, hash);
	}
}