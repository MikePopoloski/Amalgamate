using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Amalgamate {
    class Program {
        static void Main (string[] args) {
            // read and process all files in directory
            var usings = new HashSet<string>();
            var lines = new List<string>();
            foreach (var file in Directory.EnumerateFiles(args[0], "*.cs"))
                ProcessFile(file, usings, lines);

            // build the output
            var output = new StringBuilder();
            foreach (var us in usings.OrderBy(s => s.TrimEnd(';')))
                output.AppendLine(us);

            output.AppendLine();
            foreach (var line in lines)
                output.AppendLine(line);

            File.WriteAllText(args[1], output.ToString());
        }

        static void ProcessFile (string file, HashSet<string> usings, List<string> lines) {
            // don't just keep growing an existing amalgamated.cs file
            if (file.ToLower().Contains("amalgamated"))
                return;

            var inUsingBlock = true;
            foreach (var line in File.ReadLines(file)) {
                if (inUsingBlock) {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("//"))
                        continue;
                    else if (!trimmed.StartsWith("using "))
                        inUsingBlock = false;
                    else {
                        usings.Add(trimmed);
                        continue;
                    }
                }

                lines.Add(line);
            }

            if (!string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.Add("");
        }
    }
}
