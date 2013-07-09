namespace Wollump
{
    using Nancy;

    public class WollumpModule : NancyModule
    {
        public WollumpModule()
        {
            Get["/"] = _ => "hello Wollump!";
        }
    }
}