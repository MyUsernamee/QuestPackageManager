﻿using QPM.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QPM.Providers
{
    public class AndroidMkProvider
    {
        private enum Concat
        {
            None,
            Set,
            Add
        }

        private readonly string path;

        public AndroidMkProvider(string path)
        {
            this.path = path;
        }

        private static Concat GetConcatType(string line)
        {
            var ind = line.IndexOf('=');
            if (ind != -1)
                return (line[ind - 1]) switch
                {
                    '+' => Concat.Add,
                    ':' => Concat.Set,
                    _ => Concat.None,
                };
            return Concat.None;
        }

        private static string BreakString(string line, out Concat type)
        {
            var ind = line.IndexOf('=');
            if (ind != -1)
            {
                type = (line[ind - 1]) switch
                {
                    '+' => Concat.Add,
                    ':' => Concat.Set,
                    _ => Concat.None,
                };
                return line.Substring(ind + 1).TrimStart();
            }
            type = Concat.None;
            return null;
        }

        private static IEnumerable<string> ParseLine(string line)
        {
            var lst = new List<string>();
            string temp = string.Empty;
            bool wildcard = false;
            bool escapedParenth = false;
            bool escapedSingle = false;
            bool escapedDouble = false;
            bool escapeNext = false;
            foreach (var c in line)
            {
                if (escapeNext)
                {
                    escapeNext = false;
                    temp += c;
                    continue;
                }
                if (wildcard && c == '(')
                    escapedParenth = true;
                wildcard = false;

                if (c == '$')
                    wildcard = true;
                else if (c == '\\')
                    escapeNext = true;
                else if (c == '\'')
                    escapedSingle = !escapedSingle;
                else if (c == '\"')
                    escapedDouble = !escapedDouble;
                else if (c == ')')
                    escapedParenth = false;
                else if (c == ' ' && !escapedSingle && !escapedDouble && !escapedParenth)
                {
                    lst.Add(temp);
                    temp = string.Empty;
                    continue;
                }
                temp += c;
            }
            // Always add at least one
            lst.Add(temp);
            return lst;
        }

        public AndroidMk GetFile()
        {
            var mk = new AndroidMk();
            try
            {
                var lines = File.ReadAllLines(path);
                bool inModule = false;
                var module = new Module();

                foreach (var line in lines)
                {
                    if (!inModule)
                        module.PrefixLines.Add(line);
                    else
                    {
                        // Check if mod end
                        if (line.StartsWith("include $("))
                        {
                            module.BuildLine = line;
                            mk.Modules.Add(module);
                            // Exit module with build statement
                            inModule = false;
                            // Create new module to populate prefix for
                            module = new Module();
                            continue;
                        }
                        // Parse line
                        var parsed = BreakString(line, out var type);
                        if (parsed is null)
                            // If line can't be parsed, skip
                            continue;
                        if (line.StartsWith("LOCAL_MODULE"))
                            module.Id = parsed;
                        else if (line.StartsWith("LOCAL_SRC_FILES"))
                        {
                            if (type == Concat.Set)
                                module.Src.Clear();
                            module.Src.AddRange(ParseLine(parsed));
                        }
                        else if (line.StartsWith("LOCAL_EXPORT_C_INCLUDES"))
                        {
                            if (type == Concat.Set)
                                module.ExportIncludes = string.Empty;
                            module.ExportIncludes += parsed;
                        }
                        else if (line.StartsWith("LOCAL_SHARED_LIBRARIES"))
                        {
                            if (type == Concat.Set)
                                module.SharedLibs.Clear();
                            module.SharedLibs.AddRange(ParseLine(parsed));
                        }
                        else if (line.StartsWith("LOCAL_LDLIBS"))
                        {
                            if (type == Concat.Set)
                                module.LdLibs.Clear();
                            module.LdLibs.AddRange(ParseLine(parsed));
                        }
                        else if (line.StartsWith("LOCAL_CFLAGS"))
                        {
                            if (type == Concat.Set)
                                module.CFlags.Clear();
                            module.CFlags.AddRange(ParseLine(parsed));
                        }
                        else if (line.StartsWith("LOCAL_CPPFLAGS"))
                        {
                            if (type == Concat.Set)
                                module.CppFlags.Clear();
                            module.CppFlags.AddRange(ParseLine(parsed));
                        }
                        else if (line.StartsWith("LOCAL_C_INCLUDES"))
                        {
                            if (type == Concat.Set)
                                module.CIncludes.Clear();
                            module.CIncludes.AddRange(ParseLine(parsed));
                        }
                        else if (line.StartsWith("LOCAL_CPP_FEATURES"))
                        {
                            if (type == Concat.Set)
                                module.CppFeatures.Clear();
                            module.CppFeatures.AddRange(ParseLine(parsed));
                        }
                    }
                    if (line.StartsWith("include $(CLEAR_VARS)"))
                    {
                        // Enter module
                        inModule = true;
                    }
                }

                // Add last portion of module prefix to suffix of mk
                mk.Suffix.AddRange(module.PrefixLines);
                return mk;
            }
            catch
            {
                return null;
            }
        }

        public void SerializeFile(AndroidMk mk)
        {
            if (!File.Exists(path + ".backup"))
                File.Copy(path, path + ".backup");
            var sb = new StringBuilder();
            foreach (var m in mk.Modules)
            {
                foreach (var p in m.PrefixLines)
                    sb.AppendLine(p);
                sb.AppendLine("LOCAL_MODULE := " + m.Id);
                if (!string.IsNullOrEmpty(m.ExportIncludes))
                    sb.AppendLine("LOCAL_EXPORT_C_INCLUDES := " + m.ExportIncludes);
                if (m.Src.Any())
                    foreach (var src in m.Src)
                        sb.AppendLine("LOCAL_SRC_FILES += " + src);
                if (m.SharedLibs.Any())
                    foreach (var lib in m.SharedLibs)
                        sb.AppendLine("LOCAL_SHARED_LIBRARIES += " + lib);
                if (m.LdLibs.Any())
                    sb.AppendLine("LOCAL_LDLIBS += " + string.Join(' ', m.LdLibs));
                if (m.CFlags.Any())
                    sb.AppendLine("LOCAL_CFLAGS += " + string.Join(' ', m.CFlags));
                if (m.CppFlags.Any())
                    sb.AppendLine("LOCAL_CPPFLAGS += " + string.Join(' ', m.CppFlags));
                if (m.CIncludes.Any())
                    sb.AppendLine("LOCAL_C_INCLUDES += " + string.Join(' ', m.CIncludes));
                if (m.CppFeatures.Any())
                    sb.AppendLine("LOCAL_CPP_FEATURES += " + string.Join(' ', m.CppFeatures));
                sb.AppendLine(m.BuildLine);
            }

            foreach (var s in mk.Suffix)
                sb.AppendLine(s);

            // Throws
            File.WriteAllText(path, sb.ToString());
        }
    }
}