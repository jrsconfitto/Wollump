namespace Wollump
{
    using LibGit2Sharp;
    using Nancy;
    using System;
    using System.IO;
    using System.Linq;

    public class WollumpModule : NancyModule
    {
        private string[] indexPages = { "Home", "Readme", "Index" };

        public WollumpModule(Repository repo)
        {
            Get["/"] = _ =>
            {
                // Look for home/readme/index in the repository
                if (repo.Head.Tip.Tree.Any(t => AnyHomeFiles(t.Name)))
                {
                    var indexFile = repo.Head.Tip.Tree
                        .Where(t => AnyHomeFiles(t.Name))
                        .Select(t => t.Target)
                        .First();

                    return repo.Lookup<Blob>(indexFile.Id).ContentAsUtf8().ToString();
                }
                else
                {
                    return "There is no home to show. Maybe create it?";
                }
            };
        }

        private bool AnyHomeFiles(string fileName)
        {
            string strippedName = Path.GetFileNameWithoutExtension(fileName);
            return indexPages.Contains(strippedName, StringComparer.InvariantCultureIgnoreCase);
        }
    }
}