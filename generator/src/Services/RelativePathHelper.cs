namespace Builder.Services
{
    public static class RelativePathHelper
    {
        public static string GetRelativePath(string currentPath, string targetPath, string extension)
        {
            var currentComponents = currentPath.Replace("\\", "/").Split('/');

            var target = "";
            for (var i = 0; i < currentComponents.Length - 1; i++)
            {
                target += "../";
            }

            target += targetPath.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(extension))
            {
                target += "." + extension;
            }

            return target;
        }
    }
}
