using System.Collections.Generic;

namespace DuplicateFinder.Bl.Storage
{
	public interface IHashStorage
	{
		IEnumerable<HashedFile> FindDuplicates();
		void Remove(HashedFile existing);
		void AddNewItem(HashedFile hashedFile);
		IReadOnlyCollection<string> DetermineModifiedFolders(IReadOnlyCollection<string> rootFolders);
		void Persist();
		bool IsHashUpToDate(string file);
		void Choose(string path);

		void SkipDuplicate();
	}
}
