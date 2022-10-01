namespace DuplicateFinder.Bl.Storage
{
	internal interface IHashStorage
	{
		public IEnumerable<HashedFile> EnumerateHashedFiles();
		bool FindDuplicate(byte[] hash, out IEnumerable<HashedFile> existing);
		void Remove(HashedFile existing);
		void AddNewItem(HashedFile hashedFile);
		IReadOnlyCollection<string> DetermineModifiedFolders(IReadOnlyCollection<string> rootFolders);
		void Persist();
	}
}
