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

                var pageEntry = EntryForPath(page);

                if (pageEntry != null)
                {
                    return View["page", ModelForBlobId(pageEntry.Target.Id, page)];
                }
                else
                {
                    return page + " doesn't exist. Maybe create it?";
                }
            };

            Get["/edit/{page}"] = parameters =>
            {
                string page = HttpUtility.UrlDecode(parameters.page).Replace(" ", "-");

                var pageEntry = EntryForPath(page);

                if (pageEntry != null)
                {
                    return View["edit", ModelForBlobId(pageEntry.Target.Id, page, false)];
                }
                else
                {
                    return page + " doesn't exist. Maybe create it?";
                }
            };

            Post["/edit/{page}"] = parameters =>
            {
                // Now we're going to write stuff into the repository
                string page = parameters.page;
                string content;
                string message;

                if (Request.Form.content.HasValue && Request.Form.message.HasValue)
                {
                    content = Request.Form.content;
                    message = Request.Form.message;

                    // See if the repository is bare
                    if (!repo.Info.IsBare)
                    {
                        // Find the matching entry
                        var entryToUpdate = repo.Index
                            .FirstOrDefault(entry => Path.GetFileNameWithoutExtension(entry.Path).ToLowerInvariant() == page.ToLowerInvariant());

                        if (entryToUpdate != null)
                        {
                            // Write, stage, committer, commit
                            File.WriteAllText(Path.Combine(repo.Info.WorkingDirectory, entryToUpdate.Path), content);
                            repo.Index.Stage(entryToUpdate.Path);
                            Signature committer = new Signature("James", "@jugglingnutcase", DateTime.Now);
                            Commit commit = repo.Commit(message, committer, committer);

                            // Return the view
                            return View["page", ModelForBlobId(EntryForPath(page).Target.Id, page)];
                        }
                    }
                    else
                    {
                        var entryToUpdate = EntryForPath(page);
                        if (entryToUpdate != null)
                        {
                            // Get the content into a BinaryReader somehow. This is total guesswork on my part.
                            byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
                            MemoryStream ms = new MemoryStream(contentBytes);
                            BinaryReader br = new BinaryReader(ms);

                            // Sample from https://github.com/libgit2/libgit2sharp/blob/v0.9.0/LibGit2Sharp.Tests/ObjectDatabaseFixture.cs
                            // needs to be cleaned up
                            TreeDefinition td = TreeDefinition.From(repo.Head.Tip.Tree);
                            Blob newBlob = repo.ObjectDatabase.CreateBlob(br);
                            td.Add(entryToUpdate.Path, newBlob, Mode.NonExecutableFile);

                            // Committer and author
                            Signature committer = new Signature("James", "@jugglingnutcase", DateTime.Now);

                            // Create binary stream from the text
                            Tree tree = repo.ObjectDatabase.CreateTree(td);
                            Commit commit = repo.ObjectDatabase.CreateCommit(
                                message,
                                committer,
                                committer,
                                tree,
                                new[] { repo.Head.Tip });

                            // Update the HEAD reference to point to the latest commit
                            repo.Refs.UpdateTarget(repo.Refs.Head, commit.Id);
                            return View["page", ModelForBlobId(newBlob.Id, page)];

                        }
                    }
                }
                return HttpStatusCode.InternalServerError;
            };

            Get["/{pages}"] = _ =>
            {
                var validPages = repo.Head.Tip.Tree
                    .Where(t => HasRenderableExtension(t.Name))
                    .Select(file => Path.GetFileNameWithoutExtension(file.Name));

                return View["pages", validPages.ToArray()];
            };
        }

        private TreeEntry EntryForPath(string path)
        {
            return _repo.Head.Tip.Tree
                .Where(t =>
                    Path.GetFileNameWithoutExtension(t.Name).ToLowerInvariant() == path.ToLowerInvariant() &&
                    HasRenderableExtension(t.Name))
                .FirstOrDefault();
        }

        private PageModel ModelForBlobId(ObjectId blobId, string name = "Home", bool render = true)
        {
            var content = _repo.Lookup<Blob>(blobId).ContentAsUtf8();

            if (render) content = RenderContent(content);

            return new PageModel()
            {
                Name = name,
                Content = content
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
                string replacement, replacementFormat, linkHref, linkText;

                var split = match.Groups[2].Value.Split(new char[] { '|' });
                if (split.Length > 1)
                {
                    linkText = split[0];
                    linkHref = split[1];

                    if (linkHref.Contains("http"))
                    {
                        replacementFormat = externalFormat;
                    }
                    else
                    {
                        replacementFormat = internalFormat;
                    }

                    replacement = string.Format(replacementFormat, linkHref, linkText);
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