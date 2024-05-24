namespace StudioCore.Utilities;
public static class PathUtils
{
    public static string ResolveAliasPath(string aliasPath)
    {
        static bool GetAlias(string aliasPath, out string? alias)
        {
            if (string.IsNullOrWhiteSpace(aliasPath))
            {
                alias = null;
                return false;
            }

            if (aliasPath.Length < 6)
            {
                alias = null;
                return false;
            }

            if (aliasPath[0] != '$' || aliasPath[1] != '(')
            {
                alias = null;
                return false;
            }

            int aliasEndIndex = aliasPath.IndexOf(')');
            if (aliasEndIndex < 3)
            {
                alias = null;
                return false;
            }

            alias = aliasPath[..(aliasEndIndex + 1)];
            return true;
        }

        var pathVariable = GameVariables.GetPath();
        if (pathVariable == null)
        {
            return aliasPath;
        }

        if (!GetAlias(aliasPath, out string? alias))
        {
            return aliasPath;
        }

        do
        {
            if (!pathVariable.TryGetValue(alias, out string resolve))
            {
                return aliasPath;
            }

            aliasPath = aliasPath.Replace(alias, resolve);
        } while (GetAlias(aliasPath, out alias));

        return aliasPath;
    }

    public static string StripAlias(string aliasPath)
    {
        if (string.IsNullOrWhiteSpace(aliasPath))
        {
            return aliasPath;
        }

        if (aliasPath.Length < 6)
        {
            return aliasPath;
        }

        if (aliasPath[0] != '$' || aliasPath[1] != '(')
        {
            return aliasPath;
        }

        int aliasEndIndex = aliasPath.IndexOf(')');
        if (aliasEndIndex < 3)
        {
            return aliasPath;
        }

        return aliasPath[(aliasEndIndex + 1)..];
    }

    public static string StripURI(string path)
    {
        int index = path.IndexOf(':');
        if (index < 0)
        {
            return path;
        }

        if (index == path.Length)
        {
            return string.Empty;
        }

        return path[(index + 1)..];
    }
}
