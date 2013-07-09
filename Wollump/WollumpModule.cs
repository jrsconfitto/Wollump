namespace Wollump
{
    using LibGit2Sharp;
    using MarkdownSharp;
    using Nancy;
    using System;
    using System.IO;
    using System.Linq;

    using Wollump.Models;

    public class WollumpModule : NancyModule
    {
        private string[] indexPages = { "home", "readme", "index" };
        private string[] validExtensions = { ".md", ".markdown", ".mkd" };

        public WollumpModule(Repository repo, Markdown md)
        {
            Get["/"] = _ =>
            {
                // Look for home/readme/index in the repository
                var indexFile = repo.Head.Tip.Tree
                    .Where(t => MatchesHomeFile(t.Name))
                    .Select(t => t.Target)
                    .FirstOrDefault();

                if (indexFile != null)
                {
                    var indexContent = repo.Lookup<Blob>(indexFile.Id).ContentAsUtf8();

                    PageModel model = new PageModel() { Name = "Home", Content = md.Transform(indexContent) };
                    return View["page", model];
                }
                else
                {
                    return "There is no home to show. Maybe create it?";
                }
            };

            Get["/{pages}"] = _ =>
            {
                var validPages = repo.Head.Tip.Tree
                    .Where(t => HasRenderableExtension(t.Name))
                    .Select(file => Path.GetFileNameWithoutExtension(file.Name));

                return View["pages", validPages.ToArray()];
            };

            Get["/page/{page}"] = parameters =>
            {
                string page = parameters.page;

                var file = repo.Head.Tip.Tree
                    .Where(t => 
                        Path.GetFileNameWithoutExtension(t.Name).ToLowerInvariant() == page.ToLowerInvariant() &&
                        HasRenderableExtension(t.Name))
                    .Select(t => t.Target)
                    .FirstOrDefault();

                if (file != null)
                {
                    var content = repo.Lookup<Blob>(file.Id).ContentAsUtf8();
                    return md.Transform(content);
                }
                else
                {
                    return page + " doesn't exist. Maybe create it?";
                }
            };
        }

        private bool HasRenderableExtension(string filename)
        {
            return validExtensions.Contains(Path.GetExtension(filename), StringComparer.InvariantCultureIgnoreCase);
        }
    
        private bool MatchesHomeFile(string fileName)
        {
            string strippedName = Path.GetFileNameWithoutExtension(fileName);
            return indexPages.Contains(strippedName, StringComparer.InvariantCultureIgnoreCase);
        }
    }
}