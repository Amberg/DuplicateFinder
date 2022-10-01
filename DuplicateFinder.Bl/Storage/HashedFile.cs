namespace DuplicateFinder.Bl.Storage;

internal class HashedFile
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
		return Path == other.Path && Hash.Equals(other.Hash);
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
		return HashCode.Combine(Path, Hash);
	}
}