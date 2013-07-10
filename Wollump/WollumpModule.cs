namespace Wollump
{
    using LibGit2Sharp;
    using MarkdownSharp;
    using Nancy;
    using Nancy.Helpers;
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Wollump.Models;

    public class WollumpModule : NancyModule
    {
        private string[] _indexPages = { "home", "readme", "index" };
        private string[] _validExtensions = { ".md", ".markdown", ".mkd" };

        private Repository _repo;
        private Markdown _md;

        public WollumpModule(Repository repo, Markdown md)
        {
            _repo = repo;
            _md = md;

            Get["/"] = _ =>
            {
                // Look for home/readme/index in the repository
                var indexEntry = repo.Head.Tip.Tree
                    .Where(t => MatchesHomeFile(t.Name))
                    .Select(t => t.Target)
                    .FirstOrDefault();

                if (indexEntry != null)
                {
                    return View["page", ModelForBlobId(indexEntry.Id)];
                }
                else
                {
                    return "There is no home to show. Maybe create it?";
                }
            };

            Get["/page/{page}"] = parameters =>
            {
                string page = HttpUtility.UrlDecode(parameters.page).Replace(" ", "-");

                var pageEntry = repo.Head.Tip.Tree
                    .Where(t =>
                        Path.GetFileNameWithoutExtension(t.Name).ToLowerInvariant() == page.ToLowerInvariant() &&
                        HasRenderableExtension(t.Name))
                    .Select(t => t.Target)
                    .FirstOrDefault();

                if (pageEntry != null)
                {
                    return View["page", ModelForBlobId(pageEntry.Id, page)];
                }
                else
                {
                    return page + " doesn't exist. Maybe create it?";
                }
            };

            Get["/{pages}"] = _ =>
            {
                var validPages = repo.Head.Tip.Tree
                    .Where(t => HasRenderableExtension(t.Name))
                    .Select(file => Path.GetFileNameWithoutExtension(file.Name));

                return View["pages", validPages.ToArray()];
            };

        }

        private PageModel ModelForBlobId(ObjectId blobId, string name = "Home")
        {
            var content = _repo.Lookup<Blob>(blobId).ContentAsUtf8();

            return new PageModel()
            {
                Name = name,
                Content = RenderContent(content)
            };
        }

        private string RenderContent(string content)
        {
            return _md.Transform(ProcessTags(content));
        }

        private bool HasRenderableExtension(string filename)
        {
            return _validExtensions.Contains(Path.GetExtension(filename), StringComparer.InvariantCultureIgnoreCase);
        }

        private bool MatchesHomeFile(string fileName)
        {
            string strippedName = Path.GetFileNameWithoutExtension(fileName);
            return _indexPages.Contains(strippedName, StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Process the `[[link]]` tags real stoopid like!
        /// </summary>
        private string ProcessTags(string content)
        {
            // Thanks to Gollum for the Regex!
            Regex tags = new Regex(@"(.?)\[\[(.+?)\]\]([^\[]?)");
            MatchCollection matches = tags.Matches(content, 0);

            foreach (Match match in matches)
            {
                string externalFormat = @"<a href=""{0}"">{1}</a>";
                string internalFormat = @"<a href=""/page/{0}"">{1}</a>";
                string replacement, linkHref, linkText;

                var split = match.Groups[2].Value.Split(new char[] { '|' });
                if (split.Length > 1)
                {
                    // Probably external link
                    linkText = split[0];
                    linkHref = split[1];
                    replacement = string.Format(externalFormat, linkHref, linkText);
                }
                else
                {
                    // Probably internal link
                    linkText = match.Groups[2].Value;
                    linkHref = HttpUtility.UrlEncode(match.Groups[2].Value);
                    replacement = string.Format(internalFormat, linkHref, linkText);
                }

                content = content.Replace(match.Value.Trim(), replacement);
            }

            return content;
        }
    }
}