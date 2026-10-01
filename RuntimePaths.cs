namespace VRChatInstanceLogger
{
    internal static class RuntimePaths
    {
        public static readonly string Root = Path.Combine(
            AppContext.BaseDirectory);

        public static string GetFile(string fileName)
        {
            Directory.CreateDirectory(Root);
            return Path.Combine(Root, fileName);
        }
    }
}