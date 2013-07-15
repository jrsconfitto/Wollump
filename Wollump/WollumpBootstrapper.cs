namespace Wollump
{
    using LibGit2Sharp;
    using MarkdownSharp;
    using Nancy;
    using System;

    public class WollumpBootstrapper : DefaultNancyBootstrapper
    {
        protected Repository _repo;
        protected Markdown _md;

        public WollumpBootstrapper(Repository repo)
        {
            _repo = repo;
            _md = new Markdown();
        }

        protected override void ApplicationStartup(Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);

            // Register the repo for the whole application
            container.Register(_repo);
            container.Register(_md);
        }

#if DEBUG
        protected override IRootPathProvider RootPathProvider
        {
            get
            {
                return new DebugRootPathProvider();
            }
        }
#endif
    }
}
