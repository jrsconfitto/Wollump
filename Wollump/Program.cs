namespace Wollump
{
    using LibGit2Sharp;
    using Nancy.Hosting.Self;
    using System;
    using System.Diagnostics;
    using System.Net;

    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                try
                {
                    using (Repository repo = new Repository(args[0]))
                    {
                        Uri uri = new Uri("http://localhost:1234");

                        // Build the Nancy host
                        var bootstrapper = new WollumpBootstrapper(repo);
                        var nancyHost = new Nancy.Hosting.Self.NancyHost(bootstrapper, uri);

                        // Start Nancy's self host
                        nancyHost.Start();

                        // Output to the console
                        Console.WriteLine("Hosting application at " + uri.ToString());
                        Console.WriteLine("Launching the host url in your browser...");
                        Process.Start(uri.ToString());
                        Console.ReadLine();

                        // Stop hosting
                        nancyHost.Stop();
                    }
                }
                catch (HttpListenerException httpEx)
                {
                    Fail("Application failed to launch. There was an HTTP listener related error: " + httpEx.Message);
                }
                catch (Exception ex)
                {
                    Fail("Application failed to launch. You probably didn't give me a valid git repository: " + ex.Message);
                }
            }
            else
            {
                Fail("Pass 1 argument with a path to a git repo");
            }
        }

        private static void Fail(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Hit any key and try again.");
            Console.ReadKey();
        }
    }
}
