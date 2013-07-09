namespace Wollump
{
    using Nancy;
    using LibGit2Sharp;

    public class WollumpBootstrapper : DefaultNancyBootstrapper
    {
        protected Repository _repo;

        public WollumpBootstrapper(Repository repo)
        {
            _repo = repo;
        }

        protected override void ApplicationStartup(Nancy.TinyIoc.TinyIoCContainer container, Nancy.Bootstrapper.IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);

            // Register the repo for the whole application
            container.Register(_repo);
        }
    }
}
