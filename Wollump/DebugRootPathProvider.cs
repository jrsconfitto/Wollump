namespace Wollump
{
    using Nancy;
    using System;
    using System.IO;

    public class DebugRootPathProvider : IRootPathProvider
    {
        public string GetRootPath()
        {
            var appDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory);
            var viewDirectory = Path.Combine(appDirectory.Parent.Parent.FullName, "views");
            return viewDirectory;
        }
    }
}
