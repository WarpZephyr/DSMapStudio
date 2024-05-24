using StudioCore.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace StudioCore;
public static class GameVariables
{
    public static Dictionary<string, List<string>> Variables { get; set; }
    private static Dictionary<string, string> _path;

    public static void LoadVariables(string rootDir, GameType game)
    {
        Variables = new Dictionary<string, List<string>>();
        if (game == GameType.ArmoredCoreVD)
        {
            var path = $@"{rootDir}\system\acv2.ini";
            if (File.Exists(path))
            {
                DeserializeINI(File.ReadAllLines(path, DSMSEncoding.ShiftJIS));
            }
        }
        InstantiatePath();
    }

    public static bool HasVariables(string rootDir, GameType game)
    {
        if (game == GameType.ArmoredCoreVD)
        {
            var path = $@"{rootDir}\system\acv2.ini";
            return File.Exists(path);
        }

        return false;
    }

    public static Dictionary<string, string>? GetPath()
    {
        if (_path != null)
        {
            return _path;
        }

        InstantiatePath();
        return _path;
    }

    public static bool HasPath()
    {
        if (Variables != null)
        {
            if (_path != null)
            {
                return true;
            }

            foreach (var variable in Variables)
            {
                if (variable.Key.Equals("PATH", StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void InstantiatePath()
    {
        if (Variables != null)
        {
            _path = [];
            foreach (var variable in Variables)
            {
                if (variable.Key.Equals("PATH", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var path in variable.Value)
                    {
                        string[] values = path.Split('=', StringSplitOptions.TrimEntries);
                        if (values.Length < 2)
                        {
                            continue;
                        }

                        _path.Add(values[0], values[1]);
                    }

                    break;
                }
            }

            if (_path.Count < 1)
            {
                _path = null;
            }
        }
    }

    private static void DeserializeINI(string[] lines)
    {
        string variable = null;
        List<string> list = null;
        foreach (var line in lines)
        {
            var processedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(processedLine))
            {
                continue;
            }

            if (processedLine.StartsWith('[') && processedLine.EndsWith(']'))
            {
                if (variable != null)
                {
                    Variables.Add(variable, list);
                }

                variable = processedLine[1..^1];
                list = [];
                continue;
            }

            list.Add(processedLine);
        }

        if (variable != null)
        {
            Variables.Add(variable, list);
        }
    }
}
